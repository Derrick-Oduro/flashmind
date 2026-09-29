using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Data;

/// <summary>
/// Stores local ASP.NET Core Identity accounts separately from study data.
/// </summary>
public class AuthContext(DbContextOptions<AuthContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
}
