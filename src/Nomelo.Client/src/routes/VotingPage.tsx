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
