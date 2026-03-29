import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { CaseStatus } from '../api/types';

const statusStyles: Record<CaseStatus, string> = {
  Active: 'bg-blue-100 text-blue-800',
  Monitoring: 'bg-yellow-100 text-yellow-800',
  Completed: 'bg-green-100 text-green-800',
  Closed: 'bg-gray-100 text-gray-600',
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
}

function SkeletonRow() {
  return (
    <tr className="animate-pulse">
      {Array.from({ length: 5 }).map((_, i) => (
        <td key={i} className="px-6 py-4">
          <div className="h-4 bg-gray-200 rounded w-3/4" />
        </td>
      ))}
    </tr>
  );
}

export function CasesPage() {
  const navigate = useNavigate();
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['cases'],
    queryFn: () => api.getCases({ limit: 50 }),
  });

  return (
    <div>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">Cases</h1>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              {['Patient Name', 'Diagnosis', 'Status', 'Discharge Date', 'Created'].map(col => (
                <th key={col} className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {isLoading && Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}

            {isError && (
              <tr>
                <td colSpan={5} className="px-6 py-12 text-center">
                  <p className="text-gray-500 mb-3">Failed to load cases.</p>
                  <button
                    onClick={() => refetch()}
                    className="px-4 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700"
                  >
                    Retry
                  </button>
                </td>
              </tr>
            )}

            {!isLoading && !isError && data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-12 text-center">
                  <p className="text-gray-500 font-medium">No cases found</p>
                  <p className="text-gray-400 text-sm mt-1">Cases will appear here when discharge events are submitted.</p>
                </td>
              </tr>
            )}

            {data?.items.map(c => (
              <tr
                key={c.id}
                onClick={() => navigate(`/cases/${c.id}`)}
                className="cursor-pointer hover:bg-gray-50 transition-colors"
              >
                <td className="px-6 py-4 text-sm font-medium text-gray-900">{c.patientName}</td>
                <td className="px-6 py-4 text-sm text-gray-600">
                  <span className="font-mono text-xs bg-gray-100 px-1.5 py-0.5 rounded mr-1">{c.diagnosisCode}</span>
                  {c.diagnosisDescription}
                </td>
                <td className="px-6 py-4">
                  <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${statusStyles[c.status]}`}>
                    {c.status}
                  </span>
                </td>
                <td className="px-6 py-4 text-sm text-gray-600">{formatDate(c.dischargeDate)}</td>
                <td className="px-6 py-4 text-sm text-gray-400">{formatDate(c.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
