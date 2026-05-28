import { memo, useCallback, useMemo, useState } from "react";
import { Virtuoso } from "react-virtuoso";
import type { RankedItemDto } from "../api/types";
import "../styles/pages.css";

interface SelectionState {
  selected: ReadonlySet<string>;
  onToggle: (value: string) => void;
}

interface Props {
  ranked: RankedItemDto[];
  banned: RankedItemDto[];
  /** When set, ranked rows show a checkbox column wired to this state. */
  selection?: SelectionState;
  /** When set, banned rows show a checkbox column wired to this state. */
  bannedSelection?: SelectionState;
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

export function RankedTable({ ranked, banned, selection, bannedSelection }: Props) {
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
  const bannedSelectable = !!bannedSelection;
  const rankedSelected = selection?.selected;
  const rankedToggle = selection?.onToggle;
  const bannedSelected = bannedSelection?.selected;
  const bannedToggle = bannedSelection?.onToggle;

  // Wrapper padding gives the inter-row spacing the .ranked container's
  // `gap` used to provide. Virtuoso measures each item's offsetHeight, so
  // padding (unlike margin) is included in the layout calculations.
  const renderRanked = useCallback(
    (_index: number, item: RankedItemDto) => (
      <div className="ranked__virtual-item">
        <Row
          item={item}
          showRank
          maxElo={maxElo}
          minElo={minElo}
          selectable={selectable}
          isSelected={rankedSelected?.has(item.value) ?? false}
          onToggle={rankedToggle}
        />
      </div>
    ),
    [maxElo, minElo, selectable, rankedSelected, rankedToggle],
  );

  const renderBanned = useCallback(
    (_index: number, item: RankedItemDto) => (
      <div className="ranked__virtual-item">
        <Row
          item={item}
          showRank={false}
          maxElo={maxElo}
          minElo={minElo}
          selectable={bannedSelectable}
          isSelected={bannedSelected?.has(item.value) ?? false}
          onToggle={bannedToggle}
        />
      </div>
    ),
    [maxElo, minElo, bannedSelectable, bannedSelected, bannedToggle],
  );

  return (
    <div
      className="ranked"
      role="table"
      aria-label="Classement"
      data-selectable={selectable ? "true" : undefined}
    >
      <Virtuoso
        useWindowScroll
        data={ranked}
        itemContent={renderRanked}
        increaseViewportBy={400}
        // Renders enough rows on first paint so tests (jsdom has no layout
        // measurements) and SSR get real content without waiting on a resize
        // observer. Virtuoso swaps to its windowed render once mounted.
        initialItemCount={Math.min(ranked.length, 20)}
        computeItemKey={(_index, item) => item.value}
      />

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
            <div
              className="ranked__banned"
              data-selectable={bannedSelectable ? "true" : undefined}
            >
              <Virtuoso
                useWindowScroll
                data={banned}
                itemContent={renderBanned}
                increaseViewportBy={400}
                initialItemCount={Math.min(banned.length, 20)}
                computeItemKey={(_index, item) => item.value}
              />
            </div>
          )}
        </>
      )}
    </div>
  );
}
