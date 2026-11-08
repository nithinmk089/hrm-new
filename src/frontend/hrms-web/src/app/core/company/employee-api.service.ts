import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { CreateEmployeeRequest, Employee, EmployeeStatus, UpdateEmployeeRequest, UpdateEmployeeStatusRequest } from './employee.models';

@Injectable({ providedIn: 'root' })
export class EmployeeApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getEmployees(companyId: string, search?: string, status?: EmployeeStatus | ''): Observable<Employee[]> {
    let params = new HttpParams();
    if (search?.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<Employee[]>(`${this.apiBaseUrl}/companies/${companyId}/employees`, { params });
  }

  getEmployee(companyId: string, employeeId: string): Observable<Employee> {
    return this.http.get<Employee>(`${this.apiBaseUrl}/companies/${companyId}/employees/${employeeId}`);
  }

  createEmployee(companyId: string, request: CreateEmployeeRequest): Observable<Employee> {
    return this.http.post<Employee>(`${this.apiBaseUrl}/companies/${companyId}/employees`, request);
  }

  updateEmployee(companyId: string, employeeId: string, request: UpdateEmployeeRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.apiBaseUrl}/companies/${companyId}/employees/${employeeId}`, request);
  }

  updateEmployeeStatus(companyId: string, employeeId: string, request: UpdateEmployeeStatusRequest): Observable<void> {
    return this.http.patch<void>(`${this.apiBaseUrl}/companies/${companyId}/employees/${employeeId}/status`, request);
  }
}
