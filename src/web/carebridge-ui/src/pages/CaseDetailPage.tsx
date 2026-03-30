import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import type {
  CaseStatus, MilestoneStatus, ObservationResponse, AlertResponse, AlertSeverity,
  TaskResponse, TaskStatus, TaskPriority, AppointmentResponse, AppointmentStatus, AppointmentType,
} from '../api/types';

const caseStatusStyles: Record<CaseStatus, string> = {
  Active: 'bg-blue-100 text-blue-800',
  Monitoring: 'bg-yellow-100 text-yellow-800',
  Completed: 'bg-green-100 text-green-800',
  Closed: 'bg-gray-100 text-gray-600',
};

const milestoneStatusStyles: Record<MilestoneStatus, string> = {
  Pending: 'text-gray-500',
  Completed: 'text-green-600',
  Missed: 'text-red-600',
  Skipped: 'text-gray-400 line-through',
};

const severityBadgeStyles: Record<AlertSeverity, string> = {
  Critical: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Informational: 'bg-blue-100 text-blue-800',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function daysAgo(iso: string) {
  const ms = Date.now() - new Date(iso).getTime();
  return Math.floor(ms / 86_400_000);
}

function relativeTime(iso: string) {
  const ms = new Date(iso).getTime() - Date.now();
  const days = Math.round(ms / 86_400_000);
  if (days > 0) return `due in ${days} day${days !== 1 ? 's' : ''}`;
  if (days < 0) return `${Math.abs(days)} day${Math.abs(days) !== 1 ? 's' : ''} overdue`;
  return 'due today';
}

const observationTypeLabels: Record<string, string> = {
  BloodPressure: 'Blood Pressure',
  HeartRate: 'Heart Rate',
  SpO2: 'SpO2',
  Glucose: 'Glucose',
  Temperature: 'Temperature',
  Weight: 'Weight',
};

// Mirror of the server-side threshold logic for display highlighting
function getObservationSeverity(type: string, value: number): 'critical' | 'high' | 'medium' | null {
  switch (type) {
    case 'BloodPressure':
      if (value > 180) return 'critical';
      if (value > 160) return 'high';
      if (value > 140) return 'medium';
      return null;
    case 'HeartRate':
      if (value > 120 || value < 50) return 'high';
      return null;
    case 'SpO2':
      if (value < 90) return 'critical';
      if (value < 94) return 'high';
      return null;
    case 'Glucose':
      if (value > 300 || value < 70) return 'high';
      return null;
    case 'Temperature':
      if (value > 38.5) return 'medium';
      return null;
    default:
      return null;
  }
}

function ObservationValueCell({ obs }: { obs: ObservationResponse }) {
  const sev = getObservationSeverity(obs.type, obs.value);
  const cls = sev === 'critical'
    ? 'text-red-700 font-semibold bg-red-50 px-1 rounded'
    : sev === 'high'
    ? 'text-orange-700 font-semibold bg-orange-50 px-1 rounded'
    : sev === 'medium'
    ? 'text-yellow-700 font-medium'
    : 'text-gray-900';
  return <span className={cls}>{obs.value} {obs.unit}</span>;
}

function AlertCard({ alert }: { alert: AlertResponse }) {
  const qc = useQueryClient();
  const ack = useMutation({
    mutationFn: () => api.acknowledgeAlert(alert.id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['case-alerts'] }),
  });
  const res = useMutation({
    mutationFn: () => api.resolveAlert(alert.id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['case-alerts'] }),
  });

  return (
    <div className={`flex items-start justify-between gap-4 py-3 border-b border-gray-50 last:border-0`}>
      <div className="flex items-start gap-3 min-w-0">
        <span className={`mt-0.5 shrink-0 inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${severityBadgeStyles[alert.severity]}`}>
          {alert.severity}
        </span>
        <div className="min-w-0">
          <p className="text-sm font-medium text-gray-900">{alert.title}</p>
          <p className="text-xs text-gray-500 mt-0.5">{alert.description}</p>
          <p className="text-xs text-gray-400 mt-1">{alert.age} · {alert.status}</p>
        </div>
      </div>
      <div className="shrink-0 flex gap-2">
        {alert.status === 'Open' && (
          <button
            onClick={() => ack.mutate()}
            disabled={ack.isPending}
            className="text-xs px-2 py-1 rounded bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50"
          >
            Acknowledge
          </button>
        )}
        {alert.status === 'Acknowledged' && (
          <button
            onClick={() => res.mutate()}
            disabled={res.isPending}
            className="text-xs px-2 py-1 rounded bg-green-50 text-green-700 hover:bg-green-100 disabled:opacity-50"
          >
            Resolve
          </button>
        )}
      </div>
    </div>
  );
}

