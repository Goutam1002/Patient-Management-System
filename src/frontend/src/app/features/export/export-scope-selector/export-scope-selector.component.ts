import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ExportScope } from '../export.service';

type ScopeMode = 'patients' | 'dateRange';

/**
 * CSV-only scope picker (patients or a bounded date range -- the two modes
 * ExportScopeRequest supports; there is deliberately no third "everything"
 * mode). Emits null whenever the current form state doesn't yet resolve to
 * a valid scope, so the parent page can disable the Export action.
 */
@Component({
  selector: 'app-export-scope-selector',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './export-scope-selector.component.html',
})
export class ExportScopeSelectorComponent implements OnInit {
  @Output() readonly scopeChange = new EventEmitter<ExportScope | null>();

  readonly mode = new FormControl<ScopeMode>('patients', { nonNullable: true });
  readonly form = new FormGroup({
    patientIds: new FormControl<string>(''),
    from: new FormControl<string | null>(null),
    to: new FormControl<string | null>(null),
  });

  ngOnInit(): void {
    this.mode.valueChanges.subscribe(() => this.emitScope());
    this.form.valueChanges.subscribe(() => this.emitScope());
    this.emitScope();
  }

  private emitScope(): void {
    this.scopeChange.emit(this.buildScope());
  }

  private buildScope(): ExportScope | null {
    if (this.mode.value === 'patients') {
      const ids = (this.form.value.patientIds ?? '')
        .split(',')
        .map((s) => s.trim())
        .filter((s) => s.length > 0)
        .map(Number)
        .filter((n) => !Number.isNaN(n));
      return ids.length > 0 ? { patientIds: ids } : null;
    }

    const from = this.form.value.from ?? null;
    const to = this.form.value.to ?? null;
    return from && to ? { dateFrom: from, dateTo: to } : null;
  }
}
