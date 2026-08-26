import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { ExportPageComponent } from './export-page.component';

describe('ExportPageComponent', () => {
  let fixture: ComponentFixture<ExportPageComponent>;
  let httpMock: HttpTestingController;

  function setup(queryParams: Record<string, string> = {}): void {
    TestBed.configureTestingModule({
      imports: [ExportPageComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ExportPageComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => httpMock.verify());

  it('defaults to CSV format with the export button disabled until a scope is chosen', () => {
    setup();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button.btn-primary') as HTMLButtonElement;
    expect(fixture.componentInstance.format.value).toBe('csv');
    expect(button.disabled).toBeTrue();
  });

  it('pre-fills PDF format and patient id from query params (patient-profile shortcut)', () => {
    setup({ format: 'pdf', patientId: '7' });

    expect(fixture.componentInstance.format.value).toBe('pdf');
    expect(fixture.componentInstance.pdfPatientId.value).toBe(7);
  });

  it('requires confirmation before calling the export API, then downloads the result', () => {
    setup();
    fixture.componentInstance.csvScope.set({ patientIds: [1] });
    fixture.detectChanges();

    fixture.componentInstance.requestExport();
    fixture.detectChanges();
    expect(fixture.componentInstance.confirmDialogOpen()).toBeTrue();

    const createObjectURLSpy = spyOn(window.URL, 'createObjectURL').and.returnValue('blob:mock');
    const revokeSpy = spyOn(window.URL, 'revokeObjectURL');
    const clickSpy = spyOn(HTMLAnchorElement.prototype, 'click');

    fixture.componentInstance.confirmExport();
    const req = httpMock.expectOne(`${environment.apiUrl}/exports/csv`);
    expect(req.request.body).toEqual({ scope: { patientIds: [1] }, confirmed: true });
    req.flush(new Blob());

    expect(fixture.componentInstance.confirmDialogOpen()).toBeFalse();
    expect(createObjectURLSpy).toHaveBeenCalled();
    expect(clickSpy).toHaveBeenCalled();
    expect(revokeSpy).toHaveBeenCalled();
  });

  it('shows an error message when the export request fails', () => {
    setup();
    fixture.componentInstance.csvScope.set({ patientIds: [1] });

    fixture.componentInstance.confirmExport();
    const req = httpMock.expectOne(`${environment.apiUrl}/exports/csv`);
    req.flush(new Blob(), { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Export failed. Please try again.');
  });

  it('sends the PDF request shape when PDF format is selected', () => {
    setup({ format: 'pdf', patientId: '7' });

    fixture.componentInstance.confirmExport();
    const req = httpMock.expectOne(`${environment.apiUrl}/exports/pdf`);
    expect(req.request.body).toEqual({ patientId: 7, dateFrom: null, dateTo: null, confirmed: true });
    req.flush(new Blob());
  });
});
