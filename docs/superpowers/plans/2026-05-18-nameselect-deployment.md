# Nomelo Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Depends on Plans 1, 2, 3.**

**Goal:** Package the solution as a single Docker image (React build + ASP.NET runtime) and orchestrate it with PostgreSQL via Docker Compose. Provide an Authentik configuration recipe, a `.env.example`, and runtime healthchecks so `docker compose up` produces a working deployment.

**Architecture:** A three-stage Dockerfile at the repo root: (1) Node 20 builds the React app; (2) .NET 8 SDK publishes the server, copying the React output into `wwwroot` before publish; (3) `aspnet:8.0` runtime image runs the published binary on port 8080. `docker-compose.yml` runs `app` + `db` (postgres:16) with a named volume for Postgres data and a read-only bind mount for the lists directory. Migrations run on startup via the existing EF Core call from Plan 1.

**Tech Stack:** Docker, Docker Compose v2, Node 20 image, .NET 8 SDK/runtime images, PostgreSQL 16, Authentik (external, configuration documented).

---

## File Structure

```
Dockerfile                                   # multi-stage build at repo root
.dockerignore
docker-compose.yml
docker-compose.override.yml.example          # dev tweaks (port forwarding, hot reload helper)
.env.example
ops/
├── authentik-setup.md                       # provider config recipe
└── healthcheck.sh                           # used inside the container
lists/                                       # exists from Plan 1
└── sample.json
README.md                                    # add deployment section
```

The image is named `nomelo/app:latest` locally. CI / registry publishing is out of scope.

---

## Task 1: .dockerignore

**Files:**
- Create: `.dockerignore`

- [ ] **Step 1: Write .dockerignore**

Create `.dockerignore` at the repo root:

```
**/bin
**/obj
**/node_modules
**/dist
**/.vs
**/.vscode
**/.idea
**/*.user
**/appsettings.*.local.json
.env
.env.*
!.env.example
.git
.github
docs
tests
src/Nomelo.Server/wwwroot
src/Nomelo.Server.Tests
**/coverage
**/.DS_Store
migration-preview.sql
```

Note: we exclude `wwwroot` so the build cannot accidentally pick up a stale local React build. The Dockerfile produces it inside the image.

- [ ] **Step 2: Commit**

```bash
git add .dockerignore
git commit -m "chore: add .dockerignore excluding build artefacts, secrets, tests, docs"
```

---

## Task 2: Multi-stage Dockerfile

**Files:**
- Create: `Dockerfile`

- [ ] **Step 1: Write the Dockerfile**

Create `Dockerfile` at the repo root:

```dockerfile
# syntax=docker/dockerfile:1.7

# 1) React build
FROM node:20-alpine AS client-build
WORKDIR /src/client
COPY src/Nomelo.Client/package.json src/Nomelo.Client/package-lock.json* ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY src/Nomelo.Client/ ./
RUN npm run build -- --emptyOutDir --outDir /out/wwwroot

# 2) .NET build + publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /src
COPY Nomelo.sln ./
COPY src/Nomelo.Server/Nomelo.Server.csproj src/Nomelo.Server/
COPY src/Nomelo.Shared/Nomelo.Shared.csproj src/Nomelo.Shared/
COPY src/Nomelo.Client/Nomelo.Client.csproj src/Nomelo.Client/
RUN dotnet restore src/Nomelo.Server/Nomelo.Server.csproj
COPY src/Nomelo.Server/ src/Nomelo.Server/
COPY src/Nomelo.Shared/ src/Nomelo.Shared/
# Pull in React build output produced by stage 1
COPY --from=client-build /out/wwwroot src/Nomelo.Server/wwwroot
RUN dotnet publish src/Nomelo.Server/Nomelo.Server.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# 3) Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/publish ./
COPY ops/healthcheck.sh /usr/local/bin/healthcheck.sh
RUN chmod +x /usr/local/bin/healthcheck.sh && \
    apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
  CMD /usr/local/bin/healthcheck.sh
ENTRYPOINT ["dotnet", "Nomelo.Server.dll"]
```

