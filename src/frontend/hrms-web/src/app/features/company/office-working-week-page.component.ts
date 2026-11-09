import { Component, computed, inject, signal } from '@angular/core';
import { forkJoin, finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary, Office, WorkingWeek, WorkingWeekDay } from '../../core/company/company.models';

@Component({
  selector: 'hrms-office-working-week-page',
  standalone: true,
  template: `
    <div class="page-heading">
      <div>
        <div class="eyebrow">COMPANY CONFIGURATION</div>
        <h1>Office Working Weeks</h1>
        <p>Review the effective schedule for an office or configure an office-specific override.</p>
      </div>
      <button class="btn btn-primary" type="button" (click)="save()" [disabled]="!selectedCompanyId() || !selectedOfficeId() || saving() || loading() || !isDirty()">
        <i class="bi bi-check2 me-2"></i>{{ saving() ? 'Saving…' : 'Save office schedule' }}
      </button>
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
        <label for="officeSelect">Office</label>
        <select id="officeSelect" class="form-select form-select-sm" [value]="selectedOfficeId()" (change)="onOfficeChange($event)" [disabled]="!selectedCompanyId() || officesLoading() || offices().length === 0">
          <option value="">Select an office</option>
          @for (office of offices(); track office.officeId) {
            <option [value]="office.officeId">{{ office.officeName }} ({{ office.officeCode }})</option>
          }
        </select>
      </div>
    </section>

    @if (error()) { <div class="alert alert-danger">{{ error() }}</div> }
    @if (notice()) { <div class="alert alert-success">{{ notice() }}</div> }

    @if (companiesLoading()) {
      <div class="loading-state">Loading companies…</div>
    } @else if (companies().length === 0) {
      <div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account has no active company available for office schedule administration.</p></div>
    } @else if (!selectedCompanyId()) {
      <div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company to load its offices.</p></div>
    } @else if (officesLoading()) {
      <div class="loading-state">Loading offices…</div>
    } @else if (offices().length === 0) {
      <div class="empty-state card"><i class="bi bi-geo-alt"></i><h2>No active offices</h2><p>Create or activate an office before configuring an office-specific working week.</p></div>
    } @else if (!selectedOfficeId()) {
      <div class="empty-state card"><i class="bi bi-geo-alt"></i><h2>Select an office</h2><p>Choose an office to review its effective working week.</p></div>
    } @else {
      <section class="schedule-card card">
        <div class="section-heading">
          <div><strong>Effective weekly schedule</strong><small>{{ selectedOffice()?.officeName }} · Monday is day 1 and Sunday is day 7</small></div>
          @if (!loading()) {
            <span class="status-chip" [class.inherited]="inherited()">{{ inherited() ? 'Inherited from company' : 'Office override' }}</span>
          }
        </div>
        @if (loading()) {
          <div class="loading-state">Loading office and company schedules…</div>
        } @else {
          <div class="inheritance-note">
            <i class="bi bi-info-circle"></i>
            @if (inherited()) {
              This office currently follows the company working week. Change one or more days below and save to create an office-specific override.
            } @else {
              This office has its own schedule. Changes here will update the office override; the company default remains unchanged.
            }
          </div>
          <div class="day-list">
            @for (day of days(); track day.dayOfWeek) {
              <label class="day-row" [class.selected]="day.isWorkingDay">
                <span class="day-name">{{ dayNames[day.dayOfWeek - 1] }}</span>
                <span class="day-description">{{ day.isWorkingDay ? 'Working day' : 'Non-working day' }}</span>
                <input class="form-check-input" type="checkbox" [checked]="day.isWorkingDay" (change)="toggleDay(day.dayOfWeek, $event)" [disabled]="saving()" [attr.aria-label]="'Set ' + dayNames[day.dayOfWeek - 1] + ' as a working day'">
              </label>
            }
          </div>
          <div class="schedule-footer">
            <div class="schedule-hint"><i class="bi bi-shield-check me-2"></i>Only this office is affected. Other offices continue to use their own override or the company default.</div>
            <span class="working-count">{{ workingDayCount() }} working days / week</span>
          </div>
        }
      </section>
    }
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}.selection-card{display:grid!important;grid-template-columns:1fr 1fr;gap:1rem;padding:1rem}.selection-field{min-width:0}.selection-field label{display:block;margin-bottom:.3rem;color:#667085;font-size:.68rem;font-weight:600}.schedule-card{overflow:hidden}.section-heading{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.section-heading strong,.section-heading small{display:block}.section-heading strong{font-size:.84rem;color:#344054}.section-heading small{margin-top:.25rem;color:#98a2b3;font-size:.7rem}.status-chip{padding:.3rem .6rem;border-radius:2rem;background:#eff8ff;color:#175cd3;font-size:.66rem;font-weight:650;white-space:nowrap}.status-chip.inherited{background:#f2f4f7;color:#667085}.inheritance-note{display:flex;gap:.55rem;align-items:flex-start;padding:.8rem 1.15rem;background:#fafbfc;border-bottom:1px solid #eaecf0;color:#667085;font-size:.72rem;line-height:1.5}.inheritance-note i{color:#58739a}.day-list{padding:.35rem 1.15rem}.day-row{display:grid;grid-template-columns:minmax(7rem,1fr) 1fr auto;align-items:center;gap:1rem;padding:.9rem .25rem;border-bottom:1px solid #f0f1f3;cursor:pointer}.day-row:last-child{border-bottom:0}.day-name{font-size:.78rem;font-weight:650;color:#344054}.day-description{font-size:.72rem;color:#98a2b3}.day-row.selected .day-description{color:#475467}.form-check-input{width:2.1rem;height:1.15rem;margin:0;cursor:pointer}.schedule-footer{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:.9rem 1.15rem;background:#fafbfc;border-top:1px solid #eaecf0}.schedule-hint{font-size:.68rem;line-height:1.5;color:#667085}.working-count{font-size:.72rem;font-weight:650;color:#475467;white-space:nowrap}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}.empty-state{display:flex;align-items:center;flex-direction:column;text-align:center;padding:3rem 1.5rem;color:#667085}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}@media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.selection-card{grid-template-columns:1fr}.day-row{grid-template-columns:1fr auto}.day-description{display:none}.schedule-footer{align-items:flex-start;flex-direction:column}}
  `]
})
export class OfficeWorkingWeekPageComponent {
  private readonly api = inject(CompanyApiService);
  readonly dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  readonly companies = signal<CompanySummary[]>([]);
  readonly offices = signal<Office[]>([]);
  readonly selectedCompanyId = signal('');
  readonly selectedOfficeId = signal('');
  readonly days = signal<WorkingWeekDay[]>(this.createDefaultWeekdays());
  readonly originalDays = signal<WorkingWeekDay[]>(this.createDefaultWeekdays());
  readonly companiesLoading = signal(true);
  readonly officesLoading = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly inherited = signal(true);
  readonly error = signal('');
  readonly notice = signal('');
  readonly selectedOffice = computed(() => this.offices().find(item => item.officeId === this.selectedOfficeId()) ?? null);
  readonly workingDayCount = computed(() => this.days().filter(day => day.isWorkingDay).length);
  readonly isDirty = computed(() => JSON.stringify(this.days()) !== JSON.stringify(this.originalDays()));

