# Nomelo Aspire AppHost Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Nomelo.AppHost` Aspire orchestration project so `dotnet run --project src/Nomelo.AppHost` boots Postgres + Rauthy (OIDC provider) + ASP.NET server + React (Vite) client with proper service wiring and a dashboard for logs/traces/metrics.

**Architecture:** Single .NET 10 AppHost project at `src/Nomelo.AppHost`. It declares four resources: a Postgres container, a Rauthy container (self-hosted OIDC), the existing `Nomelo.Server` ASP.NET project, and the existing `Nomelo.Client` Vite app. Aspire wires the Postgres connection string into the server via service discovery; Rauthy's endpoint URL is exposed as `OIDC__Authority` to the server. The Vite dev server is launched with a proxy that points to the server. Production deployment via docker-compose (Plan 4) is unaffected — Aspire only orchestrates local dev.

**Tech Stack:** .NET 10 SDK (Aspire workload requires it; the server projects stay on net8.0), Aspire CLI 13+, Aspire.Hosting 9.x, `CommunityToolkit.Aspire.Hosting.NodeJS.Extensions` for Vite, `ghcr.io/sebadob/rauthy` container image, existing `postgres:16` container image.

---

## File Structure

```
src/Nomelo.AppHost/
├── Nomelo.AppHost.csproj          # SDK="Aspire.AppHost.Sdk"
├── AppHost.cs                     # Single-file program (Aspire 9 style)
├── appsettings.json               # Aspire dashboard settings
├── appsettings.Development.json
└── Properties/launchSettings.json # ASPIRE_DASHBOARD_PORT etc.
```

No changes to the existing server or client beyond optional config tweaks.

---

## Task 1: Install Aspire CLI + templates

If `aspire --version` already returns 13.x, skip the install. Otherwise:

- [ ] **Step 1: Install Aspire CLI**

```powershell
irm https://aspire.dev/install.ps1 | iex
```

- [ ] **Step 2: Verify**

```
aspire --version
```

Expected: 13.1 or later.

- [ ] **Step 3: Install Aspire project templates**

```
dotnet new install Aspire.ProjectTemplates
```

No commit (host-level install).

---

## Task 2: Scaffold the AppHost project

- [ ] **Step 1: Create the project**

From the repo root:

```
aspire new aspire-apphost-singlefile -o src/Nomelo.AppHost -n Nomelo.AppHost
```

- [ ] **Step 2: Add the project to the solution**

```
dotnet sln add src/Nomelo.AppHost/Nomelo.AppHost.csproj
```

- [ ] **Step 3: Add references to Server and Client projects**

