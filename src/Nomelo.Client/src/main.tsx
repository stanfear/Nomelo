import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider, QueryCache, MutationCache } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { redirectToLogin, UnauthorizedError } from "./api/client";
import "./styles/global.css";

const onError = (err: unknown) => {
  if (err instanceof UnauthorizedError) redirectToLogin();
};

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false, staleTime: 30_000 } },
  queryCache: new QueryCache({ onError }),
  mutationCache: new MutationCache({ onError }),
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);