export function CaseDetailPage() {
  const { caseId } = useParams<{ caseId: string }>();
  const [obsCursor, setObsCursor] = useState<string | undefined>(undefined);

  const { data: caseData, isLoading: caseLoading, isError: caseError } = useQuery({
    queryKey: ['case', caseId],
    queryFn: () => api.getCase(caseId!),
    enabled: !!caseId,
  });

  const { data: planData, isLoading: planLoading } = useQuery({
    queryKey: ['care-plan', caseId],
    queryFn: () => api.getCarePlan(caseId!),
    enabled: !!caseId,
    retry: false,
  });

  const { data: obsData, isLoading: obsLoading } = useQuery({
    queryKey: ['observations', caseId, obsCursor],
    queryFn: () => api.getObservations(caseId!, { limit: 10, cursor: obsCursor }),
    enabled: !!caseId,
  });

  const { data: alertData, isLoading: alertLoading } = useQuery({
    queryKey: ['case-alerts', caseId],
    queryFn: () => api.getCaseAlerts(caseId!),
    enabled: !!caseId,
  });

  if (caseError) {
    return (
      <div className="flex flex-col items-center justify-center h-64 gap-3">
        <p className="text-gray-500">Case not found.</p>
        <Link to="/cases" className="text-blue-600 hover:underline text-sm">Back to Cases</Link>
      </div>
    );
  }

  return (
    <div className="max-w-5xl">
      {/* Breadcrumb */}
      <nav className="mb-4 text-sm text-gray-500 flex items-center gap-2">
        <Link to="/cases" className="hover:text-blue-600">Cases</Link>
        <span>/</span>
        <span className="text-gray-900">{caseData?.patientName ?? '...'}</span>
      </nav>

      {/* Patient info card */}
      {caseLoading ? (
        <div className="animate-pulse bg-white border border-gray-200 rounded-lg p-6 mb-6 h-32" />
      ) : caseData && (
        <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
          <div className="flex items-start justify-between">
            <div>
              <h1 className="text-xl font-bold text-gray-900 mb-1">{caseData.patientName}</h1>
              <p className="text-sm text-gray-500">Patient ID: {caseData.patientId}</p>
            </div>
            <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${caseStatusStyles[caseData.status]}`}>
              {caseData.status}
            </span>
          </div>
          <div className="mt-4 grid grid-cols-3 gap-4 text-sm">
            <div>
              <p className="text-gray-500">Discharge Date</p>
              <p className="font-medium text-gray-800">{formatDate(caseData.dischargeDate)}</p>
            </div>
            <div>
              <p className="text-gray-500">Days Since Discharge</p>
              <p className="font-medium text-gray-800">{daysAgo(caseData.dischargeDate)} days</p>
            </div>
            <div>
              <p className="text-gray-500">Diagnosis</p>
              <p className="font-medium text-gray-800">
                <span className="font-mono text-xs bg-gray-100 px-1 py-0.5 rounded mr-1">{caseData.diagnosisCode}</span>
                {caseData.diagnosisDescription}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Care Plan section */}
      {planLoading ? (
        <div className="animate-pulse bg-white border border-gray-200 rounded-lg p-6 mb-6 h-48" />
      ) : planData ? (
        <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-base font-semibold text-gray-900">{planData.templateName}</h2>
              <p className="text-xs text-gray-400 mt-0.5">Activated {formatDate(planData.activatedAt)}</p>
            </div>
            <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${planData.status === 'Completed' ? 'bg-green-100 text-green-700' : 'bg-blue-100 text-blue-700'}`}>
              {planData.status}
            </span>
          </div>

          <div className="mb-5">
            <div className="flex justify-between text-xs text-gray-500 mb-1">
              <span>{planData.progress.completed} of {planData.progress.total} milestones completed</span>
              <span>{planData.progress.percentComplete}%</span>
            </div>
            <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
              <div
                className="h-2 bg-blue-500 rounded-full transition-all"
                style={{ width: `${planData.progress.percentComplete}%` }}
              />
            </div>
          </div>

          <table className="min-w-full">
            <thead>
              <tr className="text-xs text-gray-400 uppercase">
                <th className="text-left pb-2 font-medium">Milestone</th>
                <th className="text-left pb-2 font-medium">Due</th>
                <th className="text-left pb-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {planData.milestones.map(m => (
                <tr key={m.id} className={m.isOverdue ? 'bg-orange-50' : ''}>
                  <td className="py-3 pr-4">
                    <p className="text-sm font-medium text-gray-900">{m.name}</p>
                    <p className="text-xs text-gray-400">{m.description}</p>
                  </td>
                  <td className="py-3 pr-4 text-sm text-gray-500 whitespace-nowrap">
                    <span>{formatDate(m.dueAt)}</span>
                    {m.status === 'Pending' && (
                      <span className={`block text-xs ${m.isOverdue ? 'text-orange-600 font-medium' : 'text-gray-400'}`}>
                        {relativeTime(m.dueAt)}
                      </span>
                    )}
                  </td>
                  <td className="py-3 text-sm">
                    <span className={milestoneStatusStyles[m.status]}>{m.status}</span>
                    {m.completedAt && (
                      <span className="block text-xs text-gray-400">{formatDate(m.completedAt)}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
          <p className="text-sm text-gray-400">Care plan not yet activated for this case.</p>
        </div>
      )}

      {/* Observations section */}
      <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
        <h3 className="text-sm font-semibold text-gray-700 mb-4">Recent Observations</h3>
        {obsLoading ? (
          <div className="animate-pulse h-24 bg-gray-50 rounded" />
        ) : !obsData || obsData.items.length === 0 ? (
          <p className="text-sm text-gray-400">No observations recorded yet.</p>
        ) : (
          <>
            <table className="min-w-full">
              <thead>
                <tr className="text-xs text-gray-400 uppercase">
                  <th className="text-left pb-2 font-medium">Type</th>
                  <th className="text-left pb-2 font-medium">Value</th>
                  <th className="text-left pb-2 font-medium">Recorded At</th>
                  <th className="text-left pb-2 font-medium">Device</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {obsData.items.map(obs => (
                  <tr key={obs.id}>
                    <td className="py-2 pr-4 text-sm text-gray-700">
                      {observationTypeLabels[obs.type] ?? obs.type}
                    </td>
                    <td className="py-2 pr-4 text-sm">
                      <ObservationValueCell obs={obs} />
                    </td>
                    <td className="py-2 pr-4 text-sm text-gray-500 whitespace-nowrap">
                      {formatDateTime(obs.recordedAt)}
                    </td>
                    <td className="py-2 text-sm text-gray-400">
                      {obs.deviceId ?? '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {obsData.hasMore && (
              <button
                onClick={() => setObsCursor(obsData.nextCursor ?? undefined)}
                className="mt-3 text-xs text-blue-600 hover:underline"
              >
                Load more
              </button>
            )}
          </>
        )}
      </div>

      {/* Alerts section */}
      <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
        <h3 className="text-sm font-semibold text-gray-700 mb-4">Alerts</h3>
        {alertLoading ? (
          <div className="animate-pulse h-24 bg-gray-50 rounded" />
        ) : !alertData || alertData.items.length === 0 ? (
          <p className="text-sm text-gray-400">No alerts for this case.</p>
        ) : (
          <div>
            {alertData.items
              .sort((a, b) => {
                // Open first, then Acknowledged, then Resolved
                const statusOrder = { Open: 0, Acknowledged: 1, Resolved: 2 };
                return (statusOrder[a.status] ?? 3) - (statusOrder[b.status] ?? 3);
              })
              .map(alert => (
                <AlertCard
                  key={alert.id}
                  alert={alert}
                />
              ))}
          </div>
        )}
      </div>

      {/* Tasks section */}
      <TasksSection caseId={caseId!} />

      {/* Appointments section */}
      <AppointmentsSection caseId={caseId!} />

      {/* Timeline placeholder */}
      <div className="bg-white border border-gray-200 rounded-lg p-6 mt-4">
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Timeline</h3>
        <p className="text-sm text-gray-400">Coming in next update</p>
      </div>
    </div>
  );
}

// --- Task section ---

const taskPriorityStyles: Record<TaskPriority, string> = {
  Urgent: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Low: 'bg-gray-100 text-gray-600',
};

const taskStatusStyles: Record<TaskStatus, string> = {
  Open: 'bg-blue-100 text-blue-800',
  InProgress: 'bg-amber-100 text-amber-800',
  Completed: 'bg-green-100 text-green-800',
  Deferred: 'bg-gray-100 text-gray-600',
};

function TaskActionButtons({ task }: { task: TaskResponse }) {
  const qc = useQueryClient();
  const update = useMutation({
    mutationFn: (body: { status?: string; completedBy?: string }) => api.updateTask(task.id, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['case-tasks'] }); qc.invalidateQueries({ queryKey: ['tasks'] }); },
  });
  return (
    <div className="flex gap-1">
      {task.status === 'Open' && (
        <button onClick={() => update.mutate({ status: 'InProgress' })} disabled={update.isPending}
          className="text-xs px-2 py-1 rounded bg-amber-50 text-amber-700 hover:bg-amber-100 disabled:opacity-50">Start</button>
      )}
      {task.status === 'InProgress' && (
        <button onClick={() => update.mutate({ status: 'Completed', completedBy: 'care-coordinator' })} disabled={update.isPending}
          className="text-xs px-2 py-1 rounded bg-green-50 text-green-700 hover:bg-green-100 disabled:opacity-50">Complete</button>
      )}
      {(task.status === 'Open' || task.status === 'InProgress') && (
        <button onClick={() => update.mutate({ status: 'Deferred' })} disabled={update.isPending}
          className="text-xs px-2 py-1 rounded bg-gray-50 text-gray-600 hover:bg-gray-100 disabled:opacity-50">Defer</button>
      )}
      {task.status === 'Deferred' && (
        <button onClick={() => update.mutate({ status: 'Open' })} disabled={update.isPending}
          className="text-xs px-2 py-1 rounded bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50">Reopen</button>
      )}
    </div>
  );
}

function CreateTaskInlineForm({ caseId, onClose }: { caseId: string; onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState({ title: '', description: '', priority: 'Medium' as TaskPriority, assignedTo: '' });
  const create = useMutation({
    mutationFn: () => api.createTask({ caseId, title: form.title, description: form.description, priority: form.priority, assignedTo: form.assignedTo || undefined }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['case-tasks'] }); onClose(); },
  });
  return (
    <div className="border-t border-gray-100 pt-3 mt-3 space-y-2">
      <input placeholder="Title" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))}
        className="w-full border border-gray-300 rounded px-3 py-1.5 text-sm" />
      <textarea placeholder="Description" value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
        className="w-full border border-gray-300 rounded px-3 py-1.5 text-sm" rows={2} />
      <div className="flex gap-2">
        <select value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value as TaskPriority }))}
          className="border border-gray-300 rounded px-2 py-1 text-sm">
          <option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option><option value="Urgent">Urgent</option>
        </select>
        <input placeholder="Assigned to" value={form.assignedTo} onChange={e => setForm(f => ({ ...f, assignedTo: e.target.value }))}
          className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm" />
      </div>
      <div className="flex justify-end gap-2">
        <button onClick={onClose} className="text-xs text-gray-500 hover:text-gray-700">Cancel</button>
        <button onClick={() => create.mutate()} disabled={create.isPending || !form.title || !form.description}
          className="text-xs px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
          {create.isPending ? 'Creating...' : 'Create'}
        </button>
      </div>
    </div>
  );
}

