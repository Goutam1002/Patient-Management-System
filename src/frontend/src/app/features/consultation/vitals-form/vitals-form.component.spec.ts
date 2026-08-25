import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormBuilder, Validators } from '@angular/forms';
import { VitalsFormComponent } from './vitals-form.component';

describe('VitalsFormComponent', () => {
  let fixture: ComponentFixture<VitalsFormComponent>;
  const formBuilder = new FormBuilder();

  function group() {
    return formBuilder.nonNullable.group({
      temperature: ['', Validators.required],
      bpSystolic: ['', Validators.required],
      bpDiastolic: ['', Validators.required],
      pulse: ['', Validators.required],
      weight: ['', Validators.required],
    });
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [VitalsFormComponent] }).compileComponents();
    fixture = TestBed.createComponent(VitalsFormComponent);
  });

  it('renders all four vitals bound to the supplied group', () => {
    fixture.componentRef.setInput('group', group());
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('#temperature')).toBeTruthy();
    expect(element.querySelector('#bpSystolic')).toBeTruthy();
    expect(element.querySelector('#bpDiastolic')).toBeTruthy();
    expect(element.querySelector('#pulse')).toBeTruthy();
    expect(element.querySelector('#weight')).toBeTruthy();
  });

  it('is required-invalid when empty, matching the mandatory-at-entry rule', () => {
    const vitals = group();
    fixture.componentRef.setInput('group', vitals);
    fixture.detectChanges();

    expect(vitals.invalid).toBeTrue();

    vitals.setValue({
      temperature: '37',
      bpSystolic: '120',
      bpDiastolic: '80',
      pulse: '72',
      weight: '52.85',
    });
    expect(vitals.valid).toBeTrue();
  });

  it('reflects a disabled group (post-creation edit mode) by disabling its inputs', () => {
    const vitals = group();
    vitals.disable();
    fixture.componentRef.setInput('group', vitals);
    fixture.detectChanges();

    const temperatureInput = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#temperature');
    expect(temperatureInput?.disabled).toBeTrue();
  });
});
