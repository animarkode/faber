import { Routes } from '@angular/router';
import { authGuard } from './core/auth/guards/auth-guard';
import { ResumesStore } from './modules/resumes/resumes-store';

export const routes: Routes = [
  {
    path: 'resumes/:id',
    loadComponent: () => import('./modules/resumes/builder/builder').then(m => m.Builder),
    providers: [ResumesStore],
    canActivate: [authGuard],
  },
  {
    path: '',
    loadComponent: () => import('./core/layouts/app-shell/app-shell').then(m => m.AppShell),
    children: [
      { path: '', loadComponent: () => import('./modules/home/home').then(m => m.Home) },
      { path: 'about', loadComponent: () => import('./modules/about/about').then(m => m.About) },
      {
        path: 'account',
        loadChildren: () => import('./modules/users/users.routes').then(m => m.usersRoutes),
        canActivate: [authGuard],
      },
      {
        path: 'resumes',
        loadChildren: () => import('./modules/resumes/resumes.routes').then(m => m.resumesRoutes),
        canActivate: [authGuard],
      },
      {
        path: '**',
        loadComponent: () => import('./core/layouts/not-found/not-found').then(m => m.NotFound),
      },
    ],
  },
];
