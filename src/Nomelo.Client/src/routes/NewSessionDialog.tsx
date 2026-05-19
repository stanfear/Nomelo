import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useLists, useCreateSession } from "../api/hooks";

interface Props { onClose: () => void; }

export function NewSessionDialog({ onClose }: Props) {
  const { data: lists } = useLists();
  const create = useCreateSession();
  const navigate = useNavigate();

  const [listId, setListId] = useState("");
  const [threshold, setThreshold] = useState(3);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!listId) return;
    const session = await create.mutateAsync({ listId, confidenceThreshold: threshold });
    onClose();
    navigate(`/sessions/${session.id}`);
  };

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <form role="dialog" className="dialog" onSubmit={submit} onClick={(e) => e.stopPropagation()}>
        <h2>Nouvelle session</h2>
        <label>
          Liste
          <select value={listId} onChange={(e) => setListId(e.target.value)} required>
            <option value="">— choisir —</option>
            {lists?.map((l) => (
              <option key={l.id} value={l.id}>{l.name} ({l.itemCount})</option>
            ))}
          </select>
        </label>
        <label>
          Seuil de confiance
          <input
            type="number" min={1} max={10}
            value={threshold}
            onChange={(e) => setThreshold(Number(e.target.value))}
          />
        </label>
        <div className="dialog__actions">
          <button type="button" onClick={onClose}>Annuler</button>
          <button type="submit" disabled={!listId || create.isPending}>Démarrer</button>
        </div>
      </form>
    </div>
  );
}
