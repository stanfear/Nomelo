import { useState } from "react";
import { Link } from "react-router-dom";
import { useSessions } from "../api/hooks";
import { NewSessionDialog } from "./NewSessionDialog";

export function HomePage() {
  const { data: sessions, isLoading } = useSessions();
  const [dialogOpen, setDialogOpen] = useState(false);

  return (
    <main className="home">
      <header className="home__header">
        <h1>Nomelo</h1>
        <button type="button" onClick={() => setDialogOpen(true)}>Nouvelle session</button>
      </header>

      {isLoading && <p>Chargement…</p>}

      <ul className="home__sessions">
        {sessions?.map((s) => (
          <li key={s.id}>
            <Link to={`/sessions/${s.id}`}>
              <strong>{s.listName}</strong>
              <span> · {s.voteCount} votes · {new Date(s.updatedAt).toLocaleDateString("fr-FR")}</span>
            </Link>
            {s.shareToken && (
              <a href={`/share/${s.shareToken}`} target="_blank" rel="noreferrer">
                Lien de partage
              </a>
            )}
          </li>
        ))}
      </ul>

      {sessions?.length === 0 && <p>Aucune session pour le moment.</p>}

      {dialogOpen && <NewSessionDialog onClose={() => setDialogOpen(false)} />}
    </main>
  );
}
