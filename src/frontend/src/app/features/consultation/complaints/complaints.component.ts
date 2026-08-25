import { Component, Input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

/** Free-text complaints, bound to a FormControl the parent owns. */
@Component({
  selector: 'app-complaints',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './complaints.component.html',
})
export class ComplaintsComponent {
  @Input({ required: true }) control!: FormControl<string | null>;
}
