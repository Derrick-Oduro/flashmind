using System.ComponentModel.DataAnnotations;

namespace Flashminds.Contracts;

public static class CardTypes
{
    public const string Basic = "Basic";
    public const string Cloze = "Cloze";
}

public record CardRequest : IValidatableObject
{
    /// <summary>"Basic" or "Cloze". Cloze cards use {{c1::answer}} placeholders in the question.</summary>
    public string CardType { get; init; } = CardTypes.Basic;

    [Required]
    public string Question { get; init; } = "";

    /// <summary>Required for Basic cards. For Cloze cards it is optional and taken from the {{c1::...}} placeholder.</summary>
    public string? Answer { get; init; }

    [StringLength(1000)]
    public string? Hint { get; init; }

    [StringLength(2000)]
    public string? Explanation { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(CardType, CardTypes.Basic, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(CardType, CardTypes.Cloze, StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("CardType must be 'Basic' or 'Cloze'.", [nameof(CardType)]);
        }
    }
}

public record CardDto(
    int Id,
    int DeckId,
    string Question,
    string Answer,
    string CardType,
    string? Hint,
    string? Explanation,
    int Difficulty,
    int IntervalDays,
    DateTime NextReviewDate,
    int ReviewCount,
    int CorrectCount);

public record ImportCardsRequest
{
    /// <summary>One card per line: question and answer separated by a pipe, tab or comma. Up to 500 lines.</summary>
    [Required]
    public string Text { get; init; } = "";
}

public record ImportCardsResponse(int Imported, int Skipped);
