import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ExportAuditLogEntry, ExportService } from './export.service';

describe('ExportService', () => {
  let service: ExportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ExportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the scope and confirmed flag for a CSV export', () => {
    service.exportCsv({ patientIds: [1, 2] }, true).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/exports/csv`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ scope: { patientIds: [1, 2] }, confirmed: true });
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob());
  });

  it('posts patientId/date range/confirmed for a PDF export', () => {
    service.exportPdf(5, '2026-01-01', '2026-01-31', true).subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/exports/pdf`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ patientId: 5, dateFrom: '2026-01-01', dateTo: '2026-01-31', confirmed: true });
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob());
  });

  it('fetches the export audit log', () => {
    const entries: ExportAuditLogEntry[] = [
      { id: 1, performedAt: '2026-01-01T09:00:00', format: 'Csv', scopeType: 'SelectedPatients', scopeDetail: '1,2', username: 'doctor' },
    ];

    service.getAuditLog().subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/exports/audit-log`);
    expect(req.request.method).toBe('GET');
    req.flush(entries);
  });
});
