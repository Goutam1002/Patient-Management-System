import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExportScope } from '../export.service';
import { ExportScopeSelectorComponent } from './export-scope-selector.component';

describe('ExportScopeSelectorComponent', () => {
  let fixture: ComponentFixture<ExportScopeSelectorComponent>;
  let emitted: (ExportScope | null)[];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExportScopeSelectorComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ExportScopeSelectorComponent);
    emitted = [];
    fixture.componentInstance.scopeChange.subscribe((scope) => emitted.push(scope));
    fixture.detectChanges();
  });

  it('emits null in patients mode until at least one valid patient id is entered', () => {
    expect(emitted[emitted.length - 1]).toBeNull();

    fixture.componentInstance.form.controls.patientIds.setValue('3, 7');
    expect(emitted[emitted.length - 1]).toEqual({ patientIds: [3, 7] });
  });

  it('ignores blank and non-numeric entries in the patient id list', () => {
    fixture.componentInstance.form.controls.patientIds.setValue('3, , abc, 9');
    expect(emitted[emitted.length - 1]).toEqual({ patientIds: [3, 9] });
  });

  it('emits null in date-range mode until both from and to are set', () => {
    fixture.componentInstance.mode.setValue('dateRange');
    expect(emitted[emitted.length - 1]).toBeNull();

    fixture.componentInstance.form.controls.from.setValue('2026-01-01');
    expect(emitted[emitted.length - 1]).toBeNull();

    fixture.componentInstance.form.controls.to.setValue('2026-01-31');
    expect(emitted[emitted.length - 1]).toEqual({ dateFrom: '2026-01-01', dateTo: '2026-01-31' });
  });

  it('never produces a scope with neither patientIds nor a date range -- there is no unbounded mode', () => {
    fixture.componentInstance.form.controls.patientIds.setValue('3');
    fixture.componentInstance.mode.setValue('dateRange');
    fixture.componentInstance.form.controls.from.setValue('2026-01-01');
    fixture.componentInstance.form.controls.to.setValue('2026-01-31');
    fixture.componentInstance.mode.setValue('patients');

    const nonNullScopes = emitted.filter((scope): scope is NonNullable<typeof scope> => scope !== null);
    expect(nonNullScopes.length).toBeGreaterThan(0);
    for (const scope of nonNullScopes) {
      expect(Boolean(scope.patientIds) || Boolean(scope.dateFrom && scope.dateTo)).toBeTrue();
    }
  });
});
