import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { catchError, map, startWith, switchMap } from 'rxjs/operators';
import type { AboutInfoDto } from '../../../core/models/about-info.model';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { AboutApiService } from '../about-api.service';

interface AboutPageState {
  readonly info: AboutInfoDto | null;
  readonly loading: boolean;
  readonly failed: boolean;
}

const INITIAL_STATE: AboutPageState = { info: null, loading: true, failed: false };

@Component({
  imports: [RouterLink, EmptyState, LoadingSpinner],
  selector: 'app-about-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './about-page.scss',
  templateUrl: './about-page.html',
})
export class AboutPage {
  private readonly aboutApi = inject(AboutApiService);
  private readonly reloadToken = signal(0);

  protected readonly state = toSignal(
    toObservable(this.reloadToken).pipe(
      switchMap(() =>
        this.aboutApi.getAbout().pipe(
          map((info): AboutPageState => ({ info, loading: false, failed: false })),
          catchError(() => of<AboutPageState>({ info: null, loading: false, failed: true })),
          startWith(INITIAL_STATE),
        ),
      ),
    ),
    { initialValue: INITIAL_STATE },
  );

  protected retry(): void {
    this.reloadToken.update((token) => token + 1);
  }
}
