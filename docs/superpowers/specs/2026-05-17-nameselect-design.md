# NameSelect — Design Document

**Date:** 2026-05-17  
**Stack:** ASP.NET Core (C#) · React · PostgreSQL · Docker · Authentik (OIDC)

---

## 1. Problem Statement

NameSelect is a self-hosted web application that helps users select a preferred name from a large list (typically ~12 000 entries) through repeated pairwise comparisons. Users vote on pairs of names, and a smart algorithm surfaces the best names over time. The primary use case is choosing a baby name, but the system is list-agnostic.

---

## 2. Architecture Overview

```
Docker Compose
├── app (ASP.NET Core)
│   ├── React SPA (served from wwwroot after build copy)
│   ├── REST API (C# Minimal APIs)
│   ├── OIDC middleware (Authentik)
│   └── JSON list loader (reads from /data/lists/)
└── db (PostgreSQL)

Volume: /data/lists/ → JSON name list files
```

A single `.sln` with three projects:
- `NameSelect.Server` — ASP.NET Core, hosts API + serves React SPA
- `NameSelect.Client` — React application (built separately, output copied to `wwwroot`)
- `NameSelect.Shared` — DTOs shared between server and client (optional)

---

## 3. Authentication

- **Provider:** Authentik via OIDC (Authorization Code + PKCE)
- **Middleware:** `Microsoft.AspNetCore.Authentication.OpenIdConnect`
- **Session:** Cookie-based (httpOnly, SameSite=Strict) — no token exposed to JavaScript
- **User identity:** `user_id` = `sub` claim from OIDC token (stable Authentik identifier)

**Configuration (environment variables):**
```
OIDC__Authority=https://auth.example.com/application/o/nameselect/
OIDC__ClientId=...
OIDC__ClientSecret=...
```

---

## 4. Name Lists

### Storage

JSON files mounted at `/data/lists/` inside the container. The app scans this directory at startup and registers all valid files into the `lists` table.

### Format

```json
{
  "id": "prenoms-fr",
  "name": "Prénoms français",
  "items": [
    {
      "value": "Abigaëlle",
      "variants": ["Abigail"],
      "description": "de l'hébreu אביגיל, qui signifie « père de la joie »"
    },
    {
      "value": "Alexandre",
      "variants": [],
      "description": "du grec Ἀλέξανδρος..."
    }
  ]
}
```

- `value` — primary display name and vote key (required, unique within list)
- `variants` — alternative forms, displayed as subtitle during voting (optional)
- `description` — shown on hover/tap (optional)

The app ignores unknown fields (forward-compatible).

---

## 5. Data Model (PostgreSQL)

```sql
-- Registered lists (metadata only; content lives in JSON files)
lists (
  id          TEXT PRIMARY KEY,   -- matches JSON "id"
  name        TEXT NOT NULL,
  file_path   TEXT NOT NULL,
  item_count  INT NOT NULL,
  loaded_at   TIMESTAMPTZ NOT NULL
)

-- One voting session per user per list (resumable)
voting_sessions (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         TEXT NOT NULL,       -- OIDC sub
  list_id         TEXT NOT NULL REFERENCES lists(id),
  confidence_threshold INT NOT NULL DEFAULT 3,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  share_token     TEXT UNIQUE          -- for read-only share link
)

-- Every pair presented and its outcome
votes (
  id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  session_id  UUID NOT NULL REFERENCES voting_sessions(id),
  item_a      TEXT NOT NULL,
  item_b      TEXT NOT NULL,
  result      TEXT NOT NULL CHECK (result IN ('prefer_a','prefer_b','ban_a','ban_b','ban_both','like_both')),
  presented_at TIMESTAMPTZ NOT NULL DEFAULT now()
)

-- Aggregated state per item per session (updated incrementally after each vote)
item_states (
  session_id   UUID NOT NULL REFERENCES voting_sessions(id),
  item         TEXT NOT NULL,
  elo_score    FLOAT NOT NULL DEFAULT 1000.0,
  times_shown  INT NOT NULL DEFAULT 0,
  is_banned    BOOLEAN NOT NULL DEFAULT false,
  PRIMARY KEY (session_id, item)
)
```

`item_states` rows are created **lazily** — an entry is inserted the first time an item is drawn for a pair. Items with no row are treated as unseen (ELO=1000, times_shown=0). This avoids inserting 12 000 rows at session creation.

---

## 6. ELO Scoring System

### Initial state
Every item starts at **ELO = 1000**.

### K factor (per-item, based on times_shown)

| times_shown | K |
|---|---|
| < 5 | 48 |
| 5–14 | 32 |
| ≥ 15 | 16 |

Each item has its own K based on its own `times_shown` — updates are asymmetric.

### Vote outcomes

| Vote | Item A update | Item B update |
|---|---|---|
| `prefer_a` | `+= K_A × (1 − E_A)` | `+= K_B × (0 − E_B)` |
| `prefer_b` | `+= K_A × (0 − E_A)` | `+= K_B × (1 − E_B)` |
| `like_both` | `+= K_A × (0.5 − E_A)` | `+= K_B × (0.5 − E_B)` |
| `ban_a` | mark `is_banned = true`, no ELO update | no change |
| `ban_b` | no change | mark `is_banned = true`, no ELO update |
| `ban_both` | mark `is_banned = true`, no ELO update | mark `is_banned = true`, no ELO update |

Where `E_A = 1 / (1 + 10^((elo_B − elo_A) / 400))` and `E_B = 1 − E_A`.

Ban is a pure rejection signal — it carries no implicit preference for the other item.

### Confidence threshold

Items with `times_shown < confidence_threshold` are treated as **unseen** for display weighting purposes, regardless of their ELO score. This protects items from being de-prioritized due to early bad matchups.

`confidence_threshold` is configurable per session at creation time (default: **3**, range: 1–10).

---

## 7. Pair Selection Algorithm

### Display weights

| Condition | Status | Weight |
|---|---|---|
| `is_banned = true` | banned | **0** |
| `times_shown < confidence_threshold` | unseen | **60** |
| `times_shown ≥ threshold` AND ELO > 1050 | preferred | **25** |
| `times_shown ≥ threshold` AND ELO 800–1050 | neutral | **15** |
| `times_shown ≥ threshold` AND ELO < 800 | cold | **5** |

### Selection steps

1. Filter out banned items.
2. Calculate weight for each remaining item.
3. Draw 2 distinct items by weighted random sampling.
4. Check anti-repetition: if the drawn pair appeared among the last **20** pairs presented, redraw (up to 10 attempts; if exhausted, accept the pair anyway to avoid infinite loops on small pools).
5. Increment `times_shown` for both items.

---

## 8. Session Completion Signal

The app tracks a **consecutive non-upset counter** per session. An "upset" is defined as: the item with the lower ELO score wins a `prefer_x` vote (or achieves `like_both` when the ELO gap > 50 pts).

When the counter reaches **100 consecutive pairs without an upset**, a non-blocking notification is shown:

> *"Vos résultats semblent stables depuis 100 votes. Vous pouvez continuer pour affiner."*

The session remains fully functional after this notification. The user decides when to stop.

---

## 9. User Interface (React)

### Screen 1 — Home

- List of the user's active sessions (list name, progress, last active date)
- "New session" button → select a list, configure confidence threshold (default 3), start
- Link to shared result URLs (read-only)

### Screen 2 — Voting

Two names displayed prominently. For each name, visible metadata:
- Primary `value` (large)
- `variants` (subtitle, if any)
- `description` accessible via tap/hover info button

**Action buttons:**
- ❤️ **Préférer [A]** — `prefer_a`
- ❤️ **Préférer [B]** — `prefer_b`
- 💛 **J'aime les deux** — `like_both`
- 🚫 **Bannir [A]** — `ban_a`
- 🚫 **Bannir [B]** — `ban_b`
- 🚫 **Bannir les deux** — `ban_both`

The app is mobile-first in portrait orientation overall. The voting screen is the exception: it is designed for **landscape** orientation, with one name on the left half and the other on the right half of the screen. Action buttons are grouped below each name (ban, prefer) plus a centered "J'aime les deux" / "Bannir les deux" row. No swipe gestures in V1.

**Stability banner** (non-blocking, dismissible):
Shown once when the 100-consecutive-non-upset threshold is reached.

### Screen 3 — Results

Full list of non-banned items, sorted by ELO score descending. Banned items shown in a collapsed section at the bottom.

Columns: rank, name, variants, ELO score, times shown, status badge.

### Screen 4 — Shared Results (public, no auth)

Read-only view of a session's results, accessible at `/share/{share_token}`. Displays the same ranked list. The share token is generated at session creation and visible from the session settings.

---

## 10. API Endpoints (ASP.NET Core Minimal APIs)

```
GET    /api/lists                          → list all available lists
GET    /api/sessions                       → user's sessions
POST   /api/sessions                       → create session { listId, confidenceThreshold }
GET    /api/sessions/{id}                  → session details + stats
GET    /api/sessions/{id}/next-pair        → next pair to vote on
POST   /api/sessions/{id}/votes            → submit vote { itemA, itemB, result }
GET    /api/sessions/{id}/results          → ranked list of items
GET    /api/share/{token}                  → public results (no auth)
```

---

## 11. Deployment (Docker Compose)

```yaml
services:
  app:
    build: .
    ports: ["8080:8080"]
    environment:
      - ConnectionStrings__Default=Host=db;Database=nameselect;Username=ns;Password=${DB_PASSWORD}
      - OIDC__Authority=${OIDC_AUTHORITY}
      - OIDC__ClientId=${OIDC_CLIENT_ID}
      - OIDC__ClientSecret=${OIDC_CLIENT_SECRET}
    volumes:
      - ./lists:/data/lists:ro
    depends_on: [db]

  db:
    image: postgres:16
    environment:
      - POSTGRES_DB=nameselect
      - POSTGRES_USER=ns
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

Database migrations managed with **EF Core Migrations** (`dotnet ef database update` on startup or separate init step).

---

## 12. Out of Scope (V1)

- List creation/editing within the app (lists are JSON files only)
- Gender/tag-based filtering
- Side-by-side multi-user comparison within the app (users share result URLs externally)
- Swipe gestures
- Export (CSV/JSON)
- Email notifications
