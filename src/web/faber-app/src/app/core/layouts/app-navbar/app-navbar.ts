import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { LucideAngularModule } from 'lucide-angular';
import { AuthStore } from '../../auth/auth-store';
import { API_ROUTES } from '../../routes/api-routes';
import { APP_ROUTES } from '../../routes/app-routes';
import { FbSpinner } from '../../../shared/components/fb-spinner/fb-spinner';
import { FbInitials } from '../../../shared/components/fb-initials/fb-initials';
import { FbToggleButton } from '../../../shared/components/fb-toggle-button/fb-toggle-button';
import { FbThemeToggle } from '../../../shared/components/fb-theme-toggle/fb-theme-toggle';
import { AppUserMenu } from './app-user-menu/app-user-menu';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, RouterLinkActive, LucideAngularModule, FbSpinner, FbInitials, FbToggleButton, FbThemeToggle, AppUserMenu],
  templateUrl: './app-navbar.html',
  styleUrl: './app-navbar.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'class': 'block',
    '(window:resize)': 'onResize()',
    '(window:scroll)': 'onScroll()',
  },
})
export class AppNavbar implements OnInit {
  protected readonly authStore = inject(AuthStore);
  protected readonly routes = APP_ROUTES;
  /** Full-page navigations through the API to Keycloak's own sign-in and registration pages. */
  protected readonly signInUrl = API_ROUTES.auth.login();
  protected readonly signUpUrl = API_ROUTES.auth.register;

  isUserMenuDropdownVisible = signal(false);
  isDrawerOpen = signal(false);
  isScrolled = signal(false);
  title = input('microcivi.com');

  private readonly userMenuAnchor = viewChild<ElementRef>('userMenuAnchor');
  private readonly anchorRect = signal<DOMRect | null>(null);

  protected readonly userMenuTop = computed(() => {
    const rect = this.anchorRect();
    return rect ? rect.bottom + 8 : 0;
  });

  protected readonly userMenuRight = computed(() => {
    const rect = this.anchorRect();
    return rect ? window.innerWidth - rect.right : 0;
  });

  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly elementRef = inject(ElementRef);
  private readonly document = inject(DOCUMENT);

  ngOnInit(): void {
    this.closeMenusOnNavigation();
    this.registerOutsideClickListener();
  }

  // Capture phase runs document → target before any bubble-phase
  // stopPropagation (e.g. the Material datepicker toggle), so an outside
  // click is detected even when the target swallows it on the way up.
  private registerOutsideClickListener(): void {
    const handler = (event: MouseEvent): void => this.handleOutsideClick(event);
    this.document.addEventListener('click', handler, { capture: true });
    this.destroyRef.onDestroy(() =>
      this.document.removeEventListener('click', handler, { capture: true }),
    );
  }

  private handleOutsideClick(event: MouseEvent): void {
    if (this.elementRef.nativeElement.contains(event.target as Node)) {
      return;
    }
    this.isUserMenuDropdownVisible.set(false);
    this.closeDrawer();
  }

  private closeMenusOnNavigation(): void {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        this.closeDrawer();
        this.isUserMenuDropdownVisible.set(false);
      });
  }

  onScroll(): void {
    this.isScrolled.set(window.scrollY > 10);
  }

  onResize() {
    if (window.innerWidth >= 768 && this.isDrawerOpen()) {
      this.closeDrawer();
    }
  }

  toggleUserMenuDropdown(): void {
    const anchor = this.userMenuAnchor();
    if (anchor) {
      this.anchorRect.set(anchor.nativeElement.getBoundingClientRect());
    }
    this.isUserMenuDropdownVisible.set(!this.isUserMenuDropdownVisible());
  }

  toggleDrawer(): void {
    if (this.isDrawerOpen()) {
      this.closeDrawer();
    } else {
      this.openDrawer();
    }
  }

  openDrawer(): void {
    this.isUserMenuDropdownVisible.set(false);
    this.isDrawerOpen.set(true);
  }

  closeDrawer(): void {
    this.isDrawerOpen.set(false);
  }
}
