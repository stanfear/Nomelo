// Block characters used in the sparkline string. Each renders as a bar whose
// height is a fraction of the full glyph box; we map them to an integer scale
// 0..8 and draw SVG rects so the bars align pixel-perfect regardless of the
// platform font. Plain Unicode rendering drifts at the baseline for the full
// block (U+2588) and the heavier blocks (▅▆▇) in many fonts (Consolas being
// the typical offender on Windows).
const HEIGHT_MAP: Record<string, number> = {
  " ": 0,
  "▁": 1, // ▁
  "▂": 2, // ▂
  "▃": 3, // ▃
  "▄": 4, // ▄
  "▅": 5, // ▅
  "▆": 6, // ▆
  "▇": 7, // ▇
  "█": 8, // █
};

interface Props {
  data: string;
  className?: string;
}

export function Sparkline({ data, className }: Props) {
  const chars = Array.from(data);
  if (chars.length === 0) return null;

  const width = chars.length;
  const height = 8;

  return (
    <svg
      className={className ?? "name-card__sparkline"}
      viewBox={`0 0 ${width} ${height}`}
      preserveAspectRatio="none"
      role="img"
      aria-label="Évolution sur la période"
      data-testid="sparkline"
    >
      {chars.map((c, i) => {
        const h = HEIGHT_MAP[c] ?? 0;
        if (h === 0) return null;
        // Each bar extends slightly past its column so the antialiased right
        // edge overlaps the next bar's antialiased left edge — closes any
        // sub-pixel seam without needing crispEdges (which introduces visible
        // periodic gaps when the SVG scales to a non-multiple-of-25 width).
        return (
          <rect
            key={i}
            x={i}
            y={height - h}
            width={1.1}
            height={h}
          />
        );
      })}
    </svg>
  );
}
