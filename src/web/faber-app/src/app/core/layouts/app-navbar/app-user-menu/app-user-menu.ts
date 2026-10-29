import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { User } from '../../../auth/contracts/user';
import { AuthClient } from '../../../auth/auth-client';
import { APP_ROUTES } from '../../../routes/app-routes';

@Component({
  selector: 'app-user-menu',
  imports: [RouterLink],
  templateUrl: './app-user-menu.html',
  styleUrl: './app-user-menu.css',
  host: { class: 'glass-surface' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppUserMenu {
  user = input.required<User>();
  closed = output<void>();

  protected readonly routes = APP_ROUTES;
  private readonly authClient = inject(AuthClient);
  private readonly router = inject(Router);

  logout(): void {
    this.closed.emit();
    this.authClient.signOut().subscribe({
      complete: () => this.router.navigateByUrl(APP_ROUTES.home),
      error: () => this.router.navigateByUrl(APP_ROUTES.home),
    });
  }
}
