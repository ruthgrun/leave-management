using LeaveManagement.Api.Controllers;
using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LeaveManagement.Tests;

public class LeaveRequestsTests : IClassFixture<TestDatabase>
{
    private readonly TestDatabase _database;

    public LeaveRequestsTests(TestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public void Create_WithinQuota_Succeeds()
    {
        // Arrange
        using var db = _database.NewDb();
        var emp = new Employee { Name = "Test Emp", AnnualQuota = 20 };
        db.Employees.Add(emp);
        db.SaveChanges();

        var controller = new LeaveRequestsController(db);

        // Act: request 3 days, well within the quota.
        var result = controller.Create(new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 3)
        });

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public void Create_ExceedingRemainingQuota_IsRejected()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2026);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 3)));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public void Create_ExactlyRemainingQuota_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2026);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 2)));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(2, db.LeaveRequests.Count());
    }

    [Fact]
    public void Create_PendingRequestsCountTowardsQuota()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var controller = new LeaveRequestsController(db);

        Assert.IsType<OkObjectResult>(
            controller.Create(Vacation(emp.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 15))));
        var result = controller.Create(Vacation(emp.Id, new DateTime(2026, 4, 1), new DateTime(2026, 4, 10)));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Create_DaysUsedInPreviousYear_DoNotCount()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2025);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 10)));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Create_EndDateBeforeStartDate_IsRejected()
    {
        // Reversed dates used to produce negative Days.
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var controller = new LeaveRequestsController(db);

        var result = controller.Create(Vacation(emp.Id, new DateTime(2026, 3, 10), new DateTime(2026, 3, 1)));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.LeaveRequests);
    }

    [Fact]
    public void Approve_PendingRequest_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, LeaveStatus.Pending);
        var controller = new LeaveRequestsController(db);

        var result = controller.Approve(request.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(LeaveStatus.Approved, Assert.IsType<LeaveRequest>(ok.Value).Status);
    }

    [Theory]
    [InlineData(LeaveStatus.Approved)]
    [InlineData(LeaveStatus.Rejected)]
    public void Approve_AlreadyHandledRequest_ReturnsConflict(LeaveStatus status)
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, status);
        var controller = new LeaveRequestsController(db);

        var result = controller.Approve(request.Id);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void Approve_Twice_SecondReturnsConflict()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, LeaveStatus.Pending);
        var controller = new LeaveRequestsController(db);

        Assert.IsType<OkObjectResult>(controller.Approve(request.Id));
        Assert.IsType<ConflictObjectResult>(controller.Approve(request.Id));
    }

    [Fact]
    public void Approve_UnknownRequest_ReturnsNotFound()
    {
        using var db = _database.NewDb();
        var controller = new LeaveRequestsController(db);

        Assert.IsType<NotFoundObjectResult>(controller.Approve(999));
    }

    private static LeaveRequest SeedRequest(LeaveDbContext db, int employeeId, LeaveStatus status)
    {
        var request = new LeaveRequest
        {
            EmployeeId = employeeId,
            Type = LeaveType.Vacation,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 3),
            Days = 3,
            Status = status
        };
        db.LeaveRequests.Add(request);
        db.SaveChanges();
        return request;
    }

    private static Employee SeedEmployeeWithUsedDays(LeaveDbContext db, int quota, int usedDays, int year)
    {
        var emp = new Employee { Name = "Test Emp", AnnualQuota = quota };
        db.Employees.Add(emp);
        db.SaveChanges();

        if (usedDays > 0)
        {
            var start = new DateTime(year, 1, 5);
            db.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = emp.Id,
                Type = LeaveType.Vacation,
                StartDate = start,
                EndDate = start.AddDays(usedDays - 1),
                Days = usedDays,
                Status = LeaveStatus.Approved
            });
            db.SaveChanges();
        }

        return emp;
    }

    private static CreateLeaveRequestDto Vacation(int employeeId, DateTime start, DateTime end) => new()
    {
        EmployeeId = employeeId,
        Type = LeaveType.Vacation,
        StartDate = start,
        EndDate = end
    };
}
