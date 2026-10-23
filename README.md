# GameHub

A small learning project showing how **Angular 19**, **ASP.NET Core 9** and **SQL Server** fit together:
register / log in, then browse and search games from the [RAWG API](https://rawg.io/apidocs).

## Run it

```bash
cp .env.example .env        # then fill in JWT_KEY and RAWG_API_KEY (see comments in the file)
docker compose up --build
```

Open <http://localhost:8080>. Without a RAWG key, register and login still work and the games page shows a
"not configured" message. Run the backend tests with:

```bash
docker run --rm -v "$PWD/backend:/w" -w /w mcr.microsoft.com/dotnet/sdk:9.0 dotnet test
```

## How the pieces fit

```
Browser ──► ASP.NET Core (port 8080) ──► SQL Server        (users, via Auth module)
              │  serves Angular files    └─► RAWG API       (games, via Games module, cached)
              └─ /api/* endpoints
```

One container serves both the Angular build (from `wwwroot`) and the API, so the browser sees a single origin.
That means no CORS, and the login cookie is simply "same site".

### Backend: a modular monolith (`backend/src`)

| Project | Responsibility |
|---|---|
| `GameHub.Api` | The host. Builds the app, registers modules, serves Angular. Contains **no** feature logic. |
| `GameHub.Auth` | Register / login / logout / me. ASP.NET Identity + JWT stored in an HttpOnly cookie. Owns the `auth` DB schema and migrations. |
| `GameHub.Games` | Search and details. Wraps RAWG behind `IGamesService`, with a caching decorator. |
| `GameHub.Shared` | Tiny shared pieces: the `IModule` contract, `PagedResult<T>`, request validation. |

Rules that keep it modular:

- A module implements `IModule` (`AddServices`, `MapEndpoints`, optional `InitializeAsync`). The host just lists modules in `Program.cs`.
- Modules depend only on `Shared`, never on each other. Most types are `internal`, so only the module class is public.
- External data (RAWG's JSON) is mapped to our own contracts at the edge, so the rest of the app never depends on its shape.
- Each module owns its own DB schema, so it can be split into a separate database later.
- Configuration is typed (`JwtOptions`, `RawgOptions`) and `JwtOptions` is validated on startup.

### Frontend (`frontend/src/app`)

- `core/` holds app-wide singletons: `AuthService` (current user as a **signal**), route guards, an HTTP interceptor that handles expired sessions.
- `features/auth` and `features/games` are lazy-loaded routes. Each feature owns its components, service and models.
- Components use `inject()`, signals, `@if` / `@for`, and `toSignal` + `switchMap` for search (debounced, with stale requests cancelled).
- Components never build URLs. They call `GamesService` / `AuthService`.

### Auth flow

1. `POST /api/auth/login` checks the password with Identity, then sets a signed JWT in an **HttpOnly** cookie. JavaScript can never read it, which protects against token theft via XSS.
2. The browser sends the cookie on every request. The JWT bearer middleware reads it from the cookie (`OnMessageReceived`).
3. On page load Angular calls `GET /api/auth/me` to learn whether the cookie is still valid. The guard is only UX; **the API enforces authorization** with `RequireAuthorization()`.
4. A 401 from any other API call makes the interceptor clear local state and redirect to `/login`.

CSRF note: the cookie is `SameSite=Lax` and the API only accepts JSON, which covers this simple app. A larger app should add anti-forgery tokens.

### Adding GitHub OAuth later

All login methods should end the same way: find or create the `AppUser`, then call `AuthCookie.Write(...)`.
Add `AddGitHub(...)` (or `AddOAuth`) in `AuthModule`, map `/api/auth/github` and `/api/auth/github/callback` in `AuthEndpoints`,
and link external logins with `UserManager.AddLoginAsync`. Identity's `AspNetUserLogins` table already exists for this. Angular only needs a "Log in with GitHub" link to `/api/auth/github`.

## Local development without Docker (optional)

Needs the .NET 9 SDK, Node 22 and a SQL Server.

```bash
cd backend && dotnet run --project src/GameHub.Api    # set ConnectionStrings__Default, Jwt__Key, Rawg__ApiKey
cd frontend && npm start                              # proxies /api to http://localhost:8080
```

New migration: `dotnet ef migrations add <Name> -p src/GameHub.Auth -s src/GameHub.Api -o Data/Migrations` (from `backend/`).

## Ideas for a cleaner rebuild

- Add frontend tests (none yet) and API integration tests with `WebApplicationFactory` (`Program` is already exposed for it).
- Add rate limiting and refresh tokens, and persist the data-protection keys in a volume.
- Run the Angular app on a CDN or its own container if you need to scale them independently.
