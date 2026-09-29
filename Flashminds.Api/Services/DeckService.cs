using Flashminds.Api.Data;
using Flashminds.Api.Extensions;
using Flashminds.Api.Models;
using Flashminds.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Api.Services;

/// <summary>Deck operations. Every method is scoped to the owning user.</summary>
public class DeckService(FlashmindsContext db, SpacedRepetitionService repetition)
{
    public async Task<List<DeckSummaryDto>> GetDecksAsync(string userId)
    {
        var decks = await db.Decks.AsNoTracking()
            .Where(deck => deck.OwnerId == userId)
            .Include(deck => deck.Cards)
            .OrderByDescending(deck => deck.CreatedAt)
            .ToListAsync();

        return decks.Select(deck => deck.ToSummary(repetition)).ToList();
    }

    public async Task<DeckDetailDto?> GetDeckAsync(string userId, int deckId)
    {
        var deck = await db.Decks.AsNoTracking()
            .Include(item => item.Cards)
            .FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);

        return deck?.ToDetail();
    }

    public async Task<DeckSummaryDto> CreateAsync(string userId, DeckRequest request)
    {
        var deck = new Deck
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Decks.Add(deck);
        await db.SaveChangesAsync();
        return deck.ToSummary(repetition);
    }

    public async Task<DeckSummaryDto?> UpdateAsync(string userId, int deckId, DeckRequest request)
    {
        var deck = await db.Decks
            .Include(item => item.Cards)
            .FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);
        if (deck is null)
            return null;

        deck.Title = request.Title.Trim();
        deck.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        deck.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return deck.ToSummary(repetition);
    }

    public async Task<bool> DeleteAsync(string userId, int deckId)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);
        if (deck is null)
            return false;

        db.Decks.Remove(deck);
        await db.SaveChangesAsync();
        return true;
    }
}
