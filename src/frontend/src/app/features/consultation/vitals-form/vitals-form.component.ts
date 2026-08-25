import { Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';

/**
 * Renders the four mandatory-at-entry vitals (temperature, BP, pulse,
 * weight) bound to a FormGroup the parent owns and validates -- this
 * component doesn't build its own form so ConsultationWorkflowComponent
 * stays the single place that knows create-vs-edit mode (in edit mode the
 * parent disables this group entirely; vitals are never editable
 * retroactively).
 */
@Component({
  selector: 'app-vitals-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './vitals-form.component.html',
})
export class VitalsFormComponent {
  @Input({ required: true }) group!: FormGroup;
}
