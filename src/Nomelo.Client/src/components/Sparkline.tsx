// Block characters used in the sparkline string. Each renders as a bar whose
// height is a fraction of the full glyph box; we map them to an integer scale
// 0..8 and draw a single SVG path (staircase polygon) so a translucent fill
// stays uniform across the whole shape — drawing one rect per column would
// stack two semi-transparent edges at the bar boundaries and produce visible
// vertical banding.
// Each non-zero block is shifted down by 0.5 unit so the smallest bar (▁)
// renders as a fine 0.5-unit line instead of a thick 1-unit strip. The peak
// bar still occupies most of the viewBox (7.5/8 = 94%) so the overall scale
// is barely affected.
const HEIGHT_MAP: Record<string, number> = {
  " ": 0,
  "▁": 0.5,
  "▂": 1.5,
  "▃": 2.5,
  "▄": 3.5,
  "▅": 4.5,
  "▆": 5.5,
  "▇": 6.5,
  "█": 7.5,
};

interface Props {
  data: string;
  className?: string;
}

export function Sparkline({ data, className }: Props) {
  const chars = Array.from(data);
  if (chars.length === 0) return null;

  const n = chars.length;
  const height = 8;
  const heights = chars.map((c) => HEIGHT_MAP[c] ?? 0);

  // Build the polygon as a closed staircase: start at bottom-left, step up
  // along each column's top, then return to baseline and close.
  const parts: string[] = [];
  parts.push(`M 0 ${height}`);
  parts.push(`L 0 ${height - heights[0]}`);
  for (let i = 0; i < n; i++) {
    parts.push(`L ${i + 1} ${height - heights[i]}`);
    if (i + 1 < n && heights[i + 1] !== heights[i]) {
      parts.push(`L ${i + 1} ${height - heights[i + 1]}`);
    }
  }
  parts.push(`L ${n} ${height}`);
  parts.push("Z");

  return (
    <svg
      className={className ?? "name-card__sparkline"}
      viewBox={`0 0 ${n} ${height}`}
      preserveAspectRatio="none"
      role="img"
      aria-label="Évolution sur la période"
      data-testid="sparkline"
    >
      <path d={parts.join(" ")} />
    </svg>
  );
}
