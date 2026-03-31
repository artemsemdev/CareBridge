import type {
  CaseResponse, CarePlanResponse, PaginatedResponse, ObservationResponse, AlertResponse,
  TaskResponse, CreateTaskRequest, AppointmentResponse, CreateAppointmentRequest,
  DashboardSummaryResponse, TimelineResponse,
} from './types';

// Security: All API calls route through the BFF gateway which handles authentication
// and authorization. The frontend never calls backend microservices directly.
// PHI: API responses may contain patient-identifiable data — components rendering this
// data should only be mounted inside role-gated routes.
const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`);
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json() as Promise<T>;
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
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

  // Tasks
  getTasks: (params?: { status?: string; priority?: string; caseId?: string; limit?: number; cursor?: string }) => {
    const qs = new URLSearchParams();
    if (params?.status) qs.set('status', params.status);
    if (params?.priority) qs.set('priority', params.priority);
    if (params?.caseId) qs.set('caseId', params.caseId);
    if (params?.limit) qs.set('limit', String(params.limit));
    if (params?.cursor) qs.set('cursor', params.cursor);
    const query = qs.toString() ? `?${qs}` : '';
    return get<PaginatedResponse<TaskResponse>>(`/api/tasks${query}`);
  },
  getCaseTasks: (caseId: string) =>
    get<PaginatedResponse<TaskResponse>>(`/api/cases/${caseId}/tasks`),
  createTask: (data: CreateTaskRequest) =>
    post<TaskResponse>('/api/tasks', data),
  updateTask: (taskId: string, body: { status?: string; assignedTo?: string; priority?: string; completedBy?: string }) =>
    patch<TaskResponse>(`/api/tasks/${taskId}`, body),

  // Appointments
  getCaseAppointments: (caseId: string) =>
    get<PaginatedResponse<AppointmentResponse>>(`/api/cases/${caseId}/appointments`),
  createAppointment: (data: CreateAppointmentRequest) =>
    post<AppointmentResponse>('/api/appointments', data),
  updateAppointment: (id: string, body: { status?: string; notes?: string }) =>
    patch<AppointmentResponse>(`/api/appointments/${id}`, body),

  // Dashboard & Timeline
  getDashboardSummary: () => get<DashboardSummaryResponse>('/api/dashboard/summary'),
  getCaseTimeline: (caseId: string, params?: { limit?: number; cursor?: string; category?: string }) => {
    const qs = new URLSearchParams();
    if (params?.limit) qs.set('limit', String(params.limit));
    if (params?.cursor) qs.set('cursor', params.cursor);
    if (params?.category) qs.set('category', params.category);
    const query = qs.toString() ? `?${qs}` : '';
    return get<TimelineResponse>(`/api/cases/${caseId}/timeline${query}`);
  },
};
