import { useCallback, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useBulkBan, useResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";
import "../styles/pages.css";

export function ResultsPage() {
  const { id = "" } = useParams();
  const { data, isLoading } = useResults(id);
  const bulkBan = useBulkBan(id);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [confirming, setConfirming] = useState(false);

  const toggle = useCallback((value: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(value)) next.delete(value);
      else next.add(value);
      return next;
    });
  }, []);

  const clear = () => setSelected(new Set());

  const confirmBan = async () => {
    await bulkBan.mutateAsync({ items: Array.from(selected) });
    clear();
    setConfirming(false);
  };

  if (isLoading || !data) return <p className="page-loading">Chargement…</p>;

  const count = selected.size;

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
            <span className="stat-card__label">Classés</span>
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

        <RankedTable
          ranked={data.ranked}
          banned={data.banned}
          selection={{ selected, onToggle: toggle }}
        />
      </div>

      {count > 0 && (
        <div className="bulk-bar" role="region" aria-label="Action en lot">
          <span className="bulk-bar__count">
            {count} sélectionné{count > 1 ? "s" : ""}
          </span>
          <button type="button" className="bulk-bar__cancel" onClick={clear}>
            Annuler
          </button>
          <button
            type="button"
            className="bulk-bar__action"
            onClick={() => setConfirming(true)}
            disabled={bulkBan.isPending}
          >
            Bannir
          </button>
        </div>
      )}

      {confirming && (
        <div className="dialog-backdrop" role="dialog" aria-modal="true">
          <div className="dialog">
            <h2 className="dialog__title">Bannir {count} nom{count > 1 ? "s" : ""} ?</h2>
            <p className="dialog__body">
              Cette action retire les éléments du classement et les place dans la liste des bannis.
              Elle n'est pas annulable depuis le bouton retour de vote.
            </p>
            <div className="dialog__actions">
              <button
                type="button"
                className="dialog__cancel"
                onClick={() => setConfirming(false)}
                disabled={bulkBan.isPending}
              >
                Annuler
              </button>
              <button
                type="button"
                className="dialog__confirm dialog__confirm--danger"
                onClick={confirmBan}
                disabled={bulkBan.isPending}
              >
                {bulkBan.isPending ? "Bannissement…" : "Bannir"}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
