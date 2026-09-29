using Flashminds.Api.Services;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flashminds.Api.Controllers;

[Route("api")]
[Authorize]
public class ProgressController(ProgressService progress) : ApiControllerBase
{
    /// <summary>Home page numbers: cards due, decks, reviews today, mastered cards and the deck to study next.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard() =>
        await progress.GetDashboardAsync(UserId);

    /// <summary>Per-deck statistics and recent study sessions.</summary>
    [HttpGet("progress")]
    public async Task<ActionResult<ProgressDto>> GetProgress() =>
        await progress.GetProgressAsync(UserId);
}
