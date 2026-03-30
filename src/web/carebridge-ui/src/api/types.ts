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
