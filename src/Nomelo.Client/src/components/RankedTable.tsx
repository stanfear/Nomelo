import { useMemo, useState } from "react";
import type { RankedItemDto } from "../api/types";
import "../styles/pages.css";

interface Props { ranked: RankedItemDto[]; banned: RankedItemDto[]; }

interface RowProps {
  item: RankedItemDto;
  showRank: boolean;
  tiedContinuation: boolean;
  maxElo: number;
  minElo: number;
}

function Row({ item, showRank, tiedContinuation, maxElo, minElo }: RowProps) {
  const elo = Math.round(item.eloScore);
  // Only the head of a tie-group carries the podium chrome; continuations are
  // rendered as indented children with no medal.
  const podium =
    !tiedContinuation && showRank && item.rank <= 3 ? String(item.rank) : undefined;
  const range = Math.max(maxElo - minElo, 1);
  const pct = Math.max(8, Math.min(100, ((item.eloScore - minElo) / range) * 100));
  const ariaLabel = !showRank
    ? "Banni"
    : tiedContinuation
      ? `Rang ${item.rank} ex æquo`
      : `Rang ${item.rank}`;

  return (
    <div
      className="ranked__row"
      role="row"
      data-podium={podium}
      data-tied={tiedContinuation ? "continuation" : undefined}
    >
      <div className="ranked__rank" role="cell" aria-label={ariaLabel}>
        {!showRank ? "·" : tiedContinuation ? "" : item.rank}
      </div>
      <div className="ranked__main" role="cell">
        <span className="ranked__name">{item.value}</span>
        {item.variants.length > 0 && (
          <span className="ranked__variants">{item.variants.join(", ")}</span>
        )}
      </div>
      <div className="ranked__elo" role="cell">
        <span className="ranked__elo-badge">{elo}</span>
        {showRank && (
          <div className="ranked__elo-bar" aria-hidden>
            <div className="ranked__elo-bar-fill" style={{ width: `${pct}%` }} />
          </div>
        )}
      </div>
      <div className="ranked__seen" role="cell" aria-label="Vu">
        {item.timesShown}×
      </div>
    </div>
  );
}

export function RankedTable({ ranked, banned }: Props) {
  const [showBanned, setShowBanned] = useState(false);
  const bannedLabel = banned.length === 1 ? "1 banni" : `${banned.length} bannis`;

  const { maxElo, minElo } = useMemo(() => {
    if (ranked.length === 0) return { maxElo: 1, minElo: 0 };
    const scores = ranked.map((r) => r.eloScore);
    return { maxElo: Math.max(...scores), minElo: Math.min(...scores) };
  }, [ranked]);

  return (
    <div className="ranked" role="table" aria-label="Classement">
      {ranked.map((r, i) => (
        <Row
          key={r.value}
          item={r}
          showRank
          tiedContinuation={i > 0 && ranked[i - 1].rank === r.rank}
          maxElo={maxElo}
          minElo={minElo}
        />
      ))}

      {banned.length > 0 && (
        <>
          <button
            type="button"
            className="ranked__banned-toggle"
            onClick={() => setShowBanned((v) => !v)}
            aria-expanded={showBanned}
          >
            {showBanned ? "Masquer" : "Afficher"} {bannedLabel}
          </button>
          {showBanned && (
            <div className="ranked__banned">
              {banned.map((r) => (
                <Row
                  key={r.value}
                  item={r}
                  showRank={false}
                  tiedContinuation={false}
                  maxElo={maxElo}
                  minElo={minElo}
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