Notes:
- The `Nomelo.Client.csproj` from Plan 1/3 contains an `AfterTargets="Build"` step that runs `npm run build` — but inside the SDK image we don't have Node. We bypass that csproj entirely by publishing `Nomelo.Server.csproj` directly. The Server project does not reference Client, so the npm step never fires here.
- `npm ci` requires `package-lock.json`. Task 3 below verifies that it exists; run `npm install` once locally to generate it before building the image.

- [ ] **Step 2: Write the healthcheck script**

Create `ops/healthcheck.sh`:

```bash
#!/bin/sh
set -e
curl -fsS http://localhost:8080/healthz > /dev/null
```

- [ ] **Step 3: Commit**

```bash
git add Dockerfile ops/healthcheck.sh
git commit -m "feat: add multi-stage Dockerfile (Node build + .NET publish + ASP.NET runtime)"
```

---

## Task 3: Generate package-lock.json + verify image builds

**Files:** none (verification step)

- [ ] **Step 1: Ensure lockfile exists**

```bash
cd src/Nomelo.Client
npm install
cd ../..
git add src/Nomelo.Client/package-lock.json
git commit -m "chore: add package-lock.json required for reproducible docker build"
```

If a lockfile already exists from local dev, skip the commit.

- [ ] **Step 2: Build the image**

```bash
docker build -t nomelo/app:latest .
```

Expected: image builds successfully. The build pulls Node, .NET SDK, and runtime layers (cached on subsequent builds).

- [ ] **Step 3: Inspect image size**

```bash
docker images nomelo/app:latest
```

Expected: image around 230–280 MB.

No commit — verification only.

---

## Task 4: .env.example + docker-compose

**Files:**
- Create: `.env.example`
- Create: `docker-compose.yml`
- Create: `docker-compose.override.yml.example`

- [ ] **Step 1: Write .env.example**

Create `.env.example`:

```
# Postgres
DB_PASSWORD=change-me

# OIDC (Authentik)
OIDC_AUTHORITY=https://auth.example.com/application/o/nomelo/
OIDC_CLIENT_ID=
OIDC_CLIENT_SECRET=

# Optional: external port for the app
APP_PORT=8080
```

- [ ] **Step 2: Write docker-compose.yml**

Create `docker-compose.yml`:

```yaml
services:
  app:
    image: nomelo/app:latest
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "${APP_PORT:-8080}:8080"
    environment:
      ConnectionStrings__Default: "Host=db;Database=nomelo;Username=ns;Password=${DB_PASSWORD}"
      OIDC__Authority: "${OIDC_AUTHORITY}"
      OIDC__ClientId: "${OIDC_CLIENT_ID}"
      OIDC__ClientSecret: "${OIDC_CLIENT_SECRET}"
      Lists__Directory: "/data/lists"
    volumes:
      - ./lists:/data/lists:ro
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  db:
    image: postgres:16
    environment:
      POSTGRES_DB: nomelo
      POSTGRES_USER: ns
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ns -d nomelo"]
      interval: 10s
      timeout: 3s
      retries: 5
    restart: unless-stopped

volumes:
  pgdata:
```

`depends_on.condition: service_healthy` ensures the app does not race the database on startup. The auto-migrate step from Plan 1 then runs cleanly.

- [ ] **Step 3: Write docker-compose.override.yml.example**

Create `docker-compose.override.yml.example`:

```yaml
# Copy to docker-compose.override.yml for local development.
# This file is loaded automatically by `docker compose up`.
services:
  app:
    # Mount the source-of-truth lists for hot iteration (no restart needed if only JSON changes
    # require a restart — list registration happens at startup).
    volumes:
      - ./lists:/data/lists:ro
    environment:
      ASPNETCORE_ENVIRONMENT: Development
  db:
    ports:
      - "5432:5432"
```

- [ ] **Step 4: Commit**

