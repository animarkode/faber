/** The Keycloak flows the API can send the browser to; each returns to `/account?action={name}&status=…`. */
export type AccountAction =
  | 'password'
  | 'passkey'
  | 'totp'
  | 'email'
  | 'link-google'
  | 'link-github'
  | 'delete';

export interface ProfileField {
  name: string;
  displayName: string;
  required: boolean;
  readOnly: boolean;
  minLength: number | null;
  maxLength: number | null;
  value: string | null;
}

export interface AccountProfile {
  username: string | null;
  email: string | null;
  emailVerified: boolean;
  firstName: string | null;
  lastName: string | null;
  fields: ProfileField[];
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  attributes: Record<string, string>;
}

export interface StoredCredential {
  id: string;
  userLabel: string | null;
  createdDate: string | null;
}

export interface CredentialGroup {
  type: string;
  category: string;
  displayName: string;
  helpText: string | null;
  createAction: AccountAction | null;
  updateAction: AccountAction | null;
  removeable: boolean;
  credentials: StoredCredential[];
}

export interface AccountDevice {
  id: string;
  device: string | null;
  os: string | null;
  osVersion: string | null;
  browser: string | null;
  ipAddress: string | null;
  started: string | null;
  lastAccess: string | null;
  expires: string | null;
  current: boolean;
  mobile: boolean;
}

export interface LinkedAccount {
  providerAlias: string;
  displayName: string;
  connected: boolean;
  linkedUsername: string | null;
  linkAction: AccountAction | null;
}
