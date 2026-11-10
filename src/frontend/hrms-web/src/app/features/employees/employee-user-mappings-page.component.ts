import { Component, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary } from '../../core/company/company.models';
import { EmployeeApiService } from '../../core/company/employee-api.service';
import { AvailableTenantUser, Employee, EmployeeUserMapping } from '../../core/company/employee.models';

@Component({
  selector: 'hrms-employee-user-mappings-page',
  standalone: true,
  template: `
    <div class="page-heading">
      <div>
        <div class="eyebrow">WORKFORCE SECURITY</div>
        <h1>Employee-to-User Mapping</h1>
        <p>Explicitly link an employee profile to an existing user account in this tenant.</p>
      </div>
    </div>

    <section class="selection-card card mb-3">
      <div class="selection-field">
        <label for="companySelect">Company</label>
        <select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length === 0">
          <option value="">Select a company</option>
          @for (company of companies(); track company.companyId) {
            <option [value]="company.companyId">{{ company.displayName }} ({{ company.companyCode }})</option>
          }
        </select>
      </div>
      <div class="selection-field">
        <label for="employeeSelect">Employee</label>
        <select id="employeeSelect" class="form-select form-select-sm" [value]="selectedEmployeeId()" (change)="onEmployeeChange($event)" [disabled]="!selectedCompanyId() || employeesLoading() || employees().length === 0">
          <option value="">Select an employee</option>
          @for (employee of employees(); track employee.employeeId) {
            <option [value]="employee.employeeId">{{ employee.employeeCode }} · {{ displayName(employee) }}</option>
          }
        </select>
      </div>
    </section>

    @if (error()) { <div class="alert alert-danger" role="alert">{{ error() }}</div> }
    @if (notice()) { <div class="alert alert-success" role="status">{{ notice() }}</div> }

    @if (companiesLoading()) {
      <div class="loading-state">Loading companies…</div>
    } @else if (companies().length === 0) {
      <div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account has no active company available for employee mapping administration.</p></div>
    } @else if (!selectedCompanyId()) {
      <div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company to load its employees.</p></div>
    } @else if (employeesLoading()) {
      <div class="loading-state">Loading employees…</div>
    } @else if (employees().length === 0) {
      <div class="empty-state card"><i class="bi bi-people"></i><h2>No active employees</h2><p>Create or activate an employee before assigning a user account.</p></div>
    } @else if (!selectedEmployeeId()) {
      <div class="empty-state card"><i class="bi bi-person-check"></i><h2>Select an employee</h2><p>Choose an employee to inspect or manage their user account mapping.</p></div>
    } @else if (mappingLoading()) {
      <div class="loading-state">Loading employee mapping…</div>
    } @else {
      <section class="mapping-card card">
        <div class="section-heading">
          <div><strong>Account mapping</strong><small>{{ selectedEmployee()?.employeeCode }} · {{ selectedEmployee() ? displayName(selectedEmployee()!) : '' }}</small></div>
          <span class="status-chip" [class.unmapped]="!mapping()"> {{ mapping() ? 'Mapped' : 'Not mapped' }} </span>
        </div>

        @if (mapping()) {
          <div class="current-mapping">
            <div class="identity-icon"><i class="bi bi-person-check"></i></div>
            <div class="identity-copy">
              <strong>Linked user account</strong>
              <span class="user-id">{{ mapping()?.userId }}</span>
              <small>Mapping ID: {{ mapping()?.employeeUserMappingId }}</small>
            </div>
            @if (!confirmDeactivate()) {
              <button class="btn btn-outline-danger btn-sm" type="button" (click)="confirmDeactivate.set(true)" [disabled]="saving()"><i class="bi bi-link-45deg me-1"></i>Unmap user</button>
            }
          </div>
          @if (confirmDeactivate()) {
            <div class="confirm-panel">
              <div><strong>Deactivate this mapping?</strong><small>The employee will no longer be linked to this user. The user account itself will not be changed.</small></div>
              <div class="confirm-actions">
                <button class="btn btn-light btn-sm" type="button" (click)="confirmDeactivate.set(false)" [disabled]="saving()">Cancel</button>
                <button class="btn btn-danger btn-sm" type="button" (click)="deactivateMapping()" [disabled]="saving()">{{ saving() ? 'Unmapping…' : 'Confirm unmap' }}</button>
              </div>
            </div>
          }
        } @else {
          <div class="unmapped-note"><i class="bi bi-info-circle"></i><span>No user account is currently linked to this employee. Select an available tenant user below to create an explicit mapping.</span></div>
          <div class="user-picker">
            <div class="picker-heading"><div><strong>Available tenant users</strong><small>Only enabled users with an active membership in this tenant are listed. Already-mapped users are excluded.</small></div></div>
            <div class="picker-search">
              <input class="form-control form-control-sm" aria-label="Search available users" placeholder="Search by name or email" [value]="userSearch()" (input)="userSearch.set($any($event.target).value)" (keyup.enter)="loadAvailableUsers()">
              <button class="btn btn-light btn-sm" type="button" (click)="loadAvailableUsers()" [disabled]="usersLoading()"><i class="bi bi-search me-1"></i>Search</button>
            </div>
            @if (usersLoading()) {
              <div class="loading-state">Loading available users…</div>
            } @else if (availableUsers().length === 0) {
              <div class="empty-inline"><i class="bi bi-person-x"></i><strong>No available users found</strong><span>Ensure the intended user has an active tenant membership, or change the search.</span></div>
            } @else {
              <div class="user-list">
                @for (user of availableUsers(); track user.userId) {
                  <label class="user-option" [class.selected]="selectedUserId() === user.userId">
                    <input class="form-check-input" type="radio" name="tenantUser" [value]="user.userId" [checked]="selectedUserId() === user.userId" (change)="selectedUserId.set(user.userId)">
                    <span class="user-avatar"><i class="bi bi-person"></i></span>
                    <span class="user-details"><strong>{{ user.displayName }}</strong><small>{{ user.email }}</small></span>
                  </label>
                }
              </div>
              <div class="picker-footer">
                <span>{{ availableUsers().length }} available users</span>
                <button class="btn btn-primary btn-sm" type="button" (click)="createMapping()" [disabled]="!selectedUserId() || saving()"><i class="bi bi-link-45deg me-1"></i>{{ saving() ? 'Mapping…' : 'Map selected user' }}</button>
              </div>
            }
          </div>
        }
      </section>
    }
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}.selection-card{display:grid!important;grid-template-columns:1fr 1fr;gap:1rem;padding:1rem}.selection-field{min-width:0}.selection-field label{display:block;margin-bottom:.3rem;color:#667085;font-size:.68rem;font-weight:600}.mapping-card{overflow:hidden}.section-heading{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.section-heading strong,.section-heading small{display:block}.section-heading strong{font-size:.84rem;color:#344054}.section-heading small{margin-top:.25rem;color:#98a2b3;font-size:.7rem}.status-chip{padding:.3rem .6rem;border-radius:2rem;background:#ecfdf3;color:#027a48;font-size:.66rem;font-weight:650;white-space:nowrap}.status-chip.unmapped{background:#f2f4f7;color:#667085}.current-mapping{display:flex;align-items:center;gap:.85rem;padding:1.25rem}.identity-icon{display:grid;place-items:center;width:2.7rem;height:2.7rem;border-radius:.6rem;background:#eef3fa;color:#315b91;font-size:1.2rem}.identity-copy{display:flex;flex:1;min-width:0;flex-direction:column;gap:.18rem}.identity-copy strong{font-size:.8rem;color:#344054}.identity-copy small{font-size:.66rem;color:#98a2b3}.user-id{font-size:.72rem;color:#475467;overflow-wrap:anywhere}.unmapped-note{display:flex;align-items:flex-start;gap:.6rem;padding:1rem 1.15rem;background:#fafbfc;border-bottom:1px solid #eaecf0;color:#667085;font-size:.73rem;line-height:1.5}.unmapped-note i{color:#58739a}.user-picker{padding:1.1rem 1.15rem}.picker-heading strong,.picker-heading small{display:block}.picker-heading strong{font-size:.8rem;color:#344054}.picker-heading small{margin-top:.25rem;color:#98a2b3;font-size:.68rem;line-height:1.5}.picker-search{display:flex;gap:.5rem;margin:1rem 0 .65rem}.picker-search input{flex:1}.user-list{max-height:22rem;overflow:auto;border:1px solid #eaecf0;border-radius:.5rem}.user-option{display:flex;align-items:center;gap:.7rem;padding:.75rem .85rem;border-bottom:1px solid #f0f1f3;cursor:pointer}.user-option:last-child{border-bottom:0}.user-option.selected{background:#f5f8fc}.user-option>.form-check-input{margin:0}.user-avatar{display:grid;place-items:center;width:2rem;height:2rem;border-radius:50%;background:#eef2f7;color:#475467}.user-details{display:flex;min-width:0;flex-direction:column;gap:.15rem}.user-details strong{font-size:.75rem;color:#344054}.user-details small{font-size:.68rem;color:#667085;overflow-wrap:anywhere}.picker-footer{display:flex;align-items:center;justify-content:space-between;gap:1rem;padding-top:.9rem;color:#667085;font-size:.68rem}.confirm-panel{display:flex;justify-content:space-between;align-items:center;gap:1rem;margin:0 1.15rem 1.15rem;padding:.85rem;border:1px solid #fecdca;border-radius:.5rem;background:#fffbfa}.confirm-panel>div:first-child{display:flex;flex-direction:column;gap:.2rem}.confirm-panel strong{font-size:.75rem;color:#912018}.confirm-panel small{font-size:.67rem;color:#667085;line-height:1.4}.confirm-actions{display:flex;gap:.45rem;flex-shrink:0}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}.empty-state,.empty-inline{display:flex;align-items:center;flex-direction:column;text-align:center;color:#667085}.empty-state{padding:3rem 1.5rem}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}.empty-inline{gap:.35rem;padding:1.5rem}.empty-inline i{font-size:1.35rem;color:#98a2b3}.empty-inline strong{font-size:.75rem;color:#344054}.empty-inline span{font-size:.68rem;line-height:1.5}@media(max-width:575.98px){.selection-card{grid-template-columns:1fr}.current-mapping{align-items:flex-start;flex-wrap:wrap}.current-mapping>.btn{width:100%}.picker-footer,.confirm-panel{align-items:stretch;flex-direction:column}.confirm-actions>*{flex:1}}
  `]
})
export class EmployeeUserMappingsPageComponent {
  private readonly companyApi = inject(CompanyApiService);
  private readonly employeeApi = inject(EmployeeApiService);
  readonly companies = signal<CompanySummary[]>([]);
  readonly employees = signal<Employee[]>([]);
  readonly availableUsers = signal<AvailableTenantUser[]>([]);
  readonly selectedCompanyId = signal('');
  readonly selectedEmployeeId = signal('');
  readonly selectedUserId = signal('');
  readonly userSearch = signal('');
  readonly mapping = signal<EmployeeUserMapping | null>(null);
  readonly companiesLoading = signal(true);
  readonly employeesLoading = signal(false);
  readonly mappingLoading = signal(false);
  readonly usersLoading = signal(false);
  readonly saving = signal(false);
  readonly confirmDeactivate = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly selectedEmployee = computed(() => this.employees().find(item => item.employeeId === this.selectedEmployeeId()) ?? null);

