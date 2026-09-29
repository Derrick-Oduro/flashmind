using System.Text.RegularExpressions;
using Flashminds.Api.Data;
using Flashminds.Api.Extensions;
using Flashminds.Api.Models;
using Flashminds.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Flashminds.Api.Services;

/// <summary>Card operations. Ownership is always checked through the parent deck.</summary>
public partial class CardService(FlashmindsContext db)
{
    private const int MaxImportCards = 500;
    private const string MissingAnswerMessage = "An answer is required (for Cloze cards, use a {{c1::answer}} placeholder).";

    [GeneratedRegex(@"\{\{c\d+::(.*?)(?:::[^}]*)?\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex ClozeRegex();

    public async Task<List<CardDto>?> GetCardsAsync(string userId, int deckId)
    {
        if (!await OwnsDeckAsync(userId, deckId))
            return null;

        var cards = await db.Cards.AsNoTracking()
            .Where(card => card.DeckId == deckId)
            .OrderBy(card => card.Id)
            .ToListAsync();
        return cards.Select(card => card.ToDto()).ToList();
    }

    public async Task<ServiceResult<CardDto>> GetAsync(string userId, int cardId)
    {
        var card = await FindOwnedCardAsync(userId, cardId);
        return card is null ? ServiceResult<CardDto>.NotFound() : ServiceResult<CardDto>.Ok(card.ToDto());
    }

    public async Task<ServiceResult<CardDto>> CreateAsync(string userId, int deckId, CardRequest request)
    {
        if (!await OwnsDeckAsync(userId, deckId))
            return ServiceResult<CardDto>.NotFound();

        var cardType = NormalizeType(request.CardType);
        var answer = ResolveAnswer(cardType, request.Question, request.Answer);
        if (answer is null)
            return ServiceResult<CardDto>.Invalid(MissingAnswerMessage);

        var card = new Card
        {
            DeckId = deckId,
            Question = request.Question.Trim(),
            Answer = answer,
            CardType = cardType,
            Hint = EmptyToNull(request.Hint),
            Explanation = EmptyToNull(request.Explanation),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return ServiceResult<CardDto>.Ok(card.ToDto());
    }

    public async Task<ServiceResult<CardDto>> UpdateAsync(string userId, int cardId, CardRequest request)
    {
        var card = await FindOwnedCardAsync(userId, cardId);
        if (card is null)
            return ServiceResult<CardDto>.NotFound();

        var cardType = NormalizeType(request.CardType);
        var answer = ResolveAnswer(cardType, request.Question, request.Answer);
        if (answer is null)
            return ServiceResult<CardDto>.Invalid(MissingAnswerMessage);

        card.Question = request.Question.Trim();
        card.Answer = answer;
        card.CardType = cardType;
        card.Hint = EmptyToNull(request.Hint);
        card.Explanation = EmptyToNull(request.Explanation);
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ServiceResult<CardDto>.Ok(card.ToDto());
    }

    public async Task<bool> DeleteAsync(string userId, int cardId)
    {
        var card = await FindOwnedCardAsync(userId, cardId);
        if (card is null)
            return false;

        db.Cards.Remove(card);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ServiceResult<ImportCardsResponse>> ImportAsync(string userId, int deckId, string text)
    {
        var deck = await db.Decks
            .Include(item => item.Cards)
            .FirstOrDefaultAsync(item => item.Id == deckId && item.OwnerId == userId);
        if (deck is null)
            return ServiceResult<ImportCardsResponse>.NotFound();

        var candidates = ParseImportedCards(text).ToList();
        if (candidates.Count == 0)
            return ServiceResult<ImportCardsResponse>.Invalid("Add at least one complete question and answer pair. Use |, a tab, or a comma between them.");
        if (candidates.Count > MaxImportCards)
            return ServiceResult<ImportCardsResponse>.Invalid($"Import up to {MaxImportCards} cards at a time.");

        var knownQuestions = deck.Cards
            .Select(card => card.Question.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cardsToAdd = candidates
            .Where(candidate => knownQuestions.Add(candidate.Question))
            .Select(candidate => new Card
            {
                DeckId = deck.Id,
                Question = candidate.Question,
                Answer = candidate.Answer,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();
        if (cardsToAdd.Count == 0)
            return ServiceResult<ImportCardsResponse>.Invalid("Every imported question is already in this deck.");

        db.Cards.AddRange(cardsToAdd);
        await db.SaveChangesAsync();
        return ServiceResult<ImportCardsResponse>.Ok(new ImportCardsResponse(cardsToAdd.Count, candidates.Count - cardsToAdd.Count));
    }

    private Task<bool> OwnsDeckAsync(string userId, int deckId) =>
        db.Decks.AnyAsync(deck => deck.Id == deckId && deck.OwnerId == userId);

    private Task<Card?> FindOwnedCardAsync(string userId, int cardId) =>
        db.Cards.Include(card => card.Deck)
            .FirstOrDefaultAsync(card => card.Id == cardId && card.Deck!.OwnerId == userId);

    private static string NormalizeType(string cardType) =>
        string.Equals(cardType, CardTypes.Cloze, StringComparison.OrdinalIgnoreCase) ? CardTypes.Cloze : CardTypes.Basic;

    /// <summary>Cloze cards take their answer from the placeholder; otherwise the supplied answer is used.</summary>
    private static string? ResolveAnswer(string cardType, string question, string? answer)
    {
        var resolved = cardType == CardTypes.Cloze
            ? ExtractClozeAnswer(question) ?? answer?.Trim()
            : answer?.Trim();
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    private static string? ExtractClozeAnswer(string source)
    {
        var match = ClozeRegex().Match(source);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<(string Question, string Answer)> ParseImportedCards(string source)
    {
        foreach (var rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            var separator = line.Contains('|') ? '|' : line.Contains('\t') ? '\t' : ',';
            var separatorIndex = line.IndexOf(separator);
            if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                continue;

            var question = line[..separatorIndex].Trim().Trim('"');
            var answer = line[(separatorIndex + 1)..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                yield return (question, answer);
        }
    }
}
