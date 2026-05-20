import { useParams } from "react-router-dom";
import { useSharedResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";
import "../styles/pages.css";

export function SharedResultsPage() {
  const { token = "" } = useParams();
  const { data, isLoading, error } = useSharedResults(token);

  if (isLoading) return <p className="page-loading">Chargement…</p>;
  if (error || !data) {
    return (
      <main className="results">
        <p className="results__error">Résultats introuvables.</p>
      </main>
    );
  }

  return (
    <main className="results">
      <div className="results__main">
        <section className="results__hero">
          <p className="results__hero-eyebrow">Classement partagé</p>
          <h1 className="results__hero-title">{data.listName}</h1>
          <p className="results__shared-note">
            Vue partagée · lecture seule · {data.voteCount} votes
          </p>
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

        <RankedTable ranked={data.ranked} banned={data.banned} />
      </div>
    </main>
  );
}
