# Nomelo — quick map for a fresh Claude session

App de classement de prénoms (ex-NameSelect, renommée Nomelo). Vote par paires, score Elo, ~28k prénoms FR enrichis (INSEE 1900-2024, Wiktionnaire, Wikidata). Stack : .NET 8 minimal API + React 18 + Vite + Postgres + OIDC, orchestré en local via .NET Aspire.

Tout est exécuté depuis le repo root `C:\Users\TristanROCHE\Documents\Projet Perso\NameSelect`. Note importante : le repo git racine est `NameSelect`, pas `src/Nomelo.Client`. Pour `npm` commands le cwd doit être `src/Nomelo.Client`.

## Layout

```
Nomelo.sln
README.md                       # onboarding utilisateur (Aspire, dev certs, secrets)
Dockerfile                      # build 3-stage (sdk .NET + node + runtime)
docker-compose.{yml,prod.yml}   # déploiement
lists/                          # fichiers JSON de listes de prénoms (sources de prod montées en volume)
ops/                            # scripts ops (pipeline enrichissement prenoms, etc.)
docs/superpowers/plans/         # plans d'implémentation historiques (Plan 1-4)
src/
  Nomelo.AppHost/               # orchestration Aspire — point d'entrée local
    AppHost.cs                  # Postgres + TinyAuth + YARP TLS proxy + Server + Vite client
    lists/                      # listes de dev (sample, prenoms-france-*)
  Nomelo.Server/                # backend ASP.NET Core 8, Minimal APIs
    Program.cs                  # bootstrap + migrations EF auto + compression Brotli/gzip
    Auth/                       # OIDC integration (TinyAuth dev, Authentik prod), CurrentUser
    Data/                       # AppDbContext + entities NameList, VotingSession, Vote, ItemState
    Endpoints/                  # Lists, Sessions, Voting, Share — minimal API maps
    Voting/                     # VoteProcessor (apply/undo), NextPairService (pick next pair)
    Scoring/                    # EloCalculator, EloInverter (undo via bisection), PairSelector,
                                #   ResultsBuilder, StabilityCounter, UpsetDetector, WeightCalculator
    Lists/                      # ListFileLoader, ListCache, ListDirectoryScanner, hosted registrar
    Infrastructure/             # GlobalExceptionHandler, etc.
    Migrations/                 # EF Core migrations (PostgreSQL via Npgsql)
    wwwroot/                    # bundle React produit par `npm run build` (généré, ne pas éditer)
  Nomelo.Shared/Dtos/           # DTOs partagés client/serveur (PairDto, ResultsDto, RankedItemDto,
                                #   SessionDto, ListDto, CreateSessionRequest, VoteRequest,
                                #   BulkBanRequest)
  Nomelo.Client/                # React + TS + Vite (npm), TanStack Query, React Router, MSW pour tests
    src/
      main.tsx, App.tsx
      api/                      # client.ts (fetch), hooks.ts (useResults, useBulkBan, …), types.ts
      auth/                     # bootstrap OIDC côté front
      routes/                   # HomePage, NewSessionDialog, VotingPage, ResultsPage, SharedResultsPage
      components/               # NameCard, NumberField, SelectField, Sparkline, StabilityBanner,
                                #   RankedTable (virtualisée via react-virtuoso), globSearch
      styles/pages.css          # toutes les styles (CSS plat, pas de modules)
      test/                     # vitest + RTL + msw — setup.ts contient le mock ResizeObserver
tests/Nomelo.Server.Tests/      # xUnit — Endpoints, Voting, Scoring, Lists, Infrastructure
```

## Modèle de données (Postgres)

- `NameList` : une liste de candidats (id, nom, items chargés depuis JSON dans `lists/`).
- `VotingSession` : une session de vote utilisateur sur une liste (userId via OIDC, listId, nom optionnel, shareToken, voteCount, stabilityReached).
- `Vote` : un vote enregistré (sessionId, winner, loser, eloDeltas, timestamp). Ordre préservé pour undo.
- `ItemState` : état Elo courant par (sessionId, value) — score, timesShown, isBanned.

EF Core + Npgsql. Migrations appliquées au démarrage du serveur (`Program.cs`).

## Endpoints (Minimal APIs)

Préfixe `/api`. Authentification OIDC pour les endpoints session/vote (sauf `/api/share/...`).

- `GET /api/me` — userId courant
- `GET /api/lists` — listes disponibles
- `GET/POST /api/sessions` — lister / créer une session
- `GET /api/sessions/{id}` — métadonnées session
- `GET /api/sessions/{id}/next-pair` — paire à voter
- `POST /api/sessions/{id}/votes` — soumettre un vote
- `DELETE /api/sessions/{id}/votes/last` — undo dernier vote (utilise `EloInverter`)
- `GET /api/sessions/{id}/results` — classement + bannis + stats
- `POST /api/sessions/{id}/bulk-ban` / `POST /api/sessions/{id}/bulk-unban`
- `GET /api/share/{token}` — résultats publics (lecture seule)

## Concepts clés à connaître

