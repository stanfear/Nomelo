import "../styles/pages.css";

interface Props { onDismiss: () => void; }

export function StabilityBanner({ onDismiss }: Props) {
  return (
    <div role="status" className="stability-banner">
      <span className="stability-banner__icon" aria-hidden>★</span>
      <p className="stability-banner__text">
        Vos résultats semblent stables depuis 100 votes. Vous pouvez continuer pour affiner.
      </p>
      <button
        type="button"
        className="stability-banner__close"
        onClick={onDismiss}
        aria-label="Fermer"
      >
        ×
      </button>
    </div>
  );
}
