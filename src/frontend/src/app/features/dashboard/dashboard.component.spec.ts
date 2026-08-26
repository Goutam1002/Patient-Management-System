import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
  });

  it('welcomes the doctor to the clinic', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Welcome to the Clinic');
  });

  it('links to the export page', () => {
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[routerlink="/export"]');
    expect(link).toBeTruthy();
    expect(link!.textContent).toContain('Export Data');
  });

  it('no longer hosts the patient search bar (moved to /patients)', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).not.toContain('Search Patients');
  });
});
