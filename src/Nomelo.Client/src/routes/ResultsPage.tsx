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
