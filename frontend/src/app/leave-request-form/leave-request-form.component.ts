import { Component, DestroyRef, EventEmitter, Output, inject, signal } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { LEAVE_TYPE_LABELS, LeaveRequest, LeaveType } from '../models/leave-request.model';
import { LeaveRequestsService } from '../services/leave-requests.service';
import { getErrorMessage } from '../services/api.config';
import { countDays, dateRangeValidator } from './date-range.validator';

@Component({
  selector: 'app-leave-request-form',
  standalone: true,
  imports: [ReactiveFormsModule, AsyncPipe],
  templateUrl: './leave-request-form.component.html',
  styleUrls: ['./leave-request-form.component.css']
})
export class LeaveRequestFormComponent {
  @Output() created = new EventEmitter<LeaveRequest>();

  private readonly service = inject(LeaveRequestsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly employees$ = this.service.getEmployees();
  readonly leaveTypes = Object.values(LeaveType).filter((v): v is LeaveType => typeof v === 'number');
  readonly typeLabels = LEAVE_TYPE_LABELS;

  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form = this.fb.group(
    {
      employeeId: this.fb.control<number | null>(null, Validators.required),
      type: this.fb.control<LeaveType | null>(null, Validators.required),
      startDate: this.fb.control('', Validators.required),
      endDate: this.fb.control('', Validators.required)
    },
    { validators: dateRangeValidator('startDate', 'endDate') }
  );

  get days(): number | null {
    const { startDate, endDate } = this.form.getRawValue();
    if (!startDate || !endDate || this.form.hasError('dateRange')) return null;
    return countDays(startDate, endDate);
  }

  showError(name: keyof typeof this.form.controls, error: string): boolean {
    const control = this.form.controls[name];
    return control.hasError(error) && (control.touched || control.dirty);
  }

  get showDateRangeError(): boolean {
    const { startDate, endDate } = this.form.controls;
    return this.form.hasError('dateRange') && (startDate.touched || endDate.touched);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { employeeId, type, startDate, endDate } = this.form.getRawValue();
    this.submitting.set(true);
    this.serverError.set(null);

    this.service.create({ employeeId: employeeId!, type: type!, startDate, endDate })
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: request => {
          this.form.reset();
          this.created.emit(request);
        },
        error: err => this.serverError.set(getErrorMessage(err))
      });
  }
}
