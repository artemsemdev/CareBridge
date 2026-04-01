import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { api } from '../api/client';
import type { AuditRecordDetail } from '../api/types';

const EVENT_TYPES = [
  'CaseCreated', 'CaseUpdated', 'CarePlanActivated', 'MilestoneCompleted',
  'ObservationReceived', 'AlertRaised', 'AlertAcknowledged', 'AlertResolved',
  'TaskCreated', 'TaskCompleted', 'AppointmentBooked', 'AppointmentCompleted',
  'AppointmentMissed', 'NotificationSent',
];

const ENTITY_TYPES = ['Case', 'CarePlan', 'Milestone', 'Observation', 'Alert', 'Task', 'Appointment', 'Notification'];

function formatTimestamp(iso: string) {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

function ExpandablePayload({ recordId }: { recordId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['audit-detail', recordId],
    queryFn: () => api.getAuditRecord(recordId),
  });

  if (isLoading) return <div className="p-4 text-sm text-gray-400">Loading...</div>;
  if (!data) return <div className="p-4 text-sm text-gray-400">No data</div>;

  return (
    <div className="p-4 bg-gray-50 border-t border-gray-100">
      <p className="text-xs font-semibold text-gray-500 mb-2">Event Payload</p>
      <pre className="text-xs bg-white border border-gray-200 rounded p-3 overflow-x-auto max-h-64 text-gray-700">
        {JSON.stringify((data as AuditRecordDetail).payload, null, 2)}
      </pre>
    </div>
  );
}

