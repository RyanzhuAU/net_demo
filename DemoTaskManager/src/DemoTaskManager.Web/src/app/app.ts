import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppConfigService } from './core/config/app-config.service';
import { LoadingService } from './core/services/loading.service';
import { NotificationList } from './shared/components/notification-list/notification-list';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationList],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = 'Demo Task Manager';
  protected readonly loading = inject(LoadingService);
  protected readonly config = inject(AppConfigService);
}
