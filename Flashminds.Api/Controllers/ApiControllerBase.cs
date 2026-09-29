using System.IdentityModel.Tokens.Jwt;
using Flashminds.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flashminds.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The signed-in user's id, taken from the JWT "sub" claim.</summary>
    protected string UserId => User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("The token has no subject claim.");

    /// <summary>Maps a service result to 200 (or the given success result), 404 or 400.</summary>
    protected IActionResult ToResponse<T>(ServiceResult<T> result, Func<T, IActionResult>? onOk = null) => result.Status switch
    {
        ResultStatus.Ok => onOk is null ? Ok(result.Value) : onOk(result.Value!),
        ResultStatus.NotFound => NotFound(),
        _ => Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest, title: "The request could not be completed.")
    };
}
