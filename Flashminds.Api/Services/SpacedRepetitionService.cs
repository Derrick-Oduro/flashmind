using Flashminds.Api.Models;

namespace Flashminds.Api.Services;

/// <summary>
/// Service that implements spaced repetition scheduling logic.
/// Uses a simplified SM-2-like algorithm for managing card review intervals.
/// </summary>
public class SpacedRepetitionService
{
    /// <summary>
    /// Updates a card's interval and next review date based on user's response.
    /// Difficulty ratings: 1 (Again) - 4 (Easy)
    /// </summary>
    public void UpdateCardInterval(Card card, int difficultyRating)
    {
        if (difficultyRating < 1 || difficultyRating > 4)
            throw new ArgumentException("Difficulty must be between 1 and 4", nameof(difficultyRating));

        // Simple interval scheduling based on difficulty
        card.IntervalDays = difficultyRating switch
        {
            1 => 1,        // Again - Reset to 1 day
            2 => 3,        // Hard - 3 days
            3 => 7,        // Good - 7 days (1 week)
            4 => 14,       // Easy - 14 days (2 weeks)
            _ => card.IntervalDays
        };

        // Update the next review date
        card.NextReviewDate = DateTime.UtcNow.AddDays(card.IntervalDays);

        // Track review statistics
        card.ReviewCount++;
        if (difficultyRating >= 3) // Good or Easy = correct
        {
            card.CorrectCount++;
        }

        // Update difficulty level (simple exponential smoothing)
        card.Difficulty = (int)Math.Round(0.9 * card.Difficulty + 0.1 * difficultyRating);
        card.Difficulty = Math.Max(1, Math.Min(5, card.Difficulty)); // Clamp between 1-5

        card.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets cards that are due for review today.
    /// </summary>
    public List<Card> GetCardsDueToday(ICollection<Card> cards)
    {
        var now = DateTime.UtcNow;
        return cards.Where(c => c.NextReviewDate <= now).OrderBy(c => c.NextReviewDate).ToList();
    }

    /// <summary>
    /// Gets the number of cards due today for a deck.
    /// </summary>
    public int GetCardsDueTodayCount(ICollection<Card> cards)
    {
        var now = DateTime.UtcNow;
        return cards.Count(c => c.NextReviewDate <= now);
    }

    /// <summary>
    /// Calculates deck statistics for a progress dashboard.
    /// </summary>
    public DeckStats GetDeckStats(Deck deck)
    {
        var total = deck.Cards.Count;
        if (total == 0)
            return new DeckStats();

        var correctCount = deck.Cards.Sum(c => c.CorrectCount);
        var reviewCount = deck.Cards.Sum(c => c.ReviewCount);
        // A card cannot be mastered until it has been reviewed at least once.
        // Without this guard, 0 correct answers out of 0 reviews is treated as 100%.
        var masterCount = deck.Cards.Count(c =>
            c.ReviewCount > 0 && (double)c.CorrectCount / c.ReviewCount >= 0.8);
        var dueCount = GetCardsDueTodayCount(deck.Cards);

        return new DeckStats
        {
            TotalCards = total,
            MasteredCards = masterCount,
            DueCards = dueCount,
            TotalReviews = reviewCount,
            AverageAccuracy = reviewCount > 0 ? (double)correctCount / reviewCount * 100 : 0
        };
    }
}

/// <summary>
/// Statistics for a deck's progress.
/// </summary>
public class DeckStats
{
    public int TotalCards { get; set; }
    public int MasteredCards { get; set; }
    public int DueCards { get; set; }
    public int TotalReviews { get; set; }
    public double AverageAccuracy { get; set; }

    public double MasteryPercentage => TotalCards > 0 ? (double)MasteredCards / TotalCards * 100 : 0;
}
