using Flashminds.Api.Data;
using Flashminds.Api.Extensions;
using Flashminds.Api.Models;
using Flashminds.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Api.Services;

public class StudyService(FlashmindsContext db, SpacedRepetitionService repetition)
{
    /// <summary>Cards in the deck that are due now, oldest first. Null when the deck is not the user's.</summary>
    public async Task<List<CardDto>?> GetDueCardsAsync(string userId, int deckId)
    {
        var deck = await db.Decks.AsNoTracking()
            .Include(item => item.Cards)
            .FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);
        if (deck is null)
            return null;

        return repetition.GetCardsDueToday(deck.Cards).Select(card => card.ToDto()).ToList();
    }

    /// <summary>
    /// Records an answer: reschedules the card and adds it to a study session.
    /// The session is created on the first answer, so opening a deck and leaving records nothing.
    /// </summary>
    public async Task<ServiceResult<ReviewResponse>> ReviewAsync(string userId, int cardId, ReviewRequest request)
    {
        var card = await db.Cards.Include(item => item.Deck)
            .FirstOrDefaultAsync(item => item.Id == cardId && item.Deck!.OwnerId == userId);
        if (card is null)
            return ServiceResult<ReviewResponse>.NotFound();

        StudySession session;
        if (request.StudySessionId is int sessionId)
        {
            var existing = await db.StudySessions.FirstOrDefaultAsync(item =>
                item.Id == sessionId && item.DeckId == card.DeckId && item.Deck!.OwnerId == userId);
            if (existing is null)
                return ServiceResult<ReviewResponse>.Invalid("That study session does not exist for this deck.");
            session = existing;
        }
        else
        {
            session = new StudySession { DeckId = card.DeckId, StartedAt = DateTime.UtcNow };
            db.StudySessions.Add(session);
        }

        repetition.UpdateCardInterval(card, request.Rating);

        session.CardReviewedCount++;
        if (request.Rating >= 3)
            session.CardCorrectCount++;
        session.DurationSeconds = (int)(DateTime.UtcNow - session.StartedAt).TotalSeconds;

        db.CardReviews.Add(new CardReview
        {
            CardId = card.Id,
            StudySession = session,
            Difficulty = request.Rating,
            ResponseTimeSeconds = request.ResponseTimeSeconds
        });
        await db.SaveChangesAsync();

        // The session is finished once nothing in the deck is due any more.
        var now = DateTime.UtcNow;
        var remaining = await db.Cards.CountAsync(item => item.DeckId == card.DeckId && item.NextReviewDate <= now);
        if (remaining == 0 && session.CompletedAt is null)
        {
            session.CompletedAt = now;
            await db.SaveChangesAsync();
        }

        return ServiceResult<ReviewResponse>.Ok(new ReviewResponse(
            session.Id,
            card.Id,
            card.IntervalDays,
            card.NextReviewDate,
            session.CardReviewedCount,
            session.CardCorrectCount,
            remaining,
            session.CompletedAt is not null));
    }
}
