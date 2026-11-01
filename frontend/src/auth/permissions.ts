// Espejo del catálogo de permisos del backend (Domain/Security/Permissions.cs).
// La UI solo los usa para ocultar acciones: la autorización REAL siempre la hace la API.
export const PERMISSIONS = {
  usuarios: {
    leer: 'usuarios.leer',
    crear: 'usuarios.crear',
    editar: 'usuarios.editar',
    bloquear: 'usuarios.bloquear',
    asignarRoles: 'usuarios.asignar-roles',
  },
  roles: {
    leer: 'roles.leer',
    crear: 'roles.crear',
    editar: 'roles.editar',
    eliminar: 'roles.eliminar',
  },
  permisos: { leer: 'permisos.leer' },
  auditoria: { leer: 'auditoria.leer' },
  dashboard: { ver: 'dashboard.ver' },
  perfil: { editar: 'perfil.editar' },
} as const;

type Leaves<T> = T extends string ? T : { [K in keyof T]: Leaves<T[K]> }[keyof T];
export type PermissionCode = Leaves<typeof PERMISSIONS>;

export function hasPermission(userPermissions: readonly string[] | undefined, required: string | string[]): boolean {
  if (!userPermissions) return false;
  const list = Array.isArray(required) ? required : [required];
  return list.some((p) => userPermissions.includes(p));
}