function TasksSection({ caseId }: { caseId: string }) {
  const [showCreate, setShowCreate] = useState(false);
  const { data, isLoading } = useQuery({
    queryKey: ['case-tasks', caseId],
    queryFn: () => api.getCaseTasks(caseId),
    enabled: !!caseId,
  });
  const taskCount = data?.items.length ?? 0;

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Tasks ({taskCount})</h3>
        <button onClick={() => setShowCreate(v => !v)} className="text-xs text-blue-600 hover:underline">
          {showCreate ? 'Cancel' : '+ New Task'}
        </button>
      </div>
      {isLoading ? (
        <div className="animate-pulse h-16 bg-gray-50 rounded" />
      ) : taskCount === 0 && !showCreate ? (
        <p className="text-sm text-gray-400">No tasks for this case.</p>
      ) : (
        <div className="space-y-2">
          {data?.items.map(task => (
            <div key={task.id} className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0">
              <div className="flex items-center gap-2 min-w-0">
                <span className={`shrink-0 inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${taskPriorityStyles[task.priority]}`}>{task.priority}</span>
                <span className="text-sm text-gray-900 truncate">{task.title}</span>
                <span className={`shrink-0 inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${taskStatusStyles[task.status]}`}>
                  {task.status === 'InProgress' ? 'In Progress' : task.status}
                </span>
              </div>
              <TaskActionButtons task={task} />
            </div>
          ))}
        </div>
      )}
      {showCreate && <CreateTaskInlineForm caseId={caseId} onClose={() => setShowCreate(false)} />}
    </div>
  );
}

