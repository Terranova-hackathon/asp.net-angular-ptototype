import { HttpErrorResponse } from '@angular/common/http';

/** Turns an API ProblemDetails / validation response into one readable message. */
export function apiErrorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  const body = error.error as
    | { detail?: string; title?: string; errors?: Record<string, string[]> }
    | null;

  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ');
  }

  return body?.detail ?? body?.title ?? fallback;
}
