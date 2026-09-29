namespace Flashminds.Models;

/// <summary>
/// Represents a collection of flashcards organized by subject/topic.
/// </summary>
public class Deck
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// The ASP.NET Core Identity user who owns this deck and all of its cards.
    /// </summary>
    public string? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Collection of cards in this deck.
    /// </summary>
    public ICollection<Card> Cards { get; set; } = new List<Card>();

    /// <summary>
    /// Study sessions for this deck (for analytics).
    /// </summary>
    public ICollection<StudySession> StudySessions { get; set; } = new List<StudySession>();
}
