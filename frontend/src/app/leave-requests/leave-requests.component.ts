import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import {
  LEAVE_STATUS_LABELS,
  LEAVE_TYPE_LABELS,
  LeaveRequest,
  LeaveStatus
} from '../models/leave-request.model';
import { LeaveRequestsService } from '../services/leave-requests.service';
import { getErrorMessage } from '../services/api.config';
import { LeaveRequestFormComponent } from '../leave-request-form/leave-request-form.component';

interface Notice {
  kind: 'success' | 'error';
  text: string;
}

@Component({
  selector: 'app-leave-requests',
  standalone: true,
  imports: [DatePipe, LeaveRequestFormComponent],
  templateUrl: './leave-requests.component.html',
  styleUrls: ['./leave-requests.component.css']
})
export class LeaveRequestsComponent implements OnInit {
  private readonly service = inject(LeaveRequestsService);
  private readonly destroyRef = inject(DestroyRef);

  readonly requests = signal<LeaveRequest[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly approvingIds = signal<ReadonlySet<number>>(new Set());
  readonly notice = signal<Notice | null>(null);

  readonly LeaveStatus = LeaveStatus;
  readonly typeLabels = LEAVE_TYPE_LABELS;
  readonly statusLabels = LEAVE_STATUS_LABELS;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.service.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: requests => {
          this.requests.set(requests);
          this.loading.set(false);
        },
        error: err => {
          this.loadError.set(getErrorMessage(err));
          this.loading.set(false);
        }
      });
  }

  onCreated(request: LeaveRequest): void {
    this.requests.update(list => [request, ...list]);
  }

  isApproving(id: number): boolean {
    return this.approvingIds().has(id);
  }

  approve(request: LeaveRequest): void {
    const id = request.id;
    this.setApproving(id, true);
    this.notice.set(null);

    this.service.approve(id)
      .pipe(
        finalize(() => this.setApproving(id, false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: updated => {
          this.replaceRequest(updated);
          this.notice.set({ kind: 'success', text: `Request #${id} (${updated.employeeName}) approved.` });
        },
        error: (err: unknown) => {
          const status = err instanceof HttpErrorResponse ? err.status : null;
          if (status === 409) {
            // Someone else handled it - sync our list with the server instead of guessing the new status.
            this.notice.set({ kind: 'error', text: `Request #${id} was already handled by someone else. The list was updated.` });
            this.refresh();
          } else if (status === 404) {
            this.requests.update(list => list.filter(r => r.id !== id));
            this.notice.set({ kind: 'error', text: `Request #${id} no longer exists.` });
          } else {
            this.notice.set({ kind: 'error', text: `Could not approve request #${id}: ${getErrorMessage(err)}` });
          }
        }
      });
  }

  dismissNotice(): void {
    this.notice.set(null);
  }

  /** Reloads the list in place, without the full-page loading state. */
  private refresh(): void {
    this.service.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: requests => this.requests.set(requests), error: () => {} });
  }

  private replaceRequest(updated: LeaveRequest): void {
    this.requests.update(list => list.map(r => (r.id === updated.id ? updated : r)));
  }

  private setApproving(id: number, approving: boolean): void {
    this.approvingIds.update(ids => {
      const next = new Set(ids);
      if (approving) next.add(id);
      else next.delete(id);
      return next;
    });
  }
}
