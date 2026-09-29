using Flashminds.Api.Data;
using Flashminds.Api.Extensions;
using Flashminds.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Api.Services;

public class ProgressService(FlashmindsContext db, SpacedRepetitionService repetition)
{
    private const int RecentSessionCount = 10;

    public async Task<ProgressDto> GetProgressAsync(string userId)
    {
        var decks = await LoadSummariesAsync(userId);

        var recentSessions = await db.StudySessions.AsNoTracking()
            .Where(session => session.Deck!.OwnerId == userId)
            .OrderByDescending(session => session.StartedAt)
            .Take(RecentSessionCount)
            .Select(session => new StudySessionDto(
                session.Id,
                session.DeckId,
                session.Deck!.Title,
                session.StartedAt,
                session.CompletedAt,
                session.CardReviewedCount,
                session.CardCorrectCount,
                session.DurationSeconds))
            .ToListAsync();

        return new ProgressDto(
            decks.Count,
            decks.Sum(deck => deck.TotalCards),
            decks.Count > 0 ? decks.Average(deck => deck.MasteryPercentage) : 0,
            decks.Sum(deck => deck.DueCards),
            decks,
            recentSessions);
    }

    public async Task<DashboardDto> GetDashboardAsync(string userId)
    {
        var decks = await LoadSummariesAsync(userId);
        var dueDecks = decks
            .Where(deck => deck.DueCards > 0)
            .OrderByDescending(deck => deck.DueCards)
            .ThenBy(deck => deck.Title)
            .ToList();

        var today = DateTime.UtcNow.Date;
        var reviewedToday = await db.CardReviews.AsNoTracking()
            .CountAsync(review => review.ReviewedAt >= today && review.Card!.Deck!.OwnerId == userId);

        return new DashboardDto(
            decks.Sum(deck => deck.DueCards),
            decks.Count,
            reviewedToday,
            decks.Sum(deck => deck.MasteredCards),
            dueDecks.FirstOrDefault()?.Id,
            dueDecks);
    }

    private async Task<List<DeckSummaryDto>> LoadSummariesAsync(string userId)
    {
        var decks = await db.Decks.AsNoTracking()
            .Where(deck => deck.OwnerId == userId)
            .Include(deck => deck.Cards)
            .OrderBy(deck => deck.Title)
            .ToListAsync();
        return decks.Select(deck => deck.ToSummary(repetition)).ToList();
    }
}
