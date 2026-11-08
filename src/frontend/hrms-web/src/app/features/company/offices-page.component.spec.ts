import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { CompanyApiService } from '../../core/company/company-api.service';
import { CompanySummary, Office } from '../../core/company/company.models';
import { OfficesPageComponent } from './offices-page.component';

const company: CompanySummary = {
  companyId: 'company-1', companyCode: 'DEMO', legalName: 'Demo Technologies Pvt Ltd',
  displayName: 'Demo Technologies', isActive: true
};

const office: Office = {
  officeId: 'office-1', companyId: 'company-1', officeCode: 'HQ', officeName: 'Corporate Office',
  addressLine1: 'Main Road', addressLine2: null, city: 'Kottayam', stateOrProvince: 'Kerala',
  postalCode: '686001', countryCode: 'IN', timeZoneId: 'Asia/Kolkata', isHeadOffice: true,
  isActive: true, createdAtUtc: '2026-11-08T00:00:00Z', updatedAtUtc: null, rowVersion: 'AAAAAAAAB9E='
};

describe('OfficesPageComponent', () => {
  const setup = async (companies: CompanySummary[] = [company], offices: Office[] = [office]) => {
    const api = {
      getCompanies: vi.fn().mockReturnValue(of(companies)),
      getOffices: vi.fn().mockReturnValue(of(offices)),
      createOffice: vi.fn().mockReturnValue(of(office)),
      updateOffice: vi.fn().mockReturnValue(of(office)),
      deactivateOffice: vi.fn().mockReturnValue(of(void 0))
    };
    await TestBed.configureTestingModule({
      imports: [OfficesPageComponent],
      providers: [{ provide: CompanyApiService, useValue: api }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfficesPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  };

  it('loads the company and its offices and displays the selected company', async () => {
    const { fixture, api } = await setup();
    expect(api.getOffices).toHaveBeenCalledWith('company-1');
    expect(fixture.nativeElement.querySelector('#companySelect').value).toBe('company-1');
    expect(fixture.nativeElement.textContent).toContain('Corporate Office');
    expect(fixture.nativeElement.textContent).toContain('Asia/Kolkata');
    expect(fixture.nativeElement.textContent).toContain('Head office');
    expect(fixture.nativeElement.textContent).toContain('1 active office');
  });

  it('shows an empty state when the user has no active companies', async () => {
    const { fixture, api } = await setup([]);
    expect(fixture.nativeElement.textContent).toContain('No companies available');
    expect(api.getOffices).not.toHaveBeenCalled();
  });

  it('loads offices when a different company is selected', async () => {
    const second = { ...company, companyId: 'company-2', companyCode: 'OTHER', displayName: 'Other Company' };
    const { component, api } = await setup([company, second]);
    component.onCompanyChange({ target: { value: 'company-2' } } as unknown as Event);
    expect(component.selectedCompanyId()).toBe('company-2');
    expect(api.getOffices).toHaveBeenLastCalledWith('company-2');
  });

  it('validates required office fields and two-letter country code', async () => {
    const { component } = await setup();
    component.openCreate();
    expect(component.form.invalid).toBe(true);
    component.form.patchValue({ officeCode: 'HQ', officeName: 'Corporate Office', countryCode: 'IND' });
    expect(component.form.invalid).toBe(true);
    component.form.patchValue({ countryCode: 'IN' });
    expect(component.form.valid).toBe(true);
  });

  it('creates an office with normalized code and country code', async () => {
    const { component, api } = await setup();
    component.openCreate();
    component.form.patchValue({ officeCode: ' branch ', officeName: 'Branch Office', countryCode: 'in', city: 'Kochi' });
    component.save();
    expect(api.createOffice).toHaveBeenCalledWith('company-1', expect.objectContaining({
      officeCode: 'BRANCH', officeName: 'Branch Office', countryCode: 'IN', city: 'Kochi'
    }));
    expect(component.notice()).toBe('Office created successfully.');
  });

  it('updates an office without changing its code and sends RowVersion', async () => {
    const { component, api } = await setup();
    component.openEdit(office);
    expect(component.form.controls.officeCode.disabled).toBe(true);
    component.form.patchValue({ officeName: 'Updated Corporate Office' });
    component.save();
    expect(api.updateOffice).toHaveBeenCalledWith('company-1', 'office-1', expect.objectContaining({
      officeName: 'Updated Corporate Office', rowVersion: office.rowVersion
    }));
    expect(api.updateOffice.mock.calls[0][2]).not.toHaveProperty('officeCode');
  });

  it('requires explicit confirmation before deactivating and then refreshes the directory', async () => {
    const { component, api } = await setup();
    component.deactivate(office);
    expect(api.deactivateOffice).not.toHaveBeenCalled();
    expect(component.confirmTarget()).toEqual(office);
    component.confirmDeactivate();
    expect(api.deactivateOffice).toHaveBeenCalledWith('company-1', 'office-1', office.rowVersion);
    expect(component.notice()).toBe('Office deactivated successfully.');
    expect(component.confirmTarget()).toBeNull();
  });

  it('keeps the confirmation open and displays a conflict when deactivation fails', async () => {
    const { component, api } = await setup();
    api.deactivateOffice.mockReturnValue(throwError(() => ({ status: 409 })));
    component.deactivate(office);
    component.confirmDeactivate();
    expect(component.confirmTarget()).toEqual(office);
    expect(component.confirmError()).toContain('changed or is in use');
  });

  it('shows inactive offices without an activate action', async () => {
    const inactive = { ...office, isActive: false };
    const { fixture } = await setup([company], [inactive]);
    expect(fixture.nativeElement.textContent).toContain('Inactive');
    expect(fixture.nativeElement.querySelector('[aria-label="Activate office"]')).toBeNull();
  });
});
