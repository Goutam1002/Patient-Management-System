import { Component, Input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

/** Free-text diagnosis notes, bound to a FormControl the parent owns. */
@Component({
  selector: 'app-diagnosis',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './diagnosis.component.html',
})
export class DiagnosisComponent {
  @Input({ required: true }) control!: FormControl<string | null>;
}
