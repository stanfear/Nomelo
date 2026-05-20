import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useNextPair, useSession, useSubmitVote } from "../api/hooks";
import { NameCard, type RippleSource } from "../components/NameCard";
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

type FlashSide = "A" | "B" | "both";

const sideForResult = (r: VoteResult): FlashSide => {
  if (r === "prefer_a" || r === "ban_a") return "A";
  if (r === "prefer_b" || r === "ban_b") return "B";
  return "both";
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
  const [rippleA, setRippleA] = useState<RippleSource | undefined>(undefined);
  const [rippleB, setRippleB] = useState<RippleSource | undefined>(undefined);
  const rippleSeq = useRef(0);

  const send = (result: VoteResult, source?: RippleSource) => {
    if (!pair.data) return;
    const side = sideForResult(result);
    const key = ++rippleSeq.current;
    // For one-side actions we use the event's coordinates when available;
    // otherwise (keyboard / footer button) we let NameCard fall back to its
    // own center so the ripple still feels anchored to the affected card.
    if (side === "A") {
      setRippleA({ ...(source ?? {}), key });
    } else if (side === "B") {
      setRippleB({ ...(source ?? {}), key });
    } else {
      setRippleA({ key });
      setRippleB({ key });
    }
    submit.mutate({ itemA: pair.data.a.value, itemB: pair.data.b.value, result });
  };

  const sending = submit.isPending || pair.isFetching;

  // Clear ripples once the new pair arrives so the animation can re-fire next click.
  useEffect(() => {
    setRippleA(undefined);
    setRippleB(undefined);
  }, [pair.data]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (sending || !pair.data) return;
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
          onPrefer={(src) => send("prefer_a", src)}
          onBan={(src) => send("ban_a", src)}
          disabled={sending}
          ripple={rippleA}
        />
        <div className="voting__or" aria-hidden="true">ou</div>
        <NameCard
          item={pair.data.b}
          side="B"
          onPrefer={(src) => send("prefer_b", src)}
          onBan={(src) => send("ban_b", src)}
          disabled={sending}
          ripple={rippleB}
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
