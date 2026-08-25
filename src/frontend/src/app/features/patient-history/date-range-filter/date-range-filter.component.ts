import { Component, EventEmitter, Output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';

export interface DateRange {
  from: string | null;
  to: string | null;
}

@Component({
  selector: 'app-date-range-filter',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './date-range-filter.component.html',
})
export class DateRangeFilterComponent {
  @Output() readonly rangeChange = new EventEmitter<DateRange>();

  readonly form = new FormGroup({
    from: new FormControl<string | null>(null),
    to: new FormControl<string | null>(null),
  });

  apply(): void {
    this.rangeChange.emit({ from: this.form.value.from ?? null, to: this.form.value.to ?? null });
  }

  clear(): void {
    this.form.reset();
    this.rangeChange.emit({ from: null, to: null });
  }
}
