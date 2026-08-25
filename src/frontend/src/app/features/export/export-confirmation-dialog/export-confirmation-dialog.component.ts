import { Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * The confirmation gate the fixed Export spec requires before an export
 * executes. This is a UX affordance only -- the server-side Confirmed check
 * in ExportService/ExportsController is what actually enforces the gate,
 * since the API must not treat a request as implicitly confirmed.
 */
@Component({
  selector: 'app-export-confirmation-dialog',
  standalone: true,
  templateUrl: './export-confirmation-dialog.component.html',
})
export class ExportConfirmationDialogComponent {
  @Input() open = false;
  @Input() summary = '';
  @Output() readonly confirm = new EventEmitter<void>();
  @Output() readonly cancel = new EventEmitter<void>();
}
