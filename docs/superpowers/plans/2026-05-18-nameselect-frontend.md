# Nomelo Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Depends on Plan 1 + Plan 2 (backend endpoints reachable).**

**Goal:** Build the React SPA covering the four screens (Home, Voting, Results, Shared Results), wire it to the backend, configure SPA fallback so the ASP.NET server serves it from `wwwroot`.

**Architecture:** Vite + TypeScript React app under `src/Nomelo.Client/`. Production build outputs to `src/Nomelo.Server/wwwroot/`. Dev server proxies `/api`, `/login`, `/logout`, `/signin-oidc` to the backend on port 5000. Routing via React Router. State management kept local — no Redux/Zustand; React Query (`@tanstack/react-query`) handles server state and caching. Cookie-based auth, so the frontend never touches tokens; a 401 from any API call triggers a redirect to `/login?returnUrl=...`. Component tests with Vitest + React Testing Library.

**Tech Stack:** React 18, TypeScript 5, Vite 5, React Router 6, @tanstack/react-query 5, Vitest, @testing-library/react, MSW for API mocking in tests.

---

## File Structure

```
src/Nomelo.Client/
├── package.json
├── tsconfig.json
├── tsconfig.node.json
├── vite.config.ts
├── vitest.config.ts
├── index.html
├── src/
│   ├── main.tsx                                # bootstrap + QueryClient + Router
│   ├── App.tsx                                 # route table
│   ├── api/
│   │   ├── client.ts                           # fetch wrapper, 401 handling
│   │   ├── types.ts                            # mirrors Shared DTOs
│   │   └── hooks.ts                            # React Query hooks
│   ├── auth/
│   │   └── AuthGate.tsx                        # checks /api/me, redirects to /login
│   ├── routes/
│   │   ├── HomePage.tsx                        # screen 1
│   │   ├── NewSessionDialog.tsx
│   │   ├── VotingPage.tsx                      # screen 2 (landscape)
│   │   ├── ResultsPage.tsx                     # screen 3
│   │   └── SharedResultsPage.tsx               # screen 4 (no auth)
│   ├── components/
│   │   ├── NameCard.tsx                        # one of two voting cards
│   │   ├── StabilityBanner.tsx
│   │   ├── RankedTable.tsx
│   │   └── OrientationHint.tsx                 # portrait on voting screen warning
│   ├── styles/
│   │   ├── global.css
│   │   └── voting.css                          # landscape-specific
│   └── test/
│       ├── setup.ts                            # MSW + RTL setup
│       ├── handlers.ts                         # MSW request handlers
│       └── components/
│           ├── NameCard.test.tsx
│           ├── StabilityBanner.test.tsx
│           ├── RankedTable.test.tsx
│           ├── VotingPage.test.tsx
│           └── HomePage.test.tsx
└── public/
    └── favicon.svg
```

**Boundaries:**
- `api/` owns all HTTP; nothing else calls `fetch` directly.
- `routes/` are screen-level containers; they consume `api/hooks.ts` and render `components/`.
- `components/` are presentational, taking props only — easy to test.
- `auth/AuthGate` wraps protected routes once at the layout level.

---

## Task 1: Vite + React + TS scaffolding

**Files:**
- Create: `src/Nomelo.Client/package.json`
- Create: `src/Nomelo.Client/tsconfig.json`
- Create: `src/Nomelo.Client/tsconfig.node.json`
- Create: `src/Nomelo.Client/vite.config.ts`
- Create: `src/Nomelo.Client/index.html`
- Create: `src/Nomelo.Client/src/main.tsx`
- Create: `src/Nomelo.Client/src/App.tsx`
- Create: `src/Nomelo.Client/src/styles/global.css`
- Create: `src/Nomelo.Client/public/favicon.svg`
- Modify: `src/Nomelo.Client/Nomelo.Client.csproj` (Plan 1 placeholder → real build target)

- [ ] **Step 1: Delete the Class Library placeholder content**

```bash
cd "C:\Users\TristanROCHE\Documents\Projet Perso\Nomelo/src/Nomelo.Client"
rm -f Class1.cs
```

- [ ] **Step 2: Write package.json**

Create `src/Nomelo.Client/package.json`:

```json
{
  "name": "nomelo-client",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview",
    "test": "vitest run",
    "test:watch": "vitest"
  },
  "dependencies": {
    "@tanstack/react-query": "^5.59.0",
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "react-router-dom": "^6.27.0"
  },
  "devDependencies": {
    "@testing-library/jest-dom": "^6.5.0",
    "@testing-library/react": "^16.0.1",
    "@testing-library/user-event": "^14.5.2",
    "@types/react": "^18.3.11",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.2",
    "jsdom": "^25.0.1",
    "msw": "^2.4.9",
    "typescript": "^5.6.2",
    "vite": "^5.4.8",
    "vitest": "^2.1.2"
  }
}
```

- [ ] **Step 3: Write tsconfig files**

Create `src/Nomelo.Client/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "useDefineForClassFields": true,
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,
    "types": ["vitest/globals", "@testing-library/jest-dom"]
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

Create `src/Nomelo.Client/tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "composite": true,
    "skipLibCheck": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true,
    "strict": true
  },
  "include": ["vite.config.ts", "vitest.config.ts"]
}
```

- [ ] **Step 4: Write vite.config.ts**

Create `src/Nomelo.Client/vite.config.ts`:

```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: path.resolve(__dirname, "../Nomelo.Server/wwwroot"),
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5000",
      "/login": "http://localhost:5000",
      "/logout": "http://localhost:5000",
      "/signin-oidc": "http://localhost:5000",
      "/signout-callback-oidc": "http://localhost:5000",
    },
  },
});
```

- [ ] **Step 5: Write index.html**

Create `src/Nomelo.Client/index.html`:

```html
<!doctype html>
<html lang="fr">
  <head>
    <meta charset="UTF-8" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Nomelo</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

