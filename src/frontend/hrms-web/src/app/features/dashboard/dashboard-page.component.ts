import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'hrms-dashboard-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-header">
      <div>
        <div class="eyebrow">WORKSPACE OVERVIEW</div>
        <h1>Dashboard</h1>
        <p>Your HR workspace at a glance.</p>
      </div>
      <div class="date-label"><i class="bi bi-calendar3"></i>{{ todayLabel }}</div>
    </div>

    <section class="welcome-panel card mb-4">
      <div class="welcome-copy">
        <div class="welcome-label">HRMS WORKFORCE PLATFORM</div>
        <h2>Welcome back, {{ displayName() }}</h2>
        <p>Access the tools and information available in your workspace. Your dashboard will grow as each HRMS module is connected.</p>
      </div>
      <div class="welcome-art" aria-hidden="true"><i class="bi bi-buildings"></i></div>
    </section>

    <section class="workspace-card card mb-4" aria-label="Current workspace">
      <div class="workspace-icon"><i class="bi bi-building"></i></div>
      <div class="workspace-details">
        <span class="field-label">CURRENT WORKSPACE</span>
        <strong>{{ tenantName() }}</strong>
        <small>Workspace code: {{ tenantCode() }}</small>
      </div>
      <div class="workspace-status"><span></span> Connected</div>
    </section>

    <div class="section-heading">
      <div><h2>Quick access</h2><p>Navigate to an HRMS area</p></div>
      <span>AVAILABLE MODULES</span>
    </div>

    <div class="row g-3">
      @for (item of shortcuts; track item.title) {
        <div class="col-12 col-md-6 col-xl-3">
          <a class="shortcut-card card h-100" [routerLink]="item.route">
            <span class="shortcut-icon"><i class="bi" [class]="item.icon"></i></span>
            <strong>{{ item.title }}</strong>
            <small>{{ item.description }}</small>
            <span class="shortcut-arrow"><i class="bi bi-arrow-up-right"></i></span>
          </a>
        </div>
      }
    </div>

    <section class="information-panel mt-4">
      <i class="bi bi-info-circle"></i>
      <div>
        <strong>Dashboard insights are being prepared</strong>
        <p>Employee totals, leave summaries and approval counts will appear here when dedicated tenant-scoped dashboard data is available. No sample or estimated business figures are displayed.</p>
      </div>
    </section>
  `,
  styles: [`
    :host{display:block}
    .page-header{display:flex;align-items:flex-start;justify-content:space-between;gap:1rem;margin-bottom:1.5rem}
    .eyebrow,.welcome-label{color:#58739a;font-size:.66rem;font-weight:700;letter-spacing:.1em}
    .page-header h1{margin:.35rem 0 0;color:#1d2939;font-size:1.45rem;font-weight:650}
    .page-header p{margin:.4rem 0 0;color:#667085;font-size:.82rem}
    .date-label{display:flex;align-items:center;gap:.45rem;padding:.5rem .7rem;border:1px solid #e4e7ec;border-radius:.45rem;background:#fff;color:#667085;font-size:.74rem}
    .welcome-panel{display:flex;flex-direction:row;align-items:center;justify-content:space-between;text-align:left;min-height:12rem;padding:2rem 2.25rem;background:linear-gradient(110deg,#fff,#f7f9fc);overflow:hidden}
    .welcome-copy{max-width:46rem}
    .welcome-panel h2{margin:.65rem 0 .45rem;color:#1d2939;font-size:1.35rem;font-weight:650}
    .welcome-panel p{max-width:40rem;margin:0;color:#667085;font-size:.82rem;line-height:1.65}
    .welcome-art{display:grid;place-items:center;width:7rem;height:7rem;flex:0 0 7rem;margin-right:2rem;border-radius:2rem;background:#eaf0f8;color:#46658e;font-size:3rem;transform:rotate(-8deg)}
    .workspace-card{display:flex;flex-direction:row;align-items:center;justify-content:flex-start;text-align:left;gap:.9rem;padding:1rem 1.15rem}
    .workspace-icon{display:grid;place-items:center;width:2.8rem;height:2.8rem;flex:0 0 2.8rem;border-radius:.6rem;background:#eef3fa;color:#3d5e88;font-size:1.15rem}
    .workspace-details{display:flex;min-width:0;flex:1;flex-direction:column;align-items:flex-start;text-align:left;gap:.18rem}
    .field-label{color:#98a2b3;font-size:.61rem;font-weight:700;letter-spacing:.08em}
    .workspace-details strong{overflow:hidden;color:#344054;font-size:.82rem;text-overflow:ellipsis;white-space:nowrap}
    .workspace-details small{color:#7a8494;font-size:.7rem}
    .workspace-status{display:flex;align-items:center;gap:.4rem;color:#667085;font-size:.7rem}
    .workspace-status span{width:.42rem;height:.42rem;border-radius:50%;background:#12b76a}
    .section-heading{display:flex;align-items:flex-end;justify-content:space-between;margin:1.7rem 0 .85rem}
    .section-heading h2{margin:0;color:#344054;font-size:.95rem;font-weight:650}
    .section-heading p{margin:.25rem 0 0;color:#98a2b3;font-size:.72rem}
    .section-heading>span{color:#98a2b3;font-size:.62rem;font-weight:700;letter-spacing:.08em}
    .shortcut-card{position:relative;gap:.5rem;padding:1.15rem;color:#27364b;text-decoration:none;transition:transform .15s,border-color .15s}
    .shortcut-card:hover{transform:translateY(-2px);border-color:#c6d4e7;color:#27364b}
    .shortcut-card:focus-visible{outline:3px solid rgb(49 85 143 / 25%);outline-offset:2px}
    .shortcut-icon{display:grid;place-items:center;width:2.35rem;height:2.35rem;margin-bottom:.3rem;border-radius:.55rem;background:#eef3fa;color:#3d5e88;font-size:1.05rem}
    .shortcut-card strong{font-size:.82rem}
    .shortcut-card small{max-width:14rem;color:#7a8494;font-size:.72rem;line-height:1.5}
    .shortcut-arrow{position:absolute;top:1.15rem;right:1.1rem;color:#98a2b3}
    .information-panel{display:flex;align-items:flex-start;gap:.7rem;padding:1rem;border:1px solid #e2e8f0;border-radius:.55rem;background:#f9fafb;color:#667085}
    .information-panel>i{margin-top:.1rem;color:#58739a}
    .information-panel strong{color:#475467;font-size:.76rem}
    .information-panel p{margin:.25rem 0 0;font-size:.72rem;line-height:1.55}
    @media(max-width:575.98px){.welcome-panel{padding:1.4rem}.welcome-art{display:none}.date-label{font-size:0}.date-label i{font-size:.9rem}.workspace-status{font-size:0}.workspace-status span{width:.5rem;height:.5rem}}
  `]
})
export class DashboardPageComponent {
  private readonly auth = inject(AuthService);
  private readonly session = this.auth.session;

  readonly displayName = computed(() => this.session()?.displayName?.trim() || 'there');
  readonly tenantName = computed(() => this.session()?.tenantName?.trim() || 'Workspace');
  readonly tenantCode = computed(() => this.session()?.tenantCode || '—');
  readonly todayLabel = new Intl.DateTimeFormat(undefined, {
    weekday: 'short', day: 'numeric', month: 'short', year: 'numeric'
  }).format(new Date());

  readonly shortcuts = [
    { title: 'Employee management', description: 'People directory and employee lifecycle.', icon: 'bi-people', route: '/employees' },
    { title: 'Leave management', description: 'Leave requests, balances and approvals.', icon: 'bi-calendar2-week', route: '/leave' },
    { title: 'Timesheets', description: 'Weekly work logs and approvals.', icon: 'bi-clock-history', route: '/timesheets' },
    { title: 'Company setup', description: 'Organization and working policies.', icon: 'bi-buildings', route: '/company/departments' }
  ];
}
