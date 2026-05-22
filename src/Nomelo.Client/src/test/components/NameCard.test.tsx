import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NameCard } from "../../components/NameCard";
import type { PairItemDto } from "../../api/types";

const item: PairItemDto = {
  value: "Alice",
  variants: ["Alicia", "Alix"],
  description: "from old-French",
  sparkline: null,
  peakYear: null,
  peakCount: null,
};

const emptyItem: PairItemDto = {
  value: "Bob",
  variants: [],
  description: null,
  sparkline: null,
  peakYear: null,
  peakCount: null,
};

test("renders value and variants", () => {
  render(<NameCard item={item} onPrefer={() => {}} onBan={() => {}} side="A" />);
  expect(screen.getByText("Alice")).toBeInTheDocument();
  expect(screen.getByText(/Alicia/)).toBeInTheDocument();
  expect(screen.getByText(/Alix/)).toBeInTheDocument();
});

test("clicking Préférer fires onPrefer", async () => {
  const onPrefer = vi.fn();
  render(<NameCard item={item} onPrefer={onPrefer} onBan={() => {}} side="A" />);
  await userEvent.click(screen.getByRole("button", { name: /Préférer Alice/i }));
  expect(onPrefer).toHaveBeenCalledTimes(1);
});

test("clicking Bannir fires onBan", async () => {
  const onBan = vi.fn();
  render(<NameCard item={item} onPrefer={() => {}} onBan={onBan} side="A" />);
  await userEvent.click(screen.getByRole("button", { name: /Bannir Alice/i }));
  expect(onBan).toHaveBeenCalledTimes(1);
});

test("description is shown inline when present", () => {
  render(<NameCard item={item} onPrefer={() => {}} onBan={() => {}} side="A" />);
  expect(screen.getByText(item.description!)).toBeInTheDocument();
});

test("no description rendered when null", () => {
  render(<NameCard item={emptyItem} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByText(/old-French/)).not.toBeInTheDocument();
});

test("no variants section when array is empty", () => {
  render(<NameCard item={emptyItem} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByTestId("variants")).not.toBeInTheDocument();
});

test("renders sparkline and peak block when metadata present", () => {
  const enriched: PairItemDto = {
    value: "Catherine",
    variants: [],
    description: null,
    sparkline: "▆▇█▇▅",
    peakYear: 1963,
    peakCount: 95245,
  };
  render(<NameCard item={enriched} onPrefer={() => {}} onBan={() => {}} side="A" />);
  const svg = screen.getByTestId("sparkline");
  expect(svg).toBeInTheDocument();
  // Sparkline is drawn as a single closed path covering all 5 columns.
  const path = svg.querySelector("path");
  expect(path).not.toBeNull();
  expect(path!.getAttribute("d")).toMatch(/^M 0 8/);
  expect(screen.getByText(/Pic en 1963/)).toBeInTheDocument();
  expect(screen.getByText(/naissances/)).toBeInTheDocument();
});

test("no sparkline or peak block when metadata absent", () => {
  render(<NameCard item={emptyItem} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByText(/Pic en/)).not.toBeInTheDocument();
  expect(screen.queryByTestId("sparkline")).not.toBeInTheDocument();
});
