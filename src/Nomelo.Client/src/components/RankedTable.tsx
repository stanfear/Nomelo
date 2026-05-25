import { memo, useMemo, useState } from "react";
import type { RankedItemDto } from "../api/types";
import "../styles/pages.css";

interface Props {
  ranked: RankedItemDto[];
  banned: RankedItemDto[];
  /** When set, ranked rows show a checkbox column wired to this state. */
  selection?: {
    selected: ReadonlySet<string>;
    onToggle: (value: string) => void;
  };
}

interface RowProps {
  item: RankedItemDto;
  showRank: boolean;
  maxElo: number;
  minElo: number;
  selectable: boolean;
  isSelected: boolean;
  onToggle?: (value: string) => void;
}

// Memoised so toggling one checkbox in a multi-thousand-row list only
// re-renders the affected row(s) — without this the entire ranked table
// reconciles on every selection change and freezes the UI for seconds.
const Row = memo(function Row({ item, showRank, maxElo, minElo, selectable, isSelected, onToggle }: RowProps) {
  const elo = Math.round(item.eloScore);
  const podium = showRank && item.rank <= 3 ? String(item.rank) : undefined;
  const range = Math.max(maxElo - minElo, 1);
  const pct = Math.max(8, Math.min(100, ((item.eloScore - minElo) / range) * 100));

  return (
    <div
      className="ranked__row"
      role="row"
      data-podium={podium}
      data-selected={isSelected ? "true" : undefined}
    >
      {selectable && (
        <div className="ranked__select" role="cell">
          <input
            type="checkbox"
            checked={isSelected}
            onChange={() => onToggle?.(item.value)}
            aria-label={`Sélectionner ${item.value}`}
          />
        </div>
      )}
      <div className="ranked__rank" role="cell" aria-label={showRank ? `Rang ${item.rank}` : "Banni"}>
        {showRank ? item.rank : "·"}
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
});

export function RankedTable({ ranked, banned, selection }: Props) {
  const [showBanned, setShowBanned] = useState(false);
  const bannedLabel = banned.length === 1 ? "1 banni" : `${banned.length} bannis`;

  const { maxElo, minElo } = useMemo(() => {
    // Reduce instead of Math.max(...scores) — spreading 28k+ numbers blows the
    // V8 argument stack on large lists and is the slow path on smaller ones.
    let max = -Infinity;
    let min = Infinity;
    for (const r of ranked) {
      if (r.eloScore > max) max = r.eloScore;
      if (r.eloScore < min) min = r.eloScore;
    }
    if (ranked.length === 0) return { maxElo: 1, minElo: 0 };
    return { maxElo: max, minElo: min };
  }, [ranked]);

  const selectable = !!selection;

  return (
    <div
      className="ranked"
      role="table"
      aria-label="Classement"
      data-selectable={selectable ? "true" : undefined}
    >
      {ranked.map((r) => (
        <Row
          key={r.value}
          item={r}
          showRank
          maxElo={maxElo}
          minElo={minElo}
          selectable={selectable}
          isSelected={selection?.selected.has(r.value) ?? false}
          onToggle={selection?.onToggle}
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
                  maxElo={maxElo}
                  minElo={minElo}
                  selectable={false}
                  isSelected={false}
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
