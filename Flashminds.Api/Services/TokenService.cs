using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Flashminds.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Signing key, at least 32 characters. Supply it through configuration or an environment variable, never source control.</summary>
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "Flashminds.Api";
    public string Audience { get; set; } = "Flashminds.Web";
    public int ExpiryMinutes { get; set; } = 720;
}

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public AuthResponse CreateToken(IdentityUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expires,
            signingCredentials: credentials);

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Id, user.Email ?? "");
    }
}
