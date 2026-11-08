export interface LoginRequest {
  email: string;
  password: string;
  tenantCode: string;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: 'Bearer' | string;
  expiresAtUtc: string;
  userId: string;
  displayName: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
}

export interface CurrentUserResponse {
  userId: string;
  email: string;
  tenantId: string;
  tenantCode: string;
  membershipId: string;
}

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  displayName: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  email?: string;
  membershipId?: string;
}
