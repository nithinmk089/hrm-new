import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary, Department } from '../../core/company/company.models';

@Component({
  selector: 'hrms-departments-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-heading">
      <div><div class="eyebrow">ORGANIZATION</div><h1>Departments</h1><p>Manage departments and reporting structure for a company.</p></div>
      <button class="btn btn-primary" type="button" [disabled]="!selectedCompanyId() || loading()" (click)="openCreate()"><i class="bi bi-plus-lg me-2"></i>Add department</button>
    </div>

    <section class="company-context card mb-3">
      <div class="context-icon"><i class="bi bi-building"></i></div>
      <div class="context-copy"><label for="companySelect">Company</label><select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length === 0"><option value="">Select a company</option>@for(company of companies(); track company.companyId){<option [value]="company.companyId">{{ company.displayName }} ({{ company.companyCode }})</option>}</select></div>
      <span class="context-count">{{ activeCount() }} active</span>
    </section>

    @if (error()) { <div class="alert alert-danger" role="alert">{{ error() }}</div> }
    @if (notice()) { <div class="alert alert-success" role="status">{{ notice() }}</div> }

    @if (companiesLoading()) {
      <div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading companies…</div>
    } @else if (companies().length === 0) {
      <div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account does not currently have a company available for department administration. Check the companies.read permission and company setup.</p></div>
    } @else if (!selectedCompanyId()) {
      <div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company above to view and manage its departments.</p></div>
    } @else {
      <section class="table-card card">
        <div class="table-toolbar"><div><strong>Department directory</strong><small>{{ departments().length }} records</small></div><button class="btn btn-light btn-sm" type="button" (click)="loadDepartments()" [disabled]="loading()"><i class="bi bi-arrow-clockwise me-1"></i>Refresh</button></div>
        @if (loading()) { <div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading departments…</div> }
        @else if (departments().length === 0) { <div class="empty-inline"><i class="bi bi-diagram-3"></i><strong>No departments yet</strong><span>Create the first department for this company.</span></div> }
        @else {
          <div class="table-responsive"><table class="table align-middle mb-0"><thead><tr><th>Department</th><th>Code</th><th>Parent department</th><th>Status</th><th class="text-end">Actions</th></tr></thead><tbody>
            @for (department of departments(); track department.departmentId) {
              <tr><td><strong>{{ department.departmentName }}</strong>@if(department.description){<small class="d-block text-muted">{{ department.description }}</small>}</td><td><span class="code-chip">{{ department.departmentCode }}</span></td><td>{{ parentName(department.parentDepartmentId) }}</td><td><span class="status" [class.inactive]="!department.isActive">{{ department.isActive ? 'Active' : 'Inactive' }}</span></td><td class="text-end"><button class="btn btn-sm btn-light me-1" type="button" (click)="openEdit(department)" [disabled]="!department.isActive" aria-label="Edit department"><i class="bi bi-pencil"></i></button><button class="btn btn-sm btn-light text-danger" type="button" (click)="deactivate(department)" [disabled]="!department.isActive || busyId() === department.departmentId" aria-label="Deactivate department"><i class="bi bi-person-dash"></i></button></td></tr>
            }
          </tbody></table></div>
        }
      </section>
    }

    @if (editorOpen()) {
      <div class="modal-backdrop-custom" (click)="closeEditor()">
        <section class="editor card" role="dialog" aria-modal="true" aria-labelledby="editorTitle" (click)="$event.stopPropagation()">
          <div class="editor-heading"><div><div class="eyebrow">DEPARTMENT DETAILS</div><h2 id="editorTitle">{{ editing() ? 'Edit department' : 'Add department' }}</h2></div><button class="btn-close" type="button" aria-label="Close" (click)="closeEditor()"></button></div>
          <form [formGroup]="form" (ngSubmit)="save()">
            @if (!editing()) { <div class="mb-3"><label class="form-label" for="departmentCode">Department code <span class="required">*</span></label><input id="departmentCode" class="form-control" formControlName="departmentCode" maxlength="30" placeholder="e.g. HR"><div class="field-hint">Unique within the selected company. Stored in uppercase.</div>@if(showError('departmentCode')){<div class="field-error">Enter a department code (maximum 30 characters).</div>}</div> }
            <div class="mb-3"><label class="form-label" for="departmentName">Department name <span class="required">*</span></label><input id="departmentName" class="form-control" formControlName="departmentName" maxlength="150" placeholder="e.g. Human Resources">@if(showError('departmentName')){<div class="field-error">Department name is required (maximum 150 characters).</div>}</div>
            <div class="mb-3"><label class="form-label" for="description">Description</label><textarea id="description" class="form-control" formControlName="description" rows="3" maxlength="500" placeholder="Optional description"></textarea></div>
            <div class="mb-3"><label class="form-label" for="parentDepartment">Parent department</label><select id="parentDepartment" class="form-select" formControlName="parentDepartmentId"><option value="">No parent (top-level department)</option>@for(item of parentOptions(); track item.departmentId){<option [value]="item.departmentId">{{ item.departmentName }} ({{ item.departmentCode }})</option>}</select><div class="field-hint">Only active departments in this company can be selected.</div></div>
            @if (editorError()) { <div class="alert alert-danger py-2">{{ editorError() }}</div> }
            <div class="editor-actions"><button class="btn btn-light" type="button" (click)="closeEditor()" [disabled]="saving()">Cancel</button><button class="btn btn-primary" type="submit" [disabled]="form.invalid || saving()"><span [class.spinner-border]="saving()" [class.spinner-border-sm]="saving()" class="me-2"></span>{{ saving() ? 'Saving…' : 'Save department' }}</button></div>
          </form>
        </section>
      </div>
    }
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}
    .company-context{display:flex;align-items:center;gap:.85rem;padding:.9rem 1rem}.context-icon{display:grid;place-items:center;width:2.6rem;height:2.6rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.1rem}.context-copy{flex:1;max-width:28rem}.context-copy label{display:block;margin-bottom:.25rem;color:#667085;font-size:.67rem;font-weight:600}.context-count{color:#667085;font-size:.72rem}
    .table-card{overflow:hidden}.table-toolbar{display:flex;align-items:center;justify-content:space-between;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.table-toolbar strong,.table-toolbar small{display:block}.table-toolbar strong{font-size:.82rem;color:#344054}.table-toolbar small{margin-top:.2rem;color:#98a2b3;font-size:.68rem}.table{font-size:.78rem}.table thead th{padding:.75rem 1rem;background:#f9fafb;color:#667085;font-size:.65rem;font-weight:700;letter-spacing:.04em;white-space:nowrap}.table tbody td{padding:.8rem 1rem;color:#475467}.table tbody td strong{color:#344054}.code-chip{padding:.25rem .45rem;border-radius:.3rem;background:#f2f4f7;color:#475467;font-size:.7rem;font-weight:600}.status{display:inline-flex;padding:.25rem .5rem;border-radius:2rem;background:#ecfdf3;color:#027a48;font-size:.66rem;font-weight:600}.status.inactive{background:#f2f4f7;color:#667085}
    .empty-state,.empty-inline{display:flex;align-items:center;flex-direction:column;text-align:center;color:#667085}.empty-state{padding:3rem 1.5rem}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}.empty-inline{padding:2.5rem 1rem;gap:.4rem}.empty-inline i{font-size:1.5rem;color:#7890b2}.empty-inline strong{color:#344054;font-size:.82rem}.empty-inline span{font-size:.74rem}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}
    .modal-backdrop-custom{position:fixed;inset:0;z-index:1200;display:grid;place-items:center;padding:1rem;background:rgb(16 24 40 / 48%)}.editor{width:min(100%,34rem);max-height:90vh;overflow:auto;padding:1.35rem}.editor-heading{display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:1.2rem}.editor-heading h2{margin:.3rem 0 0;color:#1d2939;font-size:1.15rem;font-weight:650}.form-label{color:#344054;font-size:.75rem;font-weight:600}.required{color:#d92d20}.field-hint{margin-top:.3rem;color:#98a2b3;font-size:.66rem}.field-error{margin-top:.3rem;color:#d92d20;font-size:.68rem}.editor-actions{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1.4rem;padding-top:1rem;border-top:1px solid #eaecf0}
    @media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.context-count{display:none}.editor{padding:1rem}}
  `]
})
export class DepartmentsPageComponent {
  private readonly api = inject(CompanyApiService);
  private readonly fb = inject(FormBuilder);
  readonly companies = signal<CompanySummary[]>([]);
  readonly departments = signal<Department[]>([]);
  readonly selectedCompanyId = signal('');
  readonly companiesLoading = signal(true);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly editorOpen = signal(false);
  readonly editing = signal<Department | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly editorError = signal('');
  readonly busyId = signal('');
  readonly activeCount = computed(() => this.departments().filter(item => item.isActive).length);
  readonly parentOptions = computed(() => this.departments().filter(item => item.isActive && item.departmentId !== this.editing()?.departmentId));
  readonly form = this.fb.nonNullable.group({
    departmentCode: ['', [Validators.required, Validators.maxLength(30)]],
    departmentName: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', [Validators.maxLength(500)]],
    parentDepartmentId: ['']
  });

  constructor() {
    this.api.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: companies => {
        const active = companies.filter(company => company.isActive);
        this.companies.set(active);
        if (active.length === 1) {
          this.selectedCompanyId.set(active[0].companyId);
          this.loadDepartments();
        }
      },
      error: err => this.error.set(this.message(err, 'Unable to load companies. Check that your account has companies.read permission.'))
    });
  }

  onCompanyChange(event: Event): void {
    const companyId = (event.target as HTMLSelectElement).value;
    this.selectedCompanyId.set(companyId);
    this.departments.set([]);
    this.notice.set('');
    this.error.set('');
    if (companyId) this.loadDepartments();
  }

  loadDepartments(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getDepartments(companyId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => this.departments.set(items),
      error: err => this.error.set(this.message(err, 'Unable to load departments for this company.'))
    });
  }

  parentName(parentId: string | null): string {
    return parentId ? this.departments().find(item => item.departmentId === parentId)?.departmentName ?? 'Unavailable' : '—';
  }

  openCreate(): void {
    this.editing.set(null);
    this.editorError.set('');
    this.form.reset({ departmentCode: '', departmentName: '', description: '', parentDepartmentId: '' });
    this.form.controls.departmentCode.enable();
    this.editorOpen.set(true);
  }

  openEdit(item: Department): void {
    this.editing.set(item);
    this.editorError.set('');
    this.form.reset({ departmentCode: item.departmentCode, departmentName: item.departmentName, description: item.description ?? '', parentDepartmentId: item.parentDepartmentId ?? '' });
    this.form.controls.departmentCode.disable();
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    if (this.saving()) return;
    this.editorOpen.set(false);
    this.editing.set(null);
  }

  showError(control: 'departmentCode' | 'departmentName'): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  save(): void {
    if (this.form.invalid || this.saving()) { this.form.markAllAsTouched(); return; }
    const companyId = this.selectedCompanyId();
    const value = this.form.getRawValue();
    const description = value.description.trim() || null;
    const parentDepartmentId = value.parentDepartmentId || null;
    const current = this.editing();
    this.saving.set(true);
    this.editorError.set('');
    const request = current
      ? this.api.updateDepartment(companyId, current.departmentId, {
          departmentName: value.departmentName.trim(), description, parentDepartmentId, rowVersion: current.rowVersion
        })
      : this.api.createDepartment(companyId, {
          departmentCode: value.departmentCode.trim().toUpperCase(), departmentName: value.departmentName.trim(), description, parentDepartmentId
        });
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.editing.set(null);
        this.notice.set(current ? 'Department updated successfully.' : 'Department created successfully.');
        this.loadDepartments();
      },
      error: err => this.editorError.set(this.message(err, 'Unable to save the department.'))
    });
  }

  deactivate(item: Department): void {
    if (!item.isActive || this.busyId()) return;
    if (!window.confirm(`Deactivate department "${item.departmentName}"? It will remain in historical records.`)) return;
    this.busyId.set(item.departmentId);
    this.api.deactivateDepartment(this.selectedCompanyId(), item.departmentId, item.rowVersion)
      .pipe(finalize(() => this.busyId.set('')))
      .subscribe({
        next: () => { this.notice.set('Department deactivated successfully.'); this.loadDepartments(); },
        error: err => this.error.set(this.message(err, 'Unable to deactivate the department. Reload and check whether it was changed by another user.'))
      });
  }

  private message(error: any, fallback: string): string {
    if (error?.status === 409) return 'This record has changed or is in use. Refresh the list and try again.';
    if (error?.status === 403) return 'You do not have permission to perform this action.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}