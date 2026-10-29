import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_ROUTES } from '../../core/routes/app-routes';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class Home {
  // The resumes route is guarded: a signed-out visitor is sent to Keycloak's sign-in and back.
  protected readonly link = APP_ROUTES.resumes;
}
