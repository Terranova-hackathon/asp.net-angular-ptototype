export interface User {
  id: string;
  email: string;
  displayName: string | null;
}

export interface Credentials {
  email: string;
  password: string;
}

export interface RegisterRequest extends Credentials {
  displayName?: string;
}
