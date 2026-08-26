import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DrugNameTypeaheadComponent } from '../drug-name-typeahead/drug-name-typeahead.component';
import { PrescriptionService } from '../prescription.service';

type PrescriptionItemGroup = FormGroup<{
  drugName: FormControl<string>;
  dosage: FormControl<string>;
  frequency: FormControl<string>;
  duration: FormControl<string>;
  instructions: FormControl<string>;
}>;

/**
 * Medicine line-item entry -- one screen, add/remove rows freely, each
 * DrugName field backed by DrugNameTypeaheadComponent. Reached from the
 * consultation workflow's post-visit success panel or the Daily Schedule's
 * "Add Prescription" button (/visits/:visitId/prescriptions/new), so a
 * diagnosis has already been recorded on the visit this prescription
 * attaches to (see the module's own Dependencies note). Submitting calls
 * Create and returns to the Daily Schedule -- the printable view is still
 * reachable afterward from the visit's own prescription list.
 */
@Component({
  selector: 'app-prescription-form',
  standalone: true,
  imports: [ReactiveFormsModule, DrugNameTypeaheadComponent],
  templateUrl: './prescription-form.component.html',
})
export class PrescriptionFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly prescriptionService = inject(PrescriptionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private visitId!: number;

  readonly items = this.formBuilder.array<PrescriptionItemGroup>([]);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.visitId = Number(this.route.snapshot.paramMap.get('visitId'));
    this.addItem();
  }

  private newItemGroup(): PrescriptionItemGroup {
    return this.formBuilder.nonNullable.group({
      drugName: ['', Validators.required],
      dosage: [''],
      frequency: [''],
      duration: [''],
      instructions: [''],
    });
  }

  addItem(): void {
    this.items.push(this.newItemGroup());
  }

  removeItem(index: number): void {
    if (this.items.length > 1) {
      this.items.removeAt(index);
    }
  }

  itemGroup(index: number): PrescriptionItemGroup {
    return this.items.at(index);
  }

  submit(): void {
    if (this.items.invalid) {
      this.items.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.prescriptionService
      .create(this.visitId, {
        items: this.items.getRawValue().map((item) => ({
          drugName: item.drugName,
          dosage: item.dosage || null,
          frequency: item.frequency || null,
          duration: item.duration || null,
          instructions: item.instructions || null,
        })),
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.router.navigate(['/appointments']);
        },
        error: () => {
          this.saving.set(false);
          this.errorMessage.set('Could not save the prescription.');
        },
      });
  }
}
