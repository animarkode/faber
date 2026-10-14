import { environment } from '../../../environments/environment';

const base = environment.api;

function subResource(entity: string) {
  return {
    root: (resumeId: string) => `${base}/resumes/${resumeId}/${entity}`,
    byId: (resumeId: string, id: string) => `${base}/resumes/${resumeId}/${entity}/${id}`,
    reorder: (resumeId: string) => `${base}/resumes/${resumeId}/${entity}/reorder`,
  };
}

export const API_ROUTES = {
  auth: {
    signIn: `${base}/auth/sign-in`,
    signUp: `${base}/auth/sign-up`,
    signOut: `${base}/auth/sign-out`,
    me: `${base}/auth/me`,
    refresh: `${base}/auth/refresh`,
    verifyEmail: `${base}/auth/verify-email`,
    forgotPassword: `${base}/auth/forgot-password`,
    resetPassword: `${base}/auth/reset-password`,
    external: {
      providers: `${base}/auth/external/providers`,
      start: (provider: string) => `${base}/auth/external/${provider}`,
    },
  },
  identity: {
    verifyActionToken: `${base}/identity/verify-action-token`,
  },
  users: {
    byId: (id: string) => `${base}/users/${id}`,
  },
  resumes: {
    root: `${base}/resumes`,
    byId: (id: string) => `${base}/resumes/${id}`,
    download: (id: string) => `${base}/resumes/${id}/download`,
    generate: (id: string) => `${base}/resumes/${id}/generate`,
    title: (id: string) => `${base}/resumes/${id}/title`,
    summary: (id: string) => `${base}/resumes/${id}/summary`,
    hobbies: (id: string) => `${base}/resumes/${id}/hobbies`,
    localization: (id: string) => `${base}/resumes/${id}/localization`,
    persons: subResource('persons'),
    skills: subResource('skills'),
    languages: subResource('languages'),
    links: subResource('links'),
    courses: subResource('courses'),
    projects: subResource('projects'),
    educations: subResource('educations'),
    experience: subResource('experiences'),
  },
};