- [ ] **Step 6: Write minimal App + main**

Create `src/Nomelo.Client/src/main.tsx`:

```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import "./styles/global.css";

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false, staleTime: 30_000 } },
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);
```

Create `src/Nomelo.Client/src/App.tsx`:

```tsx
export default function App() {
  return <div>Nomelo</div>;
}
```

Create `src/Nomelo.Client/src/styles/global.css`:

```css
:root { color-scheme: light dark; font-family: system-ui, sans-serif; }
* { box-sizing: border-box; }
body { margin: 0; }
```

Create `src/Nomelo.Client/public/favicon.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><circle cx="32" cy="32" r="28" fill="#3b82f6"/></svg>
```

- [ ] **Step 7: Make Nomelo.Client.csproj build the React app**

Replace `src/Nomelo.Client/Nomelo.Client.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <EnableDefaultContentItems>false</EnableDefaultContentItems>
    <NoWarn>$(NoWarn);NU1503</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <None Include="package.json" />
    <None Include="vite.config.ts" />
  </ItemGroup>

  <Target Name="EnsureNodeModules" BeforeTargets="Build" Condition="!Exists('node_modules')">
    <Exec Command="npm install" WorkingDirectory="$(MSBuildProjectDirectory)" />
  </Target>

  <Target Name="BuildReactApp" AfterTargets="Build">
    <Exec Command="npm run build" WorkingDirectory="$(MSBuildProjectDirectory)" />
  </Target>
</Project>
```

The csproj remains an empty .NET project (it isn't referenced by Server) but `dotnet build` of the solution now triggers the npm build. This keeps the contract from Plan 1 intact.

- [ ] **Step 8: Install dependencies and verify**

```bash
cd src/Nomelo.Client
npm install
npm run build
```

Expected: `wwwroot/index.html` and `wwwroot/assets/*` produced.

- [ ] **Step 9: Add a .gitignore entry for node_modules and dist**

Verify root `.gitignore` already contains `node_modules/` and `src/Nomelo.Server/wwwroot/`. From Plan 1 it should. If not, add them.

- [ ] **Step 10: Commit**

```bash
git add src/Nomelo.Client
git commit -m "feat: scaffold React + Vite + TS client with build output to Server/wwwroot"
```

---

## Task 2: API client + types

**Files:**
- Create: `src/Nomelo.Client/src/api/types.ts`
- Create: `src/Nomelo.Client/src/api/client.ts`

- [ ] **Step 1: Write types mirroring Shared DTOs**

Create `src/Nomelo.Client/src/api/types.ts`:

```ts
export interface ListDto {
  id: string;
  name: string;
  itemCount: number;
}

export interface SessionDto {
  id: string;
  listId: string;
  listName: string;
  confidenceThreshold: number;
  createdAt: string;
  updatedAt: string;
  shareToken: string | null;
  voteCount: number;
}

export interface CreateSessionRequest {
  listId: string;
  confidenceThreshold: number;
}

export interface PairItemDto {
  value: string;
  variants: string[];
  description: string | null;
}

export interface PairDto {
  a: PairItemDto;
  b: PairItemDto;
}

export type VoteResult =
  | "prefer_a"
  | "prefer_b"
  | "ban_a"
  | "ban_b"
  | "ban_both"
  | "like_both";

export interface VoteRequest {
  itemA: string;
  itemB: string;
  result: VoteResult;
}

export interface RankedItemDto {
  rank: number;
  value: string;
  variants: string[];
  eloScore: number;
  timesShown: number;
  isBanned: boolean;
}

export interface ResultsDto {
  sessionId: string;
  listId: string;
  listName: string;
  voteCount: number;
  stabilityReached: boolean;
  ranked: RankedItemDto[];
  banned: RankedItemDto[];
}

export interface MeDto {
  userId: string;
}
```

- [ ] **Step 2: Write the fetch client**

Create `src/Nomelo.Client/src/api/client.ts`:

```ts
export class UnauthorizedError extends Error {
  constructor() { super("unauthorized"); this.name = "UnauthorizedError"; }
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {}
): Promise<T> {
  const res = await fetch(path, {
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      ...(init.headers ?? {}),
    },
    ...init,
  });

  if (res.status === 401) throw new UnauthorizedError();
  if (res.status === 204) return undefined as T;

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`API ${res.status}: ${text || res.statusText}`);
  }

  if (res.headers.get("content-type")?.includes("application/json")) {
    return (await res.json()) as T;
  }
  return undefined as T;
}

export function redirectToLogin(returnUrl: string = window.location.pathname) {
  window.location.href = `/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}
```

- [ ] **Step 3: Verify build**

Run: `npm run build`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Client/src/api
git commit -m "feat(client): add typed API DTOs and fetch wrapper with 401 handling"
```

---

## Task 3: API hooks (React Query)

**Files:**
- Create: `src/Nomelo.Client/src/api/hooks.ts`

- [ ] **Step 1: Write hooks**

Create `src/Nomelo.Client/src/api/hooks.ts`:

