namespace Flashminds.Contracts;

public record StudySessionDto(
    int Id,
    int DeckId,
    string DeckTitle,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int CardReviewedCount,
    int CardCorrectCount,
    int DurationSeconds);

public record ProgressDto(
    int TotalDecks,
    int TotalCards,
    double AverageMastery,
    int CardsDueToday,
    List<DeckSummaryDto> Decks,
    List<StudySessionDto> RecentSessions);

public record DashboardDto(
    int DueCards,
    int DeckCount,
    int CardsReviewedToday,
    int MasteredCards,
    int? NextDeckId,
    List<DeckSummaryDto> DueDecks);
