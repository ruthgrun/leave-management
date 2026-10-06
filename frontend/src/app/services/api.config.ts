import { InjectionToken } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => 'http://localhost:5080/api'
});

/** Turns an API error (ProblemDetails / validation errors) into a message for the user. */
export function getErrorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) return 'Something went wrong. Please try again.';
  if (err.status === 0) return 'Cannot reach the server. Please check your connection.';

  const body = err.error;
  if (body?.errors) {
    return Object.values(body.errors as Record<string, string[]>).flat().join(' ');
  }
  return body?.detail ?? body?.title ?? 'Something went wrong. Please try again.';
}
