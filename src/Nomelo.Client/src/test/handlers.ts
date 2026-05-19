import { http, HttpResponse } from "msw";
import type {
  ListDto,
  PairDto,
  ResultsDto,
  SessionDto,
} from "../api/types";

const ALICE: SessionDto = {
  id: "00000000-0000-0000-0000-000000000001",
  listId: "a",
  listName: "Liste A",
  confidenceThreshold: 3,
  createdAt: "2026-05-01T00:00:00Z",
  updatedAt: "2026-05-02T00:00:00Z",
  shareToken: "share-tok",
  voteCount: 7,
};

const LIST: ListDto = { id: "a", name: "Liste A", itemCount: 12 };

const PAIR: PairDto = {
  a: { value: "Alice", variants: ["Alicia"], description: "prénom A" },
  b: { value: "Bob", variants: [], description: null },
};

const RESULTS: ResultsDto = {
  sessionId: ALICE.id,
  listId: ALICE.listId,
  listName: ALICE.listName,
  voteCount: 7,
  stabilityReached: false,
  ranked: [
    { rank: 1, value: "Alice", variants: ["Alicia"], eloScore: 1080, timesShown: 5, isBanned: false },
    { rank: 2, value: "Carol", variants: [], eloScore: 990, timesShown: 4, isBanned: false },
  ],
  banned: [
    { rank: 0, value: "Bob", variants: [], eloScore: 1000, timesShown: 3, isBanned: true },
  ],
};

export const handlers = [
  http.get("/api/me", () => HttpResponse.json({ userId: "u-1" })),
  http.get("/api/lists", () => HttpResponse.json([LIST])),
  http.get("/api/sessions", () => HttpResponse.json([ALICE])),
  http.get(`/api/sessions/${ALICE.id}`, () => HttpResponse.json(ALICE)),
  http.post("/api/sessions", () => HttpResponse.json(ALICE, { status: 201 })),
  http.get(`/api/sessions/${ALICE.id}/next-pair`, () => HttpResponse.json(PAIR)),
  http.post(`/api/sessions/${ALICE.id}/votes`, () => new HttpResponse(null, { status: 204 })),
  http.get(`/api/sessions/${ALICE.id}/results`, () => HttpResponse.json(RESULTS)),
  http.get(`/api/share/${ALICE.shareToken}`, () => HttpResponse.json(RESULTS)),
];

export const fixtures = { ALICE, LIST, PAIR, RESULTS };
