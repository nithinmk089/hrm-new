import { Component, computed, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary, WorkingWeekDay } from '../../core/company/company.models';

@Component({
  selector: 'hrms-working-week-page',
  standalone: true,
  template: `
    <div class="page-heading">
      <div>
        <div class="eyebrow">COMPANY CONFIGURATION</div>
        <h1>Working Week</h1>
        <p>Define the standard working days used for attendance, leave and timesheet calculations.</p>
      </div>
      <button class="btn btn-primary" type="button" (click)="save()" [disabled]="!selectedCompanyId() || saving() || loading() || !isDirty()">
        <i class="bi bi-check2 me-2"></i>{{ saving() ? 'Saving…' : 'Save changes' }}
      </button>
    </div>

    <section class="company-context card mb-3">
      <div class="context-icon"><i class="bi bi-building"></i></div>
      <div class="context-copy">
        <label for="companySelect">Company</label>
        <select id="companySelect" class="form-select form-select-sm" [value]="selectedCompanyId()" (change)="onCompanyChange($event)" [disabled]="companiesLoading() || companies().length === 0">
          <option value="">Select a company</option>
          @for (company of companies(); track company.companyId) {
            <option [value]="company.companyId">{{ company.displayName }} ({{ company.companyCode }})</option>
          }
        </select>
      </div>
      @if (selectedCompanyId()) {
        <span class="context-count">{{ workingDayCount() }} working days / week</span>
      }
    </section>

    @if (error()) { <div class="alert alert-danger">{{ error() }}</div> }
    @if (notice()) { <div class="alert alert-success">{{ notice() }}</div> }

    @if (companiesLoading()) {
      <div class="loading-state">Loading companies…</div>
    } @else if (companies().length === 0) {
      <div class="empty-state card"><i class="bi bi-building"></i><h2>No companies available</h2><p>Your account has no active company available for working-week administration.</p></div>
    } @else if (!selectedCompanyId()) {
      <div class="empty-state card"><i class="bi bi-arrow-up-circle"></i><h2>Select a company</h2><p>Choose a company to configure its standard working week.</p></div>
    } @else {
      <section class="schedule-card card">
        <div class="section-heading">
          <div><strong>Weekly schedule</strong><small>Choose the days on which the company normally operates.</small></div>
          <span class="config-chip" [class.unconfigured]="!configured()">{{ configured() ? 'Configured' : 'Not configured' }}</span>
        </div>
        @if (loading()) {
          <div class="loading-state">Loading working week…</div>
        } @else {
          <div class="day-list">
            @for (day of days(); track day.dayOfWeek) {
              <label class="day-row" [class.selected]="day.isWorkingDay">
                <span class="day-name">{{ dayNames[day.dayOfWeek - 1] }}</span>
                <span class="day-description">{{ day.isWorkingDay ? 'Working day' : 'Non-working day' }}</span>
                <input class="form-check-input" type="checkbox" [checked]="day.isWorkingDay" (change)="toggleDay(day.dayOfWeek, $event)" [attr.aria-label]="'Set ' + dayNames[day.dayOfWeek - 1] + ' as a working day'">
              </label>
            }
          </div>
          <div class="schedule-footer">
            <div class="schedule-hint"><i class="bi bi-info-circle me-2"></i>Monday is day 1 and Sunday is day 7. Office-specific schedules can override this company default.</div>
            <button class="btn btn-light btn-sm" type="button" (click)="resetToWeekdays()" [disabled]="saving()"><i class="bi bi-arrow-counterclockwise me-1"></i>Reset to Monday–Friday</button>
          </div>
        }
      </section>
    }
  `,
  styles: [`
    :host{display:block}.page-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.35rem}.eyebrow{font-size:.65rem;font-weight:700;letter-spacing:.1em;color:#58739a}.page-heading h1{font-size:1.45rem;font-weight:650;color:#1d2939;margin:.35rem 0}.page-heading p{font-size:.82rem;color:#667085;margin:0}.company-context{display:flex!important;flex-direction:row!important;align-items:center!important;gap:.85rem;padding:.9rem 1rem;min-height:5.25rem}.context-icon{display:grid;place-items:center;width:2.6rem;height:2.6rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.1rem}.context-copy{flex:1;max-width:28rem;min-width:14rem}.context-copy label{display:block;margin-bottom:.25rem;color:#667085;font-size:.67rem;font-weight:600}.context-count{margin-left:auto;color:#667085;font-size:.72rem;white-space:nowrap}.schedule-card{overflow:hidden}.section-heading{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:1rem 1.15rem;border-bottom:1px solid #eaecf0}.section-heading strong,.section-heading small{display:block}.section-heading strong{font-size:.84rem;color:#344054}.section-heading small{margin-top:.25rem;color:#98a2b3;font-size:.7rem}.config-chip{padding:.3rem .6rem;border-radius:2rem;background:#ecfdf3;color:#027a48;font-size:.66rem;font-weight:650;white-space:nowrap}.config-chip.unconfigured{background:#f2f4f7;color:#667085}.day-list{padding:.35rem 1.15rem}.day-row{display:grid;grid-template-columns:minmax(7rem,1fr) 1fr auto;align-items:center;gap:1rem;padding:.9rem .25rem;border-bottom:1px solid #f0f1f3;cursor:pointer}.day-row:last-child{border-bottom:0}.day-name{font-size:.78rem;font-weight:650;color:#344054}.day-description{font-size:.72rem;color:#98a2b3}.day-row.selected .day-description{color:#475467}.form-check-input{width:2.1rem;height:1.15rem;margin:0;cursor:pointer}.schedule-footer{display:flex;justify-content:space-between;align-items:center;gap:1rem;padding:.9rem 1.15rem;background:#fafbfc;border-top:1px solid #eaecf0}.schedule-hint{font-size:.68rem;line-height:1.5;color:#667085}.loading-state{padding:1.5rem;text-align:center;color:#667085;font-size:.78rem}.empty-state{display:flex;align-items:center;flex-direction:column;text-align:center;padding:3rem 1.5rem;color:#667085}.empty-state>i{font-size:1.8rem;color:#7890b2}.empty-state h2{margin:.8rem 0 .35rem;color:#344054;font-size:1rem}.empty-state p{max-width:34rem;margin:0;font-size:.78rem;line-height:1.6}@media(max-width:575.98px){.page-heading{flex-direction:column}.page-heading>.btn{width:100%}.context-count{display:none}.day-row{grid-template-columns:1fr auto}.day-description{display:none}.schedule-footer{align-items:flex-start;flex-direction:column}}
  `]
})
export class WorkingWeekPageComponent {
  private readonly api = inject(CompanyApiService);
  readonly dayNames = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  readonly companies = signal<CompanySummary[]>([]);
  readonly selectedCompanyId = signal('');
  readonly days = signal<WorkingWeekDay[]>(this.createDefaultWeekdays());
  readonly originalDays = signal<WorkingWeekDay[]>(this.createDefaultWeekdays());
  readonly companiesLoading = signal(true);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly configured = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly workingDayCount = computed(() => this.days().filter(day => day.isWorkingDay).length);
  readonly isDirty = computed(() => !this.configured() || JSON.stringify(this.days()) !== JSON.stringify(this.originalDays()));

