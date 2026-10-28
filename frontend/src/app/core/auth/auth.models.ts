export interface User {
  id: string;
  email: string;
  displayName: string | null;
  /** Set once a Steam account is linked; the backend uses it to fetch Steam data. */
  steamId: string | null;
}

export interface Credentials {
  email: string;
  password: string;
}

export interface RegisterRequest extends Credentials {
  displayName?: string;
}
