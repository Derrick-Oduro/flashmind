using System.ComponentModel.DataAnnotations;

namespace Flashminds.Contracts;

public record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";

    [Required]
    public string ConfirmPassword { get; init; } = "";
}

public record LoginRequest
{
    [Required]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

/// <summary>Returned after a successful register or login. Send the token as "Authorization: Bearer {token}".</summary>
public record AuthResponse(string Token, DateTime ExpiresAt, string UserId, string Email);

public record UserInfo(string UserId, string Email);
