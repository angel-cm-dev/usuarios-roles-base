import { api } from './client';
import type {
  AuditEvent,
  AuthResponse,
  DashboardStats,
  DevEmail,
  MessageResponse,
  PagedParams,
  PagedResult,
  Permission,
  Role,
  UserDetail,
  UserListItem,
  UserProfile,
} from './types';

// Funciones finas por recurso: los componentes nunca construyen URLs a mano.

export const authApi = {
  login: (email: string, password: string) => api.post<AuthResponse>('/auth/login', { email, password }, false),
  register: (data: { email: string; fullName: string; password: string }) =>
    api.post<MessageResponse>('/auth/register', data, false),
  verifyEmail: (token: string) => api.post<MessageResponse>('/auth/verify-email', { token }, false),
  resendVerification: (email: string) => api.post<MessageResponse>('/auth/resend-verification', { email }, false),
  forgotPassword: (email: string) => api.post<MessageResponse>('/auth/forgot-password', { email }, false),
  resetPassword: (token: string, newPassword: string) =>
    api.post<MessageResponse>('/auth/reset-password', { token, newPassword }, false),
  logout: () => api.post<void>('/auth/logout', undefined, false),
  logoutAll: () => api.post<void>('/auth/logout-all'),
  me: () => api.get<UserProfile>('/auth/me'),
  updateProfile: (fullName: string) => api.put<UserProfile>('/auth/me', { fullName }),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<AuthResponse>('/auth/change-password', { currentPassword, newPassword }),
};

export const usersApi = {
  list: (params: PagedParams, signal?: AbortSignal) => api.get<PagedResult<UserListItem>>('/users', params, signal),
  get: (id: string) => api.get<UserDetail>(`/users/${id}`),
  create: (data: { email: string; fullName: string; password: string; emailConfirmed: boolean; roleIds: string[] }) =>
    api.post<UserDetail>('/users', data),
  update: (id: string, data: { fullName: string; isActive: boolean }) => api.put<UserDetail>(`/users/${id}`, data),
  setRoles: (id: string, roleIds: string[]) => api.put<UserDetail>(`/users/${id}/roles`, { roleIds }),
  lock: (id: string, minutes?: number) => api.post<UserDetail>(`/users/${id}/lock`, { minutes: minutes ?? null }),
  unlock: (id: string) => api.post<UserDetail>(`/users/${id}/unlock`),
  revokeSessions: (id: string) => api.post<{ revoked: number }>(`/users/${id}/revoke-sessions`),
};

export const rolesApi = {
  list: (params: PagedParams, signal?: AbortSignal) => api.get<PagedResult<Role>>('/roles', params, signal),
  all: () => api.get<PagedResult<Role>>('/roles', { pageSize: 100, sortBy: 'name' }),
  create: (data: { name: string; description: string; permissions: string[] }) => api.post<Role>('/roles', data),
  update: (id: string, data: { name: string; description: string }) => api.put<Role>(`/roles/${id}`, data),
  setPermissions: (id: string, permissions: string[]) => api.put<Role>(`/roles/${id}/permissions`, { permissions }),
  remove: (id: string) => api.delete<void>(`/roles/${id}`),
};

export const permissionsApi = {
  list: (params: PagedParams, signal?: AbortSignal) => api.get<PagedResult<Permission>>('/permissions', params, signal),
  all: () => api.get<PagedResult<Permission>>('/permissions', { pageSize: 100, sortBy: 'module' }),
  modules: () => api.get<string[]>('/permissions/modules'),
};

export const auditApi = {
  list: (params: PagedParams, signal?: AbortSignal) => api.get<PagedResult<AuditEvent>>('/audit', params, signal),
  eventTypes: () => api.get<string[]>('/audit/event-types'),
};

export const dashboardApi = {
  stats: () => api.get<DashboardStats>('/dashboard/stats'),
};

export const devApi = {
  mailbox: (to?: string) => api.get<DevEmail[]>('/dev/mailbox', { to }),
  clear: () => api.delete<void>('/dev/mailbox'),
};
