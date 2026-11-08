import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from './shared/placeholder-page.component';
export const routes: Routes = [
 {path:'',pathMatch:'full',redirectTo:'dashboard'},
 {path:'dashboard',title:'Dashboard | HRMS',loadComponent:()=>import('./features/dashboard/dashboard-page.component').then(m=>m.DashboardPageComponent)},
 {path:'company/:section',title:'Company administration | HRMS',component:PlaceholderPageComponent},
 {path:'employees',title:'Employees | HRMS',component:PlaceholderPageComponent,data:{title:'Employee management',description:'Employee directory and lifecycle management will be delivered in UI-06.'}},
 {path:'leave',title:'Leave management | HRMS',component:PlaceholderPageComponent,data:{title:'Leave management',description:'Leave balances, requests and approvals will be delivered in UI-08.'}},
 {path:'timesheets',title:'Timesheets | HRMS',component:PlaceholderPageComponent,data:{title:'Timesheet management',description:'Employee timesheets and manager approvals will be delivered in UI-09.'}},
 {path:'**',redirectTo:'dashboard'}
];