The AppHost needs to reference the Server project (project resource) and the Client project (for path resolution; it won't actually compile the Client csproj). Add to `src/Nomelo.AppHost/Nomelo.AppHost.csproj` inside an `<ItemGroup>`:

```xml
<ProjectReference Include="..\Nomelo.Server\Nomelo.Server.csproj" />
```

The Client is referenced by path in `AddViteApp(..., workingDirectory: "../Nomelo.Client")`, no csproj reference needed.

- [ ] **Step 4: Add the Postgres hosting integration**

```
cd src/Nomelo.AppHost
dotnet add package Aspire.Hosting.PostgreSQL --version 9.*
```

- [ ] **Step 5: Add the Vite hosting integration (CommunityToolkit)**

```
dotnet add package CommunityToolkit.Aspire.Hosting.NodeJS.Extensions --version 9.*
cd ../..
```

- [ ] **Step 6: Build**

```
dotnet build src/Nomelo.AppHost
```

Expected: 0 errors. The default template's `AppHost.cs` builds an empty distributed application.

- [ ] **Step 7: Commit**

```
git add src/Nomelo.AppHost Nomelo.sln
git commit -m "feat(aspire): scaffold Nomelo.AppHost project with Postgres + Vite hosting integrations"
```

---

## Task 3: Wire Postgres + Nomelo.Server

- [ ] **Step 1: Replace AppHost.cs**

Replace `src/Nomelo.AppHost/AppHost.cs` with:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Postgres — Aspire publishes ConnectionStrings__<dbName> to consumers.
// We name the database "default" so ASP.NET Core picks it up as ConnectionStrings:Default.
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("nomelo-pgdata")
    .WithPgAdmin();

var db = postgres.AddDatabase("default", databaseName: "nomelo");

// Nomelo backend
var server = builder
    .AddProject<Projects.Nomelo_Server>("server")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
```

Notes:
- `AddDatabase("default", databaseName: "nomelo")` makes the env var `ConnectionStrings__default`, which ASP.NET Core resolves as `ConnectionStrings:Default` (case-insensitive). The actual Postgres database name remains `nomelo` for parity with `docker-compose.yml` (Plan 4).
- `WithDataVolume` persists data across `aspire run` invocations.
- `WithPgAdmin` adds a pgAdmin container — handy in dev. Remove if not wanted.
- The Server already calls `db.Database.MigrateAsync()` in `Program.cs`, so migrations run on first boot.

- [ ] **Step 2: Verify the Server can find the connection string**

The Server uses `builder.Configuration.GetConnectionString("Default")` — Aspire's injected env var matches.

- [ ] **Step 3: Run the AppHost briefly to confirm**

```
dotnet run --project src/Nomelo.AppHost
```

Expected:
- Dashboard opens at `http://localhost:15888` (or similar; port shown in the console).
- `postgres` and `server` resources go green.
- `/health` on the Server resource returns 200 (Aspire dashboard exposes the endpoint).

If the Server fails because `OIDC:Authority` is empty (the OIDC handler isn't tolerant of empty Authority at challenge time but is at startup), that's expected — Task 4 fixes it.

Ctrl-C to stop.

- [ ] **Step 4: Commit**

```
git add src/Nomelo.AppHost/AppHost.cs
git commit -m "feat(aspire): wire Postgres + Server resources with health-gated startup"
```

---

## Task 4: Wire Rauthy as the OIDC provider

Rauthy is a self-hosted OIDC server. We run it as a container, bootstrap an admin user and a `nomelo` OAuth2 client at startup, and pass the resulting `Authority`/`ClientId`/`ClientSecret` to the server via env vars.

- [ ] **Step 1: Add Rauthy container to AppHost.cs**

Insert after the `postgres` block and before the `server` block:

```csharp
// Rauthy — self-hosted OIDC. Default port 8080 inside the container.
// We bootstrap an admin user and a confidential client for Nomelo.
var rauthyAdminPassword = builder.AddParameter("rauthy-admin-password", secret: true);
var rauthyClientSecret = builder.AddParameter("rauthy-nomelo-client-secret", secret: true);

var rauthy = builder
    .AddContainer("rauthy", "ghcr.io/sebadob/rauthy", "latest")
    .WithHttpEndpoint(port: 8081, targetPort: 8080, name: "http")
    .WithEnvironment("PUB_URL", "localhost:8081")
    .WithEnvironment("LISTEN_SCHEME", "http")
    .WithEnvironment("BOOTSTRAP_ADMIN_EMAIL", "admin@nomelo.local")
    .WithEnvironment("BOOTSTRAP_ADMIN_PASSWORD_PLAIN", rauthyAdminPassword)
    .WithEnvironment("BOOTSTRAP_API_KEY_SECRET", rauthyClientSecret)
    .WithVolume("nomelo-rauthy-data", "/app/data");
```

`builder.AddParameter(..., secret: true)` reads from User Secrets (or `appsettings.Development.json` with the section `Parameters`). At first run, Aspire prompts for missing parameter values.

- [ ] **Step 2: Reference Rauthy from the server**

Replace the `server` block with:

```csharp
var rauthyEndpoint = rauthy.GetEndpoint("http");

var server = builder
    .AddProject<Projects.Nomelo_Server>("server")
    .WithReference(db)
    .WaitFor(db)
    .WaitFor(rauthy)
    .WithEnvironment("OIDC__Authority", $"{rauthyEndpoint.Url}/auth/v1")
    .WithEnvironment("OIDC__ClientId", "nomelo")
    .WithEnvironment("OIDC__ClientSecret", rauthyClientSecret);
```

`rauthyEndpoint.Url` resolves to `http://localhost:8081` at runtime. The OIDC discovery document for Rauthy is at `{base}/auth/v1/.well-known/openid-configuration`, so the issuer is `{base}/auth/v1`.

- [ ] **Step 3: Set parameter values in appsettings.Development.json**

In `src/Nomelo.AppHost/appsettings.Development.json`, add:

```json
{
  "Parameters": {
    "rauthy-admin-password": "ChangeMeInDev_Rauthy123!",
    "rauthy-nomelo-client-secret": "dev-only-client-secret-change-me"
  }
}
```

Keep this file untracked locally or accept that these are dev-only credentials. The file is already excluded from prod images via `.dockerignore`.

- [ ] **Step 4: Boot and verify Rauthy admin**

```
dotnet run --project src/Nomelo.AppHost
```

Expected:
- Dashboard shows `rauthy` resource green.
- Open `http://localhost:8081`, log in as `admin@nomelo.local` / the password from step 3.
- In the Rauthy admin UI, manually create an OIDC client:
  - Client ID: `nomelo`
  - Type: Confidential
  - Allowed Redirect URIs: `http://localhost:5173/signin-oidc`, `http://localhost:5239/signin-oidc`
  - Allowed Origins: `http://localhost:5173`, `http://localhost:5239`
  - Secret: paste the value of `rauthy-nomelo-client-secret`
  - Allowed scopes: `openid`, `profile`, `email`

(Future iteration: pre-bootstrap this client via Rauthy's `bootstrap.yaml` mounted into `/app/bootstrap.yaml`. Out of scope for this plan — manual one-time admin step is acceptable for V1 dev setup.)

- [ ] **Step 5: Verify end-to-end auth flow**

With AppHost still running:
- Open `http://localhost:5239/login` (the server endpoint, not the SPA yet — Task 5 wires the SPA).
- Expected: redirect to Rauthy login screen.
- Log in as `admin@nomelo.local`.
- Expected: redirected back to `/`, cookie set, `/api/me` returns the admin's sub.

Ctrl-C to stop.

- [ ] **Step 6: Commit**

```
git add src/Nomelo.AppHost
git commit -m "feat(aspire): add Rauthy OIDC provider with bootstrap admin and nomelo client wiring"
```

---

## Task 5: Wire the Vite client

- [ ] **Step 1: Add Vite app to AppHost.cs**

Insert after the `server` block:

```csharp
var client = builder
    .AddViteApp("client", workingDirectory: "../Nomelo.Client", packageManager: "npm")
    .WithReference(server)
    .WaitFor(server)
    .WithHttpEndpoint(port: 5173, targetPort: 5173, env: "VITE_PORT");
```

`AddViteApp` runs `npm run dev` in the working directory. The Vite dev server still proxies `/api`, `/login`, etc. to the backend — Aspire's service discovery sets `services__server__http__0` env var, but our `vite.config.ts` hardcodes `http://localhost:5239` (or `5000`, see Task 6). The current config works for dev; switching to env-driven proxy is a future improvement.

- [ ] **Step 2: Verify**

```
dotnet run --project src/Nomelo.AppHost
```

Expected: 4 resources green (`postgres`, `rauthy`, `server`, `client`). Open `http://localhost:5173/`.
- AuthGate fires, calls `/api/me` → 401 → redirect to `/login` → Rauthy login.
- After login: HomePage with empty sessions list (or the `sample` list if `lists/sample.json` is still present).

- [ ] **Step 3: Commit**

```
git add src/Nomelo.AppHost
git commit -m "feat(aspire): add Vite client resource wired to server reference"
```

---

## Task 6: Reconcile dev port mismatch

The current `vite.config.ts` proxies to `http://localhost:5000`, but the server runs on `5239` per `launchSettings.json`. Pick one and align.

- [ ] **Step 1: Update launchSettings.json**

Set the server's `http` profile applicationUrl to `http://localhost:5000` to match the Vite proxy and Plan 4 conventions:

`src/Nomelo.Server/Properties/launchSettings.json` — change the `http` profile's `applicationUrl` from `http://localhost:5239` to `http://localhost:5000`. Aspire respects launchSettings when running project resources unless overridden.

- [ ] **Step 2: Verify**

```
dotnet run --project src/Nomelo.AppHost
```

Confirm in the dashboard that the server endpoint is on `5000`, and the Vite proxy forwards correctly.

- [ ] **Step 3: Commit**

```
git add src/Nomelo.Server/Properties/launchSettings.json
git commit -m "chore: align server dev port to 5000 to match Vite proxy and docker-compose"
```

---

## Task 7: Document the dev setup

- [ ] **Step 1: Create / update README**

Add a "Local development" section at the top of `README.md` (or create it):

````markdown
## Local development

The fastest way to run Nomelo locally is via the Aspire AppHost. It orchestrates
Postgres, Rauthy (OIDC), the ASP.NET server, and the React dev server in one command.

### Prerequisites

- .NET 10 SDK
- Docker Desktop (or Podman / Rancher Desktop)
- Node.js 20+
- Aspire CLI: `irm https://aspire.dev/install.ps1 | iex` (Windows) or `curl -sSL https://aspire.dev/install.sh | bash` (Linux/macOS)

### First-time setup

Set the dev secrets in `src/Nomelo.AppHost/appsettings.Development.json`:

```json
{
  "Parameters": {
    "rauthy-admin-password": "<choose a password>",
    "rauthy-nomelo-client-secret": "<choose a secret>"
  }
}
```

### Run

```
dotnet run --project src/Nomelo.AppHost
```

The dashboard opens at the port shown in the console. From there:

- Open `http://localhost:8081` and log in to Rauthy as `admin@nomelo.local` with the password you set.
- Create an OIDC client:
  - Client ID: `nomelo`
  - Type: Confidential
  - Allowed Redirect URIs: `http://localhost:5173/signin-oidc`, `http://localhost:5000/signin-oidc`
  - Secret: the value you set for `rauthy-nomelo-client-secret`
  - Scopes: `openid`, `profile`, `email`
- Open `http://localhost:5173/` to use the app.

### Production deployment

See `ops/authentik-setup.md` and `docker-compose.yml` (created in Plan 4) for production deployment. Aspire is used only for local dev; production uses the existing docker-compose stack.
````

- [ ] **Step 2: Commit**

```
git add README.md
git commit -m "docs: add Aspire local dev quick-start to README"
```

---

## Task 8: Final verification

- [ ] **Step 1: Full boot**

```
dotnet run --project src/Nomelo.AppHost
```

Verify all 4 resources go green within 30 seconds.

- [ ] **Step 2: Run backend tests in parallel**

In a second terminal:

```
dotnet test
```

Expected: 70/70 passing. The tests use their own Testcontainers Postgres, independent of the Aspire-managed instance.

- [ ] **Step 3: Run client tests**

```
cd src/Nomelo.Client
npm test
```

Expected: 18/18 passing.

- [ ] **Step 4: Smoke-test the UI**

With AppHost running, open `http://localhost:5173/`:
- Redirect → Rauthy login → back to HomePage
- Create a new session against the `sample` list
- Vote a few times → check the dashboard's traces for `POST /api/sessions/{id}/votes`
- Open Results → ranked list shows up
- Open the share link in incognito → public results render without auth

No commit — verification only.

---

## Self-Review Notes

**Out of scope** (deferred):
- Pre-bootstrapping the Rauthy `nomelo` OIDC client via `bootstrap.yaml`. Today requires a one-time manual click. Acceptable for dev.
- Switching the Vite proxy to env-driven service discovery (`services__server__http__0`) instead of hardcoded URL. Works today; future improvement.
- Aspire integration tests (`Aspire.Hosting.Testing` + xUnit). Backend keeps Testcontainers for CI determinism; AppHost is dev-only.
- Aspire deployment (Azure Container Apps, manifest export). Production stays on docker-compose per Plan 4.

**Operational note:** the AppHost requires .NET 10 SDK, but the Server / Tests / Shared projects stay on net8.0. .NET 10 can build net8.0 targets; no version conflict.

**Security note:** `appsettings.Development.json` of the AppHost holds dev secrets in plaintext. Acceptable for dev. Never commit production secrets here; production uses `.env` per Plan 4.

---

## Execution Handoff

Plan saved to `docs/superpowers/plans/2026-05-19-nomelo-aspire-apphost.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks.
2. **Inline execution** — execute tasks in this session with checkpoints.

Which approach?

## Post-implementation notes

Aspire 13.3.3's `aspire-apphost-singlefile` template produces a file-based app (no .csproj). Adapt:
- No `dotnet sln add` step.
- No `<ProjectReference>` in a csproj — use `builder.AddProject("server", "../Nomelo.Server/Nomelo.Server.csproj")`.
- Packages declared via `#:package` directives at the top of `apphost.cs`.
- Run with `dotnet run --project src/Nomelo.AppHost` (the `--project` form fails with MSB4025).
- Set `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` since Rauthy runs over HTTP.

Rauthy env vars verified against https://sebadob.github.io/rauthy/config/config.html — added `HQL_INSECURE_COOKIE=true` for HTTP cookies in dev; dropped `BOOTSTRAP_API_KEY_SECRET` (that flag is for the Rauthy admin API, not the OIDC client secret).
