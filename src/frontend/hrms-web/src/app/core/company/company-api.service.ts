import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CompanySummary, CreateDepartmentRequest, Department, UpdateDepartmentRequest } from './company.models';

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

  activateDepartment(companyId: string, departmentId: string, rowVersion: string): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/departments/${departmentId}/activate`, { rowVersion });
  }
}