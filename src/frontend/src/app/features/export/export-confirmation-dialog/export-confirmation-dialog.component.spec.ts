import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExportConfirmationDialogComponent } from './export-confirmation-dialog.component';

describe('ExportConfirmationDialogComponent', () => {
  let fixture: ComponentFixture<ExportConfirmationDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExportConfirmationDialogComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ExportConfirmationDialogComponent);
  });

  it('renders nothing when closed', () => {
    fixture.componentInstance.open = false;
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.modal')).toBeNull();
  });

  it('shows the summary and emits confirm/cancel when open', () => {
    fixture.componentInstance.open = true;
    fixture.componentInstance.summary = 'Export CSV for 2 selected patient(s).';
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Export CSV for 2 selected patient(s).');

    let confirmed = false;
    let cancelled = false;
    fixture.componentInstance.confirm.subscribe(() => (confirmed = true));
    fixture.componentInstance.cancel.subscribe(() => (cancelled = true));

    (el.querySelector('button.btn-primary') as HTMLButtonElement).click();
    (el.querySelector('button.btn-outline-secondary') as HTMLButtonElement).click();

    expect(confirmed).toBeTrue();
    expect(cancelled).toBeTrue();
  });
});
