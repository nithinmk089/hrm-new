import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from './shared/placeholder-page.component';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', title: 'Sign in | HRMS', loadComponent: () => import('./features/auth/login-page.component').then(m => m.LoginPageComponent) },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'dashboard', title: 'Dashboard | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/dashboard/dashboard-page.component').then(m => m.DashboardPageComponent) },
  { path: 'company/departments', title: 'Departments | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/departments-page.component').then(m => m.DepartmentsPageComponent) },
  { path: 'company/offices', title: 'Offices | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/offices-page.component').then(m => m.OfficesPageComponent) },
  { path: 'company/designations', title: 'Designations | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/designations-page.component').then(m => m.DesignationsPageComponent) },
  { path: 'company/holidays', title: 'Holidays | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/holidays-page.component').then(m => m.HolidaysPageComponent) },
  { path: 'company/working-week', title: 'Working Week | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/working-week-page.component').then(m => m.WorkingWeekPageComponent) },
  { path: 'company/office-working-weeks', title: 'Office Working Weeks | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/company/office-working-week-page.component').then(m => m.OfficeWorkingWeekPageComponent) },
  { path: 'company/:section', title: 'Company administration | HRMS', component: PlaceholderPageComponent, canActivate: [authGuard] },
  { path: 'employees', title: 'Employees | HRMS', canActivate: [authGuard], loadComponent: () => import('./features/employees/employees-page.component').then(m => m.EmployeesPageComponent) },
  { path: 'leave', title: 'Leave management | HRMS', component: PlaceholderPageComponent, canActivate: [authGuard], data: { title: 'Leave management', description: 'Leave balances, requests and approvals will be delivered in UI-08.' } },
  { path: 'timesheets', title: 'Timesheets | HRMS', component: PlaceholderPageComponent, canActivate: [authGuard], data: { title: 'Timesheet management', description: 'Employee timesheets and manager approvals will be delivered in UI-09.' } },
  { path: '**', redirectTo: 'dashboard' }
];
