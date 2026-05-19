import { useMe } from "../api/hooks";
import { redirectToLogin, UnauthorizedError } from "../api/client";
import { useEffect, type ReactNode } from "react";

export function AuthGate({ children }: { children: ReactNode }) {
  const { data, isLoading, error } = useMe();

  useEffect(() => {
    if (error instanceof UnauthorizedError) redirectToLogin();
  }, [error]);

  if (isLoading) return <div role="status">Chargement…</div>;

  if (error && !(error instanceof UnauthorizedError)) {
    return (
      <div role="alert" className="auth-error">
        <h1>Connexion impossible</h1>
        <p>{error instanceof Error ? error.message : "Erreur inconnue"}</p>
        <button type="button" onClick={() => window.location.reload()}>Réessayer</button>
      </div>
    );
  }

  if (!data) return null;
  return <>{children}</>;
}
