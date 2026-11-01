// Tipos que reflejan los DTOs de la API (.NET). Mantener sincronizados con el backend.

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  emailConfirmed: boolean;
  roles: string[];
  permissions: string[];
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  expiresAt: string;
  user: UserProfile;
}

export interface MessageResponse {
  message: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  [filter: string]: string | number | boolean | undefined;
}

export interface UserListItem {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  emailConfirmed: boolean;
  isLockedOut: boolean;
  lockoutEnd: string | null;
  roles: string[];
  createdAt: string;
  lastLoginAt: string | null;
}

export interface RoleRef {
  id: string;
  name: string;
}

export interface UserDetail extends Omit<UserListItem, 'roles'> {
  accessFailedCount: number;
  roles: RoleRef[];
  permissions: string[];
  activeSessions: number;
  passwordChangedAt: string | null;
}

export interface Role {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  userCount: number;
  permissions: string[];
  createdAt: string;
}

export interface Permission {
  id: string;
  code: string;
  module: string;
  description: string;
  roles: string[];
}

export interface AuditEvent {
  id: string;
  occurredAt: string;
  eventType: string;
  succeeded: boolean;
  userId: string | null;
  email: string | null;
  actorUserId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  details: string | null;
}

export interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  lockedUsers: number;
  unverifiedUsers: number;
  totalRoles: number;
  activeSessions: number;
  eventsLast24h: number;
  failedLoginsLast24h: number;
  loginsLast7Days: { date: string; succeeded: number; failed: number }[];
  recentEvents: AuditEvent[];
}

export interface DevEmail {
  id: string;
  to: string;
  subject: string;
  htmlBody: string;
  textBody: string;
  sentAt: string;
}
