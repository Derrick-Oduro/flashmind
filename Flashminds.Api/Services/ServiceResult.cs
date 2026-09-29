namespace Flashminds.Api.Services;

public enum ResultStatus
{
    Ok,
    NotFound,
    Invalid
}

/// <summary>
/// Outcome of a service call. Lets services report "not found / not yours" and validation
/// problems without knowing anything about HTTP.
/// </summary>
public record ServiceResult<T>(ResultStatus Status, T? Value = default, string? Error = null)
{
    public static ServiceResult<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static ServiceResult<T> NotFound() => new(ResultStatus.NotFound);
    public static ServiceResult<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
}
