import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <section class="page not-found">
      <h1>Page not found</h1>
      <p>The page you requested does not exist.</p>
      <a class="button button--primary" routerLink="/tasks">Back to tasks</a>
    </section>
  `,
  styles: `
    .not-found {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 0.75rem;
    }

    .not-found h1 {
      margin: 0;
      font-size: 1.6rem;
    }

    .not-found p {
      margin: 0;
      color: var(--color-muted);
    }
  `,
})
export class NotFoundPage {}
