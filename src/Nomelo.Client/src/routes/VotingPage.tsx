import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useNextPair, useSession, useSubmitVote } from "../api/hooks";
import { NameCard } from "../components/NameCard";
import { StabilityBanner } from "../components/StabilityBanner";
import { OrientationHint } from "../components/OrientationHint";
import type { VoteResult } from "../api/types";
import "../styles/voting.css";

const SHORTCUTS: Record<string, VoteResult> = {
  ArrowLeft: "prefer_a",
  ArrowRight: "prefer_b",
  ArrowUp: "like_both",
  ArrowDown: "ban_both",
};

function ArrowKey({ rotation }: { rotation: number }) {
  return (
    <svg
      width="12"
      height="12"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="3"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ transform: `rotate(${rotation}deg)` }}
      aria-hidden="true"
    >
      <polyline points="9 6 15 12 9 18" />
    </svg>
  );
}

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

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (sending || !pair.data) return;
      // Don't interfere when the user is typing in a form control.
      const target = e.target as HTMLElement | null;
      const tag = target?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || target?.isContentEditable) return;
      const result = SHORTCUTS[e.key];
      if (!result) return;
      e.preventDefault();
      send(result);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pair.data, sending]);

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

      <div className="voting__shortcuts" aria-hidden="true">
        <span className="voting__shortcut"><kbd><ArrowKey rotation={180} /></kbd> préférer</span>
        <span className="voting__shortcuts-sep">·</span>
        <span className="voting__shortcut"><kbd><ArrowKey rotation={0} /></kbd> préférer</span>
        <span className="voting__shortcuts-sep">·</span>
        <span className="voting__shortcut"><kbd><ArrowKey rotation={-90} /></kbd> j'aime les deux</span>
        <span className="voting__shortcuts-sep">·</span>
        <span className="voting__shortcut"><kbd><ArrowKey rotation={90} /></kbd> bannir les deux</span>
      </div>
    </main>
  );
}
