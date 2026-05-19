import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { http, HttpResponse } from "msw";
import { server } from "../setup";
import { VotingPage } from "../../routes/VotingPage";
import { fixtures } from "../handlers";

function wrap() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[`/sessions/${fixtures.ALICE.id}`]}>
        <Routes>
          <Route path="/sessions/:id" element={<VotingPage />} />
          <Route path="/sessions/:id/results" element={<div>results-page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

test("renders both names from /next-pair", async () => {
  wrap();
  expect(await screen.findByText("Alice")).toBeInTheDocument();
  expect(screen.getByText("Bob")).toBeInTheDocument();
});

test("clicking Préférer A submits prefer_a and fetches next pair", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /Préférer Alice/i }));

  await waitFor(() => expect(posted).toEqual({ itemA: "Alice", itemB: "Bob", result: "prefer_a" }));
});

test("J'aime les deux submits like_both", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /J'aime les deux/i }));
  await waitFor(() => expect(posted.result).toBe("like_both"));
});

test("Bannir les deux submits ban_both", async () => {
  let posted: any = null;
  server.use(
    http.post(`/api/sessions/${fixtures.ALICE.id}/votes`, async ({ request }) => {
      posted = await request.json();
      return new HttpResponse(null, { status: 204 });
    })
  );

  wrap();
  await screen.findByText("Alice");
  await userEvent.click(screen.getByRole("button", { name: /Bannir les deux/i }));
  await waitFor(() => expect(posted.result).toBe("ban_both"));
});

test("voir résultats link present", async () => {
  wrap();
  await screen.findByText("Alice");
  expect(screen.getByRole("link", { name: /Voir les résultats/i })).toHaveAttribute(
    "href", `/sessions/${fixtures.ALICE.id}/results`
  );
});
