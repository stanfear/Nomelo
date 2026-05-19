export interface ListDto {
  id: string;
  name: string;
  itemCount: number;
}

export interface SessionDto {
  id: string;
  listId: string;
  listName: string;
  confidenceThreshold: number;
  createdAt: string;
  updatedAt: string;
  shareToken: string | null;
  voteCount: number;
}

export interface CreateSessionRequest {
  listId: string;
  confidenceThreshold: number;
}

export interface PairItemDto {
  value: string;
  variants: string[];
  description: string | null;
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

export interface RankedItemDto {
  rank: number;
  value: string;
  variants: string[];
  eloScore: number;
  timesShown: number;
  isBanned: boolean;
}

export interface ResultsDto {
  sessionId: string;
  listId: string;
  listName: string;
  voteCount: number;
  stabilityReached: boolean;
  ranked: RankedItemDto[];
  banned: RankedItemDto[];
}

export interface MeDto {
  userId: string;
}
