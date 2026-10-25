import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { APP_ROUTES } from '../../core/routes/app-routes';
import { FbThemeToggle } from '../../shared/components/fb-theme-toggle/fb-theme-toggle';

@Component({
  selector: 'app-auth',
  imports: [RouterOutlet, RouterLink, FbThemeToggle],
  templateUrl: './auth.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class Auth {
  protected readonly APP_ROUTES = APP_ROUTES;
}
