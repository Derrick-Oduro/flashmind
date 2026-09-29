namespace Flashminds.Models;

/// <summary>
/// Represents a review action on a specific card during a study session.
/// Used to track the history of card reviews for analytics and adjusting intervals.
/// </summary>
public class CardReview
{
    public int Id { get; set; }

    public int CardId { get; set; }

    public int StudySessionId { get; set; }

    /// <summary>
    /// Difficulty rating given by the user: 1 (Again) - 4 (Easy)
    /// Again (1): Card needs more review
    /// Hard (2): Card was difficult
    /// Good (3): Card was good
    /// Easy (4): Card was very easy
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Time taken to answer this card in seconds.
    /// </summary>
    public int ResponseTimeSeconds { get; set; } = 0;

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation properties
    /// </summary>
    public Card? Card { get; set; }
    public StudySession? StudySession { get; set; }
}
