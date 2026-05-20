import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NameCard } from "../../components/NameCard";

const item = { value: "Alice", variants: ["Alicia", "Alix"], description: "from old-French" };

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
  expect(screen.getByText(item.description)).toBeInTheDocument();
});

test("no description rendered when null", () => {
  render(<NameCard item={{ value: "Bob", variants: [], description: null }} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByText(/old-French/)).not.toBeInTheDocument();
});

test("no variants section when array is empty", () => {
  render(<NameCard item={{ value: "Bob", variants: [], description: null }} onPrefer={() => {}} onBan={() => {}} side="B" />);
  expect(screen.queryByTestId("variants")).not.toBeInTheDocument();
});
