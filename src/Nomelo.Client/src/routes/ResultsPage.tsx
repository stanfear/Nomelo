import { useCallback, useLayoutEffect, useMemo, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import type { RankedItemDto } from "../api/types";
import { useBulkBan, useBulkUnban, useResults } from "../api/hooks";
import { RankedTable, type RankedTableHandle } from "../components/RankedTable";
import { compileQuery } from "../components/globSearch";
import "../styles/pages.css";

type ConfirmMode = "ban" | "unban" | null;

type PendingAnchor =
  | { kind: "precise"; value: string; topPx: number }
  | { kind: "center"; value: string };

// Returns the rendered .ranked__row elements that belong to `items` (so the
// ban path only sees ranked rows, not banned ones, even if the banned section
// is expanded).
function visibleRowsFor(items: RankedItemDto[]): { value: string; rect: DOMRect }[] {
  const itemSet = new Set(items.map((r) => r.value));
  const result: { value: string; rect: DOMRect }[] = [];
  const rows = document.querySelectorAll<HTMLElement>(".ranked__row[data-row-value]");
  for (const row of rows) {
    const value = row.dataset.rowValue;
    if (!value || !itemSet.has(value)) continue;
    result.push({ value, rect: row.getBoundingClientRect() });
  }
  return result;
}

// If any non-selected row is currently rendered with its top edge inside the
// viewport, capture it so we can restore it to the exact same position after
// the mutation. This is the high-fidelity path.
function captureViewportAnchor(
  items: RankedItemDto[],
  selected: ReadonlySet<string>,
): PendingAnchor | null {
  for (const { value, rect } of visibleRowsFor(items)) {
    if (selected.has(value)) continue;
    if (rect.top < 0 || rect.top >= window.innerHeight) continue;
    return { kind: "precise", value, topPx: rect.top };
  }
  return null;
}

// Fallback when the whole visible window is selected. The anchor is the first
// non-selected row in `items` AFTER the last row currently visible (so we
// re-center on what was just past where the user was looking), with a fallback
// upwards if no survivor exists below.
function captureBoundaryAnchor(
  items: RankedItemDto[],
  selected: ReadonlySet<string>,
): PendingAnchor | null {
  if (selected.size === 0) return null;
  const visible = visibleRowsFor(items);
  let lastVisibleValue: string | null = null;
  let firstVisibleValue: string | null = null;
  for (const { value, rect } of visible) {
    if (rect.bottom <= 0 || rect.top >= window.innerHeight) continue;
    if (firstVisibleValue === null) firstVisibleValue = value;
    lastVisibleValue = value;
  }
  if (lastVisibleValue === null) return null;
  const lastVisibleIdx = items.findIndex((r) => r.value === lastVisibleValue);
  if (lastVisibleIdx < 0) return null;
  for (let i = lastVisibleIdx + 1; i < items.length; i++) {
    if (!selected.has(items[i].value)) return { kind: "center", value: items[i].value };
  }
  const firstVisibleIdx = firstVisibleValue
    ? items.findIndex((r) => r.value === firstVisibleValue)
    : -1;
  for (let i = (firstVisibleIdx >= 0 ? firstVisibleIdx : items.length) - 1; i >= 0; i--) {
    if (!selected.has(items[i].value)) return { kind: "center", value: items[i].value };
  }
  return null;
}

export function ResultsPage() {
  const { id = "" } = useParams();
  const { data, isLoading } = useResults(id);
  const bulkBan = useBulkBan(id);
  const bulkUnban = useBulkUnban(id);
  const [selectedRanked, setSelectedRanked] = useState<Set<string>>(new Set());
  const [selectedBanned, setSelectedBanned] = useState<Set<string>>(new Set());
  const [confirming, setConfirming] = useState<ConfirmMode>(null);
  const [search, setSearch] = useState("");
  const pendingAnchor = useRef<PendingAnchor | null>(null);
  const tableRef = useRef<RankedTableHandle>(null);

  // After a bulk action invalidates the results query and the refreshed data
  // re-renders, restore the user's scroll position. Two strategies:
  //  - "precise": a non-selected row was visible; put it back at the same
  //    viewport offset so the page feels unchanged minus the removed rows.
  //  - "center": the whole visible window was selected; recenter on the
  //    surviving boundary row instead. We wait two animation frames for
  //    Virtuoso to lay out the new visible window before scrolling.
  useLayoutEffect(() => {
    const anchor = pendingAnchor.current;
    if (!anchor) return;
    let raf2 = 0;
    const raf1 = requestAnimationFrame(() => {
      raf2 = requestAnimationFrame(() => {
        if (anchor.kind === "precise") {
          tableRef.current?.scrollToValue(anchor.value, { align: "start", topPx: anchor.topPx });
        } else {
          tableRef.current?.scrollToValue(anchor.value, { align: "center" });
        }
        pendingAnchor.current = null;
      });
    });
    return () => {
      cancelAnimationFrame(raf1);
      cancelAnimationFrame(raf2);
    };
  }, [data]);

  const matcher = useMemo(() => compileQuery(search), [search]);

  const filteredRanked = useMemo(
    () => (matcher && data ? data.ranked.filter((r) => matcher(r.value)) : data?.ranked ?? []),
    [matcher, data],
  );
  const filteredBanned = useMemo(
    () => (matcher && data ? data.banned.filter((r) => matcher(r.value)) : data?.banned ?? []),
    [matcher, data],
  );

  const toggleRanked = useCallback((value: string) => {
    setSelectedRanked((prev) => {
      const next = new Set(prev);
      if (next.has(value)) next.delete(value);
      else next.add(value);
      return next;
    });
  }, []);

  const toggleBanned = useCallback((value: string) => {
    setSelectedBanned((prev) => {
      const next = new Set(prev);
      if (next.has(value)) next.delete(value);
      else next.add(value);
      return next;
    });
  }, []);

  const clearAll = () => {
    setSelectedRanked(new Set());
    setSelectedBanned(new Set());
  };

  const confirmBan = async () => {
    pendingAnchor.current =
      captureViewportAnchor(filteredRanked, selectedRanked) ??
      captureBoundaryAnchor(filteredRanked, selectedRanked);
    await bulkBan.mutateAsync({ items: Array.from(selectedRanked) });
    setSelectedRanked(new Set());
    setConfirming(null);
  };

  const confirmUnban = async () => {
    pendingAnchor.current =
      captureViewportAnchor(filteredBanned, selectedBanned) ??
      captureBoundaryAnchor(filteredBanned, selectedBanned);
    await bulkUnban.mutateAsync({ items: Array.from(selectedBanned) });
    setSelectedBanned(new Set());
    setConfirming(null);
  };

  if (isLoading || !data) return <p className="page-loading">Chargement…</p>;

  const banCount = selectedRanked.size;
  const unbanCount = selectedBanned.size;
  const pending = bulkBan.isPending || bulkUnban.isPending;
  const hasSelection = banCount > 0 || unbanCount > 0;

  return (
    <main className="results">
      <header className="page__top">
        <Link to="/">← Accueil</Link>
        <span className="page__top-title">Résultats</span>
        <Link to={`/sessions/${id}`}>Continuer à voter →</Link>
      </header>

      <div className="results__main">
        <section className="results__hero">
          <p className="results__hero-eyebrow">Classement</p>
          <h1 className="results__hero-title">{data.name ?? data.listName}</h1>
          {data.name && (
            <p className="results__hero-sublabel">{data.listName}</p>
          )}
        </section>

        <section className="results__stats">
          <div className="stat-card">
            <span className="stat-card__value">{data.voteCount}</span>
            <span className="stat-card__label">Votes</span>
          </div>
          <div className="stat-card">
            <span className="stat-card__value">{data.ranked.length}</span>
            <span className="stat-card__label">Actifs</span>
          </div>
          <div className="stat-card">
            <span className="stat-card__value">{data.banned.length}</span>
            <span className="stat-card__label">Bannis</span>
          </div>
        </section>

        {data.stabilityReached && (
          <div className="results__stability" role="status">
            <span className="results__stability-icon" aria-hidden>★</span>
            <span>Résultats stables depuis 100 votes.</span>
          </div>
        )}

        <div className="results__search">
          <input
            type="search"
            className="results__search-input"
            placeholder="Rechercher (utilisez * et ? pour un glob)"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Rechercher dans le classement"
          />
          {matcher && (
            <span className="results__search-count">
              {filteredRanked.length + filteredBanned.length} résultat
              {filteredRanked.length + filteredBanned.length > 1 ? "s" : ""}
              <span className="results__search-count-breakdown">
                {" "}({filteredRanked.length} actif{filteredRanked.length > 1 ? "s" : ""}
                {" · "}
                {filteredBanned.length} banni{filteredBanned.length > 1 ? "s" : ""})
              </span>
            </span>
          )}
        </div>

        <RankedTable
          ref={tableRef}
          ranked={filteredRanked}
          banned={filteredBanned}
          selection={{ selected: selectedRanked, onToggle: toggleRanked }}
          bannedSelection={{ selected: selectedBanned, onToggle: toggleBanned }}
        />
      </div>

      {hasSelection && (
        <div className="bulk-bar" role="region" aria-label="Action en lot">
          <span className="bulk-bar__count">
            {banCount > 0 && (
              <>
                {banCount} à bannir
                {unbanCount > 0 && " · "}
              </>
            )}
            {unbanCount > 0 && <>{unbanCount} à restaurer</>}
          </span>
          <button type="button" className="bulk-bar__cancel" onClick={clearAll}>
            Annuler
          </button>
          {unbanCount > 0 && (
            <button
              type="button"
              className="bulk-bar__action bulk-bar__action--restore"
              onClick={() => setConfirming("unban")}
              disabled={pending}
            >
              Restaurer
            </button>
          )}
          {banCount > 0 && (
            <button
              type="button"
              className="bulk-bar__action"
              onClick={() => setConfirming("ban")}
              disabled={pending}
            >
              Bannir
            </button>
          )}
        </div>
      )}

      {confirming === "ban" && (
        <div className="dialog-backdrop" role="dialog" aria-modal="true">
          <div className="dialog">
            <h2 className="dialog__title">Bannir {banCount} nom{banCount > 1 ? "s" : ""} ?</h2>
            <p className="dialog__body">
              Cette action retire les éléments du classement et les place dans la liste des bannis.
              Elle n'est pas annulable depuis le bouton retour de vote, mais reste réversible
              depuis cette même page via "Restaurer".
            </p>
            <div className="dialog__actions">
              <button
                type="button"
                className="dialog__cancel"
                onClick={() => setConfirming(null)}
                disabled={pending}
              >
                Annuler
              </button>
              <button
                type="button"
                className="dialog__confirm dialog__confirm--danger"
                onClick={confirmBan}
                disabled={pending}
              >
                {bulkBan.isPending ? "Bannissement…" : "Bannir"}
              </button>
            </div>
          </div>
        </div>
      )}

      {confirming === "unban" && (
        <div className="dialog-backdrop" role="dialog" aria-modal="true">
          <div className="dialog">
            <h2 className="dialog__title">Restaurer {unbanCount} nom{unbanCount > 1 ? "s" : ""} ?</h2>
            <p className="dialog__body">
              Les éléments sélectionnés sortent de la liste des bannis et réintègrent le classement
              avec leur score Elo et leur nombre d'affichages conservés.
            </p>
            <div className="dialog__actions">
              <button
                type="button"
                className="dialog__cancel"
                onClick={() => setConfirming(null)}
                disabled={pending}
              >
                Annuler
              </button>
              <button
                type="button"
                className="dialog__confirm"
                onClick={confirmUnban}
                disabled={pending}
              >
                {bulkUnban.isPending ? "Restauration…" : "Restaurer"}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
