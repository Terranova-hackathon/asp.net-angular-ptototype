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
| `GameHub.Games` | Search and details. Wraps RAWG behind `IGamesService`, with a caching decorator. Advanced search: `GET /api/games` accepts `genres`, `platforms`, `developers`, `publishers` (comma-separated, any-of), `ordering`, `metacriticMin`/`metacriticMax`; `GET /api/games/filters` lists the options and `GET /api/games/companies?kind=developer\|publisher&search=` is the company type-ahead. All values are validated against an allow-list/pattern. |
| `GameHub.Favorites` | Per-user favorite games (`favorites` schema). Game name/image are looked up server-side through `IGameCatalog` (Shared), implemented by Games. Endpoints: `GET /api/favorites`, `GET /api/favorites/ids`, `PUT`/`DELETE /api/favorites/{rawgId}`. |
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

## Architecture walkthrough

This project is a **modular monolith**: one deployable ASP.NET Core application contains separate Auth and Games feature modules. The modules have their own endpoints and implementation details, while the host wires them together. This keeps deployment simple while giving each feature a clear boundary.

### Repository map

```text
/
├── Dockerfile                   Builds Angular, publishes ASP.NET Core, then packages both
├── docker-compose.yml           Runs the app and SQL Server; configures their connection
├── .env.example                 Template for local secrets and the RAWG API key
├── frontend/                    Angular single-page application
│   ├── proxy.conf.json           Dev server proxy for /api requests
│   └── src/
│       ├── main.ts               Bootstraps the Angular application
│       └── app/
│           ├── app.config.ts     Global router, HTTP, interceptor, and startup providers
│           ├── app.routes.ts     Top-level routes, lazy loading, and route guards
│           ├── app.component.ts  Shared page shell and navigation
│           ├── core/             App-wide services and cross-cutting client behavior
│           │   ├── api-error.ts  Converts API errors into displayable messages
│           │   └── auth/         Current user, auth models, guards, and 401 handling
│           └── features/         User-facing features, loaded when their routes are visited
│               ├── auth/         Login and registration pages
│               └── games/        Game list/detail pages, API service, routes, and models
└── backend/
    ├── GameHub.sln               .NET solution
    ├── src/
    │   ├── GameHub.Api/          Application host: middleware, module wiring, static files
    │   ├── GameHub.Shared/       Small cross-module contracts and utilities
    │   │   ├── Modules/          IModule contract and module registration helpers
    │   │   ├── Paging/           Shared paged result response
    │   │   └── Validation/       DataAnnotations request validation helper
    │   ├── GameHub.Auth/         Authentication feature module
    │   │   ├── Contracts/        Register/login requests and user response DTO
    │   │   ├── Data/             Identity DbContext, factory, and EF migrations
    │   │   ├── Domain/           Application user entity
    │   │   ├── Endpoints/        Auth routes and HttpOnly cookie handling
    │   │   ├── Options/          Typed JWT configuration
    │   │   └── Services/         JWT token service and its interface
    │   ├── GameHub.Favorites/    Favorites feature module
    │   └── GameHub.Games/        Game catalog feature module
    │       ├── Contracts/        Application-facing game response DTOs
    │       ├── Endpoints/        Search and game detail HTTP routes
    │       ├── Options/          RAWG configuration
    │       ├── Rawg/             Internal models for RAWG's JSON responses
    │       └── Services/         RAWG client, interface, and caching decorator
    └── tests/
        └── GameHub.Games.Tests/  Unit tests for RAWG mapping and game caching
```

### Dependency direction

The host knows which modules exist, but it does not implement their features. Modules can use the small Shared project, but should not depend on each other.

```text
                         ┌────────────────────┐
                         │    GameHub.Api     │
                         │      (host)        │
                         └──────┬──────┬──────┘
                                │      │
                    ┌───────────▼┐    ┌▼────────────┐
                    │ GameHub.Auth│    │GameHub.Games│
                    └──────┬──────┘    └──────┬──────┘
                           │                  │
                           └────────┬─────────┘
                                    ▼
                            ┌─────────────┐
                            │GameHub.Shared│
                            └─────────────┘

                 Auth  ──────X──────  Games
                       no reference
```

This is a pragmatic boundary, not a full Clean Architecture implementation. Each module currently contains its own HTTP endpoints, services, and (for Auth) persistence code rather than separate Domain/Application/Infrastructure projects. That is intentional for this small learning app; split a module into more layers when its complexity justifies the extra structure.

### Data flow: searching for a game

```text
ANGULAR (browser)
┌──────────────────────────────────────────────────────────────┐
│ GamesListComponent                                           │
│  search input → signal → 300 ms debounce → page state        │
│        │                                                     │
│        ▼                                                     │
│ GamesService.search() → GET /api/games?search=zelda&page=1   │
│        │                                                     │
│        ▼                                                     │
│ HttpClient + unauthorizedInterceptor (handles 401 responses)│
└────────┼─────────────────────────────────────────────────────┘
         │ HTTP request + browser-managed HttpOnly auth cookie
         ▼
ASP.NET CORE (same app/container)
┌──────────────────────────────────────────────────────────────┐
│ Middleware validates the JWT cookie and authorizes the route│
│        │                                                     │
│        ▼                                                     │
│ GamesEndpoints.Search                                        │
│  clamps page/page size and maps expected provider errors     │
│        │ IGamesService                                       │
│        ▼                                                     │
│ CachingGamesService ── cache hit? ──► return cached result   │
│        │ cache miss                                          │
│        ▼                                                     │
│ RawgGamesService ── HTTP request with server-side API key ──────► RAWG
│  RAWG JSON → internal RawgGame models → our GameSummary DTO   │
└────────┼─────────────────────────────────────────────────────┘
         │ JSON response (PagedResult<GameSummary>)
         ▼
Angular receives response → updates view state → @for renders games
```

