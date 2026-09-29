using Flashminds.Api.Services;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Flashminds.Api.Controllers;

[Route("api/auth")]
public class AuthController(UserManager<IdentityUser> userManager, TokenService tokenService) : ApiControllerBase
{
    /// <summary>Creates an account and returns a bearer token.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(request.ConfirmPassword), "The passwords do not match.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        return Ok(tokenService.CreateToken(user));
    }

    /// <summary>Signs in with email and password and returns a bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return InvalidLogin();

        if (await userManager.IsLockedOutAsync(user))
            return Problem(detail: "Too many failed attempts. Try again in a few minutes.", statusCode: StatusCodes.Status401Unauthorized, title: "Account locked");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return InvalidLogin();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return Ok(tokenService.CreateToken(user));
    }

    /// <summary>Returns the signed-in user, or 401 when the token is missing or expired.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.FindByIdAsync(UserId);
        return user is null ? Unauthorized() : Ok(new UserInfo(user.Id, user.Email ?? ""));
    }

    private IActionResult InvalidLogin() =>
        Problem(detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized, title: "Sign in failed");
}
