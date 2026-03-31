export type CaseStatus = 'Active' | 'Monitoring' | 'Completed' | 'Closed';
export type MilestoneStatus = 'Pending' | 'Completed' | 'Missed' | 'Skipped';
export type CarePlanStatus = 'Active' | 'Completed';
export type AlertStatus = 'Open' | 'Acknowledged' | 'Resolved';
export type AlertSeverity = 'Informational' | 'Medium' | 'High' | 'Critical';
export type AlertType = 'AbnormalReading' | 'MissedMilestone';
export type ObservationType = 'BloodPressure' | 'HeartRate' | 'SpO2' | 'Glucose' | 'Temperature' | 'Weight';

export interface CaseResponse {
  id: string;
  patientId: string;
  patientName: string;
  diagnosisCode: string;
  diagnosisDescription: string;
  dischargeDate: string;
  status: CaseStatus;
  createdAt: string;
  updatedAt: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}

export interface MilestoneResponse {
  id: string;
  name: string;
  description: string;
  dueAt: string;
  status: MilestoneStatus;
  completedAt: string | null;
  isOverdue: boolean;
}

export interface ProgressSummary {
  completed: number;
  total: number;
  percentComplete: number;
}

export interface CarePlanResponse {
  id: string;
  caseId: string;
  templateName: string;
  status: CarePlanStatus;
  activatedAt: string;
  completedAt: string | null;
  milestones: MilestoneResponse[];
  progress: ProgressSummary;
}

export interface ObservationResponse {
  id: string;
  caseId: string;
  type: string;
  value: number;
  unit: string;
  recordedAt: string;
  receivedAt: string;
  deviceId: string | null;
  idempotencyKey: string;
}

export interface AlertResponse {
  id: string;
  caseId: string;
  type: AlertType;
  severity: AlertSeverity;
  title: string;
  description: string;
  sourceEventId: string | null;
  status: AlertStatus;
  createdAt: string;
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
  resolvedAt: string | null;
  resolvedBy: string | null;
  age: string;
}

// Task types
export type TaskStatus = 'Open' | 'InProgress' | 'Completed' | 'Deferred';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';

export interface TaskResponse {
  id: string;
  caseId: string;
  alertId: string | null;
  title: string;
  description: string;
  assignedTo: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  completedBy: string | null;
  age: string;
}

export interface CreateTaskRequest {
  caseId: string;
  title: string;
  description: string;
  priority?: TaskPriority;
  assignedTo?: string;
}

// Appointment types
export type AppointmentStatus = 'Proposed' | 'Booked' | 'Completed' | 'Canceled' | 'NoShow';
export type AppointmentType = 'FollowUp' | 'LabWork' | 'Specialist';

export interface AppointmentResponse {
  id: string;
  caseId: string;
  type: string;
  scheduledAt: string;
  status: AppointmentStatus;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  isOverdue: boolean;
}

export interface CreateAppointmentRequest {
  caseId: string;
  type?: AppointmentType;
  scheduledAt: string;
  status?: AppointmentStatus;
  notes?: string;
}

// Dashboard types
export interface DashboardSummaryResponse {
  activeCaseCount: number;
  alerts: {
    open: number;
    acknowledged: number;
    bySeverity: {
      critical: number;
      high: number;
      medium: number;
      informational: number;
    };
  };
  tasks: {
    open: number;
    overdue: number;
  };
  appointments: {
    pending: number;
  };
  recentCases: {
    id: string;
    patientName: string;
    status: string;
    dischargeDate: string;
    createdAt: string;
  }[];
  topAlerts: {
    id: string;
    caseId: string;
    title: string;
    severity: AlertSeverity;
    age: string;
    createdAt: string;
  }[];
  lastUpdatedAt: string;
}

// Timeline types
export type TimelineCategory = 'case' | 'careplan' | 'observation' | 'alert' | 'task' | 'appointment' | 'notification';

export interface TimelineEntry {
  id: string;
  timestamp: string;
  eventType: string;
  category: TimelineCategory;
  title: string;
  description: string;
  actor: string;
  severity: string | null;
  entityId: string | null;
}

export interface TimelineResponse {
  caseId: string;
  entries: TimelineEntry[];
  nextCursor: string | null;
  hasMore: boolean;
  totalCount: number;
}
