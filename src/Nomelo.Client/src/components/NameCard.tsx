import type { PairItemDto } from "../api/types";

interface Props {
  item: PairItemDto;
  side: "A" | "B";
  onPrefer: () => void;
  onBan: () => void;
  disabled?: boolean;
}

export function NameCard({ item, side, onPrefer, onBan, disabled }: Props) {
  const stop = (handler: () => void) => (e: React.MouseEvent) => {
    e.stopPropagation();
    handler();
  };

  return (
    <section className="name-card" data-side={side}>
      <button
        type="button"
        className="name-card__prefer-fullface"
        onClick={onPrefer}
        disabled={disabled}
        aria-label={`Préférer ${item.value}`}
      >
        <span className="name-card__value">{item.value}</span>
        {item.variants.length > 0 && (
          <span className="name-card__variants" data-testid="variants">
            {item.variants.join(" · ")}
          </span>
        )}
        {item.description && (
          <span className="name-card__description">{item.description}</span>
        )}
      </button>

      <button
        type="button"
        className="name-card__ban"
        onClick={stop(onBan)}
        disabled={disabled}
        aria-label={`Bannir ${item.value}`}
      >
        <span aria-hidden="true">🗑️</span>
        <span>Bannir</span>
      </button>
    </section>
  );
}