  constructor() {
    this.api.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: items => {
        const active = items.filter(company => company.isActive);
        this.companies.set(active);
        if (active.length === 1) {
          this.selectedCompanyId.set(active[0].companyId);
          this.loadOffices();
        }
      },
      error: error => this.error.set(this.getError(error, 'Unable to load companies.'))
    });
  }

  onCompanyChange(event: Event): void {
    this.selectedCompanyId.set((event.target as HTMLSelectElement).value);
    this.selectedOfficeId.set('');
    this.offices.set([]);
    this.setDays(this.createDefaultWeekdays());
    this.inherited.set(true);
    this.error.set('');
    this.notice.set('');
    if (this.selectedCompanyId()) this.loadOffices();
  }

  onOfficeChange(event: Event): void {
    this.selectedOfficeId.set((event.target as HTMLSelectElement).value);
    this.error.set('');
    this.notice.set('');
    if (this.selectedOfficeId()) this.loadWorkingWeek();
  }

  loadOffices(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId) return;
    this.officesLoading.set(true);
    this.api.getOffices(companyId).pipe(finalize(() => this.officesLoading.set(false))).subscribe({
      next: items => {
        const active = items.filter(office => office.isActive);
        this.offices.set(active);
        if (active.length === 1) {
          this.selectedOfficeId.set(active[0].officeId);
          this.loadWorkingWeek();
        }
      },
      error: error => this.error.set(this.getError(error, 'Unable to load offices.'))
    });
  }

  loadWorkingWeek(): void {
    const companyId = this.selectedCompanyId();
    const officeId = this.selectedOfficeId();
    if (!companyId || !officeId) return;
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      office: this.api.getOfficeWorkingWeek(companyId, officeId),
      company: this.api.getCompanyWorkingWeek(companyId)
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        const companyDays = this.normalizeDays(result.company.days);
        const effectiveDays = this.normalizeDays(result.office.days);
        const inherited = result.office.isInherited;
        this.inherited.set(inherited);
        this.setDays(effectiveDays ?? companyDays ?? this.createDefaultWeekdays());
      },
      error: error => this.error.set(this.getError(error, 'Unable to load the office working week.'))
    });
  }

  toggleDay(dayOfWeek: number, event: Event): void {
    const isWorkingDay = (event.target as HTMLInputElement).checked;
    this.days.update(items => items.map(day => day.dayOfWeek === dayOfWeek ? { ...day, isWorkingDay } : day));
  }

  save(): void {
    const companyId = this.selectedCompanyId();
    const officeId = this.selectedOfficeId();
    if (!companyId || !officeId || this.saving() || !this.isDirty()) return;
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    this.api.saveOfficeWorkingWeek(companyId, officeId, { days: this.days() })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => {
          this.inherited.set(result.isInherited);
          this.setDays(this.normalizeDays(result.days) ?? this.days());
          this.notice.set('Office working week saved successfully.');
        },
        error: error => this.error.set(this.getError(error, 'Unable to save the office working week.'))
      });
  }

  private normalizeDays(days: WorkingWeekDay[] | null | undefined): WorkingWeekDay[] | null {
    if (!days || days.length !== 7) return null;
    return [...days].sort((a, b) => a.dayOfWeek - b.dayOfWeek);
  }

  private createDefaultWeekdays(): WorkingWeekDay[] {
    return Array.from({ length: 7 }, (_, index) => ({ dayOfWeek: index + 1, isWorkingDay: index < 5 }));
  }

  private setDays(days: WorkingWeekDay[]): void {
    const copy = days.map(day => ({ ...day }));
    this.days.set(copy);
    this.originalDays.set(copy.map(day => ({ ...day })));
  }

  private getError(error: any, fallback: string): string {
    if (error?.status === 403) return 'You do not have permission to manage office working weeks.';
    if (error?.status === 404) return 'The selected company or office was not found or is not available to your account.';
    if (error?.status === 409) return 'The working week was changed by another user. Refresh and try again.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}