```ts
import {
  useQuery,
  useMutation,
  useQueryClient,
} from "@tanstack/react-query";
import { apiFetch } from "./client";
import type {
  CreateSessionRequest,
  ListDto,
  MeDto,
  PairDto,
  ResultsDto,
  SessionDto,
  VoteRequest,
} from "./types";

export const useMe = () =>
  useQuery({
    queryKey: ["me"],
    queryFn: () => apiFetch<MeDto>("/api/me"),
  });

export const useLists = () =>
  useQuery({
    queryKey: ["lists"],
    queryFn: () => apiFetch<ListDto[]>("/api/lists"),
  });

export const useSessions = () =>
  useQuery({
    queryKey: ["sessions"],
    queryFn: () => apiFetch<SessionDto[]>("/api/sessions"),
  });

export const useSession = (id: string | undefined) =>
  useQuery({
    queryKey: ["session", id],
    queryFn: () => apiFetch<SessionDto>(`/api/sessions/${id}`),
    enabled: !!id,
  });

export const useCreateSession = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateSessionRequest) =>
      apiFetch<SessionDto>("/api/sessions", {
        method: "POST",
        body: JSON.stringify(req),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["sessions"] }),
  });
};

export const useNextPair = (sessionId: string | undefined) =>
  useQuery({
    queryKey: ["next-pair", sessionId],
    queryFn: () => apiFetch<PairDto>(`/api/sessions/${sessionId}/next-pair`),
    enabled: !!sessionId,
    staleTime: 0,
    refetchOnWindowFocus: false,
  });

export const useSubmitVote = (sessionId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: VoteRequest) =>
      apiFetch<void>(`/api/sessions/${sessionId}/votes`, {
        method: "POST",
        body: JSON.stringify(req),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["next-pair", sessionId] });
      qc.invalidateQueries({ queryKey: ["session", sessionId] });
      qc.invalidateQueries({ queryKey: ["results", sessionId] });
    },
  });
};

export const useResults = (sessionId: string | undefined) =>
  useQuery({
    queryKey: ["results", sessionId],
    queryFn: () => apiFetch<ResultsDto>(`/api/sessions/${sessionId}/results`),
    enabled: !!sessionId,
  });

export const useSharedResults = (token: string | undefined) =>
  useQuery({
    queryKey: ["share", token],
    queryFn: () => apiFetch<ResultsDto>(`/api/share/${token}`),
    enabled: !!token,
  });
```

- [ ] **Step 2: Verify build**

Run: `npm run build`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Nomelo.Client/src/api/hooks.ts
git commit -m "feat(client): add React Query hooks for lists, sessions, voting, results, share"
```

---

## Task 4: AuthGate + global 401 handling

**Files:**
- Create: `src/Nomelo.Client/src/auth/AuthGate.tsx`
- Modify: `src/Nomelo.Client/src/main.tsx`

- [ ] **Step 1: Write AuthGate**

Create `src/Nomelo.Client/src/auth/AuthGate.tsx`:

```tsx
import { useMe } from "../api/hooks";
import { redirectToLogin, UnauthorizedError } from "../api/client";
import { useEffect, type ReactNode } from "react";

export function AuthGate({ children }: { children: ReactNode }) {
  const { data, isLoading, error } = useMe();

  useEffect(() => {
    if (error instanceof UnauthorizedError) redirectToLogin();
  }, [error]);

  if (isLoading) return <div role="status">Chargement…</div>;
  if (!data) return null;
  return <>{children}</>;
}
```

- [ ] **Step 2: Wire global 401 handler in main.tsx**

Replace `src/Nomelo.Client/src/main.tsx` with:

```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider, QueryCache, MutationCache } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { redirectToLogin, UnauthorizedError } from "./api/client";
import "./styles/global.css";

const onError = (err: unknown) => {
  if (err instanceof UnauthorizedError) redirectToLogin();
};

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false, staleTime: 30_000 } },
  queryCache: new QueryCache({ onError }),
  mutationCache: new MutationCache({ onError }),
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);
```

- [ ] **Step 3: Verify build**

Run: `npm run build`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Client/src/auth src/Nomelo.Client/src/main.tsx
git commit -m "feat(client): add AuthGate and global 401 → /login redirect"
```

---

## Task 5: Vitest + MSW test setup

**Files:**
- Create: `src/Nomelo.Client/vitest.config.ts`
- Create: `src/Nomelo.Client/src/test/setup.ts`
- Create: `src/Nomelo.Client/src/test/handlers.ts`

- [ ] **Step 1: Write vitest config**

Create `src/Nomelo.Client/vitest.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
```

- [ ] **Step 2: Write test setup**

Create `src/Nomelo.Client/src/test/setup.ts`:

```ts
import "@testing-library/jest-dom/vitest";
import { afterAll, afterEach, beforeAll } from "vitest";
import { setupServer } from "msw/node";
import { handlers } from "./handlers";

export const server = setupServer(...handlers);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

- [ ] **Step 3: Write default MSW handlers**

Create `src/Nomelo.Client/src/test/handlers.ts`:

```ts
import { http, HttpResponse } from "msw";
import type {
  ListDto,
  PairDto,
  ResultsDto,
  SessionDto,
} from "../api/types";

const ALICE: SessionDto = {
  id: "00000000-0000-0000-0000-000000000001",
  listId: "a",
  listName: "Liste A",
  confidenceThreshold: 3,
  createdAt: "2026-05-01T00:00:00Z",
  updatedAt: "2026-05-02T00:00:00Z",
  shareToken: "share-tok",
  voteCount: 7,
};

const LIST: ListDto = { id: "a", name: "Liste A", itemCount: 12 };

const PAIR: PairDto = {
  a: { value: "Alice", variants: ["Alicia"], description: "prénom A" },
  b: { value: "Bob", variants: [], description: null },
};

