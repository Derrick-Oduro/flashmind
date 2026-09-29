using Flashminds.Api.Models;
using Flashminds.Api.Services;
using Flashminds.Contracts;

namespace Flashminds.Api.Extensions;

public static class Mapping
{
    public static CardDto ToDto(this Card card) => new(
        card.Id,
        card.DeckId,
        card.Question,
        card.Answer,
        card.CardType,
        card.Hint,
        card.Explanation,
        card.Difficulty,
        card.IntervalDays,
        card.NextReviewDate,
        card.ReviewCount,
        card.CorrectCount);

    /// <summary>The deck's cards must be loaded.</summary>
    public static DeckSummaryDto ToSummary(this Deck deck, SpacedRepetitionService repetition)
    {
        var stats = repetition.GetDeckStats(deck);
        return new DeckSummaryDto(
            deck.Id,
            deck.Title,
            deck.Description,
            deck.CreatedAt,
            deck.UpdatedAt,
            stats.TotalCards,
            stats.DueCards,
            stats.MasteredCards,
            stats.MasteryPercentage,
            stats.TotalReviews,
            stats.AverageAccuracy);
    }

    public static DeckDetailDto ToDetail(this Deck deck) => new(
        deck.Id,
        deck.Title,
        deck.Description,
        deck.CreatedAt,
        deck.UpdatedAt,
        deck.Cards.OrderBy(card => card.Id).Select(card => card.ToDto()).ToList());
}
