import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RankedTable } from "../../components/RankedTable";
import { fixtures } from "../handlers";

test("renders ranked items in order with score rounding", () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={[]} />);
  const rows = screen.getAllByRole("row");
  expect(within(rows[0]).getByText("Alice")).toBeInTheDocument();
  expect(within(rows[0]).getByText("1080")).toBeInTheDocument();
  expect(within(rows[1]).getByText("Carol")).toBeInTheDocument();
});

test("banned items hidden by default, revealed on expand", async () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={fixtures.RESULTS.banned} />);
  expect(screen.queryByText("Bob")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /1 banni/i }));
  expect(screen.getByText("Bob")).toBeInTheDocument();
});

test("empty banned list does not render expander", () => {
  render(<RankedTable ranked={fixtures.RESULTS.ranked} banned={[]} />);
  expect(screen.queryByRole("button", { name: /banni/i })).not.toBeInTheDocument();
});
