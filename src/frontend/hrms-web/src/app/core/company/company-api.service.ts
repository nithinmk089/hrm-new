import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CompanySummary, CreateDepartmentRequest, CreateDesignationRequest, CreateOfficeRequest, Department, Designation, Office, UpdateDepartmentRequest, UpdateDesignationRequest, UpdateOfficeRequest } from './company.models';

@Injectable({ providedIn: 'root' })
export class CompanyApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getCompanies(): Observable<CompanySummary[]> {
    return this.http.get<CompanySummary[]>(`${this.apiBaseUrl}/companies`);
  }

  getDepartments(companyId: string): Observable<Department[]> {
    return this.http.get<Department[]>(`${this.apiBaseUrl}/companies/${companyId}/departments`);
  }

  createDepartment(companyId: string, request: CreateDepartmentRequest): Observable<Department> {
    return this.http.post<Department>(`${this.apiBaseUrl}/companies/${companyId}/departments`, request);
  }

  updateDepartment(companyId: string, departmentId: string, request: UpdateDepartmentRequest): Observable<Department> {
    return this.http.put<Department>(`${this.apiBaseUrl}/companies/${companyId}/departments/${departmentId}`, request);
  }

  deactivateDepartment(companyId: string, departmentId: string, rowVersion: string): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/departments/${departmentId}/deactivate`, { rowVersion });
  }

  getDesignations(companyId: string): Observable<Designation[]> {
    return this.http.get<Designation[]>(`${this.apiBaseUrl}/companies/${companyId}/designations`);
  }

  createDesignation(companyId: string, request: CreateDesignationRequest): Observable<Designation> {
    return this.http.post<Designation>(`${this.apiBaseUrl}/companies/${companyId}/designations`, request);
  }

  updateDesignation(companyId: string, designationId: string, request: UpdateDesignationRequest): Observable<Designation> {
    return this.http.put<Designation>(`${this.apiBaseUrl}/companies/${companyId}/designations/${designationId}`, request);
  }

  deactivateDesignation(companyId: string, designationId: string, rowVersion: string): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/designations/${designationId}/deactivate`, { rowVersion });
  }

  getOffices(companyId: string): Observable<Office[]> {
    return this.http.get<Office[]>(`${this.apiBaseUrl}/companies/${companyId}/offices`);
  }

  createOffice(companyId: string, request: CreateOfficeRequest): Observable<Office> {
    return this.http.post<Office>(`${this.apiBaseUrl}/companies/${companyId}/offices`, request);
  }

  updateOffice(companyId: string, officeId: string, request: UpdateOfficeRequest): Observable<Office> {
    return this.http.put<Office>(`${this.apiBaseUrl}/companies/${companyId}/offices/${officeId}`, request);
  }

  deactivateOffice(companyId: string, officeId: string, rowVersion: string): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/offices/${officeId}/deactivate`, { rowVersion });
  }

  activateDepartment(companyId: string, departmentId: string, rowVersion: string): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/departments/${departmentId}/activate`, { rowVersion });
  }
}