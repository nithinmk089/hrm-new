import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api.config';
import { EmployeeLeaveBalance, LeaveRequestSubmissionResult, LeaveType, MyEmployeeMapping, SubmitLeaveRequest } from './leave.models';

@Injectable({ providedIn: 'root' })
export class LeaveApiService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getMyEmployeeMapping(companyId: string): Observable<MyEmployeeMapping> {
    return this.http.get<MyEmployeeMapping>(`${this.apiBaseUrl}/companies/${companyId}/employee-user-mappings/mine`);
  }

  getLeaveTypes(companyId: string): Observable<LeaveType[]> {
    return this.http.get<LeaveType[]>(`${this.apiBaseUrl}/companies/${companyId}/leave-types`);
  }

  getMyBalances(companyId: string, employeeId: string, year: number): Observable<EmployeeLeaveBalance[]> {
    const params = new HttpParams().set('year', String(year));
    return this.http.get<EmployeeLeaveBalance[]>(
      `${this.apiBaseUrl}/companies/${companyId}/employees/${employeeId}/leave-balances`, { params });
  }

  submitLeaveRequest(companyId: string, request: SubmitLeaveRequest): Observable<LeaveRequestSubmissionResult> {
    return this.http.post<LeaveRequestSubmissionResult>(
      `${this.apiBaseUrl}/companies/${companyId}/leave-requests`, request);
  }
}
