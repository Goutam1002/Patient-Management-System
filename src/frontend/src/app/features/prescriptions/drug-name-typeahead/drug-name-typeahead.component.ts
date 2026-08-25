import { Component, Input, OnDestroy, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import { PrescriptionService } from '../prescription.service';

/**
 * A single drug-name text input with a dropdown of autocomplete suggestions
 * drawn from the doctor's own prior prescribing history -- a UX assist only,
 * never a validation constraint (the bound FormControl still accepts
 * arbitrary free text; see the module's Business Rules).
 *
 * Bound to a FormControl the parent owns, same convention as
 * ComplaintsComponent/DiagnosisComponent -- this component doesn't build its
 * own form, so PrescriptionFormComponent stays the single place that knows
 * about the line-item FormArray.
 */
@Component({
  selector: 'app-drug-name-typeahead',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './drug-name-typeahead.component.html',
})
export class DrugNameTypeaheadComponent implements OnDestroy {
  @Input({ required: true }) control!: FormControl<string>;
  @Input() id = 'drugName';

  private readonly prescriptionService = inject(PrescriptionService);
  private readonly destroyed = new Subject<void>();
  private readonly termChanged = new Subject<string>();

  readonly suggestions = signal<string[]>([]);
  readonly suggestionsOpen = signal(false);

  constructor() {
    this.termChanged
      .pipe(
        debounceTime(200),
        distinctUntilChanged(),
        switchMap((term) => (term.trim().length > 0 ? this.prescriptionService.drugSuggestions(term) : of([]))),
        takeUntil(this.destroyed),
      )
      .subscribe((results) => this.suggestions.set(results));
  }

  onInput(value: string): void {
    this.suggestionsOpen.set(true);
    this.termChanged.next(value);
  }

  select(drugName: string): void {
    this.control.setValue(drugName);
    this.suggestionsOpen.set(false);
    this.suggestions.set([]);
  }

  /** A short delay so a suggestion's (mousedown) still registers before the input's blur hides the list. */
  onBlur(): void {
    setTimeout(() => this.suggestionsOpen.set(false), 150);
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }
}