- **Elo** : `EloCalculator` applique le delta, `EloInverter` reconstruit le score *avant* un vote par bisection (utilisé par l'undo). L'undo est exact, pas approximatif.
- **PairSelector** : sélection de paires pondérée (`WeightCalculator`) pour privilégier les rapprochements informatifs ; `UpsetDetector` repère les écarts inattendus.
- **StabilityCounter** : marque `stabilityReached` quand le top n'a pas bougé sur 100 votes.
- **Clustering** : prénoms regroupés en clusters par Jaro-Winkler ≥0.85 (`prenoms-*.clustered.json`) pour éviter qu'Alice/Alicia/Alyssia forment un super-cluster. Les variantes apparaissent dans la cellule sous le nom principal.
- **Ex-aequo** : rangs identiques affichés en arbre dans `RankedTable` (rang `1`, `1=`, `1=` etc.).
- **Recherche** : `globSearch.ts` compile un pattern glob (`*`, `?`) en regex, filtré côté client dans `ResultsPage`.
- **Sélection en lot** : checkboxes par ligne, `bulk-bar` flottant, dialog de confirmation. Bannir un item l'écarte du classement mais reste réversible via "Restaurer" dans la section "Bannis".
- **Sparklines** : tendance d'usage du prénom 1900-2024 (`Sparkline.tsx`), avec metric pic.
- **Virtualisation** : `RankedTable` utilise `react-virtuoso` en mode `useWindowScroll`. Détails importants dans la section CSS ci-dessous.

## Pièges connus / contraintes non évidentes

1. **CSS direct-child combinator + virtualisation** : ne JAMAIS utiliser `>` entre `.ranked` et `.ranked__row`. Virtuoso intercale ses wrappers, ce qui casse les sélecteurs `>`. Toujours utiliser le combinateur descendant pour les règles qui dépendent du `data-selectable` du conteneur.
2. **Espacement inter-lignes** dans `.ranked` : ne peut pas reposer sur `gap` du flex parent (Virtuoso n'a qu'un seul enfant scroller). Le padding-bottom est porté par `.ranked__virtual-item` (wrapper rendu dans `itemContent`). Ne pas y mettre `:last-child` — chaque wrapper est unique enfant de sa div Virtuoso et la règle matche partout.
3. **jsdom + Virtuoso** : `ResizeObserver` n'existe pas dans jsdom. Stub présent dans `src/Nomelo.Client/src/test/setup.ts`. `initialItemCount={Math.min(n, 20)}` sur `<Virtuoso>` garantit que les tests voient les premières lignes dès le premier render.
4. **`Math.max(...arr)` interdit** sur les listes : 28k items font sauter la stack V8. Boucle `for…of` explicite (cf. `RankedTable.tsx`).
5. **Stabilité des références passées à `<Row>` mémoïsé** : le `selection` prop est un objet recréé à chaque render dans `ResultsPage`. Le `RankedTable` extrait `selection?.selected` et `selection?.onToggle` dans des variables avant de les passer à un `useCallback` `itemContent`, sinon le `memo` est inutile.
6. **Aspire + TinyAuth** : TinyAuth refuse `localhost` brut comme issuer. On utilise `nomelo.localhost` (RFC 6761) sur le port 8443 derrière YARP qui termine le TLS. APPURL et `OIDC__Authority` doivent matcher exactement.
7. **ForwardedHeaders** : `app.UseForwardedHeaders()` doit s'exécuter avant tout middleware qui lit le scheme/host (auth, redirects). Sinon le redirect_uri OIDC sort en `http://` au lieu de `https://` et Authentik refuse.
8. **`Lists__Directory`** : en dev, AppHost pointe vers `src/Nomelo.AppHost/lists/`. En prod, monter `lists/` à la racine du repo via docker-compose.
9. **`wwwroot/` est généré** : `cd src/Nomelo.Client && npm run build` produit `src/Nomelo.Server/wwwroot/`. Si le bundle est obsolète, le serveur sert une vieille version (déjà eu le cas : la barre de recherche disparue après un changement de branche, fix = rebuild).
10. **Path git vs cwd** : le repo git racine est `NameSelect/`. `git status` affiche `src/Nomelo.Client/...`. Pour `npm`, il faut être dans `src/Nomelo.Client`. Pour `dotnet`, n'importe où dans le repo.
11. **Conventions de commits** : `feat(scope):`, `fix(scope):`, `perf(scope):`, `test(scope):`. Une PR par feature.

## Commandes utiles

```powershell
# Tout l'environnement (Postgres + TinyAuth + YARP + Server + Vite) :
aspire run  # ou `dotnet run --project src/Nomelo.AppHost`

# Tests backend
dotnet test tests/Nomelo.Server.Tests

# Tests frontend
cd src/Nomelo.Client; npm test          # vitest run
cd src/Nomelo.Client; npm run test:watch

# Build du bundle React → écrit dans src/Nomelo.Server/wwwroot/
cd src/Nomelo.Client; npm run build

# Migrations EF (depuis Nomelo.Server)
dotnet ef migrations add <Name> --project src/Nomelo.Server
```

## Style / préférences observées

- Commentaires : uniquement quand le "pourquoi" est non évident (workaround, contrainte cachée, surprise). Pas de paraphrase du code. Pas de "added for X flow" / "used by Y".
- Pas de tirets longs dans les messages utilisateur ni dans le code.
- TypeScript strict côté client, nullable refs activés côté serveur.
- TanStack Query gère le cache des requêtes ; les mutations invalident `["results", id]`.
- Pas de Tailwind ; CSS flat dans `src/styles/pages.css` avec convention BEM (`.ranked__row`, `.bulk-bar__action`).
- Vitest + Testing Library + MSW. Fixtures dans `src/test/handlers.ts`.

## État actuel (2026-05-29)

- Branche `main` : code de production (Aspire + 4 plans livrés).
- Branche `perf/virtualize-ranked-table` poussée vers `origin`, PR pas encore ouverte (lien : https://github.com/stanfear/Nomelo/pull/new/perf/virtualize-ranked-table).
- Dernières features livrées : undo last vote (EloInverter), bulk ban/restore, glob search filter, virtualisation react-virtuoso.
