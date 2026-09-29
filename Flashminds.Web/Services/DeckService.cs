using Flashminds.Data;
using Flashminds.Models;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Services;

public class DeckService
{
    private readonly IDbContextFactory<FlashmindsContext> _contextFactory;

    public DeckService(IDbContextFactory<FlashmindsContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Deck>> GetAllDecksAsync()
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            return await context.Decks
                .Include(d => d.Cards)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }
    }

    public async Task<Deck?> GetDeckByIdAsync(int deckId)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            return await context.Decks
                .Include(d => d.Cards)
                .FirstOrDefaultAsync(d => d.Id == deckId);
        }
    }

    public async Task<Deck> CreateDeckAsync(string title, string? description)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var deck = new Deck
            {
                Title = title.Trim(),
                Description = description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Decks.Add(deck);
            await context.SaveChangesAsync();
            return deck;
        }
    }

    public async Task UpdateDeckAsync(Deck deck)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            deck.UpdatedAt = DateTime.UtcNow;
            context.Decks.Update(deck);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteDeckAsync(int deckId)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var deck = await context.Decks.FindAsync(deckId);
            if (deck != null)
            {
                context.Decks.Remove(deck);
                await context.SaveChangesAsync();
            }
        }
    }

    public async Task<Card> AddCardAsync(int deckId, string question, string answer)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var card = new Card
            {
                DeckId = deckId,
                Question = question.Trim(),
                Answer = answer.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Cards.Add(card);
            await context.SaveChangesAsync();
            return card;
        }
    }

    public async Task DeleteCardAsync(int cardId)
    {
        using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var card = await context.Cards.FindAsync(cardId);
            if (card != null)
            {
                context.Cards.Remove(card);
                await context.SaveChangesAsync();
            }
        }
    }
}
