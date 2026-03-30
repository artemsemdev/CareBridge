import type { CaseResponse, CarePlanResponse, PaginatedResponse, ObservationResponse, AlertResponse } from './types';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json() as Promise<T>;
}

async function patch<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json() as Promise<T>;
}

export const api = {
  getCases: (params?: { status?: string; limit?: number; cursor?: string }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.limit) qs.set('limit', String(params.limit));
    if (params?.cursor) qs.set('cursor', params.cursor);
    const query = qs.toString() ? `?${qs}` : '';
    return get<PaginatedResponse<CaseResponse>>(`/api/cases${query}`);
  },
  getCase: (caseId: string) => get<CaseResponse>(`/api/cases/${caseId}`),
  getCarePlan: (caseId: string) => get<CarePlanResponse>(`/api/cases/${caseId}/care-plan`),

  getObservations: (caseId: string, params?: { type?: string; limit?: number; cursor?: string }) => {
    const qs = new URLSearchParams();
    if (params?.type) qs.set('type', params.type);
    if (params?.limit) qs.set('limit', String(params.limit));
    if (params?.cursor) qs.set('cursor', params.cursor);
    const query = qs.toString() ? `?${qs}` : '';
    return get<PaginatedResponse<ObservationResponse>>(`/api/cases/${caseId}/observations${query}`);
  },

  getCaseAlerts: (caseId: string, params?: { status?: string; cursor?: string }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.cursor) qs.set('cursor', params.cursor);
    const query = qs.toString() ? `?${qs}` : '';
    return get<PaginatedResponse<AlertResponse>>(`/api/cases/${caseId}/alerts${query}`);
  },

  getAlerts: (params?: { status?: string; severity?: string; type?: string; cursor?: string }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.severity) qs.set('severity', params.severity);
    if (params?.type) qs.set('type', params.type);
    if (params?.cursor) qs.set('cursor', params.cursor);
    const query = qs.toString() ? `?${qs}` : '';
    return get<PaginatedResponse<AlertResponse>>(`/api/alerts${query}`);
  },

  acknowledgeAlert: (alertId: string, actor?: string) =>
    patch<AlertResponse>(`/api/alerts/${alertId}/acknowledge`, { actor: actor ?? 'care-coordinator' }),

  resolveAlert: (alertId: string, actor?: string) =>
    patch<AlertResponse>(`/api/alerts/${alertId}/resolve`, { actor: actor ?? 'care-coordinator' }),
};
