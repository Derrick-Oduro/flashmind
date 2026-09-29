namespace Flashminds.Api.Models;

/// <summary>
/// Represents a single study session where the user reviews cards.
/// </summary>
public class StudySession
{
    public int Id { get; set; }

    public int DeckId { get; set; }

    /// <summary>
    /// Total number of cards reviewed in this session.
    /// </summary>
    public int CardReviewedCount { get; set; } = 0;

    /// <summary>
    /// Number of cards the user got correct.
    /// </summary>
    public int CardCorrectCount { get; set; } = 0;

    /// <summary>
    /// Duration of the study session in seconds.
    /// </summary>
    public int DurationSeconds { get; set; } = 0;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Navigation property to the deck.
    /// </summary>
    public Deck? Deck { get; set; }
}
