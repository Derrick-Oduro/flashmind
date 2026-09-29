using System.ComponentModel.DataAnnotations;

namespace Flashminds.Contracts;

public record ReviewRequest
{
    /// <summary>1 = Again, 2 = Hard, 3 = Good, 4 = Easy.</summary>
    [Range(1, 4)]
    public int Rating { get; init; }

    /// <summary>Omit on the first answer of a session; pass back the id from the previous response afterwards.</summary>
    public int? StudySessionId { get; init; }

    [Range(0, 86400)]
    public int ResponseTimeSeconds { get; init; }
}

public record ReviewResponse(
    int StudySessionId,
    int CardId,
    int IntervalDays,
    DateTime NextReviewDate,
    int SessionReviewCount,
    int SessionCorrectCount,
    int RemainingDueCards,
    bool SessionCompleted);