  constructor() {
    this.api.getCompanies().pipe(finalize(() => this.companiesLoading.set(false))).subscribe({
      next: items => {
        const active = items.filter(company => company.isActive);
        this.companies.set(active);
        if (active.length === 1) {
          this.selectedCompanyId.set(active[0].companyId);
          this.loadWorkingWeek();
        }
      },
      error: error => this.error.set(this.getError(error, 'Unable to load companies.'))
    });
  }

  onCompanyChange(event: Event): void {
    const companyId = (event.target as HTMLSelectElement).value;
    this.selectedCompanyId.set(companyId);
    this.error.set('');
    this.notice.set('');
    this.configured.set(false);
    this.setDays(this.createDefaultWeekdays());
    if (companyId) this.loadWorkingWeek();
  }

  loadWorkingWeek(): void {
    const companyId = this.selectedCompanyId();
    if (!companyId) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getCompanyWorkingWeek(companyId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: result => {
        const fallback = this.createDefaultWeekdays();
        const normalized = result.days?.length === 7
          ? [...result.days].sort((a, b) => a.dayOfWeek - b.dayOfWeek)
          : fallback;
        this.configured.set(result.isConfigured);
        this.setDays(normalized);
      },
      error: error => this.error.set(this.getError(error, 'Unable to load the working week.'))
    });
  }

  toggleDay(dayOfWeek: number, event: Event): void {
    const isWorkingDay = (event.target as HTMLInputElement).checked;
    this.days.update(items => items.map(day => day.dayOfWeek === dayOfWeek ? { ...day, isWorkingDay } : day));
  }

  resetToWeekdays(): void {
    this.setDays(this.createDefaultWeekdays());
  }

  save(): void {
    if (!this.selectedCompanyId() || this.saving() || !this.isDirty()) return;
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    this.api.saveCompanyWorkingWeek(this.selectedCompanyId(), { days: this.days() })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: result => {
          this.configured.set(result.isConfigured);
          const saved = result.days?.length === 7
            ? [...result.days].sort((a, b) => a.dayOfWeek - b.dayOfWeek)
            : this.days();
          this.setDays(saved);
          this.notice.set('Working week saved successfully.');
        },
        error: error => this.error.set(this.getError(error, 'Unable to save the working week.'))
      });
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
    if (error?.status === 403) return 'You do not have permission to manage the working week.';
    if (error?.status === 404) return 'The selected company was not found or is not available to your account.';
    if (error?.status === 409) return 'The working week was changed by another user. Refresh and try again.';
    return error?.error?.detail || error?.error?.title || error?.error?.message || fallback;
  }
}
