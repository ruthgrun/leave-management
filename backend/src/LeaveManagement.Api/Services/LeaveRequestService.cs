using System.Linq.Expressions;
using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Services;

public class LeaveRequestService
{
    private readonly LeaveDbContext _db;

    public LeaveRequestService(LeaveDbContext db)
    {
        _db = db;
    }

    public Task<List<LeaveRequestDto>> GetAllAsync() =>
        _db.LeaveRequests
            .OrderByDescending(r => r.StartDate)
            .Select(ToDto)
            .ToListAsync();

    public Task<List<LeaveRequestDto>> SearchAsync(string name) =>
        _db.LeaveRequests
            .Where(r => r.Employee!.Name.Contains(name))
            .OrderByDescending(r => r.StartDate)
            .Select(ToDto)
            .ToListAsync();

    public async Task<ServiceResult<LeaveRequestDto>> CreateAsync(CreateLeaveRequestDto dto)
    {
        if (dto.StartDate is null || dto.EndDate is null)
            return Fail(ServiceError.Validation, "Start date and end date are required");

        var start = dto.StartDate.Value.Date;
        var end = dto.EndDate.Value.Date;

        if (end < start)
            return Fail(ServiceError.Validation, "End date must be on or after start date");

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return Fail(ServiceError.NotFound, "Employee not found");

        var days = (end - start).Days + 1;

        if (dto.Type == LeaveType.Vacation)
        {
            var used = await GetReservedVacationDaysAsync(employee.Id, start.Year);
            if (used + days > employee.AnnualQuota)
                return Fail(ServiceError.Validation,
                    $"Not enough vacation balance ({employee.AnnualQuota - used} days left)");
        }

        var request = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = dto.Type,
            StartDate = start,
            EndDate = end,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync();

        return ServiceResult<LeaveRequestDto>.Ok(new LeaveRequestDto(
            request.Id, employee.Id, employee.Name, request.Type,
            request.StartDate, request.EndDate, request.Days, request.Status));
    }

    public async Task<ServiceResult<LeaveRequestDto>> ApproveAsync(int id)
    {
        // Single conditional UPDATE: only one of two concurrent approvals can match a Pending row.
        var updated = await _db.LeaveRequests
            .Where(r => r.Id == id && r.Status == LeaveStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, LeaveStatus.Approved));

        var request = await _db.LeaveRequests
            .Where(r => r.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync();

        if (request == null)
            return Fail(ServiceError.NotFound, "Leave request not found");

        if (updated == 0)
            return Fail(ServiceError.Conflict, $"Leave request is already {request.Status}");

        return ServiceResult<LeaveRequestDto>.Ok(request);
    }

    // Pending requests count too, so several of them can't exceed the quota together.
    private Task<int> GetReservedVacationDaysAsync(int employeeId, int year)
    {
        var yearStart = new DateTime(year, 1, 1);
        var nextYearStart = yearStart.AddYears(1);

        return _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId
                        && r.Type == LeaveType.Vacation
                        && r.Status != LeaveStatus.Rejected
                        && r.StartDate >= yearStart
                        && r.StartDate < nextYearStart)
            .SumAsync(r => r.Days);
    }

    private static ServiceResult<LeaveRequestDto> Fail(ServiceError error, string message) =>
        ServiceResult<LeaveRequestDto>.Fail(error, message);

    private static readonly Expression<Func<LeaveRequest, LeaveRequestDto>> ToDto = r => new LeaveRequestDto(
        r.Id, r.EmployeeId, r.Employee!.Name, r.Type, r.StartDate, r.EndDate, r.Days, r.Status);
}
