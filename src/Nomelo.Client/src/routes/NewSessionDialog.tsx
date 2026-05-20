import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useLists, useCreateSession } from "../api/hooks";
import { SelectField } from "../components/SelectField";
import { NumberField } from "../components/NumberField";
import "../styles/pages.css";

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
    try {
      const session = await create.mutateAsync({ listId, confidenceThreshold: threshold });
      onClose();
      navigate(`/sessions/${session.id}`);
    } catch {
      // error is captured in create.error; rendered below
    }
  };

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <form
        role="dialog"
        aria-labelledby="dialog-title"
        className="dialog"
        onSubmit={submit}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id="dialog-title" className="dialog__title">Nouvelle session</h2>

        <SelectField
          label="Liste"
          value={listId}
          onChange={setListId}
          required
          options={(lists ?? []).map((l) => ({
            value: l.id,
            label: `${l.name} (${l.itemCount})`,
          }))}
        />

        <NumberField
          label="Seuil de confiance"
          value={threshold}
          min={1}
          max={10}
          onChange={setThreshold}
        />

        {create.isError && (
          <p role="alert" className="dialog__error">
            {create.error instanceof Error ? create.error.message : "Erreur lors de la création"}
          </p>
        )}

        <div className="dialog__actions">
          <button type="button" className="btn-ghost" onClick={onClose}>
            Annuler
          </button>
          <button
            type="submit"
            className="btn-primary"
            disabled={!listId || create.isPending}
          >
            Démarrer
          </button>
        </div>
      </form>
    </div>
  );
}