const RESULTS: ResultsDto = {
  sessionId: ALICE.id,
  listId: ALICE.listId,
  listName: ALICE.listName,
  voteCount: 7,
  stabilityReached: false,
  ranked: [
    { rank: 1, value: "Alice", variants: ["Alicia"], eloScore: 1080, timesShown: 5, isBanned: false },
    { rank: 2, value: "Carol", variants: [], eloScore: 990, timesShown: 4, isBanned: false },
  ],
  banned: [
    { rank: 0, value: "Bob", variants: [], eloScore: 1000, timesShown: 3, isBanned: true },
  ],
};

export const handlers = [
  http.get("/api/me", () => HttpResponse.json({ userId: "u-1" })),
  http.get("/api/lists", () => HttpResponse.json([LIST])),
  http.get("/api/sessions", () => HttpResponse.json([ALICE])),
  http.get(`/api/sessions/${ALICE.id}`, () => HttpResponse.json(ALICE)),
  http.post("/api/sessions", () => HttpResponse.json(ALICE, { status: 201 })),
  http.get(`/api/sessions/${ALICE.id}/next-pair`, () => HttpResponse.json(PAIR)),
  http.post(`/api/sessions/${ALICE.id}/votes`, () => new HttpResponse(null, { status: 204 })),
  http.get(`/api/sessions/${ALICE.id}/results`, () => HttpResponse.json(RESULTS)),
  http.get(`/api/share/${ALICE.shareToken}`, () => HttpResponse.json(RESULTS)),
];

export const fixtures = { ALICE, LIST, PAIR, RESULTS };
```

- [ ] **Step 4: Run vitest with no tests to verify config**

Run: `npm test`
Expected: "No test files found" or similar — config valid.

- [ ] **Step 5: Commit**

```bash
git add src/Nomelo.Client/vitest.config.ts src/Nomelo.Client/src/test
git commit -m "test(client): set up Vitest + MSW with default fixture handlers"
```

---

## Task 6: NameCard component (TDD)

**Files:**
- Create: `src/Nomelo.Client/src/components/NameCard.tsx`
- Test: `src/Nomelo.Client/src/test/components/NameCard.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/Nomelo.Client/src/test/components/NameCard.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NameCard } from "../../components/NameCard";

const item = { value: "Alice", variants: ["Alicia", "Alix"], description: "from old-French" };

test("renders value and variants", () => {
  render(<NameCard item={item} onPrefer={() => {}} onBan={() => {}} side="A" />);
  expect(screen.getByText("Alice")).toBeInTheDocument();
  expect(screen.getByText(/Alicia/)).toBeInTheDocument();
  expect(screen.getByText(/Alix/)).toBeInTheDocument();
});

test("clicking Préférer fires onPrefer", async () => {
  const onPrefer = vi.fn();
  render(<NameCard item={item} onPrefer={onPrefer} onBan={() => {}} side="A" />);
  await userEvent.click(screen.getByRole("button", { name: /Préférer Alice/i }));
  expect(onPrefer).toHaveBeenCalledTimes(1);
});

test("clicking Bannir fires onBan", async () => {
  const onBan = vi.fn();
  render(<NameCard item={item} onPrefer={() => {}} onBan={onBan} side="A" />);
  await userEvent.click(screen.getByRole("button", { name: /Bannir Alice/i }));
  expect(onBan).toHaveBeenCalledTimes(1);
});

test("description is hidden by default and shown on info toggle", async () => {
  render(<NameCard item={item} onPrefer={() => {}} onBan={() => {}} side="A" />);
  expect(screen.queryByText(item.description)).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Plus d'infos sur Alice/i }));
  expect(screen.getByText(item.description)).toBeInTheDocument();
});

test("no variants section when array is empty", () => {
  render(<NameCard item={{ value: "Bob", variants: [], description: null }} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByTestId("variants")).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `npm test -- NameCard`
Expected: FAIL (component missing).

- [ ] **Step 3: Implement NameCard**

Create `src/Nomelo.Client/src/components/NameCard.tsx`:

```tsx
import { useState } from "react";
import type { PairItemDto } from "../api/types";

interface Props {
  item: PairItemDto;
  side: "A" | "B";
  onPrefer: () => void;
  onBan: () => void;
}

export function NameCard({ item, side, onPrefer, onBan }: Props) {
  const [showDesc, setShowDesc] = useState(false);
  return (
    <section className="name-card" data-side={side}>
      <h2 className="name-card__value">{item.value}</h2>
      {item.variants.length > 0 && (
        <p className="name-card__variants" data-testid="variants">
          {item.variants.join(" · ")}
        </p>
      )}
      {item.description && (
        <>
          <button
            type="button"
            className="name-card__info"
            aria-expanded={showDesc}
            aria-label={`Plus d'infos sur ${item.value}`}
            onClick={() => setShowDesc((v) => !v)}
          >
            i
          </button>
          {showDesc && <p className="name-card__description">{item.description}</p>}
        </>
      )}
      <div className="name-card__actions">
        <button type="button" onClick={onBan} aria-label={`Bannir ${item.value}`}>
          🚫 Bannir
        </button>
        <button type="button" onClick={onPrefer} aria-label={`Préférer ${item.value}`}>
          ❤️ Préférer
        </button>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `npm test -- NameCard`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Nomelo.Client/src/components/NameCard.tsx src/Nomelo.Client/src/test/components/NameCard.test.tsx
