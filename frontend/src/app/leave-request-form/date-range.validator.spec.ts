import { FormControl, FormGroup } from '@angular/forms';
import { countDays, dateRangeValidator } from './date-range.validator';

describe('dateRangeValidator', () => {
  const build = (start: string, end: string) =>
    new FormGroup(
      { start: new FormControl(start), end: new FormControl(end) },
      { validators: dateRangeValidator('start', 'end') }
    );

  it('rejects a start date after the end date', () => {
    expect(build('2026-03-10', '2026-03-01').hasError('dateRange')).toBeTrue();
  });

  it('accepts the same start and end date', () => {
    expect(build('2026-03-01', '2026-03-01').hasError('dateRange')).toBeFalse();
  });

  it('ignores incomplete ranges', () => {
    expect(build('2026-03-10', '').hasError('dateRange')).toBeFalse();
  });
});

describe('countDays', () => {
  it('counts both ends inclusively', () => {
    expect(countDays('2026-03-01', '2026-03-03')).toBe(3);
  });
});
