# FlashMind

A flashcard study app with spaced repetition, built on .NET 10.

Users create decks of cards (Basic question/answer, or Cloze fill-in-the-blank), study the cards that are due, and rate how well they knew each one (Again, Hard, Good, Easy). The rating decides when the card comes back. A dashboard and progress page show what is due and what is sticking.

## Architecture

The backend is a standalone REST API. The web UI has no database access and reaches all data through that API.

```
 Browser ──▶ Flashminds.Web (Blazor Server) ──HTTP + JWT──▶ Flashminds.Api (ASP.NET Core Web API) ──▶ SQLite
                       │                                              │
                       └────────── Flashminds.Contracts (shared DTOs) ─┘
```

| Project | Role |
| --- | --- |
| `Flashminds.Api` | ASP.NET Core Web API: controllers, services (business logic), EF Core + SQLite, ASP.NET Identity, JWT bearer authentication, Swagger UI. |
| `Flashminds.Web` | Blazor Server UI. Signs users in through the API and calls it with a typed `ApiClient`. |
| `Flashminds.Contracts` | Request and response types shared by the two projects. |

Inside the API: `Controllers` handle HTTP, `Services` hold the logic (`DeckService`, `CardService`, `StudyService`, `ProgressService`, `SpacedRepetitionService`, `TokenService`), and `Data` holds the two DbContexts and migrations. Every query is scoped to the signed-in user, so one user can never see another's decks.

### API overview

Full, testable documentation is served by the API itself at `/swagger`.

| Area | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Decks | `GET, POST /api/decks`; `GET, PUT, DELETE /api/decks/{id}` |
| Cards | `GET, POST /api/decks/{id}/cards`; `POST /api/decks/{id}/cards/import`; `GET, PUT, DELETE /api/cards/{id}` |
| Study | `GET /api/decks/{id}/study/due`; `POST /api/cards/{id}/review` |
| Progress | `GET /api/dashboard`, `GET /api/progress` |

Everything except register and login needs an `Authorization: Bearer <token>` header. In Swagger, log in, then press **Authorize** and paste the token.

## Run locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
# Terminal 1: the API (http://localhost:5100, Swagger opens automatically)
dotnet run --project Flashminds.Api

# Terminal 2: the web UI (http://localhost:5185)
dotnet run --project Flashminds.Web
```

Start the API first. Databases (`flashminds.db`, `flashminds-auth.db`) are created on first start in the API folder and are git-ignored.

## Configuration

| Setting | Project | Notes |
| --- | --- | --- |
| `Jwt__Key` | API | Secret used to sign tokens, at least 32 characters. **Required**; the API refuses to start without it. A development-only key is in `appsettings.Development.json`. |
| `ConnectionStrings__DefaultConnection`, `ConnectionStrings__AuthConnection` | API | SQLite files for study data and accounts. |
| `LegacyDeckOwnerEmail` | API | Optional. Decks with no owner (from before accounts existed) are assigned to this user at startup. |
| `Api__BaseUrl` | Web | Address of the API. Defaults to `http://localhost:5100`. |

## Deploy (Render)

`render.yaml` defines two services from this repo: `flashminds-api` (with a persistent disk for the databases and a generated `Jwt__Key`) and `flashminds` (the UI). Each has its own Dockerfile, built from the repository root:

```
docker build -f Flashminds.Api/Dockerfile -t flashminds-api .
docker build -f Flashminds.Web/Dockerfile -t flashminds-web .
```

Set `Api__BaseUrl` on the web service to the API's public URL if Render assigns a different address than the one in `render.yaml`.
