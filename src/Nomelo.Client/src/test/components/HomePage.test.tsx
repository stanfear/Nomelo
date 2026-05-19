import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { HomePage } from "../../routes/HomePage";

function wrap(ui: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={["/"]}>
        <Routes>
          <Route path="/" element={ui} />
          <Route path="/sessions/:id" element={<div>session-page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

test("lists user sessions", async () => {
  wrap(<HomePage />);
  expect(await screen.findByText("Liste A")).toBeInTheDocument();
  expect(screen.getByText(/7 votes/)).toBeInTheDocument();
});

test("clicking a session navigates to /sessions/:id", async () => {
  wrap(<HomePage />);
  await userEvent.click(await screen.findByRole("link", { name: /Liste A/i }));
  await waitFor(() => expect(screen.getByText("session-page")).toBeInTheDocument());
});

test("Nouvelle session opens the dialog", async () => {
  wrap(<HomePage />);
  await userEvent.click(screen.getByRole("button", { name: /Nouvelle session/i }));
  expect(await screen.findByRole("dialog")).toBeInTheDocument();
  expect(screen.getByLabelText(/Liste/i)).toBeInTheDocument();
});