// --- Appointment section ---

const apptStatusStyles: Record<AppointmentStatus, string> = {
  Proposed: 'bg-gray-100 text-gray-600',
  Booked: 'bg-blue-100 text-blue-800',
  Completed: 'bg-green-100 text-green-800',
  Canceled: 'bg-gray-100 text-gray-500 line-through',
  NoShow: 'bg-red-100 text-red-800',
};

const apptTypeLabels: Record<string, string> = {
  FollowUp: 'Follow-Up',
  LabWork: 'Lab Work',
  Specialist: 'Specialist',
};

function AppointmentActionButtons({ appt }: { appt: AppointmentResponse }) {
  const qc = useQueryClient();
  const update = useMutation({
    mutationFn: (body: { status?: string }) => api.updateAppointment(appt.id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['case-appointments'] }),
  });
  return (
    <div className="flex gap-1">
      {appt.status === 'Proposed' && (
        <>
          <button onClick={() => update.mutate({ status: 'Booked' })} disabled={update.isPending}
            className="text-xs px-2 py-1 rounded bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50">Confirm</button>
          <button onClick={() => update.mutate({ status: 'Canceled' })} disabled={update.isPending}
            className="text-xs px-2 py-1 rounded bg-gray-50 text-gray-600 hover:bg-gray-100 disabled:opacity-50">Cancel</button>
        </>
      )}
      {appt.status === 'Booked' && (
        <>
          <button onClick={() => update.mutate({ status: 'Completed' })} disabled={update.isPending}
            className="text-xs px-2 py-1 rounded bg-green-50 text-green-700 hover:bg-green-100 disabled:opacity-50">Complete</button>
          <button onClick={() => update.mutate({ status: 'NoShow' })} disabled={update.isPending}
            className="text-xs px-2 py-1 rounded bg-red-50 text-red-700 hover:bg-red-100 disabled:opacity-50">No Show</button>
          <button onClick={() => update.mutate({ status: 'Canceled' })} disabled={update.isPending}
            className="text-xs px-2 py-1 rounded bg-gray-50 text-gray-600 hover:bg-gray-100 disabled:opacity-50">Cancel</button>
        </>
      )}
    </div>
  );
}

