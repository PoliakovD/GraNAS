export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface TokenResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
  token_type: string;
}

export interface RegisterResponse {
  userId: string;
  message: string;
}

export interface CurrentUser {
  id: string;
  email: string;
}

export interface ErrorResponse {
  error: string;
  errorDescription: string;
}
