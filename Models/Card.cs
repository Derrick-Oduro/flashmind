namespace Flashminds.Models;

/// <summary>
/// Represents a single flashcard with a question and answer.
/// </summary>
public class Card
{
    public int Id { get; set; }

    public int DeckId { get; set; }

    public required string Question { get; set; }

    public required string Answer { get; set; }

    /// <summary>
    /// Difficulty level: 1 (Easy) - 5 (Hard). Default is 3 (Medium).
    /// </summary>
    public int Difficulty { get; set; } = 3;

    /// <summary>
    /// Interval in days until the card should be reviewed again.
    /// </summary>
    public int IntervalDays { get; set; } = 1;

    /// <summary>
    /// The next date when this card should be studied.
    /// </summary>
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of times this card has been reviewed.
    /// </summary>
    public int ReviewCount { get; set; } = 0;

    /// <summary>
    /// Total number of times the user got this card correct.
    /// </summary>
    public int CorrectCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the parent deck.
    /// </summary>
    public Deck? Deck { get; set; }
}
