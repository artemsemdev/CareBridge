import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { AlertResponse, AlertSeverity, AlertType } from '../api/types';

const severityBadgeStyles: Record<AlertSeverity, string> = {
  Critical: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Informational: 'bg-blue-100 text-blue-800',
};

const severityOrder: Record<AlertSeverity, number> = {
  Critical: 0,
  High: 1,
  Medium: 2,
  Informational: 3,
};

const typeLabels: Record<AlertType, string> = {
  AbnormalReading: 'Abnormal Reading',
  MissedMilestone: 'Missed Milestone',
};

export function AlertsPage() {
  const [severityFilter, setSeverityFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const qc = useQueryClient();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['alerts', severityFilter, typeFilter],
    queryFn: () => api.getAlerts({
      status: 'Open',
      severity: severityFilter || undefined,
      type: typeFilter || undefined,
    }),
  });

  const ack = useMutation({
    mutationFn: (alertId: string) => api.acknowledgeAlert(alertId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['alerts'] }),
  });

  const res = useMutation({
    mutationFn: (alertId: string) => api.resolveAlert(alertId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['alerts'] }),
  });

  const sortedAlerts = [...(data?.items ?? [])].sort((a, b) => {
    const sevDiff = (severityOrder[a.severity] ?? 99) - (severityOrder[b.severity] ?? 99);
    if (sevDiff !== 0) return sevDiff;
    return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
  });

  return (
    <div className="max-w-5xl">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold text-gray-900">Alert Queue</h1>
          <p className="text-sm text-gray-500 mt-1">Open alerts across all cases, sorted by severity</p>
        </div>
        {data && (
          <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-red-50 text-red-700">
            {data.items.length} open
          </span>
        )}
      </div>

      {/* Filters */}
      <div className="flex gap-3 mb-4">
        <select
          value={severityFilter}
          onChange={e => setSeverityFilter(e.target.value)}
          className="text-sm border border-gray-200 rounded px-3 py-1.5 bg-white text-gray-700"
        >
          <option value="">All Severities</option>
          <option value="Critical">Critical</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Informational">Informational</option>
        </select>
        <select
          value={typeFilter}
          onChange={e => setTypeFilter(e.target.value)}
          className="text-sm border border-gray-200 rounded px-3 py-1.5 bg-white text-gray-700"
        >
          <option value="">All Types</option>
          <option value="AbnormalReading">Abnormal Reading</option>
          <option value="MissedMilestone">Missed Milestone</option>
        </select>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        {isLoading ? (
          <div className="animate-pulse h-32 bg-gray-50 m-4 rounded" />
        ) : isError ? (
          <p className="p-6 text-sm text-red-500">Failed to load alerts.</p>
        ) : sortedAlerts.length === 0 ? (
          <div className="p-8 text-center">
            <p className="text-gray-500 font-medium">No open alerts</p>
            <p className="text-gray-400 text-sm mt-1">All alerts have been resolved or none exist yet.</p>
          </div>
        ) : (
          <table className="min-w-full">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr className="text-xs text-gray-400 uppercase">
                <th className="text-left px-4 py-3 font-medium">Severity</th>
                <th className="text-left px-4 py-3 font-medium">Title / Description</th>
                <th className="text-left px-4 py-3 font-medium">Type</th>
                <th className="text-left px-4 py-3 font-medium">Case</th>
                <th className="text-left px-4 py-3 font-medium">Age</th>
                <th className="text-left px-4 py-3 font-medium">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {sortedAlerts.map(alert => (
                <AlertRow
                  key={alert.id}
                  alert={alert}
                  onAck={() => ack.mutate(alert.id)}
                  onRes={() => res.mutate(alert.id)}
                  isAcking={ack.isPending && ack.variables === alert.id}
                  isResing={res.isPending && res.variables === alert.id}
                />
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function AlertRow({
  alert,
  onAck,
  onRes,
  isAcking,
  isResing,
}: {
  alert: AlertResponse;
  onAck: () => void;
  onRes: () => void;
  isAcking: boolean;
  isResing: boolean;
}) {
  return (
    <tr className="hover:bg-gray-50">
      <td className="px-4 py-3">
        <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${severityBadgeStyles[alert.severity]}`}>
          {alert.severity}
        </span>
      </td>
      <td className="px-4 py-3">
        <p className="text-sm font-medium text-gray-900">{alert.title}</p>
        <p className="text-xs text-gray-500 mt-0.5">{alert.description}</p>
      </td>
      <td className="px-4 py-3 text-sm text-gray-500">
        {typeLabels[alert.type] ?? alert.type}
      </td>
      <td className="px-4 py-3 text-sm">
        <Link
          to={`/cases/${alert.caseId}`}
          className="text-blue-600 hover:underline font-mono text-xs"
        >
          {alert.caseId.slice(0, 8)}…
        </Link>
      </td>
      <td className="px-4 py-3 text-sm text-gray-500 whitespace-nowrap">
        {alert.age}
      </td>
      <td className="px-4 py-3">
        <div className="flex gap-2">
          {alert.status === 'Open' && (
            <button
              onClick={onAck}
              disabled={isAcking}
              className="text-xs px-2 py-1 rounded bg-blue-50 text-blue-700 hover:bg-blue-100 disabled:opacity-50"
            >
              Acknowledge
            </button>
          )}
          {alert.status === 'Acknowledged' && (
            <button
              onClick={onRes}
              disabled={isResing}
              className="text-xs px-2 py-1 rounded bg-green-50 text-green-700 hover:bg-green-100 disabled:opacity-50"
            >
              Resolve
            </button>
          )}
        </div>
      </td>
    </tr>
  );
}
