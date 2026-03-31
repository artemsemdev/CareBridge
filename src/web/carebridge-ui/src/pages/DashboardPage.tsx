import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { AlertSeverity } from '../api/types';

const severityBadgeStyles: Record<AlertSeverity, string> = {
  Critical: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Informational: 'bg-blue-100 text-blue-800',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}

function daysAgo(iso: string) {
  const ms = Date.now() - new Date(iso).getTime();
  return Math.floor(ms / 86_400_000);
}

function formatLastUpdated(iso: string) {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

const caseStatusStyles: Record<string, string> = {
  Active: 'bg-blue-100 text-blue-800',
  Monitoring: 'bg-yellow-100 text-yellow-800',
  Completed: 'bg-green-100 text-green-800',
  Closed: 'bg-gray-100 text-gray-600',
};

export function DashboardPage() {
  const queryClient = useQueryClient();
  const { data, isLoading, isError, dataUpdatedAt } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: () => api.getDashboardSummary(),
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });

  if (isError) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <p className="text-gray-500 mb-2">Unable to load dashboard data.</p>
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })}
            className="text-sm text-blue-600 hover:underline"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-6xl">
      {/* Header with refresh */}
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-xl font-bold text-gray-900">Operational Dashboard</h1>
        <div className="flex items-center gap-3">
          {dataUpdatedAt > 0 && (
            <span className="text-xs text-gray-400">
              Updated {formatLastUpdated(new Date(dataUpdatedAt).toISOString())}
            </span>
          )}
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-gray-600 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refresh
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="space-y-6">
          <div className="grid grid-cols-4 gap-4">
            {[1, 2, 3, 4].map(i => (
              <div key={i} className="animate-pulse bg-white border border-gray-200 rounded-lg p-5 h-24" />
            ))}
          </div>
          <div className="animate-pulse bg-white border border-gray-200 rounded-lg p-6 h-48" />
          <div className="animate-pulse bg-white border border-gray-200 rounded-lg p-6 h-48" />
        </div>
      ) : data && (
        <>
          {/* Summary cards */}
          <div className="grid grid-cols-4 gap-4 mb-6">
            <Link to="/cases" className="bg-white border border-gray-200 rounded-lg p-5 hover:shadow-md transition-shadow group">
              <p className="text-sm font-medium text-gray-500 group-hover:text-blue-600">Active Cases</p>
              <p className="text-3xl font-bold text-blue-600 mt-1">{data.activeCaseCount}</p>
            </Link>

            <Link to="/alerts" className="bg-white border border-gray-200 rounded-lg p-5 hover:shadow-md transition-shadow group">
              <p className="text-sm font-medium text-gray-500 group-hover:text-red-600">Open Critical Alerts</p>
              <p className={`text-3xl font-bold mt-1 ${data.alerts.bySeverity.critical > 0 ? 'text-red-600' : 'text-gray-400'}`}>
                {data.alerts.bySeverity.critical}
              </p>
            </Link>

            <Link to="/tasks" className="bg-white border border-gray-200 rounded-lg p-5 hover:shadow-md transition-shadow group">
              <p className="text-sm font-medium text-gray-500 group-hover:text-amber-600">Overdue Tasks</p>
              <p className={`text-3xl font-bold mt-1 ${data.tasks.overdue > 0 ? 'text-amber-600' : 'text-gray-400'}`}>
                {data.tasks.overdue}
              </p>
            </Link>

            <Link to="/cases" className="bg-white border border-gray-200 rounded-lg p-5 hover:shadow-md transition-shadow group">
              <p className="text-sm font-medium text-gray-500 group-hover:text-gray-700">Pending Appointments</p>
              <p className="text-3xl font-bold text-gray-600 mt-1">{data.appointments.pending}</p>
            </Link>
          </div>

          {/* Alert queue */}
          <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2">
                <h2 className="text-base font-semibold text-gray-900">Alert Queue</h2>
                {data.alerts.open > 0 && (
                  <span className="inline-flex items-center justify-center min-w-[20px] h-5 px-1.5 text-xs font-bold rounded-full bg-red-500 text-white">
                    {data.alerts.open}
                  </span>
                )}
              </div>
              <Link to="/alerts" className="text-xs text-blue-600 hover:underline">View all alerts</Link>
            </div>
            {data.topAlerts.length === 0 ? (
              <p className="text-sm text-gray-400">No open alerts.</p>
            ) : (
              <table className="min-w-full">
                <thead>
                  <tr className="text-xs text-gray-400 uppercase">
                    <th className="text-left pb-2 font-medium">Severity</th>
                    <th className="text-left pb-2 font-medium">Title</th>
                    <th className="text-left pb-2 font-medium">Case</th>
                    <th className="text-left pb-2 font-medium">Age</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {data.topAlerts.map(alert => (
                    <tr key={alert.id} className="hover:bg-gray-50 cursor-pointer group">
                      <td className="py-2.5 pr-4">
                        <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${severityBadgeStyles[alert.severity]}`}>
                          {alert.severity}
                        </span>
                      </td>
                      <td className="py-2.5 pr-4 text-sm text-gray-900">{alert.title}</td>
                      <td className="py-2.5 pr-4">
                        <Link to={`/cases/${alert.caseId}`} className="text-sm text-blue-600 hover:underline">
                          {alert.caseId.slice(0, 8)}...
                        </Link>
                      </td>
                      <td className="py-2.5 text-sm text-gray-500">{alert.age}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {/* Recent cases */}
          <div className="bg-white border border-gray-200 rounded-lg p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-base font-semibold text-gray-900">Recent Cases</h2>
              <Link to="/cases" className="text-xs text-blue-600 hover:underline">View all cases</Link>
            </div>
            {data.recentCases.length === 0 ? (
              <p className="text-sm text-gray-400">No cases yet.</p>
            ) : (
              <table className="min-w-full">
                <thead>
                  <tr className="text-xs text-gray-400 uppercase">
                    <th className="text-left pb-2 font-medium">Patient</th>
                    <th className="text-left pb-2 font-medium">Status</th>
                    <th className="text-left pb-2 font-medium">Discharge Date</th>
                    <th className="text-left pb-2 font-medium">Days Since</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {data.recentCases.map(c => (
                    <tr key={c.id} className="hover:bg-gray-50">
                      <td className="py-2.5 pr-4">
                        <Link to={`/cases/${c.id}`} className="text-sm font-medium text-blue-600 hover:underline">
                          {c.patientName}
                        </Link>
                      </td>
                      <td className="py-2.5 pr-4">
                        <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${caseStatusStyles[c.status] ?? 'bg-gray-100 text-gray-600'}`}>
                          {c.status}
                        </span>
                      </td>
                      <td className="py-2.5 pr-4 text-sm text-gray-500">{formatDate(c.dischargeDate)}</td>
                      <td className="py-2.5 text-sm text-gray-500">{daysAgo(c.dischargeDate)} days</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
