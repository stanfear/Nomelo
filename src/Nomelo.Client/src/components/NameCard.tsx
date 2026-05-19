import { useState } from "react";
import type { PairItemDto } from "../api/types";

interface Props {
  item: PairItemDto;
  side: "A" | "B";
  onPrefer: () => void;
  onBan: () => void;
  disabled?: boolean;
}

export function NameCard({ item, side, onPrefer, onBan, disabled }: Props) {
  const [showDesc, setShowDesc] = useState(false);
  return (
    <section className="name-card" data-side={side}>
      <h2 className="name-card__value">{item.value}</h2>
      {item.variants.length > 0 && (
        <p className="name-card__variants" data-testid="variants">
          {item.variants.join(" · ")}
        </p>
      )}
      {item.description && (
        <>
          <button
            type="button"
            className="name-card__info"
            aria-expanded={showDesc}
            aria-label={`Plus d'infos sur ${item.value}`}
            onClick={() => setShowDesc((v) => !v)}
          >
            i
          </button>
          {showDesc && <p className="name-card__description">{item.description}</p>}
        </>
      )}
      <div className="name-card__actions">
        <button type="button" onClick={onBan} disabled={disabled} aria-label={`Bannir ${item.value}`}>
          🚫 Bannir
        </button>
        <button type="button" onClick={onPrefer} disabled={disabled} aria-label={`Préférer ${item.value}`}>
          ❤️ Préférer
        </button>
      </div>
    </section>
  );
}
