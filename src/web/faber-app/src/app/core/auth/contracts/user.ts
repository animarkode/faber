/** The signed-in user as the API reports it. It carries no token: the browser never holds one. */
export interface User {
  id: string;
  email: string | null;
  firstName: string | null;
  lastName: string | null;
  name: string | null;
  roles: string[];
}
