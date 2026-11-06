import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client';
import type { AboutInfoDto } from '../../core/models/about-info.model';

@Injectable({ providedIn: 'root' })
export class AboutApiService {
  private readonly api = inject(ApiClient);

  getAbout(): Observable<AboutInfoDto> {
    return this.api.get<AboutInfoDto>('about');
  }
}
