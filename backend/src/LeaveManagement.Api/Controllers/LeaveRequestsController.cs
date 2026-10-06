using LeaveManagement.Api.Data;
using LeaveManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveManagement.Api.Controllers;

// NOTE: This controller was written quickly for a POC.
// It does data access, business logic and validation all in one place.
[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveDbContext _db;

    public LeaveRequestsController(LeaveDbContext db)
    {
        _db = db;
    }

    // GET /api/leave-requests
    [HttpGet]
    public IActionResult GetAll()
    {
        var all = _db.LeaveRequests
            .Include(r => r.Employee)
            .OrderByDescending(r => r.StartDate)
            .ToList();
        return Ok(all);
    }

    // GET /api/leave-requests/search?name=Dana
    // Lets the UI quickly find requests by employee name.
    [HttpGet("search")]
    public IActionResult Search([FromQuery] string name)
    {
        var results = _db.LeaveRequests
            .Where(r => r.Employee!.Name.Contains(name))
            .ToList();

        return Ok(results);
    }

    // POST /api/leave-requests
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto dto)
    {
        var employee = _db.Employees.FirstOrDefault(e => e.Id == dto.EmployeeId);
        if (employee == null)
            return NotFound("Employee not found");

        if (dto.EndDate.Date < dto.StartDate.Date)
            return BadRequest("End date must be on or after start date");

        var days = (dto.EndDate.Date - dto.StartDate.Date).Days + 1;

        if (dto.Type == LeaveType.Vacation)
        {
            // Pending requests count too, so several of them can't exceed the quota together.
            var yearStart = new DateTime(dto.StartDate.Year, 1, 1);
            var nextYearStart = yearStart.AddYears(1);

            var used = _db.LeaveRequests
                .Where(r => r.EmployeeId == dto.EmployeeId
                            && r.Type == LeaveType.Vacation
                            && r.Status != LeaveStatus.Rejected
                            && r.StartDate >= yearStart
                            && r.StartDate < nextYearStart)
                .Sum(r => r.Days);

            if (used + days > employee.AnnualQuota)
                return BadRequest("Not enough vacation balance");
        }

        var request = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Days = days,
            Status = LeaveStatus.Pending
        };

        _db.LeaveRequests.Add(request);
        _db.SaveChanges();

        return Ok(request);
    }

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id:int}/approve")]
    public IActionResult Approve(int id)
    {
        // Single conditional UPDATE: only one of two concurrent approvals can match a Pending row.
        var updated = _db.LeaveRequests
            .Where(r => r.Id == id && r.Status == LeaveStatus.Pending)
            .ExecuteUpdate(s => s.SetProperty(r => r.Status, LeaveStatus.Approved));

        var request = _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.Employee)
            .FirstOrDefault(r => r.Id == id);

        if (request == null)
            return NotFound("Leave request not found");

        if (updated == 0)
            return Conflict($"Leave request is already {request.Status}");

        return Ok(request);
    }
}
