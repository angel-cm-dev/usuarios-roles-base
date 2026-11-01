const dateTimeFormatter = new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' });
const dateFormatter = new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium' });

/** La API devuelve fechas UTC; si no traen zona horaria, se interpretan como UTC. */
export function parseUtc(value: string): Date {
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`);
}

export function formatDateTime(value: string | null | undefined): string {
  return value ? dateTimeFormatter.format(parseUtc(value)) : '—';
}

export function formatDate(value: string | null | undefined): string {
  return value ? dateFormatter.format(parseUtc(value)) : '—';
}

/** Texto legible para los tipos de evento de auditoría. */
const EVENT_LABELS: Record<string, string> = {
  'user.registered': 'Registro',
  'user.registration-duplicate': 'Registro duplicado',
  'user.email-verified': 'Email verificado',
  'user.email-verification-failed': 'Verificación fallida',
  'auth.login-succeeded': 'Login exitoso',
  'auth.login-failed': 'Login fallido',
  'auth.login-blocked': 'Login bloqueado',
  'auth.account-locked': 'Cuenta bloqueada',
  'auth.refresh-token-reuse': 'Reuso de refresh token',
  'auth.logout': 'Logout',
  'auth.logout-all': 'Logout global',
  'auth.password-reset-requested': 'Reset solicitado',
  'auth.password-reset-completed': 'Contraseña restablecida',
  'auth.password-reset-failed': 'Reset fallido',
  'auth.password-changed': 'Contraseña cambiada',
  'user.profile-updated': 'Perfil actualizado',
  'admin.user-created': 'Usuario creado',
  'admin.user-updated': 'Usuario editado',
  'admin.user-roles-changed': 'Roles asignados',
  'admin.user-locked': 'Usuario bloqueado',
  'admin.user-unlocked': 'Usuario desbloqueado',
  'admin.sessions-revoked': 'Sesiones revocadas',
  'admin.role-created': 'Rol creado',
  'admin.role-updated': 'Rol editado',
  'admin.role-permissions-changed': 'Permisos de rol',
  'admin.role-deleted': 'Rol eliminado',
};

export function eventLabel(eventType: string): string {
  return EVENT_LABELS[eventType] ?? eventType;
}

export function cn(...classes: Array<string | false | null | undefined>): string {
  return classes.filter(Boolean).join(' ');
}
