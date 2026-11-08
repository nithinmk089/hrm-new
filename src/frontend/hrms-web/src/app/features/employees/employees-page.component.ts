import { AbstractControl, Component, ValidationErrors, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary } from '../../core/company/company.models';
import { CreateEmployeeRequest, Employee, EmployeeStatus } from '../../core/company/employee.models';
import { EmployeeApiService } from '../../core/company/employee-api.service';

const trimmedEmailValidator = (control: AbstractControl): ValidationErrors | null => {
  const value = String(control.value ?? '').trim();
  return value && !/^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$/.test(value) ? { email: true } : null;
};

@Component({
  selector: 'hrms-employees-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-heading">
      <div><div class="eyebrow">WORKFORCE</div><h1>Employees</h1><p>Manage employee profiles and employment lifecycle for your company.</p></div>
      <button class="btn btn-primary" type="button" [disabled]="!selectedCompanyId() || loading()" (click)="openCreate()"><i class="bi bi-plus-lg me-2"></i>Add employee</button>
    </div>
    <section class="company-context card mb-3">
      <div class="context-icon"><i class="bi bi-building"></i></div>
      <div class="context-copy"><label for="companySelect">Company</label><select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length === 0"><option value="">Select a company</option>@for(company of companies(); track company.companyId){<option [value]="company.companyId" [selected]="company.companyId === selectedCompanyId()">{{company.displayName}} ({{company.companyCode}})</option>}</select></div>
      <span class="context-count">{{activeCount()}} active employees</span>
    </section>
    @if(error()){<div class="alert alert-danger" role="alert">{{error()}}</div>}
    @if(companiesLoading()){<div class="loading-state">Loading companies…</div>}
    @else if(companies().length === 0){<div class="empty-state card"><i class="bi bi-people"></i><h2>No companies available</h2><p>Your account has no active company available for employee administration.</p></div>}
    @else if(!selectedCompanyId()){<div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company above to view its employees.</p></div>}
    @else{
      <section class="table-card card">
        <div class="table-toolbar">
          <div><strong>Employee directory</strong><small>{{employees().length}} records</small></div>
          <div class="toolbar-filters">
            <input class="form-control form-control-sm search-input" aria-label="Search employees" placeholder="Search name, code or email" [value]="searchText()" (input)="onSearchInput($event)" (keyup.enter)="loadEmployees()">
            <select class="form-select form-select-sm status-filter" aria-label="Filter employment status" [value]="statusFilter()" (change)="onStatusChange($event)"><option value="">All statuses</option>@for(status of statuses;track status){<option [value]="status">{{statusLabel(status)}}</option>}</select>
            <button class="btn btn-light btn-sm" type="button" (click)="loadEmployees()" [disabled]="loading()"><i class="bi bi-search me-1"></i>Search</button>
          </div>
        </div>
        @if(loading()){<div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading employees…</div>}
        @else if(employees().length === 0){<div class="empty-inline"><i class="bi bi-people"></i><strong>No employees found</strong><span>Try changing the search or status filter, or add an employee.</span></div>}
        @else{
          <div class="table-responsive"><table class="table align-middle mb-0"><thead><tr><th>Employee</th><th>Employee code</th><th>Work email</th><th>Hire date</th><th>Employment status</th><th class="text-end">Actions</th></tr></thead><tbody>
          @for(employee of employees();track employee.employeeId){
            <tr>
              <td><button class="employee-name" type="button" (click)="viewEmployee(employee)"><strong>{{displayName(employee)}}</strong><small>{{employee.preferredName || employee.firstName + ' ' + employee.lastName}}</small></button></td>
              <td><span class="code-chip">{{employee.employeeCode}}</span></td><td>{{employee.workEmail || '—'}}</td><td>{{formatDate(employee.hireDate)}}</td>
              <td><span class="status" [class.inactive]="!employee.isActive" [class.leave]="employee.employmentStatus === 'OnLeave'">{{statusLabel(employee.employmentStatus)}}</span></td>
              <td class="text-end"><button class="btn btn-sm btn-light me-1" type="button" (click)="viewEmployee(employee)" aria-label="View employee" title="View employee"><i class="bi bi-eye"></i></button><button class="btn btn-sm btn-light" type="button" (click)="openEdit(employee)" [disabled]="!employee.isActive" aria-label="Edit employee" title="Edit employee"><i class="bi bi-pencil"></i></button></td>
            </tr>
          }
          </tbody></table></div>
        }
      </section>
    }
    @if(notice()){<div class="alert alert-success mt-3" role="status">{{notice()}} <button class="btn-close float-end" type="button" aria-label="Dismiss" (click)="notice.set('')"></button></div>}
    @if(detailTarget()){<div class="modal-backdrop-custom" (click)="detailTarget.set(null)"><section class="dialog card" role="dialog" aria-modal="true" aria-labelledby="detailTitle" (click)="$event.stopPropagation()">
      <div class="dialog-heading"><div><div class="eyebrow">EMPLOYEE PROFILE</div><h2 id="detailTitle">{{displayName(detailTarget()!)}}</h2></div><button class="btn-close" type="button" aria-label="Close" (click)="detailTarget.set(null)"></button></div>
      <dl class="detail-grid"><dt>Employee code</dt><dd>{{detailTarget()?.employeeCode}}</dd><dt>Work email</dt><dd>{{detailTarget()?.workEmail || '—'}}</dd><dt>Hire date</dt><dd>{{formatDate(detailTarget()!.hireDate)}}</dd><dt>Employment status</dt><dd>{{statusLabel(detailTarget()!.employmentStatus)}}</dd><dt>Termination date</dt><dd>{{detailTarget()?.terminationDate ? formatDate(detailTarget()!.terminationDate!) : '—'}}</dd></dl>
      <div class="status-editor"><label class="form-label" for="employmentStatus">Change employment status</label><select id="employmentStatus" class="form-select" [value]="statusDraft()" (change)="statusDraft.set($any($event.target).value)">@for(status of statuses;track status){<option [value]="status">{{statusLabel(status)}}</option>}</select>
      @if(statusDraft() === 'Terminated'){<label class="form-label mt-2" for="terminationDate">Termination date</label><input id="terminationDate" class="form-control" type="date" [value]="terminationDateDraft()" (input)="terminationDateDraft.set($any($event.target).value)">@if(statusError()){<div class="field-error">{{statusError()}}</div>}}
      @if(statusError() && statusDraft() !== 'Terminated'){<div class="alert alert-danger py-2 mt-2">{{statusError()}}</div>}
      <div class="actions"><button class="btn btn-light" type="button" (click)="detailTarget.set(null)">Close</button><button class="btn btn-primary" type="button" (click)="saveStatus()" [disabled]="statusSaving() || statusDraft() === detailTarget()?.employmentStatus"><span [class.spinner-border]="statusSaving()" [class.spinner-border-sm]="statusSaving()" class="me-2"></span>Update status</button></div></div>
    </section></div>}
    @if(editorOpen()){<div class="modal-backdrop-custom" (click)="closeEditor()"><section class="editor card" role="dialog" aria-modal="true" aria-labelledby="editorTitle" (click)="$event.stopPropagation()">
      <div class="dialog-heading"><div><div class="eyebrow">EMPLOYEE DETAILS</div><h2 id="editorTitle">{{editing() ? 'Edit employee' : 'Add employee'}}</h2></div><button class="btn-close" type="button" aria-label="Close" (click)="closeEditor()"></button></div>
      <form [formGroup]="form" (ngSubmit)="save()">
        @if(!editing()){<div class="mb-3"><label class="form-label" for="employeeCode">Employee code <span class="required">*</span></label><input id="employeeCode" class="form-control" formControlName="employeeCode" maxlength="30" placeholder="e.g. EMP-0001">@if(showError('employeeCode')){<div class="field-error">Employee code is required (maximum 30 characters).</div>}</div>}
        <div class="row g-3"><div class="col-md-6 mb-3"><label class="form-label" for="firstName">First name <span class="required">*</span></label><input id="firstName" class="form-control" formControlName="firstName" maxlength="100">@if(showError('firstName')){<div class="field-error">First name is required (maximum 100 characters).</div>}</div><div class="col-md-6 mb-3"><label class="form-label" for="middleName">Middle name</label><input id="middleName" class="form-control" formControlName="middleName" maxlength="100"></div></div>
        <div class="row g-3"><div class="col-md-6 mb-3"><label class="form-label" for="lastName">Last name <span class="required">*</span></label><input id="lastName" class="form-control" formControlName="lastName" maxlength="100">@if(showError('lastName')){<div class="field-error">Last name is required (maximum 100 characters).</div>}</div><div class="col-md-6 mb-3"><label class="form-label" for="preferredName">Preferred name</label><input id="preferredName" class="form-control" formControlName="preferredName" maxlength="100"></div></div>
        <div class="mb-3"><label class="form-label" for="workEmail">Work email</label><input id="workEmail" class="form-control" type="email" formControlName="workEmail" maxlength="254" placeholder="name@company.com">@if(showError('workEmail')){<div class="field-error">Enter a valid email address (maximum 254 characters).</div>}</div>
        <div class="mb-3"><label class="form-label" for="hireDate">Hire date <span class="required">*</span></label><input id="hireDate" class="form-control" type="date" formControlName="hireDate">@if(showError('hireDate')){<div class="field-error">Hire date is required.</div>}</div>
        @if(editorError()){<div class="alert alert-danger py-2">{{editorError()}}</div>}
        <div class="actions"><button class="btn btn-light" type="button" (click)="closeEditor()" [disabled]="saving()">Cancel</button><button class="btn btn-primary" type="submit" [disabled]="form.invalid || saving()"><span [class.spinner-border]="saving()" [class.spinner-border-sm]="saving()" class="me-2"></span>{{saving() ? 'Saving…' : 'Save employee'}}</button></div>
      </form>
    </section></div>}
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}
    .company-context{display:flex!important;flex-direction:row!important;align-items:center!important;gap:.85rem;padding:.9rem 1rem;min-height:5.25rem}.context-icon{display:grid;place-items:center;width:2.6rem;height:2.6rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.1rem}.context-copy{flex:1;max-width:28rem;min-width:14rem}.context-copy label{display:block;margin-bottom:.25rem;color:#667085;font-size:.67rem;font-weight:600}.context-count{margin-left:auto;color:#667085;font-size:.72rem;white-space:nowrap}
    .table-card{overflow:hidden}.table-toolbar{display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.table-toolbar strong,.table-toolbar small{display:block}.table-toolbar strong{font-size:.82rem;color:#344054}.table-toolbar small{margin-top:.2rem;color:#98a2b3;font-size:.68rem}.toolbar-filters{display:flex;align-items:center;gap:.5rem}.search-input{width:14rem}.status-filter{width:10rem}.table{font-size:.76rem}.table thead th{padding:.75rem 1rem;background:#f9fafb;color:#667085;font-size:.65rem;font-weight:700;white-space:nowrap}.table tbody td{padding:.8rem 1rem;color:#475467}.employee-name{display:flex;flex-direction:column;align-items:flex-start;padding:0;border:0;background:none;text-align:left}.employee-name strong{color:#344054;font-size:.76rem}.employee-name small{margin-top:.15rem;color:#98a2b3;font-size:.67rem}.code-chip{padding:.25rem .45rem;border-radius:.3rem;background:#f2f4f7;color:#475467;font-size:.68rem;font-weight:650}.status{display:inline-flex;padding:.25rem .5rem;border-radius:2rem;background:#ecfdf3;color:#027a48;font-size:.66rem;font-weight:600;white-space:nowrap}.status.inactive{background:#f2f4f7;color:#667085}.status.leave{background:#fff7e6;color:#93370d}
    .empty-state,.empty-inline{display:flex;align-items:center;flex-direction:column;text-align:center;color:#667085}.empty-state{padding:3rem 1.5rem}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}.empty-inline{padding:2.5rem 1rem;gap:.4rem}.empty-inline i{font-size:1.5rem;color:#7890b2}.empty-inline strong{color:#344054;font-size:.82rem}.empty-inline span{font-size:.74rem}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}
    .modal-backdrop-custom{position:fixed;inset:0;z-index:1200;display:grid;place-items:center;padding:1rem;background:rgb(16 24 40 / 48%)}.editor,.dialog{width:min(100%,38rem);max-height:90vh;overflow:auto;padding:1.35rem}.dialog{width:min(100%,34rem)}.dialog-heading{display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:1.2rem}.dialog-heading h2{margin:.3rem 0 0;color:#1d2939;font-size:1.15rem;font-weight:650}.form-label{color:#344054;font-size:.75rem;font-weight:600}.required{color:#d92d20}.field-error{margin-top:.3rem;color:#d92d20;font-size:.68rem}.detail-grid{display:grid;grid-template-columns:9rem 1fr;gap:.65rem;margin:0 0 1.2rem;font-size:.78rem}.detail-grid dt{color:#667085;font-weight:500}.detail-grid dd{margin:0;color:#344054;font-weight:600;overflow-wrap:anywhere}.status-editor{border-top:1px solid #eaecf0;padding-top:1rem}.actions{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1.2rem;padding-top:1rem;border-top:1px solid #eaecf0}
    @media(max-width:850px){.table-toolbar{align-items:flex-start;flex-direction:column}.toolbar-filters{width:100%;flex-wrap:wrap}.search-input{flex:1;width:auto}}@media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.context-count{display:none}.editor,.dialog{padding:1rem}.toolbar-filters>*{flex:1}.search-input{min-width:100%}}
  `]
})
export class EmployeesPageComponent {
  private readonly companyApi = inject(CompanyApiService);
  private readonly employeeApi = inject(EmployeeApiService);
  private readonly fb = inject(FormBuilder);
  readonly statuses: EmployeeStatus[] = ['Preboarding','Active','OnLeave','Inactive','Terminated'];
  readonly companies = signal<CompanySummary[]>([]);
  readonly employees = signal<Employee[]>([]);
  readonly selectedCompanyId = signal('');
  readonly companiesLoading = signal(true);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly statusSaving = signal(false);
  readonly editorOpen = signal(false);
  readonly editing = signal<Employee | null>(null);
  readonly detailTarget = signal<Employee | null>(null);
  readonly error = signal('');
  readonly editorError = signal('');
  readonly notice = signal('');
  readonly searchText = signal('');
  readonly statusFilter = signal<EmployeeStatus | ''>('');
  readonly statusDraft = signal<EmployeeStatus>('Active');
  readonly terminationDateDraft = signal('');
  readonly statusError = signal('');
  readonly activeCount = computed(() => this.employees().filter(item => item.isActive).length);
  readonly form = this.fb.nonNullable.group({
    employeeCode: ['', [Validators.required, Validators.maxLength(30)]],
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    middleName: [''],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    preferredName: [''],
    workEmail: ['', [trimmedEmailValidator, Validators.maxLength(254)]],
    hireDate: ['', Validators.required]
  });

  constructor() {
    this.companyApi.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: items => { const active = items.filter(item => item.isActive); this.companies.set(active); if (active.length === 1) { this.selectedCompanyId.set(active[0].companyId); this.loadEmployees(); } },
      error: err => this.error.set(this.message(err, 'Unable to load companies. Check the companies.read permission.'))
    });
  }

  onCompanyChange(event: Event): void { this.selectedCompanyId.set((event.target as HTMLSelectElement).value); this.employees.set([]); this.error.set(''); this.notice.set(''); if (this.selectedCompanyId()) this.loadEmployees(); }
  onSearchInput(event: Event): void { this.searchText.set((event.target as HTMLInputElement).value); }
  onStatusChange(event: Event): void { this.statusFilter.set((event.target as HTMLSelectElement).value as EmployeeStatus | ''); this.loadEmployees(); }
  loadEmployees(): void {
    const companyId = this.selectedCompanyId(); if (!companyId) return;
    this.loading.set(true); this.error.set('');
    this.employeeApi.getEmployees(companyId, this.searchText(), this.statusFilter()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => this.employees.set(items),
      error: err => this.error.set(this.message(err, 'Unable to load employees. Check employees.read permission.'))
    });
  }
  openCreate(): void {
    this.editing.set(null); this.editorError.set('');
    this.form.reset({employeeCode:'',firstName:'',middleName:'',lastName:'',preferredName:'',workEmail:'',hireDate:''});
    this.form.controls.employeeCode.enable(); this.editorOpen.set(true);
  }
  openEdit(item: Employee): void {
    this.editing.set(item); this.editorError.set('');
    this.form.reset({employeeCode:item.employeeCode,firstName:item.firstName,middleName:item.middleName ?? '',lastName:item.lastName,preferredName:item.preferredName ?? '',workEmail:item.workEmail ?? '',hireDate:item.hireDate.slice(0,10)});
    this.form.controls.employeeCode.disable(); this.editorOpen.set(true);
  }
  closeEditor(): void { if (!this.saving()) this.editorOpen.set(false); }
  showError(name: keyof typeof this.form.controls): boolean { const control=this.form.controls[name]; return control.invalid && (control.touched || control.dirty); }
  save(): void {
    if (this.form.invalid || !this.selectedCompanyId()) { this.form.markAllAsTouched(); return; }
    const value=this.form.getRawValue(); const editing=this.editing(); this.saving.set(true); this.editorError.set('');
    const optional=(input:string)=>input.trim() || null;
    if (!editing) {
      const request:CreateEmployeeRequest={employeeCode:value.employeeCode.trim().toUpperCase(),firstName:value.firstName.trim(),middleName:optional(value.middleName),lastName:value.lastName.trim(),preferredName:optional(value.preferredName),workEmail:optional(value.workEmail)?.toLowerCase() ?? null,hireDate:value.hireDate};
      this.employeeApi.createEmployee(this.selectedCompanyId(),request).pipe(finalize(()=>this.saving.set(false))).subscribe({
        next:()=>{this.editorOpen.set(false);this.notice.set('Employee created successfully.');this.loadEmployees();},
        error:err=>this.editorError.set(this.message(err,'Unable to create employee. Check employees.create permission and employee code uniqueness.'))
      });
    } else {
      this.employeeApi.updateEmployee(this.selectedCompanyId(),editing.employeeId,{firstName:value.firstName.trim(),middleName:optional(value.middleName),lastName:value.lastName.trim(),preferredName:optional(value.preferredName),workEmail:optional(value.workEmail)?.toLowerCase() ?? null,hireDate:value.hireDate,rowVersion:editing.rowVersion}).pipe(finalize(()=>this.saving.set(false))).subscribe({
        next:()=>{this.editorOpen.set(false);this.notice.set('Employee updated successfully.');this.loadEmployees();},
        error:err=>this.editorError.set(this.message(err,'Unable to update employee. The profile may have changed; refresh and try again.'))
      });
    }
  }
  viewEmployee(item: Employee): void { this.detailTarget.set(item); this.statusDraft.set(item.employmentStatus); this.terminationDateDraft.set(item.terminationDate?.slice(0,10) ?? ''); this.statusError.set(''); }
  saveStatus(): void {
    const item=this.detailTarget(); if(!item || !this.selectedCompanyId()) return;
    if(this.statusDraft()==='Terminated' && !this.terminationDateDraft()){this.statusError.set('Termination date is required for terminated employees.');return;}
    this.statusSaving.set(true);this.statusError.set('');
    this.employeeApi.updateEmployeeStatus(this.selectedCompanyId(),item.employeeId,{employmentStatus:this.statusDraft(),terminationDate:this.statusDraft()==='Terminated'?this.terminationDateDraft():null,rowVersion:item.rowVersion}).pipe(finalize(()=>this.statusSaving.set(false))).subscribe({
      next:()=>{this.detailTarget.set(null);this.notice.set('Employee status updated successfully.');this.loadEmployees();},
      error:err=>this.statusError.set(this.message(err,'Unable to update employee status. The record may have changed; refresh and try again.'))
    });
  }
  displayName(item: Employee): string { return [item.firstName,item.middleName,item.lastName].filter(Boolean).join(' '); }
  statusLabel(status: EmployeeStatus): string { return status==='OnLeave'?'On leave':status==='Preboarding'?'Preboarding':status; }
  formatDate(value: string): string { return new Intl.DateTimeFormat('en-IN',{day:'2-digit',month:'short',year:'numeric',timeZone:'UTC'}).format(new Date(value)); }
  private message(error: {status?:number;error?:{detail?:string;title?:string;message?:string}},fallback:string):string {
    if(error.status===403)return 'You do not have permission to perform this action.';
    if(error.status===409)return 'This employee record has changed. Refresh and try again.';
    return error.error?.detail || error.error?.message || fallback;
  }
}