git commit -m "feat(client): add NameCard component with prefer/ban actions and description toggle"
```

---

## Task 7: StabilityBanner & OrientationHint

**Files:**
- Create: `src/Nomelo.Client/src/components/StabilityBanner.tsx`
- Create: `src/Nomelo.Client/src/components/OrientationHint.tsx`
- Test: `src/Nomelo.Client/src/test/components/StabilityBanner.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/Nomelo.Client/src/test/components/StabilityBanner.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StabilityBanner } from "../../components/StabilityBanner";

test("renders the FR message", () => {
  render(<StabilityBanner onDismiss={() => {}} />);
  expect(screen.getByText(/résultats semblent stables/i)).toBeInTheDocument();
});

test("dismiss button fires callback", async () => {
  const onDismiss = vi.fn();
  render(<StabilityBanner onDismiss={onDismiss} />);
  await userEvent.click(screen.getByRole("button", { name: /Fermer/i }));
  expect(onDismiss).toHaveBeenCalled();
});
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `npm test -- StabilityBanner`
Expected: FAIL.

- [ ] **Step 3: Implement StabilityBanner**

Create `src/Nomelo.Client/src/components/StabilityBanner.tsx`:

```tsx
interface Props { onDismiss: () => void; }

export function StabilityBanner({ onDismiss }: Props) {
  return (
    <div role="status" className="stability-banner">
      <p>Vos résultats semblent stables depuis 100 votes. Vous pouvez continuer pour affiner.</p>
      <button type="button" onClick={onDismiss} aria-label="Fermer">×</button>
    </div>
  );
}
```

- [ ] **Step 4: Implement OrientationHint**

Create `src/Nomelo.Client/src/components/OrientationHint.tsx`:

```tsx
export function OrientationHint() {
  return (
    <div className="orientation-hint" role="status">
      <p>Pour une meilleure expérience, tournez votre appareil en mode paysage.</p>
    </div>
  );
}
```

- [ ] **Step 5: Run tests**

Run: `npm test -- StabilityBanner`
Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Client/src/components/StabilityBanner.tsx src/Nomelo.Client/src/components/OrientationHint.tsx src/Nomelo.Client/src/test/components/StabilityBanner.test.tsx
git commit -m "feat(client): add StabilityBanner and OrientationHint components"
```

---

## Task 8: RankedTable (TDD)

**Files:**
- Create: `src/Nomelo.Client/src/components/RankedTable.tsx`
- Test: `src/Nomelo.Client/src/test/components/RankedTable.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/Nomelo.Client/src/test/components/RankedTable.test.tsx`:

```tsx
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RankedTable } from "../../components/RankedTable";
import { fixtures } from "../handlers";

test("renders ranked items in order with score rounding", () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={[]} />);
  const rows = screen.getAllByRole("row").slice(1);
  expect(within(rows[0]).getByText("Alice")).toBeInTheDocument();
  expect(within(rows[0]).getByText("1080")).toBeInTheDocument();
  expect(within(rows[1]).getByText("Carol")).toBeInTheDocument();
});

test("banned items hidden by default, revealed on expand", async () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={fixtures.RESULTS.banned} />);
  expect(screen.queryByText("Bob")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /1 banni/i }));
  expect(screen.getByText("Bob")).toBeInTheDocument();
});

test("empty banned list does not render expander", () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={[]} />);
  expect(screen.queryByRole("button", { name: /banni/i })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `npm test -- RankedTable`
Expected: FAIL.

- [ ] **Step 3: Implement RankedTable**

Create `src/Nomelo.Client/src/components/RankedTable.tsx`:

```tsx
import { useState } from "react";
import type { RankedItemDto } from "../api/types";

interface Props { ranked: RankedItemDto[]; banned: RankedItemDto[]; }

function Row({ item, showRank }: { item: RankedItemDto; showRank: boolean }) {
  return (
    <tr>
      <td>{showRank ? item.rank : ""}</td>
      <td>
        {item.value}
        {item.variants.length > 0 && <span className="muted"> · {item.variants.join(", ")}</span>}
      </td>
      <td>{Math.round(item.eloScore)}</td>
      <td>{item.timesShown}</td>
    </tr>
  );
}

export function RankedTable({ ranked, banned }: Props) {
  const [showBanned, setShowBanned] = useState(false);
  const bannedLabel = banned.length === 1 ? "1 banni" : `${banned.length} bannis`;
  return (
    <div className="ranked-table">
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Prénom</th>
            <th>ELO</th>
            <th>Vu</th>
          </tr>
        </thead>
        <tbody>
          {ranked.map((r) => <Row key={r.value} item={r} showRank />)}
        </tbody>
      </table>

      {banned.length > 0 && (
        <>
          <button
            type="button"
            className="ranked-table__expand"
            onClick={() => setShowBanned((v) => !v)}
            aria-expanded={showBanned}
          >
            {showBanned ? "Masquer" : "Afficher"} {bannedLabel}
          </button>
          {showBanned && (
            <table>
              <tbody>
                {banned.map((r) => <Row key={r.value} item={r} showRank={false} />)}
              </tbody>
            </table>
          )}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `npm test -- RankedTable`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add src/Nomelo.Client/src/components/RankedTable.tsx src/Nomelo.Client/src/test/components/RankedTable.test.tsx
git commit -m "feat(client): add RankedTable component with collapsible banned section"
```

---

## Task 9: HomePage (TDD)

**Files:**
- Create: `src/Nomelo.Client/src/routes/HomePage.tsx`
- Create: `src/Nomelo.Client/src/routes/NewSessionDialog.tsx`
- Test: `src/Nomelo.Client/src/test/components/HomePage.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/Nomelo.Client/src/test/components/HomePage.test.tsx`:

```tsx
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { HomePage } from "../../routes/HomePage";

function wrap(ui: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/"]}>
        <Routes>
          <Route path="/" element={ui} />
          <Route path="/sessions/:id" element={<div>session-page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

test("lists user sessions", async () => {
  wrap(<HomePage />);
  expect(await screen.findByText("Liste A")).toBeInTheDocument();
  expect(screen.getByText(/7 votes/)).toBeInTheDocument();
});

test("clicking a session navigates to /sessions/:id", async () => {
  wrap(<HomePage />);
  await userEvent.click(await screen.findByRole("link", { name: /Liste A/i }));
  await waitFor(() => expect(screen.getByText("session-page")).toBeInTheDocument());
});

test("Nouvelle session opens the dialog", async () => {
  wrap(<HomePage />);
  await userEvent.click(screen.getByRole("button", { name: /Nouvelle session/i }));
  expect(await screen.findByRole("dialog")).toBeInTheDocument();
  expect(screen.getByLabelText(/Liste/i)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `npm test -- HomePage`
Expected: FAIL.

- [ ] **Step 3: Implement NewSessionDialog**

Create `src/Nomelo.Client/src/routes/NewSessionDialog.tsx`:

```tsx
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useLists, useCreateSession } from "../api/hooks";

