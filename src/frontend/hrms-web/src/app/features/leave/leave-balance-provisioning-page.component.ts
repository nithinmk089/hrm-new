import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary } from '../../core/company/company.models';
import { EmployeeApiService } from '../../core/company/employee-api.service';
import { Employee } from '../../core/company/employee.models';
import { ApplyLeaveBalanceTransactionRequest, EmployeeLeaveBalance, LeaveBalanceTransactionType, LeaveType } from '../../core/company/leave.models';
import { LeaveApiService } from '../../core/company/leave-api.service';

@Component({
  selector: 'hrms-leave-balance-provisioning-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-heading">
      <div><div class="eyebrow">HR ADMINISTRATION</div><h1>Leave balance provisioning</h1><p>Initialize and maintain annual employee leave entitlements with an auditable transaction ledger.</p></div>
    </div>
    @if(error()){<div class="alert alert-danger" role="alert">{{error()}}</div>}
    @if(success()){<div class="alert alert-success" role="status">{{success()}}</div>}
    <section class="card selector-card">
      <div class="selector-grid">
        <div><label class="form-label" for="company">Company</label><select id="company" class="form-select" [value]="companyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading()"><option value="">Select company</option>@for(company of companies();track company.companyId){<option [value]="company.companyId">{{company.displayName}} ({{company.companyCode}})</option>}</select></div>
        <div><label class="form-label" for="employee">Employee</label><select id="employee" class="form-select" [value]="employeeId()" (change)="onEmployeeChange($event)" [disabled]="!companyId() || employeesLoading()"><option value="">{{!companyId() ? "Select a company first" : employeesLoading() ? "Loading employees…" : "Select active employee"}}</option>@if(companyId()){@for(employee of employees();track employee.employeeId){<option [value]="employee.employeeId">{{employee.employeeCode}} — {{employee.firstName}} {{employee.lastName}}</option>}}</select></div>
        <div><label class="form-label" for="leaveYear">Leave year</label><input id="leaveYear" class="form-control" type="number" min="2000" max="2100" [value]="year()" (change)="onYearChange($event)" [disabled]="!companyId()"></div>
      </div>
    </section>
    @if(loading()){<div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading leave balances…</div>}
    @else if(companyId() && employeeId()){
      <div class="section-heading"><div><h2>Annual entitlement balances</h2><p>{{selectedEmployeeName()}} · Leave year {{year()}}</p></div><button class="btn btn-light btn-sm" type="button" (click)="loadBalances()" [disabled]="loading()"><i class="bi bi-arrow-clockwise me-1"></i>Refresh</button></div>
      @if(balances().length===0){<div class="empty-state card"><i class="bi bi-calendar2-plus"></i><h2>No balances initialized</h2><p>Choose a leave type below and create an Opening transaction to provision this employee's annual entitlement.</p></div>}
      @else {
        <div class="balance-grid">
          @for(balance of balances();track balance.employeeLeaveBalanceId){
            <article class="balance-card card"><div class="balance-top"><span class="balance-code">{{balance.leaveTypeCode}}</span><span class="balance-available">{{balance.availableDays}} available</span></div><h3>{{balance.leaveTypeName}}</h3><div class="balance-values"><div><strong>{{balance.openingDays}}</strong><small>Opening</small></div><div><strong>{{balance.accruedDays}}</strong><small>Accrued</small></div><div><strong>{{balance.carriedForwardDays}}</strong><small>Carry forward</small></div><div><strong>{{balance.adjustmentDays}}</strong><small>Adjustment</small></div><div><strong>{{balance.usedDays}}</strong><small>Used</small></div><div><strong>{{balance.reservedDays}}</strong><small>Reserved</small></div></div></article>
          }
        </div>
      }
      <section class="card transaction-card">
        <div class="section-heading"><div><h2>Provision or adjust balance</h2><p>Each transaction is recorded in the employee leave balance ledger.</p></div></div>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="transaction-grid">
            <div><label class="form-label" for="leaveType">Leave type <span class="required">*</span></label><select id="leaveType" class="form-select" formControlName="leaveTypeId"><option value="">Select leave type</option>@for(type of leaveTypes();track type.leaveTypeId){<option [value]="type.leaveTypeId">{{type.leaveTypeName}} ({{type.leaveTypeCode}})</option>}</select></div>
            <div><label class="form-label" for="transactionType">Transaction type <span class="required">*</span></label><select id="transactionType" class="form-select" formControlName="transactionType"><option value="Opening">Opening entitlement</option><option value="Accrual">Accrual</option><option value="CarryForward">Carry forward</option><option value="Adjustment">Adjustment</option></select></div>
            <div><label class="form-label" for="daysDelta">Days <span class="required">*</span></label><input id="daysDelta" class="form-control" type="number" step="0.5" min="-366" max="366" formControlName="daysDelta"><small class="field-hint">{{form.controls.transactionType.value==='Adjustment'?'Use a negative value to reduce entitlement.':'Use a positive value to grant days.'}}</small></div>
          </div>
          <div class="mt-3"><label class="form-label" for="notes">Notes</label><textarea id="notes" class="form-control" rows="2" maxlength="500" formControlName="notes" placeholder="Reason or reference for this transaction"></textarea></div>
          @if(formError()){<div class="alert alert-danger py-2 mt-3">{{formError()}}</div>}
          <div class="actions"><button class="btn btn-primary" type="submit" [disabled]="saving() || form.invalid"><span [class.spinner-border]="saving()" [class.spinner-border-sm]="saving()" class="me-2"></span>{{saving()?'Saving transaction…':'Save balance transaction'}}</button></div>
        </form>
      </section>
    } @else if(companyId()){<div class="empty-state card"><i class="bi bi-person-lines-fill"></i><h2>Select an employee</h2><p>Choose an active employee to view and provision annual leave balances.</p></div>}
    <section class="info-panel card"><i class="bi bi-shield-check"></i><div><strong>Audited balance management</strong><p>Opening, accrual, carry-forward and adjustment transactions are processed by the server. Balance changes and ledger entries are committed atomically. Used and reserved days cannot be reduced below the amount already consumed or reserved.</p></div></section>
  `,
  styles: [`
    :host{display:block}.page-heading{margin-bottom:1.25rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}.selector-card{padding:1rem;margin-bottom:1.25rem}.selector-grid{display:grid;grid-template-columns:1fr 1.4fr .6fr;gap:1rem}.form-label{display:block;color:#344054;font-size:.72rem;font-weight:600;margin-bottom:.35rem}.section-heading{display:flex;align-items:center;justify-content:space-between;gap:1rem;margin:1.3rem 0 .85rem}.section-heading h2{margin:0;color:#344054;font-size:.95rem;font-weight:650}.section-heading p{margin:.25rem 0 0;color:#98a2b3;font-size:.7rem}.balance-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(17rem,1fr));gap:.85rem}.balance-card{padding:1rem}.balance-top{display:flex;justify-content:space-between;align-items:center}.balance-code{padding:.25rem .45rem;border-radius:.3rem;background:#f2f4f7;color:#667085;font-size:.65rem;font-weight:650}.balance-available{color:#027a48;font-size:.7rem;font-weight:650}.balance-card h3{margin:.75rem 0;color:#344054;font-size:.85rem;font-weight:650}.balance-values{display:grid;grid-template-columns:repeat(3,1fr);gap:.7rem .5rem}.balance-values strong,.balance-values small{display:block}.balance-values strong{color:#344054;font-size:.85rem;font-variant-numeric:tabular-nums}.balance-values small{color:#98a2b3;font-size:.62rem}.transaction-card{padding:1rem;margin-top:1.25rem}.transaction-card .section-heading{margin:.1rem 0 1rem}.transaction-grid{display:grid;grid-template-columns:1.2fr 1fr .7fr;gap:1rem}.required{color:#b42318}.field-hint{display:block;margin-top:.3rem;color:#98a2b3;font-size:.64rem}.actions{display:flex;justify-content:flex-end;margin-top:1rem;padding-top:1rem;border-top:1px solid #eaecf0}.actions .btn{font-size:.72rem}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}.empty-state{display:flex;align-items:center;flex-direction:column;text-align:center;padding:2rem 1rem;color:#667085}.empty-state>i{font-size:1.7rem;color:#7890b2}.empty-state h2{margin:.7rem 0 .3rem;color:#344054;font-size:.95rem}.empty-state p{max-width:36rem;margin:0;font-size:.72rem}.info-panel{display:flex!important;flex-direction:row!important;gap:.7rem;padding:.9rem 1rem;margin-top:1.25rem;color:#52739e}.info-panel>i{font-size:1rem}.info-panel strong{color:#344054;font-size:.72rem}.info-panel p{margin:.2rem 0 0;color:#667085;font-size:.68rem;line-height:1.55}@media(max-width:760px){.selector-grid,.transaction-grid{grid-template-columns:1fr}.balance-grid{grid-template-columns:1fr}}`
  ]
})
export class LeaveBalanceProvisioningPageComponent {
  private readonly companyApi=inject(CompanyApiService);
  private readonly employeeApi=inject(EmployeeApiService);
  private readonly leaveApi=inject(LeaveApiService);
  private readonly fb=inject(FormBuilder);

  readonly companies=signal<CompanySummary[]>([]);
  readonly employees=signal<Employee[]>([]);
  readonly leaveTypes=signal<LeaveType[]>([]);
  readonly balances=signal<EmployeeLeaveBalance[]>([]);
  readonly companyId=signal('');
  readonly employeeId=signal('');
  readonly year=signal(new Date().getFullYear());
  readonly companiesLoading=signal(true);
  readonly employeesLoading=signal(false);
  readonly loading=signal(false);
  readonly saving=signal(false);
  readonly error=signal('');
  readonly success=signal('');
  readonly formError=signal('');
  readonly selectedEmployeeName=()=>{const e=this.employees().find(x=>x.employeeId===this.employeeId());return e?e.firstName+' '+e.lastName:'';};
  readonly form=this.fb.nonNullable.group({
    leaveTypeId:['',Validators.required],
    transactionType:['Opening' as LeaveBalanceTransactionType,Validators.required],
    daysDelta:[0,[Validators.required,Validators.min(-366),Validators.max(366)]],
    notes:['',[Validators.maxLength(500)]]
  });

  constructor(){
    this.companyApi.getCompanies().pipe(finalize(()=>this.companiesLoading.set(false))).subscribe({
      next:items=>{this.companies.set(items);if(items.length===1)this.onCompanyChangeValue(items[0].companyId);},
      error:()=>this.error.set('Unable to load companies.')
    });
  }

  onCompanyChange(event:Event):void{this.onCompanyChangeValue((event.target as HTMLSelectElement).value);}
  private onCompanyChangeValue(id:string):void{
    this.companyId.set(id);this.employeeId.set('');this.employees.set([]);this.leaveTypes.set([]);this.balances.set([]);this.error.set('');
    this.form.reset({leaveTypeId:'',transactionType:'Opening',daysDelta:0,notes:''});this.formError.set('');this.success.set('');
    if(!id)return;
    this.employeesLoading.set(true);
    this.employeeApi.getEmployees(id,undefined,'Active').pipe(finalize(()=>this.employeesLoading.set(false))).subscribe({
      next:items=>this.employees.set(items),
      error:()=>this.error.set('Unable to load active employees for this company.')
    });
    this.leaveApi.getLeaveTypes(id).subscribe({next:items=>this.leaveTypes.set(items),error:()=>this.error.set('Unable to load active leave types.')});
  }
  onEmployeeChange(event:Event):void{if(!this.companyId()){this.employeeId.set('');this.balances.set([]);return;}this.employeeId.set((event.target as HTMLSelectElement).value);this.balances.set([]);this.loadBalances();}
  onYearChange(event:Event):void{
    const value=Number((event.target as HTMLInputElement).value);
    if(Number.isInteger(value)&&value>=2000&&value<=2100){this.year.set(value);this.loadBalances();}
  }
  loadBalances():void{
    if(!this.companyId()||!this.employeeId()){this.balances.set([]);return;}
    this.loading.set(true);this.error.set('');
    this.leaveApi.getEmployeeBalances(this.companyId(),this.employeeId(),this.year()).pipe(finalize(()=>this.loading.set(false))).subscribe({
      next:items=>this.balances.set(items),
      error:error=>this.error.set(this.message(error,'Unable to load employee leave balances.'))
    });
  }
  submit():void{
    this.form.markAllAsTouched();this.formError.set('');this.success.set('');
    const value=this.form.getRawValue();
    if(this.form.invalid||!this.companyId()||!this.employeeId())return;
    if(value.daysDelta===0){this.formError.set('Days must be greater than zero or less than zero.');return;}
    if(value.transactionType!=='Adjustment'&&value.daysDelta<0){this.formError.set('Only Adjustment transactions can reduce a balance.');return;}
    const selectedType=this.leaveTypes().find(type=>type.leaveTypeId===value.leaveTypeId);
    const currentBalance=this.balances().find(balance=>balance.leaveTypeId===value.leaveTypeId);
    if(!selectedType){this.formError.set('Select a valid active leave type.');return;}
    if(value.transactionType==='CarryForward'){
      if(value.daysDelta>0&&!selectedType.allowCarryForward){this.formError.set('Carry forward is not enabled for this leave type.');return;}
      const currentCarry=currentBalance?.carriedForwardDays??0;
      if(currentCarry+value.daysDelta<0){this.formError.set('Carried-forward days cannot be reduced below zero.');return;}
      if(selectedType.allowCarryForward&&currentCarry+value.daysDelta>selectedType.maxCarryForwardDays){
        this.formError.set('This leave type allows a maximum of '+selectedType.maxCarryForwardDays+' carry-forward days. Current: '+currentCarry+'; requested change: '+value.daysDelta+'.');return;
      }
    }else{
      const currentAnnual=(currentBalance?.openingDays??0)+(currentBalance?.accruedDays??0)+(currentBalance?.adjustmentDays??0);
      const projectedAnnual=currentAnnual+value.daysDelta;
      if(projectedAnnual<0){this.formError.set('Annual entitlement cannot be reduced below zero.');return;}
      if(selectedType.annualEntitlementDays!==null&&projectedAnnual>selectedType.annualEntitlementDays){
        this.formError.set('This leave type allows a maximum annual entitlement of '+selectedType.annualEntitlementDays+' days. Current: '+currentAnnual+'; requested change: '+value.daysDelta+'.');return;
      }
      const consumed=(currentBalance?.usedDays??0)+(currentBalance?.reservedDays??0);
      if(projectedAnnual<consumed){this.formError.set('Entitlement cannot be reduced below '+consumed+' used and reserved days.');return;}
    }
    const request:ApplyLeaveBalanceTransactionRequest={
      leaveTypeId:value.leaveTypeId,leaveYear:this.year(),transactionType:value.transactionType,
      daysDelta:value.daysDelta,notes:value.notes.trim()||null
    };
    this.saving.set(true);
    this.leaveApi.applyEmployeeBalanceTransaction(this.companyId(),this.employeeId(),request).pipe(finalize(()=>this.saving.set(false))).subscribe({
      next:()=>{this.success.set('Leave balance transaction saved successfully.');this.formError.set('');this.loadBalances();this.form.patchValue({daysDelta:0,notes:''});},
      error:error=>this.formError.set(this.message(error,'Unable to save the balance transaction.'))
    });
  }
  daysHint():string{
    const type=this.leaveTypes().find(item=>item.leaveTypeId===this.form.controls.leaveTypeId.value);
    const transaction=this.form.controls.transactionType.value;
    if(!type)return 'Select a leave type to see its entitlement limit.';
    if(transaction==='CarryForward')return type.allowCarryForward?'Maximum carry forward: '+type.maxCarryForwardDays+' days.':'Carry forward is disabled for this leave type.';
    return type.annualEntitlementDays===null?'No annual entitlement cap is configured.':'Annual entitlement limit: '+type.annualEntitlementDays+' days.';
  }
  private message(error:any,fallback:string):string{
    if(error?.status===400)return error?.error?.detail||error?.error?.title||'The balance transaction is invalid.';
    if(error?.status===403)return 'You do not have permission to provision or adjust leave balances.';
    if(error?.status===404)return 'The employee, company or leave type could not be found.';
    if(error?.status===409)return error?.error?.detail||'A concurrent balance change was detected. Refresh and try again.';
    return error?.error?.detail||error?.error?.title||fallback;
  }
}
