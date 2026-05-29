import { useCallback, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useBulkBan, useBulkUnban, useResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";
import { compileQuery } from "../components/globSearch";
import "../styles/pages.css";

type ConfirmMode = "ban" | "unban" | null;

export function ResultsPage() {
  const { id = "" } = useParams();
  const { data, isLoading } = useResults(id);
  const bulkBan = useBulkBan(id);
  const bulkUnban = useBulkUnban(id);
  const [selectedRanked, setSelectedRanked] = useState<Set<string>>(new Set());
  const [selectedBanned, setSelectedBanned] = useState<Set<string>>(new Set());
  const [confirming, setConfirming] = useState<ConfirmMode>(null);
  const [search, setSearch] = useState("");

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
    await bulkBan.mutateAsync({ items: Array.from(selectedRanked) });
    setSelectedRanked(new Set());
    setConfirming(null);
  };

  const confirmUnban = async () => {
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
