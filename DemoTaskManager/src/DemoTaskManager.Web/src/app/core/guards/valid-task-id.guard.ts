import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Rejects malformed ids before a pointless HTTP round-trip is made. */
export const validTaskIdGuard: CanActivateFn = (route) => {
  const id = route.paramMap.get('id');
  if (id && GUID_PATTERN.test(id)) {
    return true;
  }

  return inject(Router).createUrlTree(['/not-found']);
};
