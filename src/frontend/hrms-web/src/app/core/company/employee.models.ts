export interface Employee {
  employeeId: string;
  companyId: string;
  employeeCode: string;
  firstName: string;
  middleName: string | null;
  lastName: string;
  preferredName: string | null;
  workEmail: string | null;
  hireDate: string;
  employmentStatus: EmployeeStatus;
  terminationDate: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export type EmployeeStatus = 'Preboarding' | 'Active' | 'OnLeave' | 'Inactive' | 'Terminated';

export interface CreateEmployeeRequest {
  employeeCode: string;
  firstName: string;
  middleName: string | null;
  lastName: string;
  preferredName: string | null;
  workEmail: string | null;
  hireDate: string;
}

export interface UpdateEmployeeRequest {
  firstName: string;
  middleName: string | null;
  lastName: string;
  preferredName: string | null;
  workEmail: string | null;
  hireDate: string;
  rowVersion: string;
}

export interface UpdateEmployeeStatusRequest {
  employmentStatus: EmployeeStatus;
  terminationDate: string | null;
  rowVersion: string;
}

export interface EmployeeUserMapping {
  employeeUserMappingId: string;
  companyId: string;
  employeeId: string;
  userId: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface AvailableTenantUser {
  userId: string;
  displayName: string;
  email: string;
}

export interface CreateEmployeeUserMappingRequest {
  employeeId: string;
  userId: string;
}

export interface DeactivateEmployeeUserMappingRequest {
  rowVersion: string;
}