interface Props { onClose: () => void; }

export function NewSessionDialog({ onClose }: Props) {
  const { data: lists } = useLists();
  const create = useCreateSession();
  const navigate = useNavigate();

  const [listId, setListId] = useState("");
  const [threshold, setThreshold] = useState(3);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!listId) return;
    const session = await create.mutateAsync({ listId, confidenceThreshold: threshold });
    onClose();
    navigate(`/sessions/${session.id}`);
  };

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <form role="dialog" className="dialog" onSubmit={submit} onClick={(e) => e.stopPropagation()}>
        <h2>Nouvelle session</h2>
        <label>
          Liste
          <select value={listId} onChange={(e) => setListId(e.target.value)} required>
            <option value="">— choisir —</option>
            {lists?.map((l) => (
              <option key={l.id} value={l.id}>{l.name} ({l.itemCount})</option>
            ))}
          </select>
        </label>
        <label>
          Seuil de confiance
          <input
            type="number" min={1} max={10}
            value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
          />
        </label>
        <div className="dialog__actions">
          <button type="button" onClick={onClose}>Annuler</button>
          <button type="submit" disabled={!listId || create.isPending}>Démarrer</button>
        </div>
      </form>
    </div>
  );
}
```

- [ ] **Step 4: Implement HomePage**

Create `src/Nomelo.Client/src/routes/HomePage.tsx`:

```tsx
import { useState } from "react";
import { Link } from "react-router-dom";
import { useSessions } from "../api/hooks";
import { NewSessionDialog } from "./NewSessionDialog";

export function HomePage() {
  const { data: sessions, isLoading } = useSessions();
  const [dialogOpen, setDialogOpen] = useState(false);

  return (
    <main className="home">
      <header className="home__header">
        <h1>Nomelo</h1>
        <button type="button" onClick={() => setDialogOpen(true)}>Nouvelle session</button>
      </header>

      {isLoading && <p>Chargement…</p>}

      <ul className="home__sessions">
        {sessions?.map((s) => (
          <li key={s.id}>
            <Link to={`/sessions/${s.id}`}>
              <strong>{s.listName}</strong>
              <span> · {s.voteCount} votes · {new Date(s.updatedAt).toLocaleDateString("fr-FR")}</span>
            </Link>
            {s.shareToken && (
              <a href={`/share/${s.shareToken}`} target="_blank" rel="noreferrer">
                Lien de partage
              </a>
            )}
          </li>
        ))}
      </ul>

      {sessions?.length === 0 && <p>Aucune session pour le moment.</p>}

      {dialogOpen && <NewSessionDialog onClose={() => setDialogOpen(false)} />}
    </main>
  );
}
```

- [ ] **Step 5: Run tests**

Run: `npm test -- HomePage`
Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Client/src/routes/HomePage.tsx src/Nomelo.Client/src/routes/NewSessionDialog.tsx src/Nomelo.Client/src/test/components/HomePage.test.tsx
git commit -m "feat(client): add HomePage with session list and NewSessionDialog"
```

---

## Task 10: VotingPage (TDD)

**Files:**
- Create: `src/Nomelo.Client/src/routes/VotingPage.tsx`
- Create: `src/Nomelo.Client/src/styles/voting.css`
- Test: `src/Nomelo.Client/src/test/components/VotingPage.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/Nomelo.Client/src/test/components/VotingPage.test.tsx`:

```tsx
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { http, HttpResponse } from "msw";
import { server } from "../setup";
import { VotingPage } from "../../routes/VotingPage";
import { fixtures } from "../handlers";

function wrap() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[`/sessions/${fixtures.ALICE.id}`]}>
        <Routes>
          <Route path="/sessions/:id" element={<VotingPage />} />
          <Route path="/sessions/:id/results" element={<div>results-page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

test("renders both names from /next-pair", async () => {
  wrap();
  expect(await screen.findByText("Alice")).toBeInTheDocument();
  expect(screen.getByText("Bob")).toBeInTheDocument();
});

test("clicking Préférer A submits prefer_a and fetches next pair", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /Préférer Alice/i }));

  await waitFor(() => expect(posted).toEqual({ itemA: "Alice", itemB: "Bob", result: "prefer_a" }));
});

test("J'aime les deux submits like_both", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /J'aime les deux/i }));
  await waitFor(() => expect(posted.result).toBe("like_both"));
});

test("Bannir les deux submits ban_both", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /Bannir les deux/i }));
  await waitFor(() => expect(posted.result).toBe("ban_both"));
});

test("voir résultats link present", async () => {
  wrap();
  await screen.findByText("Alice");
  expect(screen.getByRole("link", { name: /Voir les résultats/i })).toHaveAttribute(
    "href", `/sessions/${fixtures.ALICE.id}/results`
  );
});
```

