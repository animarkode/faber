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
    /** Who is signed in (the session cookie identifies the browser). */
    user: `${base}/auth/user`,
    logout: `${base}/auth/logout`,
    /** A page navigation, not a fetch: the API sends the browser on to Keycloak's sign-in page. */
    login: (returnUrl = '/') => `${base}/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`,
    /** A page navigation to Keycloak's registration page. */
    register: `${base}/auth/register`,
    account: {
      profile: `${base}/auth/account/profile`,
      credentials: `${base}/auth/account/credentials`,
      credential: (id: string) => `${base}/auth/account/credentials/${encodeURIComponent(id)}`,
      devices: `${base}/auth/account/devices`,
      linkedAccounts: `${base}/auth/account/linked-accounts`,
      linkedAccount: (alias: string) => `${base}/auth/account/linked-accounts/${encodeURIComponent(alias)}`,
      session: (id: string) => `${base}/auth/account/sessions/${encodeURIComponent(id)}`,
      otherSessions: `${base}/auth/account/sessions/others`,
      /** A page navigation, not a fetch: the API sends the browser on to Keycloak to perform the action. */
      action: (name: string) => `${base}/auth/account-action/${encodeURIComponent(name)}`,
    },
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
