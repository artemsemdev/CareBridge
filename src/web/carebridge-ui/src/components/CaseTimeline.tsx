import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/client';
import type { TimelineCategory, TimelineEntry } from '../api/types';

const categoryConfig: Record<TimelineCategory, { label: string; icon: string; color: string }> = {
  case: { label: 'Case', icon: 'M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z', color: 'text-blue-500 bg-blue-50 border-blue-200' },
  careplan: { label: 'Care Plan', icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4', color: 'text-green-500 bg-green-50 border-green-200' },
  observation: { label: 'Observation', icon: 'M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z', color: 'text-cyan-500 bg-cyan-50 border-cyan-200' },
  alert: { label: 'Alert', icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z', color: 'text-red-500 bg-red-50 border-red-200' },
  task: { label: 'Task', icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z', color: 'text-purple-500 bg-purple-50 border-purple-200' },
  appointment: { label: 'Appointment', icon: 'M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z', color: 'text-teal-500 bg-teal-50 border-teal-200' },
  notification: { label: 'Notification', icon: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9', color: 'text-gray-500 bg-gray-50 border-gray-200' },
};

const alertSeverityColors: Record<string, string> = {
  Critical: 'text-red-500 bg-red-50 border-red-200',
  High: 'text-orange-500 bg-orange-50 border-orange-200',
  Medium: 'text-yellow-600 bg-yellow-50 border-yellow-200',
};

const severityBadgeStyles: Record<string, string> = {
  Critical: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Informational: 'bg-blue-100 text-blue-800',
};

const categories: { key: TimelineCategory | 'all'; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'case', label: 'Case' },
  { key: 'careplan', label: 'Care Plan' },
  { key: 'observation', label: 'Observations' },
  { key: 'alert', label: 'Alerts' },
  { key: 'task', label: 'Tasks' },
  { key: 'appointment', label: 'Appointments' },
];

function relativeTimestamp(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'yesterday';
  if (days < 30) return `${days}d ago`;
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

function fullTimestamp(iso: string): string {
  return new Date(iso).toLocaleString('en-US', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

function TimelineIcon({ entry }: { entry: TimelineEntry }) {
  const cat = entry.category as TimelineCategory;
  const config = categoryConfig[cat] ?? categoryConfig.case;
  const colorOverride = cat === 'alert' && entry.severity ? alertSeverityColors[entry.severity] : undefined;
  const colorClass = colorOverride ?? config.color;

  return (
    <div className={`flex-shrink-0 w-8 h-8 rounded-full border flex items-center justify-center ${colorClass}`}>
      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
        <path strokeLinecap="round" strokeLinejoin="round" d={config.icon} />
      </svg>
    </div>
  );
}

export function CaseTimeline({ caseId }: { caseId: string }) {
  const [selectedCategory, setSelectedCategory] = useState<TimelineCategory | 'all'>('all');
  const [sortNewestFirst, setSortNewestFirst] = useState(true);
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [accumulated, setAccumulated] = useState<TimelineEntry[]>([]);

  const categoryFilter = selectedCategory === 'all' ? undefined : selectedCategory;

  const { data, isLoading } = useQuery({
    queryKey: ['case-timeline', caseId, categoryFilter, cursor],
    queryFn: () => api.getCaseTimeline(caseId, { limit: 50, cursor, category: categoryFilter }),
    enabled: !!caseId,
  });

  const handleCategoryChange = (cat: TimelineCategory | 'all') => {
    setSelectedCategory(cat);
    setCursor(undefined);
    setAccumulated([]);
  };

  const allEntries = cursor ? [...accumulated, ...(data?.entries ?? [])] : (data?.entries ?? []);
  const sorted = sortNewestFirst ? allEntries : [...allEntries].reverse();

  const handleLoadMore = () => {
    if (data?.nextCursor) {
      setAccumulated(allEntries);
      setCursor(data.nextCursor);
    }
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold text-gray-700">Timeline</h3>
        <button
          onClick={() => setSortNewestFirst(v => !v)}
          className="text-xs text-gray-500 hover:text-gray-700 flex items-center gap-1"
        >
          <svg className={`w-3.5 h-3.5 transition-transform ${sortNewestFirst ? '' : 'rotate-180'}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
          </svg>
          {sortNewestFirst ? 'Newest first' : 'Oldest first'}
        </button>
      </div>

      {/* Category filter chips */}
      <div className="flex flex-wrap gap-1.5 mb-4">
        {categories.map(cat => (
          <button
            key={cat.key}
            onClick={() => handleCategoryChange(cat.key)}
            className={`px-2.5 py-1 text-xs font-medium rounded-full transition-colors ${
              selectedCategory === cat.key
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            {cat.label}
          </button>
        ))}
      </div>

      {isLoading && accumulated.length === 0 ? (
        <div className="space-y-4">
          {[1, 2, 3].map(i => (
            <div key={i} className="flex gap-3 animate-pulse">
              <div className="w-8 h-8 rounded-full bg-gray-100" />
              <div className="flex-1 space-y-2">
                <div className="h-3 bg-gray-100 rounded w-1/3" />
                <div className="h-3 bg-gray-100 rounded w-2/3" />
              </div>
            </div>
          ))}
        </div>
      ) : sorted.length === 0 ? (
        <p className="text-sm text-gray-400">No timeline events yet.</p>
      ) : (
        <>
          <div className="relative">
            {/* Vertical line */}
            <div className="absolute left-4 top-4 bottom-4 w-px bg-gray-200" />

            <div className="space-y-0">
              {sorted.map((entry) => (
                <div key={entry.id} className="relative flex gap-3 pb-4 last:pb-0">
                  {/* Icon */}
                  <TimelineIcon entry={entry} />

                  {/* Content */}
                  <div className="flex-1 min-w-0 pt-0.5">
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-gray-900">
                          {entry.title}
                          {entry.severity && (
                            <span className={`ml-2 inline-flex px-1.5 py-0 text-[10px] font-semibold rounded-full ${severityBadgeStyles[entry.severity] ?? 'bg-gray-100 text-gray-600'}`}>
                              {entry.severity}
                            </span>
                          )}
                        </p>
                        <p className="text-xs text-gray-500 mt-0.5">{entry.description}</p>
                      </div>
                      <span
                        className="shrink-0 text-xs text-gray-400 whitespace-nowrap cursor-default"
                        title={fullTimestamp(entry.timestamp)}
                      >
                        {relativeTimestamp(entry.timestamp)}
                      </span>
                    </div>
                    {entry.actor !== 'system' && (
                      <p className="text-[10px] text-gray-400 mt-1">by {entry.actor}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>

          {(data?.hasMore ?? false) && (
            <button
              onClick={handleLoadMore}
              className="mt-4 w-full text-center text-xs text-blue-600 hover:underline py-2"
            >
              Load more events
            </button>
          )}

          {data && (
            <p className="text-[10px] text-gray-400 mt-2 text-center">
              Showing {sorted.length} of {data.totalCount} events
            </p>
          )}
        </>
      )}
    </div>
  );
}
