import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  standalone: true,
  selector: 'hrms-login-page',
  imports: [ReactiveFormsModule],
  template: `
    <section class="login-page">
      <div class="login-panel">
        <div class="brand-mark"><i class="bi bi-layers-half"></i></div>
        <p class="eyebrow">HRMS WORKFORCE PLATFORM</p>
        <h1>Welcome back</h1>
        <p class="intro">Sign in to continue to your workspace.</p>

        @if (errorMessage()) {
          <div class="alert alert-danger" role="alert">{{ errorMessage() }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="mb-3">
            <label class="form-label" for="tenantCode">Workspace code</label>
            <input id="tenantCode" class="form-control" formControlName="tenantCode"
              autocomplete="organization" placeholder="Enter your workspace code"
              [class.is-invalid]="isInvalid('tenantCode')" />
            @if (isInvalid('tenantCode')) {
              <div class="invalid-feedback">Workspace code is required.</div>
            }
          </div>
          <div class="mb-3">
            <label class="form-label" for="email">Email address</label>
            <input id="email" type="email" class="form-control" formControlName="email"
              autocomplete="username" placeholder="name@company.com"
              [class.is-invalid]="isInvalid('email')" />
            @if (isInvalid('email')) {
              <div class="invalid-feedback">Enter a valid email address.</div>
            }
          </div>
          <div class="mb-4">
            <label class="form-label" for="password">Password</label>
            <input id="password" type="password" class="form-control" formControlName="password"
              autocomplete="current-password" placeholder="Enter your password"
              [class.is-invalid]="isInvalid('password')" />
            @if (isInvalid('password')) {
              <div class="invalid-feedback">Password is required.</div>
            }
          </div>
          <button class="btn btn-primary w-100 login-button" type="submit"
            [disabled]="form.invalid || submitting()">
            @if (submitting()) {
              <span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>
              Signing in…
            } @else {
              Sign in
            }
          </button>
        </form>
        <p class="security-note"><i class="bi bi-shield-lock me-1"></i> Your session is protected.</p>
      </div>
    </section>
  `,
  styles: [`
    :host{display:block;min-height:100vh;background:#f4f6fa}
    .login-page{display:grid;place-items:center;min-height:100vh;padding:2rem 1rem}
    .login-panel{width:min(100%,430px);padding:2.5rem;background:#fff;border:1px solid #e5e9f0;border-radius:12px;box-shadow:0 16px 45px rgb(16 24 40 / 6%)}
    .brand-mark{display:grid;place-items:center;width:44px;height:44px;margin-bottom:1.6rem;border-radius:10px;background:#263f65;color:#fff;font-size:1.35rem}
    .eyebrow{margin:0 0 .55rem;color:#667085;font-size:.68rem;font-weight:700;letter-spacing:.1em}
    h1{margin:0;color:#1d2939;font-size:1.75rem;font-weight:700;letter-spacing:-.03em}
    .intro{margin:.55rem 0 1.8rem;color:#667085;font-size:.9rem}
    .form-label{color:#344054;font-size:.8rem;font-weight:600}
    .form-control{min-height:44px;border-color:#d0d5dd;font-size:.88rem}
    .form-control:focus{border-color:#5476a6;box-shadow:0 0 0 .2rem rgb(49 85 143 / 12%)}
    .login-button{min-height:45px;background:#294a78;border-color:#294a78;font-size:.88rem;font-weight:600}
    .login-button:hover{background:#203d66;border-color:#203d66}
    .security-note{margin:1.5rem 0 0;color:#98a2b3;text-align:center;font-size:.72rem}
  `]
})
export class LoginPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly submitting = signal(false);
  readonly errorMessage = signal('');

  readonly form = this.formBuilder.nonNullable.group({
    tenantCode: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(256)]]
  });

  isInvalid(controlName: 'tenantCode' | 'email' | 'password'): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.submitting.set(true);

    this.auth.login(this.form.getRawValue()).pipe(
      finalize(() => this.submitting.set(false))
    ).subscribe({
      next: () => {
        const requestedUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const returnUrl = requestedUrl?.startsWith('/') && !requestedUrl.startsWith('//')
          ? requestedUrl
          : '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(error.status === 401
          ? 'The workspace code, email address or password is incorrect.'
          : 'Unable to sign in right now. Please try again.');
      }
    });
  }
}
