using Flashminds.Api.Services;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flashminds.Api.Controllers;

[Route("api/cards")]
[Authorize]
public class CardsController(CardService cards, StudyService study) : ApiControllerBase
{
    [HttpGet("{cardId:int}", Name = "GetCard")]
    public async Task<IActionResult> Get(int cardId) =>
        ToResponse(await cards.GetAsync(UserId, cardId));

    [HttpPut("{cardId:int}")]
    public async Task<IActionResult> Update(int cardId, CardRequest request) =>
        ToResponse(await cards.UpdateAsync(UserId, cardId, request));

    [HttpDelete("{cardId:int}")]
    public async Task<IActionResult> Delete(int cardId) =>
        await cards.DeleteAsync(UserId, cardId) ? NoContent() : NotFound();

    /// <summary>
    /// Submits a rating (1 Again, 2 Hard, 3 Good, 4 Easy) and reschedules the card.
    /// Leave StudySessionId empty on the first answer and send the returned id with later ones.
    /// </summary>
    [HttpPost("{cardId:int}/review")]
    public async Task<IActionResult> Review(int cardId, ReviewRequest request) =>
        ToResponse(await study.ReviewAsync(UserId, cardId, request));
}