- [ ] **Step 2: Run the test and verify it fails**

Run: `npm test -- VotingPage`
Expected: FAIL.

- [ ] **Step 3: Implement voting CSS**

Create `src/Nomelo.Client/src/styles/voting.css`:

```css
.voting { display: grid; grid-template-rows: auto 1fr auto; min-height: 100dvh; }
.voting__pair { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; padding: 1rem; }
.voting__shared { display: flex; justify-content: center; gap: 1rem; padding: 1rem; }
.name-card { display: flex; flex-direction: column; align-items: center; gap: 0.5rem; padding: 1rem; }
.name-card__value { font-size: clamp(2rem, 6vw, 4rem); margin: 0; text-align: center; }
.name-card__variants { color: #666; }
.name-card__actions { display: flex; gap: 0.5rem; margin-top: auto; }
@media (orientation: portrait) {
  .orientation-hint { display: block; background: #fff7d6; padding: 0.5rem; text-align: center; }
}
@media (orientation: landscape) { .orientation-hint { display: none; } }
```

- [ ] **Step 4: Implement VotingPage**

Create `src/Nomelo.Client/src/routes/VotingPage.tsx`:

```tsx
import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useNextPair, useSession, useSubmitVote } from "../api/hooks";
import { NameCard } from "../components/NameCard";
import { StabilityBanner } from "../components/StabilityBanner";
import { OrientationHint } from "../components/OrientationHint";
import type { VoteResult } from "../api/types";
import "../styles/voting.css";

export function VotingPage() {
  const { id = "" } = useParams();
  const pair = useNextPair(id);
  const session = useSession(id);
  const submit = useSubmitVote(id);
  const [bannerDismissed, setBannerDismissed] = useState(false);

  const send = (result: VoteResult) => {
    if (!pair.data) return;
    submit.mutate({ itemA: pair.data.a.value, itemB: pair.data.b.value, result });
  };

  if (pair.isLoading || !pair.data) return <p>Chargement…</p>;

  return (
    <main className="voting">
      <OrientationHint />

      <header className="voting__header">
        <Link to="/">← Accueil</Link>
        <span>{session.data?.listName}</span>
        <Link to={`/sessions/${id}/results`}>Voir les résultats</Link>
      </header>

      {/* stability banner placeholder — full wiring uses /results.stabilityReached */}
      {!bannerDismissed && session.data && session.data.voteCount >= 100 && (
        <StabilityBanner onDismiss={() => setBannerDismissed(true)} />
      )}

      <section className="voting__pair">
        <NameCard
          item={pair.data.a}
          side="A"
          onPrefer={() => send("prefer_a")}
          onBan={() => send("ban_a")}
        />
        <NameCard
          item={pair.data.b}
          side="B"
          onPrefer={() => send("prefer_b")}
          onBan={() => send("ban_b")}
        />
      </section>

      <footer className="voting__shared">
        <button type="button" onClick={() => send("ban_both")}>🚫 Bannir les deux</button>
        <button type="button" onClick={() => send("like_both")}>💛 J'aime les deux</button>
      </footer>
    </main>
  );
}
```

The stability check above uses `voteCount >= 100` as a placeholder trigger. The accurate `stabilityReached` flag lives in `/results` — we wire that properly in the ResultsPage. Keeping the banner here gives the user the in-flow notification once they have 100+ votes. Adjust threshold once Plan 2's `stabilityReached` is surfaced via `useSession` or a dedicated endpoint.

- [ ] **Step 5: Run tests**

