import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { ComplaintsComponent } from './complaints.component';

describe('ComplaintsComponent', () => {
  let fixture: ComponentFixture<ComplaintsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ComplaintsComponent] }).compileComponents();
    fixture = TestBed.createComponent(ComplaintsComponent);
  });

  it('renders a textarea bound to the supplied control', () => {
    const control = new FormControl<string | null>('Cough');
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    const textarea = (fixture.nativeElement as HTMLElement).querySelector<HTMLTextAreaElement>('#complaints');
    expect(textarea?.value).toBe('Cough');
  });

  it('has no required validator -- complaints are optional', () => {
    const control = new FormControl<string | null>(null);
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();

    expect(control.valid).toBeTrue();
  });
});
