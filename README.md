# Nomelo

Self-hosted name-selection web app. Users vote on pairs of names via repeated comparisons; an ELO-based algorithm surfaces the best names over time.

Stack: ASP.NET Core 8 (Minimal APIs) · React 18 + Vite · PostgreSQL 16 · OIDC (Rauthy in dev / Authentik in prod) · .NET Aspire (local orchestration).

## Local development

The fastest way to run Nomelo locally is via the Aspire AppHost. It orchestrates Postgres, Rauthy (OIDC), the ASP.NET server, and the React dev server in one command, with a dashboard for logs, traces, and metrics.

### Prerequisites

- .NET 10 SDK (the AppHost requires it; the server projects target net8.0)
- Docker Desktop, Podman, or Rancher Desktop
- Node.js 20+
- Aspire CLI 13+: `irm https://aspire.dev/install.ps1 | iex` (Windows) or `curl -sSL https://aspire.dev/install.sh | bash` (Linux/macOS)

### First-time setup

Set dev secrets in `src/Nomelo.AppHost/appsettings.Development.json`:

```json
{
  "Parameters": {
    "rauthy-admin-password": "ChangeMeInDev_Rauthy123!",
    "rauthy-nomelo-client-secret": "dev-only-client-secret-change-me"
  }
}
```

Allow Aspire to use plain-HTTP transport (Rauthy is unencrypted locally):

```powershell
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"
```

(Linux/macOS: `export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`.)

### Run

```
dotnet run src/Nomelo.AppHost/apphost.cs
```

The Aspire dashboard opens at the URL shown in the console (typically `https://localhost:17xxx`). Resources should turn green within a minute:

- `postgres` — PostgreSQL 16 with persisted volume `nomelo-pgdata`
- `rauthy` — OIDC provider at `http://localhost:8081`
- `server` — ASP.NET API at `http://localhost:5000`
- `client` — Vite dev server at `http://localhost:5173`

### First-boot Rauthy setup (one-time)

After the first boot:

1. Open `http://localhost:8081` and log in as `admin@nomelo.local` with the password set above.
2. Create an OIDC client:
   - **Client ID:** `nomelo`
   - **Type:** Confidential
   - **Allowed Redirect URIs:** `http://localhost:5173/signin-oidc`, `http://localhost:5000/signin-oidc`
   - **Allowed Origins:** `http://localhost:5173`, `http://localhost:5000`
   - **Secret:** the value of `rauthy-nomelo-client-secret` from step above
   - **Allowed scopes:** `openid`, `profile`, `email`
3. Open `http://localhost:5173/` to use the app. You'll be redirected to Rauthy, then back.

### Running tests

```
dotnet test                              # 70 backend tests (Testcontainers Postgres)
cd src/Nomelo.Client && npm test         # 18 client tests
```

These tests are independent of the Aspire stack — they spin their own Postgres / MSW mocks.

### Production deployment

See `docker-compose.yml` and `ops/authentik-setup.md` (created in Plan 4). Aspire is for local dev only.
