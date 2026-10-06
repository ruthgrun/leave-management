import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Group validator: start date must not be after end date (dates as yyyy-MM-dd). */
export function dateRangeValidator(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start: string = group.get(startKey)?.value;
    const end: string = group.get(endKey)?.value;
    if (!start || !end) return null;
    return start > end ? { dateRange: true } : null;
  };
}

/** Inclusive number of calendar days between two yyyy-MM-dd dates. */
export function countDays(start: string, end: string): number {
  const msPerDay = 24 * 60 * 60 * 1000;
  return Math.round((Date.parse(end) - Date.parse(start)) / msPerDay) + 1;
}
