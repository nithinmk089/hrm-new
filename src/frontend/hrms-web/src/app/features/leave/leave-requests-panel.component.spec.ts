import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { LeaveApiService } from '../../core/company/leave-api.service';
import { EmployeeLeaveRequestItem } from '../../core/company/leave.models';
import { LeaveRequestsPanelComponent } from './leave-requests-panel.component';

const request: EmployeeLeaveRequestItem = {
  leaveRequestId: 'request-1', companyId: 'company-1', employeeId: 'employee-1',
  leaveTypeId: 'type-1', leaveTypeCode: 'ANNUAL', leaveTypeName: 'Annual Leave',
  fromDate: '2099-12-24', toDate: '2099-12-28', isHalfDay: false, halfDayPeriod: null,
  daysRequested: 2, reason: 'Personal work', status: 'Pending',
  submittedAtUtc: '2026-11-11T10:00:00Z', reviewedByEmployeeId: null, reviewedAtUtc: null,
  reviewComments: null, cancelledAtUtc: null, rowVersion: 'AAAAAAAAB9E='
};

describe('LeaveRequestsPanelComponent', () => {
  const setup = async () => {
    const leaveApi = {
      getMyLeaveRequests: vi.fn().mockReturnValue(of([request])),
      getMyLeaveRequestHistory: vi.fn().mockReturnValue(of([
        { action: 'Submitted', actorEmployeeId: 'employee-1', comments: null, actionAtUtc: '2026-11-11T10:00:00Z' }
      ])),
      cancelMyLeaveRequest: vi.fn().mockReturnValue(of({
        leaveRequestId: 'request-1', status: 'Cancelled', cancelledAtUtc: '2026-11-11T10:00:00Z', rowVersion: 'BBBBBBBBB9E='
      }))
    };
    await TestBed.configureTestingModule({
      imports: [LeaveRequestsPanelComponent],
      providers: [{ provide: LeaveApiService, useValue: leaveApi }]
    }).compileComponents();
    const fixture = TestBed.createComponent(LeaveRequestsPanelComponent);
    fixture.componentRef.setInput('companyId', 'company-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, leaveApi };
  };

  it('loads only the authenticated employee request list for the selected company', async () => {
    const { fixture, leaveApi } = await setup();
    expect(leaveApi.getMyLeaveRequests).toHaveBeenCalledWith('company-1');
    expect(fixture.nativeElement.textContent).toContain('Annual Leave');
    expect(fixture.nativeElement.textContent).toContain('Pending');
  });

  it('shows an empty state when the employee has no requests', async () => {
    const { fixture, component } = await setup();
    component.requests.set([]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No leave requests yet');
  });

  it('offers cancellation only for pending or future approved requests', async () => {
    const { component } = await setup();
    expect(component.canCancel(request)).toBe(true);
    expect(component.canCancel({ ...request, status: 'Approved', fromDate: '2099-12-24' })).toBe(true);
    expect(component.canCancel({ ...request, status: 'Approved', fromDate: '2000-01-01' })).toBe(false);
    expect(component.canCancel({ ...request, status: 'Rejected' })).toBe(false);
    expect(component.canCancel({ ...request, status: 'Cancelled' })).toBe(false);
  });

  it('loads request history and displays recorded actions', async () => {
    const { fixture, component, leaveApi } = await setup();
    component.viewHistory(request);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(leaveApi.getMyLeaveRequestHistory).toHaveBeenCalledWith('company-1', 'request-1');
    expect(fixture.nativeElement.textContent).toContain('Request history');
    expect(fixture.nativeElement.textContent).toContain('Submitted');
  });

  it('cancels with the unchanged RowVersion and optional comments, then refreshes', async () => {
    const { component, leaveApi } = await setup();
    component.beginCancel(request);
    component.cancelComments.set(' Plans changed ');
    component.confirmCancel();
    expect(leaveApi.cancelMyLeaveRequest).toHaveBeenCalledWith('company-1', 'request-1', {
      rowVersion: 'AAAAAAAAB9E=', comments: 'Plans changed'
    });
    expect(component.cancelRequest()).toBeNull();
    expect(component.success()).toContain('cancelled successfully');
    expect(leaveApi.getMyLeaveRequests).toHaveBeenCalledTimes(2);
  });

  it('keeps the confirmation open and reports stale RowVersion conflicts', async () => {
    const { component, leaveApi } = await setup();
    leaveApi.cancelMyLeaveRequest.mockReturnValue(throwError(() => ({
      status: 409, error: { detail: 'The leave request has changed. Refresh it and try again.' }
    })));
    component.beginCancel(request);
    component.confirmCancel();
    expect(component.cancelRequest()).toEqual(request);
    expect(component.cancelError()).toContain('has changed');
  });

  it('shows an empty history and handles history authorization errors', async () => {
    const { component, leaveApi } = await setup();
    leaveApi.getMyLeaveRequestHistory.mockReturnValue(throwError(() => ({ status: 403 })));
    component.viewHistory(request);
    expect(component.historyError()).toContain('permission');
  });
});