Run: `npm test -- VotingPage`
Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add src/Nomelo.Client/src/routes/VotingPage.tsx src/Nomelo.Client/src/styles/voting.css src/Nomelo.Client/src/test/components/VotingPage.test.tsx
git commit -m "feat(client): add VotingPage with landscape pair UI and six vote actions"
```

---

## Task 11: ResultsPage + SharedResultsPage

**Files:**
- Create: `src/Nomelo.Client/src/routes/ResultsPage.tsx`
- Create: `src/Nomelo.Client/src/routes/SharedResultsPage.tsx`

- [ ] **Step 1: Write ResultsPage**

Create `src/Nomelo.Client/src/routes/ResultsPage.tsx`:

```tsx
import { Link, useParams } from "react-router-dom";
import { useResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";

export function ResultsPage() {
  const { id = "" } = useParams();
  const { data, isLoading } = useResults(id);

  if (isLoading || !data) return <p>Chargement…</p>;

  return (
    <main className="results">
      <header>
        <Link to="/">← Accueil</Link>
        <h1>{data.listName}</h1>
        <Link to={`/sessions/${id}`}>Continuer à voter</Link>
      </header>
      <p>{data.voteCount} votes</p>
      {data.stabilityReached && <p className="muted">Résultats stables depuis 100 votes.</p>}
      <RankedTable ranked={data.ranked} banned={data.banned} />
    </main>
  );
}
```

- [ ] **Step 2: Write SharedResultsPage**

Create `src/Nomelo.Client/src/routes/SharedResultsPage.tsx`:

```tsx
import { useParams } from "react-router-dom";
import { useSharedResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";

export function SharedResultsPage() {
  const { token = "" } = useParams();
  const { data, isLoading, error } = useSharedResults(token);

  if (isLoading) return <p>Chargement…</p>;
  if (error || !data) return <p>Résultats introuvables.</p>;

  return (
    <main className="results">
      <h1>{data.listName}</h1>
      <p className="muted">Vue partagée · lecture seule · {data.voteCount} votes</p>
      <RankedTable ranked={data.ranked} banned={data.banned} />
    </main>
  );
}
```

- [ ] **Step 3: Verify build**

Run: `npm run build`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Client/src/routes/ResultsPage.tsx src/Nomelo.Client/src/routes/SharedResultsPage.tsx
git commit -m "feat(client): add ResultsPage and public SharedResultsPage"
```

---

## Task 12: App route table

**Files:**
- Modify: `src/Nomelo.Client/src/App.tsx`

- [ ] **Step 1: Wire all routes**

Replace `src/Nomelo.Client/src/App.tsx`:

```tsx
import { Route, Routes } from "react-router-dom";
import { AuthGate } from "./auth/AuthGate";
import { HomePage } from "./routes/HomePage";
import { VotingPage } from "./routes/VotingPage";
import { ResultsPage } from "./routes/ResultsPage";
import { SharedResultsPage } from "./routes/SharedResultsPage";

export default function App() {
  return (
    <Routes>
      <Route path="/share/:token" element={<SharedResultsPage />} />
      <Route
        path="/*"
        element={
          <AuthGate>
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/sessions/:id" element={<VotingPage />} />
              <Route path="/sessions/:id/results" element={<ResultsPage />} />
              <Route path="*" element={<div>Page introuvable</div>} />
            </Routes>
          </AuthGate>
        }
      />
    </Routes>
  );
}
```

`/share/:token` is mounted outside `AuthGate` so anonymous users hit it without OIDC redirect.

- [ ] **Step 2: Verify build**

Run: `npm run build`
Expected: succeeds.

- [ ] **Step 3: Run full test suite**

Run: `npm test`
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Client/src/App.tsx
git commit -m "feat(client): wire route table with public /share/:token outside AuthGate"
```

---

## Task 13: Server-side SPA fallback

The React app uses BrowserRouter, so non-API routes must serve `index.html`. The static files come from `wwwroot`.

**Files:**
- Modify: `src/Nomelo.Server/Program.cs`

- [ ] **Step 1: Add static files + fallback**

Open `src/Nomelo.Server/Program.cs`. Replace the section between `app.UseAuthorization();` and `app.Run();` with:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapListsEndpoints();
app.MapSessionsEndpoints();
app.MapVotingEndpoints();
app.MapShareEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
```

The fallback must come **after** all API endpoint mappings so `/api/*` is not swallowed.

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: succeeds.

- [ ] **Step 3: Smoke test (manual)**

In one terminal:

```bash
docker run --rm -d --name ns-pg -e POSTGRES_DB=nomelo -e POSTGRES_USER=ns -e POSTGRES_PASSWORD=ns -p 5432:5432 postgres:16
cd src/Nomelo.Server
dotnet run
```

In another terminal:

```bash
cd src/Nomelo.Client
npm run dev
```

Open `http://localhost:5173/`. Expected: OIDC redirect to Authentik (or 401 if not configured). With auth configured: HomePage renders sessions list.

Then test SPA fallback via production-style: stop `npm run dev`, run `npm run build`, refresh `http://localhost:5000/`. Expected: index.html served, React app loads.

```bash
docker stop ns-pg
```

No commit — verification only.

- [ ] **Step 4: Commit**

```bash
git add src/Nomelo.Server/Program.cs
git commit -m "feat(server): serve SPA from wwwroot with index.html fallback for client routes"
```

---

## Self-Review Notes

**Spec coverage:**
- §9 Screen 1 (Home): Task 9 — session list, "Nouvelle session" with list selector and threshold (default 3, range 1–10), share link surfaced.
- §9 Screen 2 (Voting): Task 10 — landscape layout via CSS grid columns 1fr 1fr, six action buttons (prefer A/B, ban A/B, both variants), stability banner placeholder. Description toggle inline (Task 6 NameCard).
- §9 Screen 3 (Results): Task 11 — ranked table with rank/name/variants/ELO/times_shown, banned collapsed section.
- §9 Screen 4 (Shared): Task 11/12 — `/share/:token` route outside AuthGate, reuses RankedTable.
- §10 endpoints: all consumed via `api/hooks.ts` (Task 3).
- §3 Auth: AuthGate (Task 4) probes `/api/me`; 401 redirects to `/login?returnUrl=...`. Cookie is `httpOnly` so JS doesn't touch it (matches §3 SameSite=Strict requirement set on the server in Plan 1).

**Placeholder scan:** the stability banner trigger in VotingPage uses `voteCount >= 100` as a stand-in. Marked clearly with a comment, and the precise `stabilityReached` flag is shown in ResultsPage. Acceptable simplification; revisit if usage shows the banner firing too often.

**Type consistency:** `VoteResult` literal union matches the server's `TryParseResult` strings in Plan 2 (`prefer_a/prefer_b/ban_a/ban_b/ban_both/like_both`). `PairItemDto`/`PairDto`/`ResultsDto`/`RankedItemDto`/`SessionDto`/`ListDto` fields match Shared C# DTOs name-for-name (camelCase via default System.Text.Json policy on the server).

**Naming reconciliation:** the `useNextPair` hook uses key `["next-pair", sessionId]`; `useSubmitVote` invalidates the same key — consistent.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-18-nomelo-frontend.md`. Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
