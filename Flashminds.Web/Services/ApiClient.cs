using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Flashminds.Contracts;
using Microsoft.AspNetCore.Components;

namespace Flashminds.Services;

/// <summary>An error response from the API. Message is safe to show to the user.</summary>
public class ApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>
/// The web app's only route to data: a typed client for the FlashMind API.
/// </summary>
public class ApiClient(HttpClient http, AccessTokenProvider tokens, NavigationManager navigation)
{
    // ---- Auth ----
    public Task<AuthResponse> LoginAsync(LoginRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", request, authenticated: false);
    public Task<AuthResponse> RegisterAsync(RegisterRequest request) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", request, authenticated: false);

    // ---- Home and progress ----
    public Task<DashboardDto> GetDashboardAsync() => SendAsync<DashboardDto>(HttpMethod.Get, "api/dashboard");
    public Task<ProgressDto> GetProgressAsync() => SendAsync<ProgressDto>(HttpMethod.Get, "api/progress");

    // ---- Decks ----
    public Task<List<DeckSummaryDto>> GetDecksAsync() => SendAsync<List<DeckSummaryDto>>(HttpMethod.Get, "api/decks");
    public Task<DeckDetailDto?> GetDeckAsync(int deckId) => SendOrNullAsync<DeckDetailDto>(HttpMethod.Get, $"api/decks/{deckId}");
    public Task<DeckSummaryDto> CreateDeckAsync(DeckRequest request) => SendAsync<DeckSummaryDto>(HttpMethod.Post, "api/decks", request);
    public Task<DeckSummaryDto> UpdateDeckAsync(int deckId, DeckRequest request) => SendAsync<DeckSummaryDto>(HttpMethod.Put, $"api/decks/{deckId}", request);
    public Task DeleteDeckAsync(int deckId) => SendAsync(HttpMethod.Delete, $"api/decks/{deckId}");

    // ---- Cards ----
    public Task<CardDto> CreateCardAsync(int deckId, CardRequest request) => SendAsync<CardDto>(HttpMethod.Post, $"api/decks/{deckId}/cards", request);
    public Task<CardDto> UpdateCardAsync(int cardId, CardRequest request) => SendAsync<CardDto>(HttpMethod.Put, $"api/cards/{cardId}", request);
    public Task DeleteCardAsync(int cardId) => SendAsync(HttpMethod.Delete, $"api/cards/{cardId}");
    public Task<ImportCardsResponse> ImportCardsAsync(int deckId, string text) =>
        SendAsync<ImportCardsResponse>(HttpMethod.Post, $"api/decks/{deckId}/cards/import", new ImportCardsRequest { Text = text });

    // ---- Study ----
    public Task<List<CardDto>?> GetDueCardsAsync(int deckId) => SendOrNullAsync<List<CardDto>>(HttpMethod.Get, $"api/decks/{deckId}/study/due");
    public Task<ReviewResponse> ReviewCardAsync(int cardId, ReviewRequest request) => SendAsync<ReviewResponse>(HttpMethod.Post, $"api/cards/{cardId}/review", request);

    // ---- Plumbing ----
    private async Task<T?> SendOrNullAsync<T>(HttpMethod method, string url) where T : class
    {
        try
        {
            return await SendAsync<T>(method, url);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null, bool authenticated = true)
    {
        using var response = await SendCoreAsync(method, url, body, authenticated);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Web);
        return result ?? throw new ApiException(response.StatusCode, "The server returned an empty response.");
    }

    private async Task SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendCoreAsync(method, url, body, authenticated: true);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, object? body, bool authenticated)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);

        if (authenticated)
        {
            var token = await tokens.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(HttpStatusCode.ServiceUnavailable, "FlashMind could not reach the server. Please try again in a moment.");
        }

        if (response.IsSuccessStatusCode)
            return response;

        using (response)
        {
            // An expired or rejected token: end the web session so the user is sent back to sign in.
            if (authenticated && response.StatusCode == HttpStatusCode.Unauthorized && tokens.InCircuit)
                navigation.NavigateTo("/account/expired", forceLoad: true);

            throw new ApiException(response.StatusCode, await ReadErrorAsync(response));
        }
    }

    /// <summary>Turns a ProblemDetails / ValidationProblemDetails body into one readable sentence.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(property => property.Value.EnumerateArray())
                    .Select(message => message.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message));
                var joined = string.Join(" ", messages);
                if (joined.Length > 0)
                    return joined;
            }

            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
                return text;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Not a problem-details body; fall through to a generic message.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please sign in again.",
            HttpStatusCode.NotFound => "That item could not be found.",
            _ => "Something went wrong. Please try again."
        };
    }
}
