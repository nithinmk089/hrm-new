export interface MyEmployeeMapping {
  companyId: string;
  employeeId: string;
}

export interface LeaveType {
  leaveTypeId: string;
  companyId: string;
  leaveTypeCode: string;
  leaveTypeName: string;
  description: string | null;
  isPaid: boolean;
  requiresApproval: boolean;
  annualEntitlementDays: number | null;
  allowHalfDay: boolean;
  allowCarryForward: boolean;
  maxCarryForwardDays: number;
  isActive: boolean;
  rowVersion: string;
}

export interface EmployeeLeaveBalance {
  employeeLeaveBalanceId: string;
  companyId: string;
  employeeId: string;
  leaveTypeId: string;
  leaveTypeCode: string;
  leaveTypeName: string;
  leaveYear: number;
  openingDays: number;
  accruedDays: number;
  carriedForwardDays: number;
  adjustmentDays: number;
  usedDays: number;
  reservedDays: number;
  availableDays: number;
  rowVersion: string;
}

export interface SubmitLeaveRequest {
  leaveTypeId: string;
  fromDate: string;
  toDate: string;
  isHalfDay: boolean;
  halfDayPeriod: 'AM' | 'PM' | null;
  reason: string;
}

export interface LeaveYearReservation {
  leaveYear: number;
  days: number;
}

export interface LeaveRequestDto {
  leaveRequestId: string;
  companyId: string;
  employeeId: string;
  leaveTypeId: string;
  leaveTypeCode: string;
  leaveTypeName: string;
  fromDate: string;
  toDate: string;
  isHalfDay: boolean;
  halfDayPeriod: string | null;
  daysRequested: number;
  reason: string;
  status: string;
  submittedAtUtc: string;
  rowVersion: string;
}

export interface LeaveRequestSubmissionResult {
  request: LeaveRequestDto;
  yearAllocations: LeaveYearReservation[];
  excludedDates: string[];
}
