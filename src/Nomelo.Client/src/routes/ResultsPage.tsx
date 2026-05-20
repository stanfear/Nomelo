import { Link, useParams } from "react-router-dom";
import { useResults } from "../api/hooks";
import { RankedTable } from "../components/RankedTable";
import "../styles/pages.css";

export function ResultsPage() {
  const { id = "" } = useParams();
  const { data, isLoading } = useResults(id);

  if (isLoading || !data) return <p className="page-loading">Chargement…</p>;

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
          <h1 className="results__hero-title">{data.listName}</h1>
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
