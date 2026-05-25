import {
  useQuery,
  useMutation,
  useQueryClient,
} from "@tanstack/react-query";
import { apiFetch } from "./client";
import type {
  CreateSessionRequest,
  ListDto,
  MeDto,
  PairDto,
  ResultsDto,
  SessionDto,
  VoteRequest,
} from "./types";

export const useMe = () =>
  useQuery({
    queryKey: ["me"],
    queryFn: () => apiFetch<MeDto>("/api/me"),
  });

export const useLists = () =>
  useQuery({
    queryKey: ["lists"],
    queryFn: () => apiFetch<ListDto[]>("/api/lists"),
  });

export const useSessions = () =>
  useQuery({
    queryKey: ["sessions"],
    queryFn: () => apiFetch<SessionDto[]>("/api/sessions"),
  });

export const useSession = (id: string | undefined) =>
  useQuery({
    queryKey: ["session", id],
    queryFn: () => apiFetch<SessionDto>(`/api/sessions/${id}`),
    enabled: !!id,
  });

export const useCreateSession = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateSessionRequest) =>
      apiFetch<SessionDto>("/api/sessions", {
        method: "POST",
        body: JSON.stringify(req),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["sessions"] }),
  });
};

export const useNextPair = (sessionId: string | undefined) =>
  useQuery({
    queryKey: ["next-pair", sessionId],
    queryFn: () => apiFetch<PairDto>(`/api/sessions/${sessionId}/next-pair`),
    enabled: !!sessionId,
    staleTime: 0,
    refetchOnWindowFocus: false,
  });

export const useSubmitVote = (sessionId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: VoteRequest) =>
      apiFetch<void>(`/api/sessions/${sessionId}/votes`, {
        method: "POST",
        body: JSON.stringify(req),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["next-pair", sessionId] });
      qc.invalidateQueries({ queryKey: ["session", sessionId] });
      qc.invalidateQueries({ queryKey: ["results", sessionId] });
    },
  });
};

export const useUndoLastVote = (sessionId: string) => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiFetch<void>(`/api/sessions/${sessionId}/votes/last`, {
        method: "DELETE",
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["next-pair", sessionId] });
      qc.invalidateQueries({ queryKey: ["session", sessionId] });
      qc.invalidateQueries({ queryKey: ["results", sessionId] });
    },
  });
};

export const useResults = (sessionId: string | undefined) =>
  useQuery({
    queryKey: ["results", sessionId],
    queryFn: () => apiFetch<ResultsDto>(`/api/sessions/${sessionId}/results`),
    enabled: !!sessionId,
  });

export const useSharedResults = (token: string | undefined) =>
  useQuery({
    queryKey: ["share", token],
    queryFn: () => apiFetch<ResultsDto>(`/api/share/${token}`),
    enabled: !!token,
  });
