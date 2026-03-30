import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { TaskResponse, TaskStatus, TaskPriority } from '../api/types';

const priorityStyles: Record<TaskPriority, string> = {
  Urgent: 'bg-red-100 text-red-800',
  High: 'bg-orange-100 text-orange-800',
  Medium: 'bg-yellow-100 text-yellow-800',
  Low: 'bg-gray-100 text-gray-600',
};

const statusStyles: Record<TaskStatus, string> = {
  Open: 'bg-blue-100 text-blue-800',
  InProgress: 'bg-amber-100 text-amber-800',
  Completed: 'bg-green-100 text-green-800',
  Deferred: 'bg-gray-100 text-gray-600',
};

function CreateTaskModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState({ caseId: '', title: '', description: '', priority: 'Medium' as TaskPriority, assignedTo: '' });
  const create = useMutation({
    mutationFn: () => api.createTask({
      caseId: form.caseId,
      title: form.title,
      description: form.description,
      priority: form.priority,
      assignedTo: form.assignedTo || undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tasks'] }); onClose(); },
  });

  return (
    <div className="fixed inset-0 bg-black/30 flex items-center justify-center z-50" onClick={onClose}>
      <div className="bg-white rounded-lg p-6 w-full max-w-md shadow-lg" onClick={e => e.stopPropagation()}>
        <h3 className="text-lg font-semibold mb-4">Create Task</h3>
        <div className="space-y-3">
          <input placeholder="Case ID" value={form.caseId} onChange={e => setForm(f => ({ ...f, caseId: e.target.value }))}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm" />
          <input placeholder="Title" value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm" />
          <textarea placeholder="Description" value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm" rows={3} />
          <select value={form.priority} onChange={e => setForm(f => ({ ...f, priority: e.target.value as TaskPriority }))}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm">
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
            <option value="Urgent">Urgent</option>
          </select>
          <input placeholder="Assigned To (optional)" value={form.assignedTo} onChange={e => setForm(f => ({ ...f, assignedTo: e.target.value }))}
            className="w-full border border-gray-300 rounded px-3 py-2 text-sm" />
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <button onClick={onClose} className="px-3 py-1.5 text-sm text-gray-600 hover:text-gray-800">Cancel</button>
          <button onClick={() => create.mutate()} disabled={create.isPending || !form.title || !form.caseId || !form.description}
            className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
            {create.isPending ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}

function TaskActionButtons({ task }: { task: TaskResponse }) {
  const qc = useQueryClient();
  const update = useMutation({
    mutationFn: (body: { status?: string; completedBy?: string }) => api.updateTask(task.id, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tasks'] }); qc.invalidateQueries({ queryKey: ['case-tasks'] }); },
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

export function TasksPage() {
  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['tasks', statusFilter, priorityFilter],
    queryFn: () => api.getTasks({ status: statusFilter || undefined, priority: priorityFilter || undefined, limit: 50 }),
  });

  return (
    <div className="max-w-6xl">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-xl font-bold text-gray-900">Tasks</h1>
        <button onClick={() => setShowCreate(true)}
          className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700">
          Create Task
        </button>
      </div>

      <div className="flex gap-3 mb-4">
        <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}
          className="border border-gray-300 rounded px-3 py-1.5 text-sm">
          <option value="">All Statuses</option>
          <option value="Open">Open</option>
          <option value="InProgress">In Progress</option>
          <option value="Completed">Completed</option>
          <option value="Deferred">Deferred</option>
        </select>
        <select value={priorityFilter} onChange={e => setPriorityFilter(e.target.value)}
          className="border border-gray-300 rounded px-3 py-1.5 text-sm">
          <option value="">All Priorities</option>
          <option value="Urgent">Urgent</option>
          <option value="High">High</option>
          <option value="Medium">Medium</option>
          <option value="Low">Low</option>
        </select>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Title</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Priority</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Assigned To</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Age</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading && Array.from({ length: 5 }).map((_, i) => (
              <tr key={i}><td colSpan={6} className="px-4 py-3"><div className="animate-pulse h-4 bg-gray-100 rounded" /></td></tr>
            ))}
            {isError && (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-gray-500">
                Failed to load tasks. <button onClick={() => refetch()} className="text-blue-600 hover:underline">Retry</button>
              </td></tr>
            )}
            {data?.items.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-gray-400">No tasks found.</td></tr>
            )}
            {data?.items.map(task => (
              <tr key={task.id} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <div className="text-sm font-medium text-gray-900">{task.title}</div>
                  <Link to={`/cases/${task.caseId}`} className="text-xs text-blue-600 hover:underline">
                    Case {task.caseId.substring(0, 8)}
                  </Link>
                </td>
                <td className="px-4 py-3">
                  <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${priorityStyles[task.priority]}`}>
                    {task.priority}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={`inline-flex px-2 py-0.5 text-xs font-semibold rounded-full ${statusStyles[task.status]}`}>
                    {task.status === 'InProgress' ? 'In Progress' : task.status}
                  </span>
                </td>
                <td className="px-4 py-3 text-sm text-gray-500">{task.assignedTo ?? 'Unassigned'}</td>
                <td className="px-4 py-3 text-sm text-gray-400">{task.age}</td>
                <td className="px-4 py-3"><TaskActionButtons task={task} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showCreate && <CreateTaskModal onClose={() => setShowCreate(false)} />}
    </div>
  );
}
