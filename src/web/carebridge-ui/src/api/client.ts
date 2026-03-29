import type { CaseResponse, CarePlanResponse, PaginatedResponse } from './types';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`);
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
};