```bash
git add .env.example docker-compose.yml docker-compose.override.yml.example
git commit -m "feat: add docker-compose.yml with app + postgres, .env.example, dev override sample"
```

---

## Task 5: Verify the stack boots end-to-end

**Files:** none

- [ ] **Step 1: Create a working .env**

```bash
cp .env.example .env
# edit .env: set DB_PASSWORD to something non-empty; OIDC values optional for boot-only smoke
```

OIDC values can be empty for this smoke test: the auth middleware initialises lazily, and `/healthz` plus list registration don't require it. Endpoints under `RequireAuthorization()` will 401, which is the correct behaviour.

- [ ] **Step 2: Start the stack**

```bash
docker compose up --build -d
```

Expected: both containers start, `app` becomes healthy within ~30 seconds.

- [ ] **Step 3: Watch logs**

```bash
docker compose logs -f app
```

Expected log lines (excerpt):
- EF Core migrations applied
- `Registered N lists from /data/lists`
- `Now listening on: http://[::]:8080`

Stop tailing with Ctrl-C.

- [ ] **Step 4: Hit /healthz**

```bash
curl -fsS http://localhost:8080/healthz
```

Expected: `{"status":"ok"}`.

- [ ] **Step 5: Hit /api/lists without auth**

```bash
curl -i http://localhost:8080/api/lists
```

Expected: `401 Unauthorized`.

- [ ] **Step 6: Hit / (the SPA)**

```bash
curl -i http://localhost:8080/
```

Expected: `200 OK`, HTML body containing `<div id="root">`.

- [ ] **Step 7: Tear down**

```bash
docker compose down
```

To wipe the database volume as well:

```bash
docker compose down -v
```

No commit — verification only.

---

## Task 6: Authentik configuration recipe

**Files:**
- Create: `ops/authentik-setup.md`

- [ ] **Step 1: Document the Authentik provider + application**

Create `ops/authentik-setup.md`:

````markdown
# Authentik configuration for Nomelo

These steps assume a running Authentik instance reachable at `https://auth.example.com`.

## 1. Create the OIDC provider

In the Authentik admin UI:

1. Providers → Create → OAuth2/OpenID Provider.
2. Name: `Nomelo`.
3. Authorization flow: `default-provider-authorization-implicit-consent` (or your hardened flow).
4. Client type: Confidential.
5. Client ID: auto-generated, copy it.
6. Client Secret: auto-generated, copy it.
7. Redirect URIs (one per line):
   ```
   https://nomelo.example.com/signin-oidc
   http://localhost:8080/signin-oidc
   ```
8. Signing Key: any active key from your instance.
9. Subject mode: `Based on the User's hashed ID` (stable per-user `sub`).
10. Scopes: keep defaults `openid`, `profile`, `email`.
11. Save.

## 2. Create the application

1. Applications → Create.
2. Name: `Nomelo`.
3. Slug: `nomelo`.
4. Provider: the OIDC provider created above.
5. Launch URL: `https://nomelo.example.com/`.
6. Save.

## 3. Note the Authority URL

The OIDC discovery document is at:

```
https://auth.example.com/application/o/nomelo/.well-known/openid-configuration
```

The `Authority` value for `appsettings`/`.env` is the issuer (the path **without** `.well-known/...`):

```
https://auth.example.com/application/o/nomelo/
```

## 4. Populate .env

```
OIDC_AUTHORITY=https://auth.example.com/application/o/nomelo/
OIDC_CLIENT_ID=<copied from step 1.5>
OIDC_CLIENT_SECRET=<copied from step 1.6>
```

## 5. Restart the app

```bash
docker compose up -d app
```

Open `https://nomelo.example.com/` — it should redirect to Authentik, then back to Nomelo after consent. `/api/me` returns the OIDC `sub` claim.

## Troubleshooting

