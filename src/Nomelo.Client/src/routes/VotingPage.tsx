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

  const sending = submit.isPending || pair.isFetching;

  if (pair.isLoading) return <p>Chargement…</p>;

  if (!pair.data) {
    return (
      <main className="voting">
        <header className="voting__header">
          <Link to="/">← Accueil</Link>
          <span>{session.data?.listName}</span>
        </header>
        <section className="voting__empty">
          <p>Plus de paires disponibles. Tous les éléments restants sont équivalents ou bannis.</p>
          <Link to={`/sessions/${id}/results`}>Voir les résultats</Link>
        </section>
      </main>
    );
  }

  const voteCount = session.data?.voteCount ?? 0;
  const pairKey = `${pair.data.a.value}__${pair.data.b.value}`;

  return (
    <main className="voting">
      <OrientationHint />

      <header className="voting__header">
        <Link to="/">← Accueil</Link>
        <span className="voting__title">
          <span className="voting__title-name">{session.data?.listName}</span>
          <span className="voting__title-count">{voteCount} vote{voteCount > 1 ? "s" : ""}</span>
        </span>
        <Link to={`/sessions/${id}/results`}>Voir les résultats</Link>
      </header>

      {!bannerDismissed && session.data?.stabilityReached && (
        <StabilityBanner onDismiss={() => setBannerDismissed(true)} />
      )}

      <section className="voting__pair" key={pairKey}>
        <NameCard
          item={pair.data.a}
          side="A"
          onPrefer={() => send("prefer_a")}
          onBan={() => send("ban_a")}
          disabled={sending}
        />
        <div className="voting__or" aria-hidden="true">ou</div>
        <NameCard
          item={pair.data.b}
          side="B"
          onPrefer={() => send("prefer_b")}
          onBan={() => send("ban_b")}
          disabled={sending}
        />
      </section>

      <footer className="voting__shared">
        <button
          type="button"
          className="btn-ban"
          disabled={sending}
          onClick={() => send("ban_both")}
        >
          <span aria-hidden="true">🚫</span> Bannir les deux
        </button>
        <button
          type="button"
          className="btn-like"
          disabled={sending}
          onClick={() => send("like_both")}
        >
          <span aria-hidden="true">💛</span> J'aime les deux
        </button>
      </footer>
    </main>
  );
}
