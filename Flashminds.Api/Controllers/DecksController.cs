using Flashminds.Api.Services;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flashminds.Api.Controllers;

[Route("api/decks")]
[Authorize]
public class DecksController(DeckService decks, CardService cards, StudyService study) : ApiControllerBase
{
    /// <summary>Lists the user's decks with study statistics.</summary>
    [HttpGet]
    public async Task<ActionResult<List<DeckSummaryDto>>> GetAll() =>
        await decks.GetDecksAsync(UserId);

    /// <summary>Gets one deck including its cards.</summary>
    [HttpGet("{deckId:int}")]
    public async Task<ActionResult<DeckDetailDto>> Get(int deckId)
    {
        var deck = await decks.GetDeckAsync(UserId, deckId);
        return deck is null ? NotFound() : deck;
    }

    [HttpPost]
    public async Task<ActionResult<DeckSummaryDto>> Create(DeckRequest request)
    {
        var deck = await decks.CreateAsync(UserId, request);
        return CreatedAtAction(nameof(Get), new { deckId = deck.Id }, deck);
    }

    [HttpPut("{deckId:int}")]
    public async Task<ActionResult<DeckSummaryDto>> Update(int deckId, DeckRequest request)
    {
        var deck = await decks.UpdateAsync(UserId, deckId, request);
        return deck is null ? NotFound() : deck;
    }

    /// <summary>Deletes the deck together with its cards and study history.</summary>
    [HttpDelete("{deckId:int}")]
    public async Task<IActionResult> Delete(int deckId) =>
        await decks.DeleteAsync(UserId, deckId) ? NoContent() : NotFound();

    [HttpGet("{deckId:int}/cards")]
    public async Task<ActionResult<List<CardDto>>> GetCards(int deckId)
    {
        var result = await cards.GetCardsAsync(UserId, deckId);
        return result is null ? NotFound() : result;
    }

    /// <summary>Adds a Basic or Cloze card. Cloze answers are read from the {{c1::answer}} placeholder.</summary>
    [HttpPost("{deckId:int}/cards")]
    public async Task<IActionResult> CreateCard(int deckId, CardRequest request) =>
        ToResponse(await cards.CreateAsync(UserId, deckId, request),
            card => CreatedAtRoute("GetCard", new { cardId = card.Id }, card));

    /// <summary>Bulk-adds cards from text, one "question | answer" pair per line. Duplicate questions are skipped.</summary>
    [HttpPost("{deckId:int}/cards/import")]
    public async Task<IActionResult> ImportCards(int deckId, ImportCardsRequest request) =>
        ToResponse(await cards.ImportAsync(UserId, deckId, request.Text));

    /// <summary>Cards in the deck that are due for review now.</summary>
    [HttpGet("{deckId:int}/study/due")]
    public async Task<ActionResult<List<CardDto>>> GetDueCards(int deckId)
    {
        var result = await study.GetDueCardsAsync(UserId, deckId);
        return result is null ? NotFound() : result;
    }
}
