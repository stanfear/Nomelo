import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StabilityBanner } from "../../components/StabilityBanner";

test("renders the FR message", () => {
  render(<StabilityBanner onDismiss={() => {}} />);
  expect(screen.getByText(/résultats semblent stables/i)).toBeInTheDocument();
});

test("dismiss button fires callback", async () => {
  const onDismiss = vi.fn();
  render(<StabilityBanner onDismiss={onDismiss} />);
  await userEvent.click(screen.getByRole("button", { name: /Fermer/i }));
  expect(onDismiss).toHaveBeenCalled();
});
