import { HttpErrorResponse } from '@angular/common/http';
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

// Turns HTTP failures, including .NET validation problems, into one readable message.
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (!(error instanceof HttpErrorResponse))
    return new ApiError('The request could not be completed.', 0);
  const data = (error.error ?? {}) as { detail?: string; errors?: Record<string, string[]> };
  const fieldErrors = data.errors ? Object.values(data.errors).flat().join(' ') : '';
  return new ApiError(
    data.detail ||
      fieldErrors ||
      (error.status === 401 ? 'Sign in to continue.' : 'The request could not be completed.'),
    error.status,
  );
}

export const message = (e: unknown) =>
  e instanceof HttpErrorResponse || e instanceof ApiError
    ? toApiError(e).message
    : e instanceof Error
      ? e.message
      : 'Something went wrong. Please try again.';
