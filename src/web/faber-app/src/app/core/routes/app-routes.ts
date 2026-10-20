export const APP_ROUTES = {
  home: '/',
  resumes: '/resumes',
  about: '/about',
  account: '/account',
  settings: '/settings',
  auth: {
    root: '/auth',
    signIn: '/auth/sign-in',
    signUp: '/auth/sign-up',
    forgotPassword: '/auth/forgot-password',
    resetPassword: '/auth/reset-password',
    verifyEmail: '/auth/verify-email',
    externalCallback: '/auth/external/callback',
  },
} as const;
