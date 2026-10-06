// Intentionally thin. Part of the task is to introduce proper typing
// across the frontend instead of the `any` usage in the component.

export interface Employee {
  id: number;
  name: string;
  annualQuota: number;
}

export interface LeaveRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  type: number;
  startDate: string;
  endDate: string;
  status: number;
  days: number;
}
