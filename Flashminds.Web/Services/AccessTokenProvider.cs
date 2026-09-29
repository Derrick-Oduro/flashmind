using Microsoft.AspNetCore.Components.Authorization;

namespace Flashminds.Services;

/// <summary>
/// Finds the API bearer token of the signed-in user. The token is stored as a claim in the
/// web app's auth cookie when the user signs in.
/// </summary>
public class AccessTokenProvider(IHttpContextAccessor httpContextAccessor, AuthenticationStateProvider authenticationStateProvider)
{
    public const string ClaimType = "access_token";

    /// <summary>
    /// True when running inside a live Blazor circuit, where there is no HttpContext.
    /// Plain HTTP requests (static pages and form endpoints) always have one.
    /// </summary>
    public bool InCircuit => httpContextAccessor.HttpContext is null;

    public async Task<string?> GetTokenAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
            return httpContext.User.FindFirst(ClaimType)?.Value;

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimType)?.Value;
    }
}
