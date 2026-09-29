using System.ComponentModel.DataAnnotations;

namespace Flashminds.Contracts;

public record DeckRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = "";

    [StringLength(1000)]
    public string? Description { get; init; }
}

/// <summary>A deck with its study statistics, used for list and dashboard views.</summary>
public record DeckSummaryDto(
    int Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int TotalCards,
    int DueCards,
    int MasteredCards,
    double MasteryPercentage,
    int TotalReviews,
    double AverageAccuracy);

public record DeckDetailDto(
    int Id,
    string Title,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<CardDto> Cards);
