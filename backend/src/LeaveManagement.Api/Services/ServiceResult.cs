namespace LeaveManagement.Api.Services;

public enum ServiceError
{
    None,
    Validation,
    NotFound,
    Conflict
}

public record ServiceResult<T>(T? Value, ServiceError Error = ServiceError.None, string? Message = null)
{
    public bool IsSuccess => Error == ServiceError.None;

    public static ServiceResult<T> Ok(T value) => new(value);
    public static ServiceResult<T> Fail(ServiceError error, string message) => new(default, error, message);
}
