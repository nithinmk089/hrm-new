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

export interface Office {
  officeId: string;
  companyId: string;
  officeCode: string;
  officeName: string;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateOrProvince: string | null;
  postalCode: string | null;
  countryCode: string;
  timeZoneId: string | null;
  isHeadOffice: boolean;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface CreateOfficeRequest {
  officeCode: string;
  officeName: string;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateOrProvince: string | null;
  postalCode: string | null;
  countryCode: string;
  timeZoneId: string | null;
  isHeadOffice: boolean;
}

export interface UpdateOfficeRequest extends Omit<CreateOfficeRequest, 'officeCode'> {
  rowVersion: string;
}


export interface Designation {
  designationId: string;
  companyId: string;
  designationCode: string;
  designationName: string;
  description: string | null;
  jobLevel: number | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface CreateDesignationRequest {
  designationCode: string;
  designationName: string;
  description: string | null;
  jobLevel: number | null;
}

export interface UpdateDesignationRequest {
  designationName: string;
  description: string | null;
  jobLevel: number | null;
  rowVersion: string;
}
