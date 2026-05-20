import { useMemo, useState } from "react";
import type { RankedItemDto } from "../api/types";
import "../styles/pages.css";

interface Props { ranked: RankedItemDto[]; banned: RankedItemDto[]; }

interface RowProps {
  item: RankedItemDto;
  showRank: boolean;
  tied: boolean;
  maxElo: number;
  minElo: number;
}

function Row({ item, showRank, tied, maxElo, minElo }: RowProps) {
  const elo = Math.round(item.eloScore);
  const podium = showRank && item.rank <= 3 ? String(item.rank) : undefined;
  const range = Math.max(maxElo - minElo, 1);
  const pct = Math.max(8, Math.min(100, ((item.eloScore - minElo) / range) * 100));
  const rankLabel = showRank
    ? (tied ? `=${item.rank}` : `${item.rank}`)
    : "·";
  const ariaLabel = showRank
    ? (tied ? `Rang ${item.rank} ex æquo` : `Rang ${item.rank}`)
    : "Banni";

  return (
    <div className="ranked__row" role="row" data-podium={podium}>
      <div className="ranked__rank" role="cell" aria-label={ariaLabel}>
        {rankLabel}
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

  // Items sharing a rank with at least one other item are flagged as tied so
  // the row can render a "=N" badge instead of a plain "N".
  const tiedRanks = useMemo(() => {
    const counts = new Map<number, number>();
    for (const r of ranked) counts.set(r.rank, (counts.get(r.rank) ?? 0) + 1);
    return new Set(
      Array.from(counts.entries())
        .filter(([, n]) => n > 1)
        .map(([rank]) => rank),
    );
  }, [ranked]);

  return (
    <div className="ranked" role="table" aria-label="Classement">
      {ranked.map((r) => (
        <Row
          key={r.value}
          item={r}
          showRank
          tied={tiedRanks.has(r.rank)}
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
                  tied={false}
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
