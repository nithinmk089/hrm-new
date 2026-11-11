import { Component, Input, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { LeaveApiService } from '../../core/company/leave-api.service';
import { EmployeeLeaveRequestItem, LeaveRequestHistoryItem } from '../../core/company/leave.models';

@Component({
  selector: 'hrms-leave-requests-panel',
  standalone: true,
  template: `
    <section class="requests-section">
      <div class="section-heading">
        <div><h2>My leave requests</h2><p>Review request status, action history and cancellation options.</p></div>
        <button class="btn btn-light btn-sm" type="button" (click)="load()" [disabled]="loading()"><i class="bi bi-arrow-clockwise me-1"></i>Refresh</button>
      </div>
      @if(error()){<div class="alert alert-danger py-2" role="alert">{{error()}}</div>}
      @if(success()){<div class="alert alert-success py-2" role="status">{{success()}}</div>}
      @if(loading()){<div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading your leave requests…</div>}
      @else if(requests().length===0){<div class="empty-state card"><i class="bi bi-calendar2-x"></i><h3>No leave requests yet</h3><p>Leave requests you submit will appear here.</p></div>}
      @else {
        <div class="table-responsive card requests-table">
          <table class="table align-middle mb-0">
            <thead><tr><th>Leave type</th><th>Dates</th><th>Days</th><th>Submitted</th><th>Status</th><th class="text-end">Actions</th></tr></thead>
            <tbody>
              @for(request of requests();track request.leaveRequestId){
                <tr>
                  <td><strong>{{request.leaveTypeName}}</strong><small>{{request.leaveTypeCode}}</small></td>
                  <td>{{formatDate(request.fromDate)}}<span class="date-to">to {{formatDate(request.toDate)}}</span>@if(request.isHalfDay){<small>{{request.halfDayPeriod}} half-day</small>}</td>
                  <td>{{request.daysRequested}}</td>
                  <td>{{formatDateTime(request.submittedAtUtc)}}</td>
                  <td><span class="status" [attr.data-status]="request.status.toLowerCase()">{{request.status}}</span></td>
                  <td><div class="row-actions"><button class="btn btn-sm btn-outline-secondary" type="button" (click)="viewHistory(request)"><i class="bi bi-clock-history me-1"></i>History</button>@if(canCancel(request)){<button class="btn btn-sm btn-outline-danger" type="button" (click)="beginCancel(request)">Cancel</button>}</div></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="list-note">Showing the latest {{requests().length}} request(s).</p>
      }
    </section>

    @if(historyRequest()){
      <div class="modal-backdrop-custom" (click)="closeHistory()">
        <section class="dialog card" role="dialog" aria-modal="true" aria-labelledby="historyTitle" (click)="$event.stopPropagation()">
          <div class="dialog-heading"><div><div class="eyebrow">LEAVE REQUEST</div><h3 id="historyTitle">Request history</h3><p>{{historyRequest()?.leaveTypeName}} · {{formatDate(historyRequest()!.fromDate)}} to {{formatDate(historyRequest()!.toDate)}}</p></div><button class="btn-close" type="button" aria-label="Close" (click)="closeHistory()"></button></div>
          @if(historyError()){<div class="alert alert-danger py-2">{{historyError()}}</div>}
          @if(historyLoading()){<div class="loading-state">Loading request history…</div>}
          @else if(history().length===0){<div class="empty-history">No workflow actions have been recorded for this request yet.</div>}
          @else {
            <ol class="history-list">
              @for(item of history();track $index){
                <li><span class="history-dot"></span><div><strong>{{item.action}}</strong><time>{{formatDateTime(item.actionAtUtc)}}</time>@if(item.comments){<p>{{item.comments}}</p>}</div></li>
              }
            </ol>
          }
          <div class="actions"><button class="btn btn-light" type="button" (click)="closeHistory()">Close</button></div>
        </section>
      </div>
    }

    @if(cancelRequest()){
      <div class="modal-backdrop-custom" (click)="closeCancel()">
        <section class="dialog card" role="dialog" aria-modal="true" aria-labelledby="cancelTitle" (click)="$event.stopPropagation()">
          <div class="dialog-heading"><div><div class="eyebrow">CANCEL LEAVE</div><h3 id="cancelTitle">Confirm cancellation</h3></div><button class="btn-close" type="button" aria-label="Close" (click)="closeCancel()" [disabled]="cancelling()"></button></div>
          <p class="confirm-copy">Cancel your <strong>{{cancelRequest()?.leaveTypeName}}</strong> request from <strong>{{formatDate(cancelRequest()!.fromDate)}}</strong> to <strong>{{formatDate(cancelRequest()!.toDate)}}</strong>?</p>
          <p class="confirm-note">This action will release reserved days for a pending request or restore used days for an eligible approved request. The server will validate the request status and RowVersion.</p>
          <label class="form-label" for="cancelComments">Comments (optional)</label>
          <textarea id="cancelComments" class="form-control" rows="3" maxlength="1000" [value]="cancelComments()" (input)="onCommentsChange($event)" placeholder="Add a reason for cancellation"></textarea>
          @if(cancelError()){<div class="alert alert-danger py-2 mt-3">{{cancelError()}}</div>}
          <div class="actions"><button class="btn btn-light" type="button" (click)="closeCancel()" [disabled]="cancelling()">Keep request</button><button class="btn btn-danger" type="button" (click)="confirmCancel()" [disabled]="cancelling()"><span [class.spinner-border]="cancelling()" [class.spinner-border-sm]="cancelling()" class="me-2"></span>{{cancelling()?'Cancelling…':'Confirm cancellation'}}</button></div>
        </section>
      </div>
    }
  `,
  styles: [`
    :host{display:block}.requests-section{margin-top:1.5rem}.section-heading{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin:1.4rem 0 .9rem}.section-heading h2{margin:0;color:#344054;font-size:.95rem;font-weight:650}.section-heading p{margin:.25rem 0 0;color:#98a2b3;font-size:.7rem}.requests-table{overflow:hidden}.requests-table table{font-size:.72rem}.requests-table th{padding:.75rem .8rem;background:#f9fafb;color:#667085;font-size:.64rem;font-weight:650;white-space:nowrap}.requests-table td{padding:.75rem .8rem;color:#475467;white-space:nowrap}.requests-table td strong{display:block;color:#344054;font-weight:650}.requests-table td small{display:block;margin-top:.15rem;color:#98a2b3;font-size:.63rem}.date-to{display:block;color:#667085}.status{display:inline-block;padding:.25rem .5rem;border-radius:1rem;background:#f2f4f7;color:#475467;font-size:.64rem;font-weight:650}.status[data-status="pending"]{background:#fffaeb;color:#93370d}.status[data-status="approved"]{background:#ecfdf3;color:#027a48}.status[data-status="rejected"]{background:#fef3f2;color:#b42318}.status[data-status="cancelled"]{background:#f2f4f7;color:#667085}.row-actions{display:flex;justify-content:flex-end;gap:.35rem}.row-actions .btn{font-size:.65rem}.list-note{margin:.45rem 0 0;color:#98a2b3;font-size:.65rem;text-align:right}.empty-state{display:flex;align-items:center;flex-direction:column;text-align:center;padding:2rem 1rem;color:#667085}.empty-state>i{font-size:1.6rem;color:#7890b2}.empty-state h3{margin:.65rem 0 .25rem;color:#344054;font-size:.9rem}.empty-state p{margin:0;font-size:.72rem}.modal-backdrop-custom{position:fixed;inset:0;z-index:1250;display:grid;place-items:center;padding:1rem;background:rgb(16 24 40 / 48%)}.dialog{width:min(100%,34rem);max-height:88vh;overflow:auto;padding:1.25rem}.dialog-heading{display:flex;justify-content:space-between;gap:1rem;margin-bottom:1rem}.eyebrow{font-size:.62rem;font-weight:700;letter-spacing:.1em;color:#58739a}.dialog-heading h3{margin:.25rem 0;color:#1d2939;font-size:1.05rem;font-weight:650}.dialog-heading p{margin:0;color:#667085;font-size:.68rem}.loading-state{padding:1rem;text-align:center;color:#667085;font-size:.75rem}.empty-history{padding:1rem;border-radius:.4rem;background:#f8fafc;color:#667085;font-size:.72rem}.history-list{list-style:none;padding:0;margin:.3rem 0}.history-list li{position:relative;display:flex;gap:.75rem;padding:0 0 1rem .15rem}.history-list li:not(:last-child):after{position:absolute;top:.65rem;bottom:0;left:.42rem;width:1px;background:#eaecf0;content:''}.history-dot{position:relative;z-index:1;flex:0 0 .6rem;height:.6rem;margin-top:.2rem;border-radius:50%;background:#52739e}.history-list strong{display:block;color:#344054;font-size:.75rem}.history-list time{display:block;margin-top:.15rem;color:#98a2b3;font-size:.65rem}.history-list p{margin:.3rem 0 0;color:#667085;font-size:.7rem}.confirm-copy{color:#344054;font-size:.78rem}.confirm-note{padding:.65rem;border-radius:.4rem;background:#f8fafc;color:#667085;font-size:.68rem;line-height:1.5}.form-label{color:#344054;font-size:.72rem;font-weight:600}.actions{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1.2rem;padding-top:1rem;border-top:1px solid #eaecf0}.actions .btn{font-size:.72rem}@media(max-width:700px){.requests-table{overflow-x:auto}.row-actions{min-width:8rem}}`
  ]
})
export class LeaveRequestsPanelComponent implements OnChanges {
  @Input({ required: true }) companyId = '';
  private readonly leaveApi = inject(LeaveApiService);

  readonly requests = signal<EmployeeLeaveRequestItem[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly historyRequest = signal<EmployeeLeaveRequestItem | null>(null);
  readonly history = signal<LeaveRequestHistoryItem[]>([]);
  readonly historyLoading = signal(false);
  readonly historyError = signal('');
  readonly cancelRequest = signal<EmployeeLeaveRequestItem | null>(null);
  readonly cancelComments = signal('');
  readonly cancelling = signal(false);
  readonly cancelError = signal('');

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['companyId'] && this.companyId) this.load();
  }

  load(): void {
    if (!this.companyId) return;
    this.loading.set(true);
    this.error.set('');
    this.leaveApi.getMyLeaveRequests(this.companyId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => this.requests.set(items),
      error: error => this.error.set(this.message(error, 'Unable to load your leave requests.'))
    });
  }

  canCancel(request: EmployeeLeaveRequestItem): boolean {
    if (request.status === 'Pending') return true;
    if (request.status !== 'Approved') return false;
    return request.fromDate.slice(0, 10) > this.localToday();
  }

  viewHistory(request: EmployeeLeaveRequestItem): void {
    this.historyRequest.set(request);
    this.history.set([]);
    this.historyError.set('');
    this.historyLoading.set(true);
    this.leaveApi.getMyLeaveRequestHistory(this.companyId, request.leaveRequestId)
      .pipe(finalize(() => this.historyLoading.set(false)))
      .subscribe({
        next: items => this.history.set(items),
        error: error => this.historyError.set(this.message(error, 'Unable to load request history.'))
      });
  }

  closeHistory(): void { this.historyRequest.set(null); }

  beginCancel(request: EmployeeLeaveRequestItem): void {
    this.cancelRequest.set(request);
    this.cancelComments.set('');
    this.cancelError.set('');
  }

  closeCancel(): void {
    if (this.cancelling()) return;
    this.cancelRequest.set(null);
  }

  onCommentsChange(event: Event): void {
    this.cancelComments.set((event.target as HTMLTextAreaElement).value);
  }

  confirmCancel(): void {
    const request = this.cancelRequest();
    if (!request || this.cancelling()) return;
    this.cancelling.set(true);
    this.cancelError.set('');
    this.leaveApi.cancelMyLeaveRequest(this.companyId, request.leaveRequestId, {
      rowVersion: request.rowVersion,
      comments: this.cancelComments().trim() || null
    }).pipe(finalize(() => this.cancelling.set(false))).subscribe({
      next: result => {
        this.cancelRequest.set(null);
        this.success.set(`Leave request cancelled successfully. Status: ${result.status}.`);
        this.load();
      },
      error: error => this.cancelError.set(this.message(error, 'Unable to cancel this leave request. Refresh and try again.'))
    });
  }

  formatDate(value: string): string {
    const parts = value.slice(0, 10).split('-');
    return parts.length === 3
      ? new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2])).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
      : value;
  }

  formatDateTime(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString(undefined, { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  private localToday(): string {
    const date = new Date();
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }

  private message(error: any, fallback: string): string {
    if (error?.status === 400) return error?.error?.detail || error?.error?.title || 'The cancellation request is invalid.';
    if (error?.status === 403) return 'You do not have permission to access this leave function.';
    if (error?.status === 404) return 'The leave request could not be found for your account.';
    if (error?.status === 409) return error?.error?.detail || error?.error?.title || 'The leave request has changed. Refresh and try again.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}
