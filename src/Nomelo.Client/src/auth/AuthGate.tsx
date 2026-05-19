import { useMe } from "../api/hooks";
import { redirectToLogin, UnauthorizedError } from "../api/client";
import { useEffect, type ReactNode } from "react";

export function AuthGate({ children }: { children: ReactNode }) {
  const { data, isLoading, error } = useMe();

  useEffect(() => {
    if (error instanceof UnauthorizedError) redirectToLogin();
  }, [error]);

  if (isLoading) return <div role="status">Chargement…</div>;
  if (!data) return null;
  return <>{children}</>;
}
