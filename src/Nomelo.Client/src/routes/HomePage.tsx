import { useState } from "react";
import { Link } from "react-router-dom";
import { useSessions } from "../api/hooks";
import { NewSessionDialog } from "./NewSessionDialog";
import "../styles/pages.css";

export function HomePage() {
  const { data: sessions, isLoading } = useSessions();
  const [dialogOpen, setDialogOpen] = useState(false);

  return (
    <main className="home">
      <section className="home__hero">
        <h1 className="home__logo">Nomelo</h1>
        <p className="home__tagline">Décide-toi.</p>
        <button
          type="button"
          className="home__cta"
          onClick={() => setDialogOpen(true)}
        >
          Nouvelle session
        </button>
      </section>

      {isLoading && <p className="home__loading">Chargement…</p>}

      {sessions && sessions.length > 0 && (
        <>
          <h2 className="home__section-title">Vos sessions</h2>
          <ul className="home__sessions">
            {sessions.map((s) => (
              <li key={s.id} className="session-card">
                <Link to={`/sessions/${s.id}`} className="session-card__link">
                  <span className="session-card__name">{s.listName}</span>
                  <span className="session-card__meta">
                    {s.voteCount} votes ·{" "}
                    {new Date(s.updatedAt).toLocaleDateString("fr-FR")}
                  </span>
                </Link>
                {s.shareToken && (
                  <a
                    className="session-card__share"
                    href={`/share/${s.shareToken}`}
                    target="_blank"
                    rel="noreferrer"
                  >
                    Lien de partage
                  </a>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      {sessions?.length === 0 && (
        <div className="home__empty">
          <div className="home__empty-icon" aria-hidden>◇</div>
          <p>Aucune session pour le moment.</p>
          <p>Lancez une comparaison pour démarrer.</p>
        </div>
      )}

      {dialogOpen && <NewSessionDialog onClose={() => setDialogOpen(false)} />}
    </main>
  );
}
