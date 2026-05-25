// Case- and diacritic-insensitive matcher used by the Résultats search box.
//
// Behaviour:
//  - Empty / whitespace-only query → matches everything (returns null).
//  - Query without `*` or `?` → substring match.
//  - Query containing `*` or `?` → anchored glob:
//      `*`  matches any (possibly empty) run of characters
//      `?`  matches exactly one character
//    Any other regex meta-character in the query is treated as a literal.
//
// All comparisons strip diacritics (NFD + Unicode category cleanup) and
// lowercase both sides so "marie" matches "Marie", "Mârié", etc.

export function normalize(value: string): string {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

function escapeRegex(s: string): string {
  return s.replace(/[.+^${}()|[\]\\]/g, "\\$&");
}

export function compileQuery(raw: string): ((value: string) => boolean) | null {
  const trimmed = raw.trim();
  if (trimmed.length === 0) return null;

  const normalized = normalize(trimmed);
  const hasGlob = /[*?]/.test(normalized);

  if (!hasGlob) {
    return (value) => normalize(value).includes(normalized);
  }

  // Build an anchored regex by escaping everything except * and ?, then
  // substituting them with their regex equivalents.
  const pattern = normalized
    .split("")
    .map((c) => {
      if (c === "*") return ".*";
      if (c === "?") return ".";
      return escapeRegex(c);
    })
    .join("");

  const re = new RegExp(`^${pattern}$`);
  return (value) => re.test(normalize(value));
}
