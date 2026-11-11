import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary } from '../../core/company/company.models';
import { EmployeeLeaveBalance, LeaveRequestSubmissionResult, LeaveType, SubmitLeaveRequest } from '../../core/company/leave.models';
import { LeaveApiService } from '../../core/company/leave-api.service';

@Component({
  selector: 'hrms-leave-dashboard-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-heading">
      <div><div class="eyebrow">EMPLOYEE SELF-SERVICE</div><h1>Leave management</h1><p>Review your annual leave balance and submit a leave request.</p></div>
      <button class="btn btn-primary" type="button" [disabled]="!selectedCompanyId() || loading() || !employeeId() || balances().length===0" (click)="openApply()"><i class="bi bi-plus-lg me-2"></i>Apply for leave</button>
    </div>
    <section class="company-context card mb-3">
      <div class="context-icon"><i class="bi bi-building"></i></div>
      <div class="context-copy"><label for="companySelect">Company</label><select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length===0"><option value="">Select a company</option>@for(company of companies();track company.companyId){<option [value]="company.companyId">{{company.displayName}} ({{company.companyCode}})</option>}</select></div>
      <span class="context-count">Leave year {{selectedYear()}}</span>
    </section>
    @if(error()){<div class="alert alert-danger" role="alert">{{error()}}</div>}
    @if(companiesLoading()){<div class="loading-state">Loading companies…</div>}
    @else if(companies().length===0){<div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account has no active company available.</p></div>}
    @else if(!selectedCompanyId()){<div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose your company to view your leave balance.</p></div>}
    @else {
      <div class="section-heading"><div><h2>Annual leave balance</h2><p>Available, used and pending-reservation days for {{selectedYear()}}.</p></div><button class="btn btn-light btn-sm" type="button" (click)="loadDashboard()" [disabled]="loading()"><i class="bi bi-arrow-clockwise me-1"></i>Refresh</button></div>
      @if(loading()){<div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading your leave information…</div>}
      @else if(!employeeId()){<div class="empty-state card"><i class="bi bi-person-exclamation"></i><h2>Employee account mapping required</h2><p>Your login is not linked to an active employee record in this company. Contact your HR administrator to configure the explicit employee-user mapping. Your email address is not used to guess this association.</p></div>}
      @else if(balances().length===0){<div class="empty-state card"><i class="bi bi-calendar2-check"></i><h2>No leave balances provisioned</h2><p>There are no leave balance records for {{selectedYear()}}. Please contact HR to provision your annual leave balances before applying.</p></div>}
      @else {
        <div class="balance-grid">
          @for(balance of balances();track balance.employeeLeaveBalanceId){
            <article class="balance-card card">
              <div class="balance-top"><span class="balance-icon"><i class="bi bi-calendar2-week"></i></span><span class="leave-code">{{balance.leaveTypeCode}}</span></div>
              <h3>{{balance.leaveTypeName}}</h3>
              <div class="available"><strong>{{balance.availableDays}}</strong><span>days available</span></div>
              <div class="balance-track"><span [style.width.%]="balancePercent(balance)"></span></div>
              <div class="balance-breakdown"><span>Used <strong>{{balance.usedDays}}</strong></span><span>Reserved <strong>{{balance.reservedDays}}</strong></span><span>Total <strong>{{totalDays(balance)}}</strong></span></div>
            </article>
          }
        </div>
      }
      <section class="info-panel card mt-3"><i class="bi bi-info-circle"></i><div><strong>How leave balances work</strong><p>Submitting a request reserves the calculated working days. Approval moves reserved days to used days; rejection or eligible cancellation releases the reservation. Company working weeks, office schedules and holidays are calculated by the server.</p></div></section>
    }
    @if(submissionResult()){<div class="modal-backdrop-custom" (click)="submissionResult.set(null)"><section class="dialog card" role="dialog" aria-modal="true" aria-labelledby="submissionTitle" (click)="$event.stopPropagation()"><div class="success-icon"><i class="bi bi-check-lg"></i></div><h2 id="submissionTitle">Leave request submitted</h2><p>Your request for <strong>{{submissionResult()?.request.daysRequested}} day(s)</strong> has been recorded with status <strong>{{submissionResult()?.request.status}}</strong>.</p><div class="result-summary">@for(item of submissionResult()?.yearAllocations??[];track item.leaveYear){<div><span>{{item.leaveYear}} allocation</span><strong>{{item.days}} days</strong></div>}</div>@if((submissionResult()?.excludedDates??[]).length>0){<div class="excluded-dates"><strong>Excluded dates</strong><span>{{formatDates(submissionResult()!.excludedDates)}}</span><small>These dates were excluded from the chargeable leave calculation by the server.</small></div>}<div class="actions"><button class="btn btn-primary" type="button" (click)="submissionResult.set(null)">Done</button></div></section></div>}
    @if(editorOpen()){<div class="modal-backdrop-custom" (click)="closeApply()"><section class="editor card" role="dialog" aria-modal="true" aria-labelledby="applyTitle" (click)="$event.stopPropagation()"><div class="dialog-heading"><div><div class="eyebrow">NEW LEAVE REQUEST</div><h2 id="applyTitle">Apply for leave</h2></div><button class="btn-close" type="button" aria-label="Close" (click)="closeApply()" [disabled]="saving()"></button></div>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <div class="mb-3"><label class="form-label" for="leaveType">Leave type <span class="required">*</span></label><select id="leaveType" class="form-select" formControlName="leaveTypeId"><option value="">Select leave type</option>@for(type of leaveTypes();track type.leaveTypeId){<option [value]="type.leaveTypeId">{{type.leaveTypeName}} ({{type.leaveTypeCode}})</option>}</select>@if(showError('leaveTypeId')){<div class="field-error">Select a leave type.</div>}</div>
        <div class="row g-3"><div class="col-md-6 mb-3"><label class="form-label" for="fromDate">From date <span class="required">*</span></label><input id="fromDate" class="form-control" type="date" formControlName="fromDate">@if(showError('fromDate')){<div class="field-error">Select a start date.</div>}</div><div class="col-md-6 mb-3"><label class="form-label" for="toDate">To date <span class="required">*</span></label><input id="toDate" class="form-control" type="date" formControlName="toDate">@if(showError('toDate')){<div class="field-error">Select an end date.</div>}</div></div>
        @if(form.controls.toDate.value && form.controls.fromDate.value && form.controls.toDate.value<form.controls.fromDate.value){<div class="field-error mb-3">End date cannot be earlier than start date.</div>}
        <div class="form-check mb-3"><input id="isHalfDay" class="form-check-input" type="checkbox" formControlName="isHalfDay"><label class="form-check-label" for="isHalfDay">Half-day leave</label></div>
        @if(form.controls.isHalfDay.value){<div class="mb-3"><label class="form-label" for="halfDayPeriod">Half-day period <span class="required">*</span></label><select id="halfDayPeriod" class="form-select" formControlName="halfDayPeriod"><option value="">Select period</option><option value="AM">Morning (AM)</option><option value="PM">Afternoon (PM)</option></select>@if(showError('halfDayPeriod')){<div class="field-error">Select AM or PM.</div>}<div class="field-hint">Half-day leave must cover one date and be enabled for the selected leave type.</div></div>}
        @if(selectedLeaveType() && !selectedLeaveType()!.allowHalfDay){<div class="field-hint mb-3">The selected leave type does not allow half-day requests.</div>}
        <div class="mb-3"><label class="form-label" for="reason">Reason <span class="required">*</span></label><textarea id="reason" class="form-control" rows="3" maxlength="1000" formControlName="reason" placeholder="Provide a brief reason for your leave"></textarea><div class="field-hint text-end">{{form.controls.reason.value.length}} / 1000</div>@if(showError('reason')){<div class="field-error">A reason is required (maximum 1000 characters).</div>}</div>
        @if(editorError()){<div class="alert alert-danger py-2">{{editorError()}}</div>}
        <div class="actions"><button class="btn btn-light" type="button" (click)="closeApply()" [disabled]="saving()">Cancel</button><button class="btn btn-primary" type="submit" [disabled]="saving() || form.invalid || dateRangeInvalid() || halfDayInvalid()"><span [class.spinner-border]="saving()" [class.spinner-border-sm]="saving()" class="me-2"></span>{{saving()?'Submitting…':'Submit request'}}</button></div>
      </form>
    </section></div>}
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}.company-context{display:flex!important;flex-direction:row!important;align-items:center!important;gap:.85rem;padding:.9rem 1rem;min-height:5.25rem}.context-icon{display:grid;place-items:center;width:2.6rem;height:2.6rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.1rem}.context-copy{flex:1;max-width:28rem;min-width:14rem}.context-copy label{display:block;margin-bottom:.25rem;color:#667085;font-size:.67rem;font-weight:600}.context-count{margin-left:auto;color:#667085;font-size:.72rem;white-space:nowrap}.section-heading{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin:1.4rem 0 .9rem}.section-heading h2{margin:0;color:#344054;font-size:.95rem;font-weight:650}.section-heading p{margin:.25rem 0 0;color:#98a2b3;font-size:.7rem}.balance-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(15rem,1fr));gap:.85rem}.balance-card{padding:1rem 1.05rem}.balance-top{display:flex;align-items:center;justify-content:space-between}.balance-icon{display:grid;place-items:center;width:2.25rem;height:2.25rem;border-radius:.5rem;background:#eef3fa;color:#3d5e88}.leave-code{padding:.25rem .45rem;border-radius:.3rem;background:#f2f4f7;color:#667085;font-size:.65rem;font-weight:650}.balance-card h3{margin:.85rem 0 .5rem;color:#344054;font-size:.82rem;font-weight:650}.available{display:flex;align-items:baseline;gap:.45rem}.available strong{color:#1d2939;font-size:1.65rem;font-weight:700;font-variant-numeric:tabular-nums}.available span{color:#667085;font-size:.7rem}.balance-track{height:.35rem;margin:.65rem 0 .75rem;border-radius:1rem;background:#eef1f5;overflow:hidden}.balance-track span{display:block;height:100%;border-radius:inherit;background:#52739e}.balance-breakdown{display:flex;justify-content:space-between;gap:.35rem;color:#98a2b3;font-size:.64rem}.balance-breakdown strong{color:#475467}.info-panel{display:flex!important;flex-direction:row!important;gap:.7rem;padding:.9rem 1rem;color:#52739e}.info-panel>i{font-size:1rem}.info-panel strong{color:#344054;font-size:.72rem}.info-panel p{margin:.2rem 0 0;color:#667085;font-size:.68rem;line-height:1.55}.empty-state{display:flex;align-items:center;flex-direction:column;text-align:center;padding:2.5rem 1.5rem;color:#667085}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:35rem;margin:0;font-size:.78rem;line-height:1.6}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}.modal-backdrop-custom{position:fixed;inset:0;z-index:1200;display:grid;place-items:center;padding:1rem;background:rgb(16 24 40 / 48%)}.editor,.dialog{width:min(100%,38rem);max-height:90vh;overflow:auto;padding:1.35rem}.dialog{width:min(100%,30rem)}.dialog-heading{display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:1.2rem}.dialog-heading h2{margin:.3rem 0 0;color:#1d2939;font-size:1.15rem;font-weight:650}.form-label{color:#344054;font-size:.75rem;font-weight:600}.form-check-label{color:#344054;font-size:.75rem;font-weight:600}.required{color:#d92d20}.field-error{margin-top:.3rem;color:#d92d20;font-size:.68rem}.field-hint{margin-top:.3rem;color:#98a2b3;font-size:.66rem}.actions{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1.2rem;padding-top:1rem;border-top:1px solid #eaecf0}.success-icon{display:grid;place-items:center;width:2.7rem;height:2.7rem;margin-bottom:.9rem;border-radius:50%;background:#ecfdf3;color:#027a48;font-size:1.2rem}.dialog h2{margin:.1rem 0 .55rem;color:#1d2939;font-size:1.05rem}.dialog p{color:#667085;font-size:.78rem;line-height:1.6}.result-summary{display:grid;gap:.45rem;margin:.8rem 0}.result-summary div{display:flex;justify-content:space-between;padding:.55rem .7rem;border:1px solid #eaecf0;border-radius:.4rem;font-size:.72rem}.result-summary span{color:#667085}.result-summary strong{color:#344054}.excluded-dates{display:grid;gap:.25rem;padding:.7rem;border-radius:.4rem;background:#f8fafc;font-size:.7rem}.excluded-dates strong{color:#344054}.excluded-dates span{color:#475467}.excluded-dates small{color:#98a2b3}@media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.context-count{display:none}.editor,.dialog{padding:1rem}.section-heading{align-items:flex-start}}`
  ]
})
export class LeaveDashboardPageComponent {
  private readonly companyApi=inject(CompanyApiService);
  private readonly leaveApi=inject(LeaveApiService);
  private readonly fb=inject(FormBuilder);
  readonly companies=signal<CompanySummary[]>([]);
  readonly selectedCompanyId=signal('');
  readonly employeeId=signal('');
  readonly leaveTypes=signal<LeaveType[]>([]);
  readonly balances=signal<EmployeeLeaveBalance[]>([]);
  readonly selectedYear=signal(new Date().getFullYear());
  readonly companiesLoading=signal(true);
  readonly loading=signal(false);
  readonly saving=signal(false);
  readonly editorOpen=signal(false);
  readonly error=signal('');
  readonly editorError=signal('');
  readonly submissionResult=signal<LeaveRequestSubmissionResult|null>(null);
  readonly selectedLeaveType=computed(()=>this.leaveTypes().find(x=>x.leaveTypeId===this.form.controls.leaveTypeId.value)??null);
  readonly form=this.fb.nonNullable.group({
    leaveTypeId:['',[Validators.required]],
    fromDate:['',[Validators.required]],
    toDate:['',[Validators.required]],
    isHalfDay:[false],
    halfDayPeriod:['' as ''|'AM'|'PM'],
    reason:['',[Validators.required,Validators.maxLength(1000)]]
  });
  constructor(){
    this.companyApi.getCompanies().pipe(finalize(()=>this.companiesLoading.set(false))).subscribe({
      next:items=>{const active=items.filter(x=>x.isActive);this.companies.set(active);if(active.length===1){this.selectedCompanyId.set(active[0].companyId);this.loadDashboard();}},
      error:e=>this.error.set(this.message(e,'Unable to load companies.'))
    });
    this.form.controls.isHalfDay.valueChanges.subscribe(value=>{if(!value)this.form.controls.halfDayPeriod.setValue('');});
  }
  onCompanyChange(event:Event):void{const id=(event.target as HTMLSelectElement).value;this.selectedCompanyId.set(id);this.employeeId.set('');this.balances.set([]);this.leaveTypes.set([]);this.error.set('');if(id)this.loadDashboard();}
  loadDashboard():void{
    const companyId=this.selectedCompanyId();if(!companyId)return;
    this.loading.set(true);this.error.set('');
    forkJoin({mapping:this.leaveApi.getMyEmployeeMapping(companyId),types:this.leaveApi.getLeaveTypes(companyId)})
      .pipe(finalize(()=>this.loading.set(false)))
      .subscribe({
        next:result=>{
          this.employeeId.set(result.mapping.employeeId);
          this.leaveTypes.set(result.types.filter(x=>x.isActive));
          this.loadBalances();
        },
        error:e=>{
          if(e?.status===404){this.employeeId.set('');this.leaveTypes.set([]);this.balances.set([]);}
          else this.error.set(this.message(e,'Unable to load your leave information.'));
        }
      });
  }
  private loadBalances():void{
    const companyId=this.selectedCompanyId(),employeeId=this.employeeId();if(!companyId||!employeeId)return;
    this.leaveApi.getMyBalances(companyId,employeeId,this.selectedYear()).subscribe({
      next:items=>this.balances.set(items),
      error:e=>this.error.set(this.message(e,'Unable to load your leave balances.'))
    });
  }
  openApply():void{this.editorError.set('');this.form.reset({leaveTypeId:'',fromDate:'',toDate:'',isHalfDay:false,halfDayPeriod:'',reason:''});this.editorOpen.set(true);}
  closeApply():void{if(this.saving())return;this.editorOpen.set(false);}
  showError(control:'leaveTypeId'|'fromDate'|'toDate'|'halfDayPeriod'|'reason'):boolean{const c=this.form.controls[control];return c.invalid&&(c.touched||c.dirty);}
  dateRangeInvalid():boolean{const v=this.form.getRawValue();return !!v.fromDate&&!!v.toDate&&v.toDate<v.fromDate;}
  halfDayInvalid():boolean{const v=this.form.getRawValue();return v.isHalfDay&&(!v.halfDayPeriod||v.fromDate!==v.toDate||!this.selectedLeaveType()?.allowHalfDay);}
  submit():void{
    if(this.form.invalid||this.dateRangeInvalid()||this.halfDayInvalid()||this.saving()){this.form.markAllAsTouched();return;}
    const v=this.form.getRawValue();
    const request:SubmitLeaveRequest={leaveTypeId:v.leaveTypeId,fromDate:v.fromDate,toDate:v.toDate,isHalfDay:v.isHalfDay,halfDayPeriod:v.isHalfDay?v.halfDayPeriod:null,reason:v.reason.trim()};
    if(!request.reason){this.form.controls.reason.setErrors({required:true});return;}
    this.saving.set(true);this.editorError.set('');
    this.leaveApi.submitLeaveRequest(this.selectedCompanyId(),request).pipe(finalize(()=>this.saving.set(false))).subscribe({
      next:result=>{this.editorOpen.set(false);this.submissionResult.set(result);this.loadBalances();},
      error:e=>this.editorError.set(this.message(e,'Unable to submit the leave request.'))
    });
  }
  totalDays(balance:EmployeeLeaveBalance):number{return balance.openingDays+balance.accruedDays+balance.carriedForwardDays+balance.adjustmentDays;}
  balancePercent(balance:EmployeeLeaveBalance):number{const total=this.totalDays(balance);return total>0?Math.max(0,Math.min(100,(balance.availableDays/total)*100)):0;}
  formatDates(dates:string[]):string{return dates.map(x=>this.formatDate(x)).join(', ');}
  private formatDate(value:string):string{const p=value.slice(0,10).split('-');return p.length===3?new Date(+p[0],+p[1]-1,+p[2]).toLocaleDateString(undefined,{day:'2-digit',month:'short',year:'numeric'}):value;}
  private message(error:any,fallback:string):string{
    if(error?.status===400)return error?.error?.detail||error?.error?.title||'The request is invalid. Check the dates, leave type and reason.';
    if(error?.status===403)return 'You do not have permission to access this leave function.';
    if(error?.status===404)return 'Your employee mapping or selected company could not be found.';
    if(error?.status===409)return error?.error?.detail||error?.error?.title||'The request conflicts with an existing leave request or available balance. Refresh and try again.';
    return error?.error?.detail||error?.error?.title||error?.error?.message||fallback;
  }
}
