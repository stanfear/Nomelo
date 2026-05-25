import { describe, expect, test } from "vitest";
import { compileQuery } from "../../components/globSearch";

describe("compileQuery", () => {
  test("returns null for empty / whitespace input", () => {
    expect(compileQuery("")).toBeNull();
    expect(compileQuery("   ")).toBeNull();
  });

  test("plain text behaves as case-insensitive substring", () => {
    const m = compileQuery("ali")!;
    expect(m("Alice")).toBe(true);
    expect(m("ALICE")).toBe(true);
    expect(m("Magalie")).toBe(true);
    expect(m("Bob")).toBe(false);
  });

  test("strips diacritics on both sides", () => {
    const m = compileQuery("eloïse")!;
    expect(m("Eloïse")).toBe(true);
    expect(m("Eloise")).toBe(true);
    expect(m("ÉLOISE")).toBe(true);
    expect(m("Heloise")).toBe(true); // substring match: "eloise" inside "heloise"
  });

  test("* matches any sequence (anchored)", () => {
    const m = compileQuery("mari*")!;
    expect(m("Marie")).toBe(true);
    expect(m("Maria")).toBe(true);
    expect(m("Marianne")).toBe(true);
    expect(m("Amarine")).toBe(false); // anchored at start
  });

  test("? matches exactly one character", () => {
    const m = compileQuery("m?rie")!;
    expect(m("Marie")).toBe(true);
    expect(m("Murie")).toBe(true);
    expect(m("Marrie")).toBe(false); // two chars where ? sits
    expect(m("Mrie")).toBe(false); // zero chars where ? sits
  });

  test("* both ends acts like substring with glob semantics", () => {
    const m = compileQuery("*ette*")!;
    expect(m("Colette")).toBe(true);
    expect(m("Annette")).toBe(true);
    expect(m("Anne")).toBe(false);
  });

  test("regex metacharacters in input are escaped", () => {
    const m = compileQuery("(test)")!;
    // No * or ? → substring path; parens must not break.
    expect(m("(test)")).toBe(true);
    expect(m("test")).toBe(false);
  });

  test("regex metacharacters mixed with glob are escaped", () => {
    const m = compileQuery("a.b*")!;
    // Anchored glob path: literal "a.b" then any tail.
    expect(m("a.b")).toBe(true);
    expect(m("a.bcd")).toBe(true);
    expect(m("aXb")).toBe(false); // the dot must be literal, not regex "any char"
  });
});
