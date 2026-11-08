import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary, Office } from '../../core/company/company.models';

@Component({
  selector: 'hrms-offices-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="page-heading">
      <div><div class="eyebrow">ORGANIZATION</div><h1>Offices</h1><p>Manage company locations, addresses and head-office details.</p></div>
      <button class="btn btn-primary" type="button" [disabled]="!selectedCompanyId() || loading()" (click)="openCreate()"><i class="bi bi-plus-lg me-2"></i>Add office</button>
    </div>

    <section class="company-context card mb-3">
      <div class="context-icon"><i class="bi bi-building"></i></div>
      <div class="context-copy"><label for="companySelect">Company</label><select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length === 0"><option value="">Select a company</option>@for(company of companies(); track company.companyId){<option [value]="company.companyId">{{ company.displayName }} ({{ company.companyCode }})</option>}</select></div>
      <span class="context-count">{{ activeCount() }} active offices</span>
    </section>

    @if(error()){<div class="alert alert-danger" role="alert">{{ error() }}</div>}
    @if(companiesLoading()){
      <div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading companies…</div>
    }@else if(companies().length === 0){
      <div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account does not currently have a company available for office administration. Check company setup and companies.read permission.</p></div>
    }@else if(!selectedCompanyId()){
      <div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company above to view and manage its offices.</p></div>
    }@else{
      <section class="table-card card">
        <div class="table-toolbar"><div><strong>Office directory</strong><small>{{ offices().length }} records</small></div><button class="btn btn-light btn-sm" type="button" (click)="loadOffices()" [disabled]="loading()"><i class="bi bi-arrow-clockwise me-1"></i>Refresh</button></div>
        @if(loading()){<div class="loading-state"><span class="spinner-border spinner-border-sm me-2"></span>Loading offices…</div>}
        @else if(offices().length === 0){<div class="empty-inline"><i class="bi bi-geo-alt"></i><strong>No offices yet</strong><span>Create the first office for this company.</span></div>}
        @else{
          <div class="table-responsive"><table class="table align-middle mb-0"><thead><tr><th>Office</th><th>Location</th><th>Country</th><th>Time zone</th><th>Type</th><th>Status</th><th class="text-end">Actions</th></tr></thead><tbody>
            @for(office of offices(); track office.officeId){
              <tr>
                <td><strong>{{ office.officeName }}</strong><small class="d-block text-muted">{{ office.officeCode }}</small></td>
                <td>{{ location(office) }}</td><td>{{ office.countryCode }}</td><td>{{ office.timeZoneId || '—' }}</td>
                <td>@if(office.isHeadOffice){<span class="head-office"><i class="bi bi-star-fill me-1"></i>Head office</span>}@else{<span class="text-muted">Branch</span>}</td>
                <td><span class="status" [class.inactive]="!office.isActive">{{ office.isActive ? 'Active' : 'Inactive' }}</span></td>
                <td class="text-end"><button class="btn btn-sm btn-light me-1 action-button" type="button" (click)="openEdit(office)" [disabled]="!office.isActive" aria-label="Edit office" title="Edit office"><i class="bi bi-pencil"></i></button>@if(office.isActive){<button class="btn btn-sm btn-light text-danger action-button" type="button" (click)="deactivate(office)" [disabled]="busyId() === office.officeId" aria-label="Deactivate office" title="Deactivate office"><i class="bi bi-building-dash"></i></button>}@else{<span class="text-muted small">—</span>}</td>
              </tr>
            }
          </tbody></table></div>
        }
      </section>
    }

    @if(confirmTarget()){
      <div class="modal-backdrop-custom" (click)="cancelDeactivate()"><section class="confirm-dialog card" role="alertdialog" aria-modal="true" aria-labelledby="confirmTitle" (click)="$event.stopPropagation()">
        <div class="confirm-icon"><i class="bi bi-exclamation-triangle"></i></div><h2 id="confirmTitle">Deactivate office?</h2>
        <p><strong>{{ confirmTarget()?.officeName }}</strong> will be marked inactive and retained in historical records. If it is the head office, the company will no longer have an active head office.</p>
        @if(confirmError()){<div class="alert alert-danger py-2">{{ confirmError() }}</div>}
        <div class="editor-actions"><button class="btn btn-light" type="button" (click)="cancelDeactivate()" [disabled]="busyId() !== ''">Cancel</button><button class="btn btn-danger" type="button" (click)="confirmDeactivate()" [disabled]="busyId() !== ''">{{ busyId() ? 'Deactivating…' : 'Deactivate' }}</button></div>
      </section></div>
    }
    @if(notice()){
      <div class="modal-backdrop-custom" (click)="notice.set('')"><section class="confirm-dialog card success-dialog" role="dialog" aria-modal="true" aria-labelledby="noticeTitle" (click)="$event.stopPropagation()"><div class="success-icon"><i class="bi bi-check-lg"></i></div><h2 id="noticeTitle">Success</h2><p>{{ notice() }}</p><div class="editor-actions"><button class="btn btn-primary" type="button" (click)="notice.set('')">OK</button></div></section></div>
    }
    @if(editorOpen()){
      <div class="modal-backdrop-custom" (click)="closeEditor()"><section class="editor card" role="dialog" aria-modal="true" aria-labelledby="editorTitle" (click)="$event.stopPropagation()">
        <div class="editor-heading"><div><div class="eyebrow">OFFICE DETAILS</div><h2 id="editorTitle">{{ editing() ? 'Edit office' : 'Add office' }}</h2></div><button class="btn-close" type="button" aria-label="Close" (click)="closeEditor()"></button></div>
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="section-label">Basic information</div>
          @if(!editing()){<div class="mb-3"><label class="form-label" for="officeCode">Office code <span class="required">*</span></label><input id="officeCode" class="form-control" formControlName="officeCode" maxlength="30" placeholder="e.g. HQ"><div class="field-hint">Unique within this company. Stored in uppercase.</div>@if(showError('officeCode')){<div class="field-error">Enter an office code (maximum 30 characters).</div>}</div>}
          <div class="mb-3"><label class="form-label" for="officeName">Office name <span class="required">*</span></label><input id="officeName" class="form-control" formControlName="officeName" maxlength="150" placeholder="e.g. Corporate Office">@if(showError('officeName')){<div class="field-error">Office name is required (maximum 150 characters).</div>}</div>
          <div class="form-check mb-3"><input id="isHeadOffice" class="form-check-input" type="checkbox" formControlName="isHeadOffice"><label class="form-check-label" for="isHeadOffice">This is the company's head office</label><div class="field-hint">A company can have only one active head office.</div></div>
          <div class="section-label mt-4">Address</div>
          <div class="mb-3"><label class="form-label" for="addressLine1">Address line 1</label><input id="addressLine1" class="form-control" formControlName="addressLine1" maxlength="200"></div>
          <div class="mb-3"><label class="form-label" for="addressLine2">Address line 2</label><input id="addressLine2" class="form-control" formControlName="addressLine2" maxlength="200"></div>
          <div class="row g-3 mb-3"><div class="col-md-6"><label class="form-label" for="city">City</label><input id="city" class="form-control" formControlName="city" maxlength="100"></div><div class="col-md-6"><label class="form-label" for="stateOrProvince">State / Province</label><input id="stateOrProvince" class="form-control" formControlName="stateOrProvince" maxlength="100"></div></div>
          <div class="row g-3 mb-3"><div class="col-md-6"><label class="form-label" for="postalCode">Postal code</label><input id="postalCode" class="form-control" formControlName="postalCode" maxlength="20"></div><div class="col-md-6"><label class="form-label" for="countryCode">Country code <span class="required">*</span></label><input id="countryCode" class="form-control text-uppercase" formControlName="countryCode" maxlength="2" placeholder="IN">@if(showError('countryCode')){<div class="field-error">Enter a two-letter country code, e.g. IN.</div>}</div></div>
          <div class="section-label mt-4">Regional settings</div>
          <div class="mb-3"><label class="form-label" for="timeZoneId">Time zone ID</label><input id="timeZoneId" class="form-control" formControlName="timeZoneId" maxlength="100" placeholder="e.g. Asia/Kolkata"><div class="field-hint">Use a system time-zone identifier supported by the server.</div></div>
          @if(editorError()){<div class="alert alert-danger py-2">{{ editorError() }}</div>}
          <div class="editor-actions"><button class="btn btn-light" type="button" (click)="closeEditor()" [disabled]="saving()">Cancel</button><button class="btn btn-primary" type="submit" [disabled]="form.invalid || saving()"><span [class.spinner-border]="saving()" [class.spinner-border-sm]="saving()" class="me-2"></span>{{ saving() ? 'Saving…' : 'Save office' }}</button></div>
        </form>
      </section></div>
    }
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}
    .company-context{display:flex!important;flex-direction:row!important;align-items:center!important;justify-content:flex-start!important;gap:.85rem;padding:.9rem 1rem;min-height:5.25rem}.context-icon{display:grid;place-items:center;width:2.6rem;height:2.6rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.1rem}.context-copy{flex:1;max-width:28rem;min-width:14rem}.context-copy label{display:block;margin-bottom:.25rem;color:#667085;font-size:.67rem;font-weight:600}.context-count{margin-left:auto;color:#667085;font-size:.72rem;white-space:nowrap}
    .table-card{overflow:hidden}.table-toolbar{display:flex;align-items:center;justify-content:space-between;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.table-toolbar strong,.table-toolbar small{display:block}.table-toolbar strong{font-size:.82rem;color:#344054}.table-toolbar small{margin-top:.2rem;color:#98a2b3;font-size:.68rem}.table{font-size:.76rem}.table thead th{padding:.75rem 1rem;background:#f9fafb;color:#667085;font-size:.65rem;font-weight:700;letter-spacing:.04em;white-space:nowrap}.table tbody td{padding:.8rem 1rem;color:#475467}.table tbody td strong{color:#344054}.status{display:inline-flex;padding:.25rem .5rem;border-radius:2rem;background:#ecfdf3;color:#027a48;font-size:.66rem;font-weight:600}.status.inactive{background:#f2f4f7;color:#667085}.head-office{padding:.25rem .45rem;border-radius:.35rem;background:#eff6ff;color:#31558f;font-size:.67rem;font-weight:600;white-space:nowrap}.action-button:hover{background:#eef3fa;border-color:#d0d5dd}
    .empty-state,.empty-inline{display:flex;align-items:center;flex-direction:column;text-align:center;color:#667085}.empty-state{padding:3rem 1.5rem}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}.empty-inline{padding:2.5rem 1rem;gap:.4rem}.empty-inline i{font-size:1.5rem;color:#7890b2}.empty-inline strong{color:#344054;font-size:.82rem}.empty-inline span{font-size:.74rem}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}
    .modal-backdrop-custom{position:fixed;inset:0;z-index:1200;display:grid;place-items:center;padding:1rem;background:rgb(16 24 40 / 48%)}.confirm-dialog{width:min(100%,27rem);padding:1.5rem}.confirm-dialog h2{margin:.1rem 0 .55rem;font-size:1.05rem;font-weight:650;color:#1d2939}.confirm-dialog p{margin:0;color:#667085;font-size:.8rem;line-height:1.6}.confirm-icon,.success-icon{display:grid;place-items:center;width:2.7rem;height:2.7rem;margin-bottom:.9rem;border-radius:50%;background:#fff4e5;color:#b54708;font-size:1.2rem}.success-icon{background:#ecfdf3;color:#027a48}.editor{width:min(100%,38rem);max-height:90vh;overflow:auto;padding:1.35rem}.editor-heading{display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:1.2rem}.editor-heading h2{margin:.3rem 0 0;color:#1d2939;font-size:1.15rem;font-weight:650}.section-label{margin-bottom:.8rem;color:#344054;font-size:.75rem;font-weight:700}.form-label{color:#344054;font-size:.75rem;font-weight:600}.form-check-label{color:#344054;font-size:.75rem;font-weight:600}.required{color:#d92d20}.field-hint{margin-top:.3rem;color:#98a2b3;font-size:.66rem}.field-error{margin-top:.3rem;color:#d92d20;font-size:.68rem}.editor-actions{display:flex;justify-content:flex-end;gap:.5rem;margin-top:1.4rem;padding-top:1rem;border-top:1px solid #eaecf0}
    @media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.context-count{display:none}.editor{padding:1rem}}
  `]
})
export class OfficesPageComponent {
  private readonly api = inject(CompanyApiService);
  private readonly fb = inject(FormBuilder);
  readonly companies = signal<CompanySummary[]>([]);
  readonly offices = signal<Office[]>([]);
  readonly selectedCompanyId = signal('');
  readonly companiesLoading = signal(true);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly editorOpen = signal(false);
  readonly editing = signal<Office | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly editorError = signal('');
  readonly busyId = signal('');
  readonly confirmTarget = signal<Office | null>(null);
  readonly confirmError = signal('');
  readonly activeCount = computed(() => this.offices().filter(item => item.isActive).length);
  readonly form = this.fb.nonNullable.group({
    officeCode: ['', [Validators.required, Validators.maxLength(30)]],
    officeName: ['', [Validators.required, Validators.maxLength(150)]],
    addressLine1: ['', [Validators.maxLength(200)]],
    addressLine2: ['', [Validators.maxLength(200)]],
    city: ['', [Validators.maxLength(100)]],
    stateOrProvince: ['', [Validators.maxLength(100)]],
    postalCode: ['', [Validators.maxLength(20)]],
    countryCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
    timeZoneId: ['', [Validators.maxLength(100)]],
    isHeadOffice: [false]
  });

  constructor() {
    this.api.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: companies => {
        const active = companies.filter(company => company.isActive);
        this.companies.set(active);
        if (active.length === 1) {
          this.selectedCompanyId.set(active[0].companyId);
          this.loadOffices();
        }
      },
      error: err => this.error.set(this.message(err, 'Unable to load companies. Check companies.read permission.'))
    });
  }

  onCompanyChange(event: Event): void {
    const companyId = (event.target as HTMLSelectElement).value;
    this.selectedCompanyId.set(companyId);
    this.offices.set([]);
    this.notice.set('');
    this.error.set('');
    if (companyId) this.loadOffices();
  }

  loadOffices(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getOffices(companyId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: items => this.offices.set(items),
      error: err => this.error.set(this.message(err, 'Unable to load offices for this company.'))
    });
  }

  location(office: Office): string {
    return [office.city, office.stateOrProvince].filter(Boolean).join(', ') || office.addressLine1 || '—';
  }

  openCreate(): void {
    this.editing.set(null);
    this.editorError.set('');
    this.form.reset({ officeCode: '', officeName: '', addressLine1: '', addressLine2: '', city: '', stateOrProvince: '', postalCode: '', countryCode: '', timeZoneId: '', isHeadOffice: false });
    this.form.controls.officeCode.enable();
    this.editorOpen.set(true);
  }

  openEdit(item: Office): void {
    this.editing.set(item);
    this.editorError.set('');
    this.form.reset({ officeCode: item.officeCode, officeName: item.officeName, addressLine1: item.addressLine1 ?? '', addressLine2: item.addressLine2 ?? '', city: item.city ?? '', stateOrProvince: item.stateOrProvince ?? '', postalCode: item.postalCode ?? '', countryCode: item.countryCode, timeZoneId: item.timeZoneId ?? '', isHeadOffice: item.isHeadOffice });
    this.form.controls.officeCode.disable();
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    if (this.saving()) return;
    this.editorOpen.set(false);
    this.editing.set(null);
  }

  showError(control: 'officeCode' | 'officeName' | 'countryCode'): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  save(): void {
    if (this.form.invalid || this.saving()) { this.form.markAllAsTouched(); return; }
    const companyId = this.selectedCompanyId();
    const value = this.form.getRawValue();
    const optional = (text: string) => text.trim() || null;
    const current = this.editing();
    const common = {
      officeName: value.officeName.trim(),
      addressLine1: optional(value.addressLine1),
      addressLine2: optional(value.addressLine2),
      city: optional(value.city),
      stateOrProvince: optional(value.stateOrProvince),
      postalCode: optional(value.postalCode),
      countryCode: value.countryCode.trim().toUpperCase(),
      timeZoneId: optional(value.timeZoneId),
      isHeadOffice: value.isHeadOffice
    };
    this.saving.set(true);
    this.editorError.set('');
    const request = current
      ? this.api.updateOffice(companyId, current.officeId, { ...common, rowVersion: current.rowVersion })
      : this.api.createOffice(companyId, { ...common, officeCode: value.officeCode.trim().toUpperCase() });
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.editorOpen.set(false);
        this.editing.set(null);
        this.notice.set(current ? 'Office updated successfully.' : 'Office created successfully.');
        this.loadOffices();
      },
      error: err => this.editorError.set(this.message(err, 'Unable to save the office.'))
    });
  }

  deactivate(item: Office): void {
    if (!item.isActive || this.busyId()) return;
    this.confirmError.set('');
    this.confirmTarget.set(item);
  }

  cancelDeactivate(): void {
    if (this.busyId()) return;
    this.confirmTarget.set(null);
    this.confirmError.set('');
  }

  confirmDeactivate(): void {
    const item = this.confirmTarget();
    if (!item || this.busyId()) return;
    this.busyId.set(item.officeId);
    this.confirmError.set('');
    this.api.deactivateOffice(this.selectedCompanyId(), item.officeId, item.rowVersion)
      .pipe(finalize(() => this.busyId.set('')))
      .subscribe({
        next: () => {
          this.confirmTarget.set(null);
          this.notice.set('Office deactivated successfully.');
          this.loadOffices();
        },
        error: err => this.confirmError.set(this.message(err, 'Unable to deactivate the office. Reload and check whether it was changed by another user.'))
      });
  }

  private message(error: any, fallback: string): string {
    if (error?.status === 409) return 'This record has changed or is in use. Refresh the list and try again.';
    if (error?.status === 403) return 'You do not have permission to perform this action.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}