- **`OpenIdConnectProtocolException: IDX21323`** — usually a redirect URI mismatch. Verify the URI list in step 1.7 matches the actual host header.
- **`unauthorized_client`** — the Authentik application binding to the provider is missing or the user isn't in a permitted group.
- **Cookie not set after callback** — check that the app is behind HTTPS in production. The cookie is `SameSite=Strict` and `Secure` policies behave differently over plain HTTP.

````

- [ ] **Step 2: Commit**

```bash
git add ops/authentik-setup.md
git commit -m "docs: add Authentik OIDC provider + application setup recipe"
```

---

## Task 7: Update README with deployment section

**Files:**
- Modify: `README.md` (or create if missing)

- [ ] **Step 1: Check if README exists**

```bash
ls README.md
```

If it does not exist, create it with the section below as the body. If it exists, append the section to it.

- [ ] **Step 2: Write the deployment section**

Append to `README.md`:

````markdown
## Deployment

Nomelo ships as a single Docker image plus PostgreSQL.

### Prerequisites
- Docker 24+
- Docker Compose v2
- A running Authentik instance (or any OIDC provider; tested against Authentik)

### Quick start

```bash
cp .env.example .env
# fill DB_PASSWORD, OIDC_* values
docker compose up --build -d
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

```bash
git pull
docker compose build app
docker compose up -d app
```

EF Core migrations run automatically on container start.
````

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add deployment quick-start, lists management, backups, and upgrade notes"
```

---

## Task 8: Production runtime smoke test

Verify the production image behaves correctly: SPA served, API endpoints behind auth, OIDC challenge fires.

**Files:** none

- [ ] **Step 1: Boot with real OIDC values**

Edit `.env` with valid Authentik values, then:

```bash
docker compose up --build -d
docker compose logs -f app
```

Wait for "Registered N lists from /data/lists" and "Now listening on…".

- [ ] **Step 2: Verify the SPA**

Open `http://localhost:8080/` in a browser. Expected: redirect to Authentik login, then back to HomePage after authentication, listing zero sessions on first login.

- [ ] **Step 3: Create a session via the UI**

Use "Nouvelle session", pick the sample list, set threshold 3, start. Expected: redirected to `/sessions/{id}`, NameCards render, a few votes succeed and the session counter increments.

- [ ] **Step 4: Visit /share/{token}**

Open the share link from the home page in an incognito window. Expected: results load without auth challenge.

- [ ] **Step 5: Tear down**

```bash
docker compose down
```

Optional volume wipe:

```bash
docker compose down -v
```

No commit — verification only.

---

## Self-Review Notes

**Spec coverage:**
- §11 Deployment: Dockerfile (Task 2), docker-compose (Task 4), environment variables wired identically to the spec (Task 4), `pgdata` named volume (Task 4), EF Core auto-migrate retained from Plan 1.
- §11 detail "migrations managed with EF Core Migrations on startup": the runtime image relies on `db.Database.Migrate()` already in Plan 1 — no `dotnet ef` invocation needed in the container, which keeps the runtime image free of the SDK.
- §3 Authentication is documented operationally in Task 6 (Authentik setup recipe), closing the loop between server code (Plan 1, OIDC config) and a runnable instance.

**Placeholder scan:** none. Every command and file body is concrete.

**Type / contract consistency:**
- Env var names in `docker-compose.yml` (`ConnectionStrings__Default`, `OIDC__*`, `Lists__Directory`) match `Program.cs` configuration keys from Plans 1–3.
- Healthcheck endpoint `/healthz` exists from Plan 1.
- Port 8080 is set via `ASPNETCORE_URLS` in the Dockerfile and exposed identically in compose.

**Operational note:** the `Nomelo.Client.csproj` from Plan 1 has an `AfterTargets="Build"` Node step. That target runs only when the Client project is built directly (e.g., `dotnet build Nomelo.sln`). The Dockerfile bypasses it by publishing only the Server project, so the runtime image build does not require Node inside the .NET SDK stage. If a developer runs a full solution build outside Docker, Node must be installed locally — call this out in a follow-up CONTRIBUTING note if friction emerges.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-18-nomelo-deployment.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
