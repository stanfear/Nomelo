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