The RAWG key stays on the backend. The Games module currently does **not** access SQL Server; SQL Server stores Identity users for the Auth module.

### Data flow: authentication

```text
Angular AuthService
       │ POST /api/auth/login (email + password)
       ▼
AuthEndpoints ──► ASP.NET Identity UserManager
                         │
                         ▼
                  AuthDbContext (EF Core)
                         │
                         ▼
                  SQL Server, auth schema

AuthEndpoints ──► JwtTokenService signs a token
       │
       └── Set-Cookie: HttpOnly (browser stores/sends it; JavaScript cannot read it)

Later request ──► JWT bearer middleware reads cookie and validates the token
       │
       └── RequireAuthorization() protects Games endpoints
```

The Angular guard is a navigation convenience, not a security boundary. The backend must authorize every protected operation.

### What each abstraction buys us

| Boundary | Abstraction | Benefit |
|---|---|---|
| Angular component → API | `GamesService` / `AuthService` | Components don't own URLs or HTTP details. |
| Games module → RAWG | `IGamesService`, internal RAWG models, and mapping | RAWG's response format doesn't leak into the rest of the app. |
| Game service → cache | `CachingGamesService` decorator | Caching can be replaced or removed without changing the endpoint. |
| Feature module → host | `IModule` | The host can register and map modules without knowing their internals. |
| Configuration → code | `JwtOptions` / `RawgOptions` | Settings are grouped and read as typed options. |

## Growth considerations

The current structure is deliberately small. These are the boundaries and trade-offs to revisit as features are added.

### Backend

- **Cross-feature data needs a contract.** Favorites are a likely next feature: a user saves a RAWG game. Avoid having Favorites query Auth's tables or Games' internal data. A `Favorites` module can store the current user ID and RAWG game ID; expose only the minimum identity abstraction needed (for example, the authenticated user ID).
- **Schemas are a convention, not a hard wall.** Auth owns the `auth` schema, but modules share one database and code could still query another module's tables. Keep ownership explicit and consider architecture tests if module isolation becomes important.
- **Migrations run at application startup.** This is convenient for local use and the prototype. In a multi-instance or production deployment, prefer a separate migration/deployment step so app replicas don't own schema changes.
- **Keep Shared small.** If business rules and feature-specific helpers accumulate there, it becomes a hidden coupling point. Shared should hold only genuinely shared primitives and contracts.
- **In-memory cache is local to one process.** It is lost on restart and not shared between replicas. For multiple app instances, use a distributed cache such as Redis. Consider stampede protection for popular simultaneous misses.
- **External API resilience is basic.** RAWG may rate-limit or become unavailable. Add measured timeouts, retry/circuit-breaker policies where appropriate, and server-side rate limits. Don't blindly retry requests that may consume quota.
- **Auth is intentionally minimal.** There is no refresh-token flow, email confirmation, or role/policy system yet. Add protections appropriate to new operations; for cookie-authenticated state-changing routes, assess anti-forgery protection.
- **Validation is manual.** Endpoints call a shared DataAnnotations helper. If validation grows, centralize it with endpoint filters or a validation library rather than repeating endpoint code.
- **Keep endpoints thin as behavior grows.** The current endpoints are small. Put multi-step use cases into feature services/handlers instead of letting route methods accumulate business logic.
- **Tests are limited.** There are Games unit tests, but no Auth tests or API/database integration tests. Add integration tests for important flows as the behavior expands.

### Frontend

- **TypeScript API types are handwritten.** The frontend game models mirror the backend DTOs and can drift. When the API surface grows, expose an OpenAPI document and generate TypeScript types/client code.
- **Shared UI is not yet a feature.** If multiple features need the same game card or filter controls, extract reusable UI into a shared folder. Avoid adding a global state library until state complexity warrants it.
- **Startup waits for session restoration.** Angular checks `/api/auth/me` before startup completes. That is simple and avoids guard races, but can affect perceived startup time; revisit if the app grows.
- **Frontend tests and component-level styles are limited.** Add tests for important user flows and move styles into component styles when the global stylesheet becomes hard to maintain.

### Deployment and operations

- **One image deploys frontend and backend together.** This is easy to run, but either change requires rebuilding and deploying the whole app. Split static hosting from the API only when independent deployment or CDN delivery matters.
- **HTTPS is not terminated inside this local container.** Put a TLS reverse proxy/load balancer in front in a real deployment and configure forwarded headers correctly.
- **Data-protection keys are not persisted.** Persist them if relying on ASP.NET Core features that require stable keys across restarts/replicas.
- **Secrets are local-development configuration.** `.env` is convenient locally; use the deployment platform's secret manager in production.
- **Automation and observability are early-stage.** Add CI for frontend/backend builds and tests, then structured logs, health checks, and tracing as operational needs grow.

## A good next feature to learn from

**Favorites** is a useful exercise because it introduces a write operation, ownership by the current user, persistence in a new feature boundary, and a frontend workflow. Before implementing it, decide whether Favorites should store only `userId` + `rawgGameId` or whether it needs a local game snapshot; avoid coupling it to Auth or RAWG's full response schema.
