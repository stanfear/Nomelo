import { useId, useState } from "react";
import "../styles/pages.css";

interface Props {
  sessionId: string;
  defaultId: string;
  defaultName: string;
  onClose: () => void;
}

// Parses RFC 6266 Content-Disposition headers for the filename attribute.
// Falls back to null if absent or malformed; the dialog then derives a
// filename from the requested id slug.
function filenameFromDisposition(header: string | null): string | null {
  if (!header) return null;
  const match = /filename\*?="?([^";]+)"?/i.exec(header);
  return match?.[1] ?? null;
}

export function ExportUnbannedDialog({ sessionId, defaultId, defaultName, onClose }: Props) {
  const [newId, setNewId] = useState(defaultId);
  const [newName, setNewName] = useState(defaultName);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const idFieldId = useId();
  const nameFieldId = useId();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setPending(true);
    try {
      const res = await fetch(`/api/sessions/${sessionId}/export-unbanned`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ newId: newId.trim(), newName: newName.trim() }),
      });
      if (!res.ok) {
        const body = (await res.json().catch(() => null)) as { error?: string } | null;
        throw new Error(body?.error ?? `HTTP ${res.status}`);
      }
      const filename =
        filenameFromDisposition(res.headers.get("content-disposition")) ?? `${newId.trim()}.json`;
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erreur lors de l'export");
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <form
        role="dialog"
        aria-labelledby="export-dialog-title"
        className="dialog"
        onSubmit={submit}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id="export-dialog-title" className="dialog__title">
          Exporter les non-bannis
        </h2>
        <p className="dialog__body">
          Génère un fichier JSON au même format que la liste source, sans les prénoms bannis dans
          cette session. Dépose-le dans le dossier <code>lists/</code> du serveur puis redémarre
          l'application pour le voir apparaître.
        </p>

        <label className="dialog__field" htmlFor={idFieldId}>
          Identifiant (slug)
          <input
            id={idFieldId}
            type="text"
            value={newId}
            onChange={(e) => setNewId(e.target.value)}
            placeholder="ma-liste-filtree"
            pattern="[a-z0-9][a-z0-9_\-]*"
            maxLength={64}
            required
          />
        </label>

        <label className="dialog__field" htmlFor={nameFieldId}>
          Nom affiché
          <input
            id={nameFieldId}
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            maxLength={120}
            required
          />
        </label>

        {error && (
          <p role="alert" className="dialog__error">
            {error}
          </p>
        )}

        <div className="dialog__actions">
          <button type="button" className="dialog__cancel" onClick={onClose} disabled={pending}>
            Annuler
          </button>
          <button type="submit" className="dialog__confirm" disabled={pending}>
            {pending ? "Export…" : "Télécharger"}
          </button>
        </div>
      </form>
    </div>
  );
}
