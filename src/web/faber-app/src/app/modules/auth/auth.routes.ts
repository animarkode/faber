import { Routes } from '@angular/router';
import { Auth } from './auth';

export const authRoutes: Routes = [
  {
    path: '',
    component: Auth,
    children: [
      { path: '', redirectTo: 'sign-in', pathMatch: 'full' },
      {
        path: 'sign-in',
        loadComponent: () => import('./sign-in/sign-in').then(m => m.SignIn),
      },
      {
        path: 'sign-up',
        loadComponent: () => import('./sign-up/sign-up').then(m => m.SignUp),
      },
      {
        path: 'forgot-password',
        loadComponent: () => import('./forgot-password/forgot-password').then(m => m.ForgotPassword),
      },
      {
        path: 'reset-password',
        loadComponent: () => import('./reset-password/reset-password').then(m => m.ResetPassword),
      },
      {
        path: 'verify-email',
        loadComponent: () => import('./verify-email/verify-email').then(m => m.VerifyEmail),
      },
      {
        path: 'external/callback',
        loadComponent: () =>
          import('./external-sign-in/external-callback/external-callback').then(m => m.ExternalCallback),
      },
    ],
  },
];
