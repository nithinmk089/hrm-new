import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { EmployeeApiService } from '../../core/company/employee-api.service';
import { LeaveApiService } from '../../core/company/leave-api.service';
import { CompanySummary } from '../../core/company/company.models';
import { Employee } from '../../core/company/employee.models';
import { EmployeeLeaveBalance, LeaveType } from '../../core/company/leave.models';
import { LeaveBalanceProvisioningPageComponent } from './leave-balance-provisioning-page.component';

const company:CompanySummary={companyId:'company-1',companyCode:'DEMO',legalName:'Demo Technologies Pvt Ltd',displayName:'Demo Technologies',isActive:true};
const employee:Employee={employeeId:'employee-1',companyId:'company-1',employeeCode:'EMP001',firstName:'Alex',middleName:null,lastName:'Morgan',preferredName:null,workEmail:'alex@example.com',hireDate:'2024-01-01',employmentStatus:'Active',terminationDate:null,isActive:true,createdAtUtc:'2024-01-01T00:00:00Z',updatedAtUtc:null,rowVersion:'AAAAAAAAB9E='};
const leaveType:LeaveType={leaveTypeId:'type-1',companyId:'company-1',leaveTypeCode:'ANNUAL',leaveTypeName:'Annual Leave',description:null,isPaid:true,requiresApproval:true,annualEntitlementDays:20,allowHalfDay:true,allowCarryForward:false,maxCarryForwardDays:0,isActive:true,rowVersion:'AAAAAAAAB9E='};
const balance:EmployeeLeaveBalance={employeeLeaveBalanceId:'balance-1',companyId:'company-1',employeeId:'employee-1',leaveTypeId:'type-1',leaveTypeCode:'ANNUAL',leaveTypeName:'Annual Leave',leaveYear:new Date().getFullYear(),openingDays:20,accruedDays:0,carriedForwardDays:0,adjustmentDays:0,usedDays:0,reservedDays:0,availableDays:20,rowVersion:'AAAAAAAAB9E='};

describe('LeaveBalanceProvisioningPageComponent',()=>{
 const setup=async(selectCompany=true)=>{
  const companyApi={getCompanies:vi.fn().mockReturnValue(of([company]))};
  const employeeApi={getEmployees:vi.fn().mockReturnValue(of([employee]))};
  const leaveApi={
   getLeaveTypes:vi.fn().mockReturnValue(of([leaveType])),
   getEmployeeBalances:vi.fn().mockReturnValue(of([balance])),
   applyEmployeeBalanceTransaction:vi.fn().mockReturnValue(of(balance))
  };
  await TestBed.configureTestingModule({imports:[LeaveBalanceProvisioningPageComponent],providers:[
   {provide:CompanyApiService,useValue:companyApi},{provide:EmployeeApiService,useValue:employeeApi},{provide:LeaveApiService,useValue:leaveApi}
  ]}).compileComponents();
  const fixture=TestBed.createComponent(LeaveBalanceProvisioningPageComponent);fixture.detectChanges();await fixture.whenStable();
  if(selectCompany)fixture.componentInstance.onCompanyChange({target:{value:'company-1'}} as unknown as Event);
  await fixture.whenStable();fixture.detectChanges();
  return{fixture,component:fixture.componentInstance,companyApi,employeeApi,leaveApi};
 };
 it('does not auto-select a single company or load employees before explicit company selection',async()=>{
  const{component,fixture,employeeApi,leaveApi}=await setup(false);
  expect(component.companies()).toHaveLength(1);
  expect(component.companyId()).toBe('');
  expect(component.employees()).toEqual([]);
  expect(component.leaveTypes()).toEqual([]);
  expect(employeeApi.getEmployees).not.toHaveBeenCalled();
  expect(leaveApi.getLeaveTypes).not.toHaveBeenCalled();
  const companySelect=fixture.nativeElement.querySelector('#company') as HTMLSelectElement;
  const employeeSelect=fixture.nativeElement.querySelector('#employee') as HTMLSelectElement;
  expect(companySelect.value).toBe('');
  expect(employeeSelect.disabled).toBe(true);
  expect(employeeSelect.options).toHaveLength(1);
  expect(employeeSelect.options[0].textContent).toContain('Select a company first');
 });
 it('loads company, active employees and leave types',async()=>{
  const{component,employeeApi,leaveApi}=await setup();
  expect(component.companyId()).toBe('company-1');expect(employeeApi.getEmployees).toHaveBeenCalledWith('company-1',undefined,'Active');
  expect(leaveApi.getLeaveTypes).toHaveBeenCalledWith('company-1');expect(component.employees()).toHaveLength(1);expect(component.leaveTypes()).toHaveLength(1);
 });
 it('clears employee and balance data when the company selection is cleared',async()=>{
  const{component,fixture,leaveApi}=await setup();
  component.onEmployeeChange({target:{value:'employee-1'}} as unknown as Event);await fixture.whenStable();
  expect(component.balances()).toEqual([balance]);
  component.onCompanyChange({target:{value:''}} as unknown as Event);fixture.detectChanges();
  expect(component.companyId()).toBe('');expect(component.employeeId()).toBe('');
  expect(component.employees()).toEqual([]);expect(component.leaveTypes()).toEqual([]);expect(component.balances()).toEqual([]);
  const employeeSelect=fixture.nativeElement.querySelector('#employee') as HTMLSelectElement;
  expect(employeeSelect.disabled).toBe(true);expect(employeeSelect.options[0].textContent).toContain('Select a company first');
  expect(leaveApi.getEmployeeBalances).toHaveBeenCalledTimes(1);
 });
 it('loads annual balances for the selected employee and year',async()=>{
  const{component,leaveApi,fixture}=await setup();
  component.onEmployeeChange({target:{value:'employee-1'}} as unknown as Event);await fixture.whenStable();
  expect(leaveApi.getEmployeeBalances).toHaveBeenCalledWith('company-1','employee-1',new Date().getFullYear());expect(component.balances()).toEqual([balance]);
 });
 it('submits an Opening transaction with the selected employee and leave year',async()=>{
  const{component,leaveApi}=await setup();
  component.employeeId.set('employee-1');component.form.patchValue({leaveTypeId:'type-1',transactionType:'Opening',daysDelta:20,notes:'Annual entitlement'});
  component.submit();
  expect(leaveApi.applyEmployeeBalanceTransaction).toHaveBeenCalledWith('company-1','employee-1',{
   leaveTypeId:'type-1',leaveYear:new Date().getFullYear(),transactionType:'Opening',daysDelta:20,notes:'Annual entitlement'
  });
  expect(component.success()).toContain('saved successfully');
 });
 it('prevents negative values for non-adjustment transaction types',async()=>{
  const{component,leaveApi}=await setup();
  component.employeeId.set('employee-1');component.form.patchValue({leaveTypeId:'type-1',transactionType:'Opening',daysDelta:-2});
  component.submit();
  expect(component.formError()).toContain('Only Adjustment transactions');
  expect(leaveApi.applyEmployeeBalanceTransaction).not.toHaveBeenCalled();
 });
 it('shows permission and server validation errors',async()=>{
  const{component,leaveApi}=await setup();
  component.employeeId.set('employee-1');component.form.patchValue({leaveTypeId:'type-1',transactionType:'Opening',daysDelta:10});
  leaveApi.applyEmployeeBalanceTransaction.mockReturnValue(throwError(()=>({status:403})));
  component.submit();
  expect(component.formError()).toContain('do not have permission');
 });
});
