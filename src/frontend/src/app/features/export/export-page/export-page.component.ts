import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ExportConfirmationDialogComponent } from '../export-confirmation-dialog/export-confirmation-dialog.component';
import { ExportScopeSelectorComponent } from '../export-scope-selector/export-scope-selector.component';
import { ExportScope, ExportService } from '../export.service';

type Format = 'csv' | 'pdf';

/**
 * One screen for both export formats (module file: ExportService,
 * ExportScopeSelectorComponent, ExportConfirmationDialogComponent, download
 * handling -- no separate CSV/PDF pages called for). PDF's scope shape
 * (single patient + optional date range) genuinely differs from CSV's
 * (ExportScopeRequest), so it's handled with its own two fields here rather
 * than forcing it through ExportScopeSelectorComponent.
 */
@Component({
  selector: 'app-export-page',
  standalone: true,
  imports: [ReactiveFormsModule, ExportScopeSelectorComponent, ExportConfirmationDialogComponent],
  templateUrl: './export-page.component.html',
})
export class ExportPageComponent implements OnInit {
  private readonly exportService = inject(ExportService);
  private readonly route = inject(ActivatedRoute);

  readonly format = new FormControl<Format>('csv', { nonNullable: true });
  readonly pdfPatientId = new FormControl<number | null>(null);
  readonly pdfFrom = new FormControl<string | null>(null);
  readonly pdfTo = new FormControl<string | null>(null);

  readonly csvScope = signal<ExportScope | null>(null);
  readonly confirmDialogOpen = signal(false);
  readonly exporting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    // Reached from a patient profile's "Export PDF" shortcut with the
    // patient (and format) pre-filled, per the module's own nav requirement.
    const params = this.route.snapshot.queryParamMap;
    const queryFormat = params.get('format');
    if (queryFormat === 'pdf') {
      this.format.setValue('pdf');
    }
    const queryPatientId = params.get('patientId');
    if (queryPatientId) {
      this.pdfPatientId.setValue(Number(queryPatientId));
    }
  }

  onCsvScopeChange(scope: ExportScope | null): void {
    this.csvScope.set(scope);
  }

  get canExport(): boolean {
    if (this.format.value === 'csv') {
      return this.csvScope() !== null;
    }
    return this.pdfPatientId.value !== null && this.pdfPatientId.value >= 0;
  }

  get confirmSummary(): string {
    if (this.format.value === 'csv') {
      const scope = this.csvScope();
      if (!scope) {
        return '';
      }
      return scope.patientIds
        ? `Export patients.csv + visits.csv for ${scope.patientIds.length} selected patient(s).`
        : `Export patients.csv + visits.csv for visits between ${scope.dateFrom} and ${scope.dateTo}.`;
    }
    return `Export a PDF summary for patient ${this.pdfPatientId.value}.`;
  }

  requestExport(): void {
    if (!this.canExport) {
      return;
    }
    this.errorMessage.set(null);
    this.confirmDialogOpen.set(true);
  }

  cancelExport(): void {
    this.confirmDialogOpen.set(false);
  }

  confirmExport(): void {
    this.confirmDialogOpen.set(false);
    this.exporting.set(true);

    const request$ =
      this.format.value === 'csv'
        ? this.exportService.exportCsv(this.csvScope()!, true)
        : this.exportService.exportPdf(this.pdfPatientId.value!, this.pdfFrom.value, this.pdfTo.value, true);

    request$.subscribe({
      next: (blob) => {
        this.exporting.set(false);
        const filename = this.format.value === 'csv' ? 'patient-export.zip' : `patient-${this.pdfPatientId.value}-summary.pdf`;
        this.downloadBlob(blob, filename);
      },
      error: () => {
        this.exporting.set(false);
        this.errorMessage.set('Export failed. Please try again.');
      },
    });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }
}