function CreateAppointmentForm({ caseId, onClose }: { caseId: string; onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState({ type: 'FollowUp' as AppointmentType, scheduledAt: '', notes: '' });
  const createAs = (status: AppointmentStatus) =>
    api.createAppointment({ caseId, type: form.type, scheduledAt: new Date(form.scheduledAt).toISOString(), status, notes: form.notes || undefined });
  const createProposed = useMutation({ mutationFn: () => createAs('Proposed'), onSuccess: () => { qc.invalidateQueries({ queryKey: ['case-appointments'] }); onClose(); } });
  const createBooked = useMutation({ mutationFn: () => createAs('Booked'), onSuccess: () => { qc.invalidateQueries({ queryKey: ['case-appointments'] }); onClose(); } });
  const isPending = createProposed.isPending || createBooked.isPending;

  return (
    <div className="border-t border-gray-100 pt-3 mt-3 space-y-2">
      <div className="flex gap-2">
        <select value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value as AppointmentType }))}
          className="border border-gray-300 rounded px-2 py-1.5 text-sm">
          <option value="FollowUp">Follow-Up</option><option value="LabWork">Lab Work</option><option value="Specialist">Specialist</option>
        </select>
        <input type="datetime-local" value={form.scheduledAt} onChange={e => setForm(f => ({ ...f, scheduledAt: e.target.value }))}
          className="flex-1 border border-gray-300 rounded px-2 py-1.5 text-sm" />
      </div>
      <textarea placeholder="Notes (optional)" value={form.notes} onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
        className="w-full border border-gray-300 rounded px-3 py-1.5 text-sm" rows={2} />
      <div className="flex justify-end gap-2">
        <button onClick={onClose} className="text-xs text-gray-500 hover:text-gray-700">Cancel</button>
        <button onClick={() => createProposed.mutate()} disabled={isPending || !form.scheduledAt}
          className="text-xs px-3 py-1 bg-gray-200 text-gray-700 rounded hover:bg-gray-300 disabled:opacity-50">Propose</button>
        <button onClick={() => createBooked.mutate()} disabled={isPending || !form.scheduledAt}
          className="text-xs px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">Book</button>
      </div>
    </div>
  );
}

