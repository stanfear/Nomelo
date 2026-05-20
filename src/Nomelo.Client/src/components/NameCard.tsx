import { useRef } from "react";
import type { PairItemDto } from "../api/types";

export interface RippleSource {
  /** Coordinates in client space (event.clientX/Y). Falls back to card center. */
  clientX?: number;
  clientY?: number;
  /** Bumped each time a new ripple should be triggered. */
  key: number;
}

interface Props {
  item: PairItemDto;
  side: "A" | "B";
  onPrefer: (source: RippleSource) => void;
  onBan: (source: RippleSource) => void;
  disabled?: boolean;
  ripple?: RippleSource;
}

export function NameCard({ item, side, onPrefer, onBan, disabled, ripple }: Props) {
  const sectionRef = useRef<HTMLElement>(null);
  const seqRef = useRef(0);

  const nextKey = () => ++seqRef.current;

  const handlePrefer = (e: React.MouseEvent) => {
    onPrefer({ clientX: e.clientX, clientY: e.clientY, key: nextKey() });
  };

  const handleBan = (e: React.MouseEvent) => {
    e.stopPropagation();
    onBan({ clientX: e.clientX, clientY: e.clientY, key: nextKey() });
  };

  const rect = sectionRef.current?.getBoundingClientRect();
  const rippleX = ripple
    ? ripple.clientX !== undefined && rect
      ? ripple.clientX - rect.left
      : (rect?.width ?? 0) / 2
    : 0;
  const rippleY = ripple
    ? ripple.clientY !== undefined && rect
      ? ripple.clientY - rect.top
      : (rect?.height ?? 0) / 2
    : 0;

  return (
    <section className="name-card" data-side={side} ref={sectionRef}>
      {ripple && (
        <span className="name-card__ripple-clip" aria-hidden="true">
          <span
            key={ripple.key}
            className="name-card__ripple"
            style={{ left: `${rippleX}px`, top: `${rippleY}px` }}
          />
        </span>
      )}
      <button
        type="button"
        className="name-card__prefer-fullface"
        onClick={handlePrefer}
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
        onClick={handleBan}
        disabled={disabled}
        aria-label={`Bannir ${item.value}`}
      >
        <span aria-hidden="true">🗑️</span>
        <span>Bannir</span>
      </button>
    </section>
  );
}
