import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import type { CaseStatus, MilestoneStatus, ObservationResponse, AlertResponse, AlertSeverity } from '../api/types';

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

      {/* Other placeholder sections */}
      <div className="grid grid-cols-2 gap-4">
        <PlaceholderCard title="Tasks" />
        <PlaceholderCard title="Appointments" />
      </div>
      <div className="mt-4">
        <PlaceholderCard title="Timeline" />
      </div>
    </div>
  );
}

function PlaceholderCard({ title }: { title: string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6">
      <h3 className="text-sm font-semibold text-gray-700 mb-2">{title}</h3>
      <p className="text-sm text-gray-400">Coming in next update</p>
    </div>
  );
}
