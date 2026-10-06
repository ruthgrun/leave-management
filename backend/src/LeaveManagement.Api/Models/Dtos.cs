using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Api.Models;

// Incoming payload for creating a leave request.
public class CreateLeaveRequestDto
{
    [Range(1, int.MaxValue)]
    public int EmployeeId { get; set; }

    [EnumDataType(typeof(LeaveType))]
    public LeaveType Type { get; set; }

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }
}

public record LeaveRequestDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    LeaveType Type,
    DateTime StartDate,
    DateTime EndDate,
    int Days,
    LeaveStatus Status);
