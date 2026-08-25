import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Appointment,
  AppointmentService,
  AppointmentStatus,
  MANUALLY_SETTABLE_STATUSES,
  toDateParam,
} from '../appointment.service';

@Component({
  selector: 'app-daily-schedule',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './daily-schedule.component.html',
})
export class DailyScheduleComponent implements OnInit {
  private readonly appointmentService = inject(AppointmentService);
  private readonly route = inject(ActivatedRoute);

  readonly dateControl = new FormControl(toDateParam(new Date()), { nonNullable: true });

  readonly appointments = signal<Appointment[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly updatingId = signal<number | null>(null);

  readonly settableStatuses = MANUALLY_SETTABLE_STATUSES;

  ngOnInit(): void {
    // ?date= lets other screens (e.g. after scheduling) land on that day.
    const requestedDate = this.route.snapshot.queryParamMap.get('date');
    if (requestedDate) {
      this.dateControl.setValue(requestedDate);
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.appointmentService.getDaily(this.dateControl.value).subscribe({
      next: (appointments) => {
        // Already time-ordered server-side; scheduled and walk-in entries
        // arrive interleaved in this one list, not as two separate feeds.
        this.appointments.set(appointments);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Could not load the schedule for this date.');
      },
    });
  }

  changeStatus(appointment: Appointment, status: string): void {
    this.updatingId.set(appointment.id);
    this.errorMessage.set(null);

    this.appointmentService.updateStatus(appointment.id, status as AppointmentStatus).subscribe({
      next: (updated) => {
        this.appointments.update((list) =>
          list.map((a) => (a.id === updated.id ? updated : a)),
        );
        this.updatingId.set(null);
      },
      error: () => {
        this.updatingId.set(null);
        this.errorMessage.set('Could not update the appointment status.');
      },
    });
  }

  statusBadgeClass(status: AppointmentStatus): string {
    switch (status) {
      case 'Completed':
        return 'bg-success';
      case 'Cancelled':
        return 'bg-secondary';
      case 'NoShow':
        return 'bg-warning text-dark';
      default:
        return 'bg-primary';
    }
  }
}
