import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Mirrors the backend's ExportScopeRequest -- exactly one of patientIds
 * (non-empty) or dateFrom+dateTo (both present) is ever sent. There is no
 * "export everything" shape: ExportScopeSelectorComponent never produces one.
 */
export interface ExportScope {
  patientIds?: number[] | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

export interface ExportAuditLogEntry {
  id: number;
  performedAt: string;
  format: string;
  scopeType: string;
  scopeDetail: string;
  username: string;
}

@Injectable({ providedIn: 'root' })
export class ExportService {
  private readonly http = inject(HttpClient);

  /** confirmed must be true -- the API rejects false/omitted server-side regardless of this call site. */
  exportCsv(scope: ExportScope, confirmed: boolean): Observable<Blob> {
    return this.http.post(`${environment.apiUrl}/exports/csv`, { scope, confirmed }, { responseType: 'blob' });
  }

  exportPdf(patientId: number, dateFrom: string | null, dateTo: string | null, confirmed: boolean): Observable<Blob> {
    return this.http.post(
      `${environment.apiUrl}/exports/pdf`,
      { patientId, dateFrom, dateTo, confirmed },
      { responseType: 'blob' },
    );
  }

  getAuditLog(): Observable<ExportAuditLogEntry[]> {
    return this.http.get<ExportAuditLogEntry[]>(`${environment.apiUrl}/exports/audit-log`);
  }
}
