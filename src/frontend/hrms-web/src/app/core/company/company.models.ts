export interface CompanySummary {
  companyId: string;
  companyCode: string;
  legalName: string;
  displayName: string;
  isActive: boolean;
}

export interface Department {
  departmentId: string;
  companyId: string;
  departmentCode: string;
  departmentName: string;
  description: string | null;
  parentDepartmentId: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface CreateDepartmentRequest {
  departmentCode: string;
  departmentName: string;
  description: string | null;
  parentDepartmentId: string | null;
}

export interface UpdateDepartmentRequest {
  departmentName: string;
  description: string | null;
  parentDepartmentId: string | null;
  rowVersion: string;
}