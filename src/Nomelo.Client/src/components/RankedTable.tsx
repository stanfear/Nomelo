import { useState } from "react";
import type { RankedItemDto } from "../api/types";

interface Props { ranked: RankedItemDto[]; banned: RankedItemDto[]; }

function Row({ item, showRank }: { item: RankedItemDto; showRank: boolean }) {
  return (
    <tr>
      <td>{showRank ? item.rank : ""}</td>
      <td>
        {item.value}
        {item.variants.length > 0 && <span className="muted"> · {item.variants.join(", ")}</span>}
      </td>
      <td>{Math.round(item.eloScore)}</td>
      <td>{item.timesShown}</td>
    </tr>
  );
}

export function RankedTable({ ranked, banned }: Props) {
  const [showBanned, setShowBanned] = useState(false);
  const bannedLabel = banned.length === 1 ? "1 banni" : `${banned.length} bannis`;
  return (
    <div className="ranked-table">
      <table>
        <thead>
          <tr>
            <th>#</th>
            <th>Prénom</th>
            <th>ELO</th>
            <th>Vu</th>
          </tr>
        </thead>
        <tbody>
          {ranked.map((r) => <Row key={r.value} item={r} showRank />)}
        </tbody>
      </table>

      {banned.length > 0 && (
        <>
          <button
            type="button"
            className="ranked-table__expand"
            onClick={() => setShowBanned((v) => !v)}
            aria-expanded={showBanned}
          >
            {showBanned ? "Masquer" : "Afficher"} {bannedLabel}
          </button>
          {showBanned && (
            <table>
              <tbody>
                {banned.map((r) => <Row key={r.value} item={r} showRank={false} />)}
              </tbody>
            </table>
          )}
        </>
      )}
    </div>
  );
}
