using LeaveManagement.Api.Models;
using LeaveManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveManagement.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly LeaveRequestService _service;

    public LeaveRequestsController(LeaveRequestService service)
    {
        _service = service;
    }

    // GET /api/leave-requests
    [HttpGet]
    public async Task<ActionResult<List<LeaveRequestDto>>> GetAll() =>
        await _service.GetAllAsync();

    // GET /api/leave-requests/search?name=Dana
    [HttpGet("search")]
    public async Task<ActionResult<List<LeaveRequestDto>>> Search([FromQuery] string name) =>
        await _service.SearchAsync(name);

    // POST /api/leave-requests
    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Create([FromBody] CreateLeaveRequestDto dto) =>
        ToActionResult(await _service.CreateAsync(dto));

    // POST /api/leave-requests/{id}/approve
    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(int id) =>
        ToActionResult(await _service.ApproveAsync(id));

    private ActionResult<LeaveRequestDto> ToActionResult(ServiceResult<LeaveRequestDto> result) =>
        result.Error switch
        {
            ServiceError.None => Ok(result.Value),
            ServiceError.NotFound => Problem(result.Message, statusCode: StatusCodes.Status404NotFound),
            ServiceError.Conflict => Problem(result.Message, statusCode: StatusCodes.Status409Conflict),
            _ => Problem(result.Message, statusCode: StatusCodes.Status400BadRequest)
        };
}
