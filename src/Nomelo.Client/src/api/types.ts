export interface ListDto {
  id: string;
  name: string;
  itemCount: number;
}

export interface SessionDto {
  id: string;
  listId: string;
  listName: string;
  /** User-chosen label; if null, fall back to listName for display. */
  name: string | null;
  confidenceThreshold: number;
  createdAt: string;
  updatedAt: string;
  shareToken: string | null;
  voteCount: number;
  stabilityReached: boolean;
}

export interface CreateSessionRequest {
  listId: string;
  confidenceThreshold: number;
  name?: string;
}

export interface PairItemDto {
  value: string;
  variants: string[];
  description: string | null;
  sparkline: string | null;
  peakYear: number | null;
  peakCount: number | null;
}

export interface PairDto {
  a: PairItemDto;
  b: PairItemDto;
}

export type VoteResult =
  | "prefer_a"
  | "prefer_b"
  | "ban_a"
  | "ban_b"
  | "ban_both"
  | "like_both";

export interface VoteRequest {
  itemA: string;
  itemB: string;
  result: VoteResult;
}

export interface BulkBanRequest {
  items: string[];
}

export interface RankedItemDto {
  rank: number;
  value: string;
  variants: string[];
  eloScore: number;
  timesShown: number;
  isBanned: boolean;
  sparkline: string | null;
  peakYear: number | null;
  peakCount: number | null;
}

export interface ResultsDto {
  sessionId: string;
  listId: string;
  listName: string;
  /** User-chosen label inherited from the session; null falls back to listName. */
  name: string | null;
  voteCount: number;
  stabilityReached: boolean;
  ranked: RankedItemDto[];
  banned: RankedItemDto[];
}

export interface MeDto {
  userId: string;
}
