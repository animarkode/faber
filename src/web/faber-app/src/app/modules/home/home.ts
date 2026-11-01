import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
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

  // The API sends the browser here with ?accountDeleted=true after Keycloak has deleted the account.
  protected readonly accountDeleted = toSignal(
    inject(ActivatedRoute).queryParamMap.pipe(map(params => params.get('accountDeleted') === 'true')),
    { initialValue: false },
  );
}