function AppointmentsSection({ caseId }: { caseId: string }) {
  const [showCreate, setShowCreate] = useState(false);
  const { data, isLoading } = useQuery({
    queryKey: ['case-appointments', caseId],
    queryFn: () => api.getCaseAppointments(caseId),
    enabled: !!caseId,
  });

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Appointments</h3>
        <button onClick={() => setShowCreate(v => !v)} className="text-xs text-blue-600 hover:underline">
          {showCreate ? 'Cancel' : '+ New Appointment'}
        </button>
      </div>
      {isLoading ? (
        <div className="animate-pulse h-16 bg-gray-50 rounded" />
      ) : !data || data.items.length === 0 ? (
        <p className="text-sm text-gray-400">No appointments for this case.</p>
      ) : (
        <table className="min-w-full">
          <thead>
            <tr className="text-xs text-gray-400 uppercase">
              <th className="text-left pb-2 font-medium">Type</th>
              <th className="text-left pb-2 font-medium">Scheduled</th>
              <th className="text-left pb-2 font-medium">Status</th>
              <th className="text-left pb-2 font-medium">Notes</th>
              <th className="text-left pb-2 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {data.items.map(appt => (
              <tr key={appt.id} className={appt.isOverdue ? 'bg-amber-50' : ''}>
                <td className="py-2 pr-4 text-sm text-gray-700">{apptTypeLabels[appt.type] ?? appt.type}</td>
                <td className="py-2 pr-4 text-sm text-gray-500 whitespace-nowrap">
                  {new Date(appt.scheduledAt).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                  {appt.isOverdue && <span className="block text-xs text-amber-600 font-medium">Past due</span>}
                </td>
                <td className="py-2 pr-4">
                  <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${apptStatusStyles[appt.status]}`}>
                    {appt.status === 'NoShow' ? 'No Show' : appt.status}
                  </span>
                </td>
                <td className="py-2 pr-4 text-sm text-gray-400 max-w-[200px] truncate">{appt.notes ?? '—'}</td>
                <td className="py-2"><AppointmentActionButtons appt={appt} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {showCreate && <CreateAppointmentForm caseId={caseId} onClose={() => setShowCreate(false)} />}
    </div>
  );
}