  constructor() {
    this.companyApi.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: items => {
        const active = items.filter(item => item.isActive);
        this.companies.set(active);
        if (active.length === 1) {
          this.selectedCompanyId.set(active[0].companyId);
          this.loadEmployees();
        }
      },
      error: error => this.error.set(this.getError(error, 'Unable to load companies.'))
    });
  }

  onCompanyChange(event: Event): void {
    this.selectedCompanyId.set((event.target as HTMLSelectElement).value);
    this.selectedEmployeeId.set('');
    this.employees.set([]);
    this.mapping.set(null);
    this.availableUsers.set([]);
    this.error.set('');
    this.notice.set('');
    if (this.selectedCompanyId()) this.loadEmployees();
  }

  onEmployeeChange(event: Event): void {
    this.selectedEmployeeId.set((event.target as HTMLSelectElement).value);
    this.mapping.set(null);
    this.availableUsers.set([]);
    this.selectedUserId.set('');
    this.confirmDeactivate.set(false);
    this.error.set('');
    this.notice.set('');
    if (this.selectedEmployeeId()) this.loadMapping();
  }

  loadEmployees(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId) return;
    this.employeesLoading.set(true);
    this.employeeApi.getEmployees(companyId).pipe(finalize(() => this.employeesLoading.set(false))).subscribe({
      next: items => this.employees.set(items.filter(item => item.isActive)),
      error: error => this.error.set(this.getError(error, 'Unable to load employees.'))
    });
  }

  loadMapping(): void {
    const companyId = this.selectedCompanyId();
    const employeeId = this.selectedEmployeeId();
    if (!companyId || !employeeId) return;
    this.mappingLoading.set(true);
    this.error.set('');
    this.employeeApi.getEmployeeUserMapping(companyId, employeeId).pipe(finalize(() => this.mappingLoading.set(false))).subscribe({
      next: result => this.mapping.set(result),
      error: error => {
        if (error?.status === 404) {
          this.mapping.set(null);
          this.loadAvailableUsers();
        } else {
          this.error.set(this.getError(error, 'Unable to load the employee mapping.'));
        }
      }
    });
  }

  loadAvailableUsers(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId || !this.selectedEmployeeId() || this.mapping()) return;
    this.usersLoading.set(true);
    this.error.set('');
    this.employeeApi.getAvailableTenantUsers(companyId, this.userSearch()).pipe(finalize(() => this.usersLoading.set(false))).subscribe({
      next: users => {
        this.availableUsers.set(users);
        if (!users.some(user => user.userId === this.selectedUserId())) this.selectedUserId.set('');
      },
      error: error => this.error.set(this.getError(error, 'Unable to load available tenant users.'))
    });
  }

  createMapping(): void {
    const companyId = this.selectedCompanyId();
    const employeeId = this.selectedEmployeeId();
    const userId = this.selectedUserId();
    if (!companyId || !employeeId || !userId || this.saving() || this.mapping()) return;
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    this.employeeApi.createEmployeeUserMapping(companyId, { employeeId, userId }).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.notice.set('Employee successfully mapped to the selected user account.');
        this.loadMapping();
      },
      error: error => this.error.set(this.getError(error, 'Unable to create the mapping. The employee or user may already be mapped.'))
    });
  }

  deactivateMapping(): void {
    const companyId = this.selectedCompanyId();
    const mapping = this.mapping();
    if (!companyId || !mapping || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    this.employeeApi.deactivateEmployeeUserMapping(companyId, mapping.employeeUserMappingId, { rowVersion: mapping.rowVersion })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.mapping.set(null);
          this.confirmDeactivate.set(false);
          this.selectedUserId.set('');
          this.notice.set('Employee mapping deactivated. The user account remains unchanged.');
          this.loadAvailableUsers();
        },
        error: error => this.error.set(this.getError(error, 'Unable to deactivate the mapping. Refresh and try again.'))
      });
  }

  displayName(employee: Employee): string {
    return [employee.firstName, employee.middleName, employee.lastName].filter(Boolean).join(' ');
  }

  private getError(error: any, fallback: string): string {
    if (error?.status === 403) return 'You do not have permission to manage employee-to-user mappings.';
    if (error?.status === 404) return 'The selected company, employee or mapping was not found.';
    if (error?.status === 409) return 'The mapping changed or the selected user is already mapped. Refresh and try again.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}
