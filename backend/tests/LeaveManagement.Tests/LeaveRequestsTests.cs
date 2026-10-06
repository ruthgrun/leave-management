using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
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
    public async Task Create_WithinQuota_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 3)));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Days);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task Create_ExceedingRemainingQuota_IsRejected()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2026);
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 3)));

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Single(db.LeaveRequests);
    }

    [Fact]
    public async Task Create_ExactlyRemainingQuota_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2026);
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 2)));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, db.LeaveRequests.Count());
    }

    [Fact]
    public async Task Create_PendingRequestsCountTowardsQuota()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var service = new LeaveRequestService(db);

        var first = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 15)));
        var second = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 4, 1), new DateTime(2026, 4, 10)));

        Assert.True(first.IsSuccess);
        Assert.Equal(ServiceError.Validation, second.Error);
    }

    [Fact]
    public async Task Create_DaysUsedInPreviousYear_DoNotCount()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 18, year: 2025);
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 10)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_EndDateBeforeStartDate_IsRejected()
    {
        // Reversed dates used to produce negative Days.
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(emp.Id, new DateTime(2026, 3, 10), new DateTime(2026, 3, 1)));

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Empty(db.LeaveRequests);
    }

    [Fact]
    public async Task Create_UnknownEmployee_ReturnsNotFound()
    {
        using var db = _database.NewDb();
        var service = new LeaveRequestService(db);

        var result = await service.CreateAsync(Vacation(999, new DateTime(2026, 3, 1), new DateTime(2026, 3, 2)));

        Assert.Equal(ServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task Approve_PendingRequest_Succeeds()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, LeaveStatus.Pending);
        var service = new LeaveRequestService(db);

        var result = await service.ApproveAsync(request.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(LeaveStatus.Approved, result.Value!.Status);
    }

    [Theory]
    [InlineData(LeaveStatus.Approved)]
    [InlineData(LeaveStatus.Rejected)]
    public async Task Approve_AlreadyHandledRequest_ReturnsConflict(LeaveStatus status)
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, status);
        var service = new LeaveRequestService(db);

        var result = await service.ApproveAsync(request.Id);

        Assert.Equal(ServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task Approve_Twice_SecondReturnsConflict()
    {
        using var db = _database.NewDb();
        var emp = SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 0, year: 2026);
        var request = SeedRequest(db, emp.Id, LeaveStatus.Pending);
        var service = new LeaveRequestService(db);

        var first = await service.ApproveAsync(request.Id);
        var second = await service.ApproveAsync(request.Id);

        Assert.True(first.IsSuccess);
        Assert.Equal(ServiceError.Conflict, second.Error);
    }

    [Fact]
    public async Task Approve_UnknownRequest_ReturnsNotFound()
    {
        using var db = _database.NewDb();
        var service = new LeaveRequestService(db);

        var result = await service.ApproveAsync(999);

        Assert.Equal(ServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task Search_SqlInjectionPayload_ReturnsNothing()
    {
        using var db = _database.NewDb();
        SeedEmployeeWithUsedDays(db, quota: 20, usedDays: 5, year: 2026);
        var service = new LeaveRequestService(db);

        var results = await service.SearchAsync("%') OR 1=1 --");

        Assert.Empty(results);
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
