import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DoctorDetailsService } from '../doctor-details.service';

@Component({
  selector: 'app-doctor-details-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './doctor-details-form.component.html',
})
export class DoctorDetailsFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly doctorDetailsService = inject(DoctorDetailsService);

  readonly form = this.formBuilder.nonNullable.group({
    clinicName: ['', Validators.required],
    doctorName: ['', Validators.required],
    qualifications: [''],
    registrationNumber: [''],
  });

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saveSucceeded = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly logoPreview = signal<string | null>(null);
  readonly signaturePreview = signal<string | null>(null);

  // Base64 payloads staged by a file picker, sent on the next submit only --
  // kept outside the reactive form since a file input can't hold a base64
  // string as its bound value.
  private pendingLogo: string | null = null;
  private pendingSignature: string | null = null;

  ngOnInit(): void {
    this.doctorDetailsService.get().subscribe({
      next: (details) => {
        this.form.patchValue({
          clinicName: details.clinicName,
          doctorName: details.doctorName,
          qualifications: details.qualifications ?? '',
          registrationNumber: details.registrationNumber ?? '',
        });
        this.logoPreview.set(toDataUrl(details.logo));
        this.signaturePreview.set(toDataUrl(details.signature));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load clinic/doctor details.');
      },
    });
  }

  onLogoSelected(event: Event): void {
    this.readFileAsBase64(event, (base64) => {
      this.pendingLogo = base64;
      this.logoPreview.set(`data:image/*;base64,${base64}`);
    });
  }

  onSignatureSelected(event: Event): void {
    this.readFileAsBase64(event, (base64) => {
      this.pendingSignature = base64;
      this.signaturePreview.set(`data:image/*;base64,${base64}`);
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saveSucceeded.set(false);
    this.errorMessage.set(null);
    const { clinicName, doctorName, qualifications, registrationNumber } = this.form.getRawValue();

    this.doctorDetailsService
      .update({
        clinicName,
        doctorName,
        qualifications: qualifications || null,
        registrationNumber: registrationNumber || null,
        logo: this.pendingLogo,
        signature: this.pendingSignature,
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.saveSucceeded.set(true);
          this.pendingLogo = null;
          this.pendingSignature = null;
        },
        error: () => {
          this.saving.set(false);
          this.errorMessage.set('Could not save clinic/doctor details.');
        },
      });
  }

  private readFileAsBase64(event: Event, onLoaded: (base64: string) => void): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      // result is a data: URL ("data:image/png;base64,AAAA...") -- keep just the base64 payload.
      const base64 = result.substring(result.indexOf(',') + 1);
      onLoaded(base64);
    };
    reader.readAsDataURL(file);
  }
}

function toDataUrl(base64: string | null): string | null {
  return base64 ? `data:image/*;base64,${base64}` : null;
}
