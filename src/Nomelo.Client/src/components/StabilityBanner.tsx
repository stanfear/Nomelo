interface Props { onDismiss: () => void; }

export function StabilityBanner({ onDismiss }: Props) {
  return (
    <div role="status" className="stability-banner">
      <p>Vos résultats semblent stables depuis 100 votes. Vous pouvez continuer pour affiner.</p>
      <button type="button" onClick={onDismiss} aria-label="Fermer">×</button>
    </div>
  );
}