export function AuditPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [cursors, setCursors] = useState<string[]>([]);

  const caseId = searchParams.get('caseId') ?? '';
  const actorId = searchParams.get('actorId') ?? '';
  const eventType = searchParams.get('eventType') ?? '';
  const entityType = searchParams.get('entityType') ?? '';
  const from = searchParams.get('from') ?? '';
  const to = searchParams.get('to') ?? '';
  const cursor = cursors.length > 0 ? cursors[cursors.length - 1] : undefined;

  const { data, isLoading } = useQuery({
    queryKey: ['audit', caseId, actorId, eventType, entityType, from, to, cursor],
    queryFn: () => api.getAuditRecords({
      caseId: caseId || undefined,
      actorId: actorId || undefined,
      eventType: eventType || undefined,
      entityType: entityType || undefined,
      from: from || undefined,
      to: to ? `${to}T23:59:59Z` : undefined,
      limit: 50,
      cursor,
    }),
  });

  function updateFilter(key: string, value: string) {
    setCursors([]);
    setSearchParams(prev => {
      const next = new URLSearchParams(prev);
      if (value) next.set(key, value);
      else next.delete(key);
      return next;
    });
  }

  function clearFilters() {
    setCursors([]);
    setSearchParams({});
  }

  return (
    <div className="max-w-full">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-xl font-bold text-gray-900">Audit Log</h1>
        {data && <span className="text-sm text-gray-400">{data.totalCount} total records</span>}
      </div>

      {/* Filters */}
      <div className="bg-white border border-gray-200 rounded-lg p-4 mb-6">
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
          <div>
            <label className="block text-xs text-gray-500 mb-1">Case ID</label>
            <input
              type="text"
              value={caseId}
              onChange={e => updateFilter('caseId', e.target.value)}
              placeholder="Filter by case..."
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Actor</label>
            <input
              type="text"
              value={actorId}
              onChange={e => updateFilter('actorId', e.target.value)}
              placeholder="Filter by actor..."
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Event Type</label>
            <select
              value={eventType}
              onChange={e => updateFilter('eventType', e.target.value)}
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            >
              <option value="">All</option>
              {EVENT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">Entity Type</label>
            <select
              value={entityType}
              onChange={e => updateFilter('entityType', e.target.value)}
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            >
              <option value="">All</option>
              {ENTITY_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">From</label>
            <input
              type="date"
              value={from}
              onChange={e => updateFilter('from', e.target.value)}
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1">To</label>
            <input
              type="date"
              value={to}
              onChange={e => updateFilter('to', e.target.value)}
              className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
            />
          </div>
        </div>
        {(caseId || actorId || eventType || entityType || from || to) && (
          <button onClick={clearFilters} className="mt-3 text-xs text-blue-600 hover:underline">
            Clear all filters
          </button>
        )}
      </div>

      {/* Table */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        {isLoading ? (
          <div className="p-8">
            <div className="animate-pulse space-y-3">
              {[...Array(5)].map((_, i) => <div key={i} className="h-8 bg-gray-100 rounded" />)}
            </div>
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="p-8 text-center text-sm text-gray-400">
            No audit records found.
          </div>
        ) : (
          <>
            <table className="min-w-full">
              <thead>
                <tr className="text-xs text-gray-400 uppercase bg-gray-50 border-b border-gray-200">
                  <th className="text-left px-4 py-3 font-medium">Timestamp</th>
                  <th className="text-left px-4 py-3 font-medium">Event Type</th>
                  <th className="text-left px-4 py-3 font-medium">Action</th>
                  <th className="text-left px-4 py-3 font-medium">Actor</th>
                  <th className="text-left px-4 py-3 font-medium">Entity Type</th>
                  <th className="text-left px-4 py-3 font-medium">Entity</th>
                  <th className="text-left px-4 py-3 font-medium">Case</th>
                  <th className="text-left px-4 py-3 font-medium">Service</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.items.map(record => (
                  <tr key={record.id} className="group">
                    <td colSpan={8} className="p-0">
                      <button
                        onClick={() => setExpandedId(expandedId === record.id ? null : record.id)}
                        className="w-full text-left grid grid-cols-[auto_auto_1fr_auto_auto_auto_auto_auto] items-center hover:bg-gray-50 transition-colors"
                      >
                        <span className="px-4 py-3 text-xs text-gray-500 whitespace-nowrap">{formatTimestamp(record.timestamp)}</span>
                        <span className="px-4 py-3 text-xs font-mono text-gray-700">{record.eventType}</span>
                        <span className="px-4 py-3 text-sm text-gray-900">{record.action}</span>
                        <span className="px-4 py-3 text-xs text-gray-500">{record.actorId}</span>
                        <span className="px-4 py-3 text-xs text-gray-500">{record.entityType}</span>
                        <span className="px-4 py-3 text-xs text-gray-400 font-mono truncate max-w-[120px]">{record.entityId.substring(0, 8)}...</span>
                        <span className="px-4 py-3 text-xs">
                          <Link
                            to={`/cases/${record.caseId}`}
                            onClick={e => e.stopPropagation()}
                            className="text-blue-600 hover:underline font-mono"
                          >
                            {record.caseId.substring(0, 8)}...
                          </Link>
                        </span>
                        <span className="px-4 py-3 text-xs text-gray-400">{record.serviceSource}</span>
                      </button>
                      {expandedId === record.id && <ExpandablePayload recordId={record.id} />}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Pagination */}
            <div className="flex items-center justify-between px-4 py-3 border-t border-gray-200 bg-gray-50">
              <span className="text-xs text-gray-400">
                Showing {data.items.length} of {data.totalCount} records
              </span>
              <div className="flex gap-2">
                {cursors.length > 0 && (
                  <button
                    onClick={() => setCursors(prev => prev.slice(0, -1))}
                    className="text-xs px-3 py-1 rounded bg-white border border-gray-300 text-gray-600 hover:bg-gray-100"
                  >
                    Previous
                  </button>
                )}
                {data.hasMore && data.nextCursor && (
                  <button
                    onClick={() => setCursors(prev => [...prev, data.nextCursor!])}
                    className="text-xs px-3 py-1 rounded bg-white border border-gray-300 text-gray-600 hover:bg-gray-100"
                  >
                    Next
                  </button>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
