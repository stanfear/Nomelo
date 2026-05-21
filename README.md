# Nomelo

Self-hosted name-selection web app. Users vote on pairs of names via repeated comparisons; an ELO-based algorithm surfaces the best names over time.

Stack: ASP.NET Core 8 (Minimal APIs) · React 18 + Vite · PostgreSQL 16 · OIDC (TinyAuth in dev / Authentik in prod) · .NET Aspire (local orchestration).

## Local development

The fastest way to run Nomelo locally is via the Aspire AppHost. It orchestrates Postgres, TinyAuth (OIDC) fronted by a YARP HTTPS proxy, the ASP.NET server, and the React dev server in one command, with a dashboard for logs, traces, and metrics.

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
    "tinyauth-admin-users": "admin:$2y$05$nbQgAO4QQraEWF.P1kKTyuIusK8lESJ.WhCwhrCysqWiDzKdK.FBu",
    "tinyauth-nomelo-client-secret": "dev-nomelo-client-secret-change-me"
  }
}
```

The `tinyauth-admin-users` value is in TinyAuth's `username:bcrypt-hash` format. The hash above is for the password `admin`.

Allow Aspire to use plain-HTTP transport (used by the internal TinyAuth and server endpoints):

```powershell
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = "true"
```

(Linux/macOS: `export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`.)

### TLS dev certificate

YARP fronts TinyAuth with HTTPS using the .NET dev certificate. If you've never trusted it on this machine, run once:

```
dotnet dev-certs https --trust
```

(You may be prompted to accept the certificate.)

### Run

```
dotnet run --project src/Nomelo.AppHost
```

The Aspire dashboard opens at the URL shown in the console (typically `https://localhost:17xxx`). Resources should turn green within a minute:

- `postgres` — PostgreSQL 16 with persisted volume `nomelo-pgdata`
- `tinyauth` — OIDC provider, reachable only inside the Aspire network
- `auth-proxy` — YARP HTTPS terminator in front of TinyAuth at `https://nomelo.localhost:8443`
- `server` — ASP.NET API at `http://localhost:5000`
- `client` — Vite dev server (port shown in the dashboard)

The OIDC discovery document is at `https://nomelo.localhost:8443/.well-known/openid-configuration`. The nomelo client is pre-declared via env vars on the TinyAuth resource; no manual provider setup is needed.

Default login: `admin` / `admin` (change `tinyauth-admin-users` to override).

### Running tests

```
dotnet test                              # 70 backend tests (Testcontainers Postgres)
cd src/Nomelo.Client && npm test         # 18 client tests
```

These tests are independent of the Aspire stack — they spin their own Postgres / MSW mocks.

## Deployment

Nomelo ships as a single Docker image plus PostgreSQL. Aspire is for local dev only; production deployments use `docker compose`.

### Prerequisites
- Docker 24+
- Docker Compose v2
- A running Authentik instance (or any OIDC provider; tested against Authentik)

### Quick start

Two deployment modes are supported; pick whichever fits your workflow.

#### A. Build on the server

Clone the repo on the host, then:

```bash
cp .env.example .env
# fill DB_PASSWORD, OIDC_* values
docker compose up --build -d
```

#### B. Pull a prebuilt image from GHCR

The repo's CI publishes an image to `ghcr.io/stanfear/nomelo` on every push to `main` (`latest`) and on every tag matching `v*` (`v1.2.3`, `1.2`, etc.). On the host, you only need `docker-compose.yml`, `docker-compose.prod.yml`, `.env`, and a `lists/` directory — no source code:

```bash
mkdir -p nomelo-host && cd nomelo-host
curl -O https://raw.githubusercontent.com/stanfear/Nomelo/main/docker-compose.yml
curl -O https://raw.githubusercontent.com/stanfear/Nomelo/main/docker-compose.prod.yml
curl -O https://raw.githubusercontent.com/stanfear/Nomelo/main/.env.example
cp .env.example .env
# fill DB_PASSWORD, OIDC_* values; optionally set NOMELO_IMAGE_TAG=v1.0.0 to pin a release
mkdir lists  # populate with your JSON lists

docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Once `app` is healthy, open `http://localhost:8080/`.

### Name lists

Drop JSON files in `./lists/` matching the format documented in `docs/superpowers/specs/2026-05-17-nomelo-design.md` §4. They are registered at container startup. Restart `app` after adding or modifying files:

```bash
docker compose restart app
```

### Authentik

See `ops/authentik-setup.md` for the provider + application recipe.

### Database backups

```bash
docker compose exec db pg_dump -U ns nomelo > backup-$(date +%F).sql
```

Restore:

```bash
cat backup-2026-05-18.sql | docker compose exec -T db psql -U ns nomelo
```

### Upgrades

**Build-on-server mode:**
```bash
git pull
docker compose build app
docker compose up -d app
```

**GHCR mode:**
```bash
# bump NOMELO_IMAGE_TAG in .env if you pin a version, otherwise latest is implicit
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

EF Core migrations run automatically on container start.
