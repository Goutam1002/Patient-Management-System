import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { DiagnosisComponent } from './diagnosis.component';

describe('DiagnosisComponent', () => {
  let fixture: ComponentFixture<DiagnosisComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [DiagnosisComponent] }).compileComponents();
    fixture = TestBed.createComponent(DiagnosisComponent);
  });

  it('renders a textarea bound to the supplied control', () => {
    const control = new FormControl<string | null>('URI');
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    const textarea = (fixture.nativeElement as HTMLElement).querySelector<HTMLTextAreaElement>('#diagnosis');
    expect(textarea?.value).toBe('URI');
  });

  it('has no required validator -- diagnosis is optional', () => {
    const control = new FormControl<string | null>(null);
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    expect(control.valid).toBeTrue();
  });
});
