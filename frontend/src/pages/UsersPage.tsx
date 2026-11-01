import { useEffect, useState, type FormEvent } from 'react';
import { Lock, LogOut, Pencil, Plus, Search, ShieldCheck, Unlock } from 'lucide-react';
import { ApiError } from '../api/client';
import { rolesApi, usersApi } from '../api/services';
import type { Role, UserListItem } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Can } from '../auth/guards';
import { PERMISSIONS } from '../auth/permissions';
import { Button } from '../components/ui/Button';
import { DataTable, type Column } from '../components/ui/DataTable';
import { Checkbox, Input, PasswordInput, Select } from '../components/ui/Field';
import { Alert, Badge, PageHeader } from '../components/ui/Feedback';
import { ConfirmDialog, Modal } from '../components/ui/Modal';
import { Pagination } from '../components/ui/Pagination';
import { PasswordStrengthMeter } from '../components/ui/PasswordStrengthMeter';
import { errorMessage, usePagedQuery } from '../hooks/usePagedQuery';
import { formatDateTime } from '../lib/format';
import { compactErrors, hasErrors, validateEmail, validateFullName, validateNewPassword, type FieldErrors } from '../lib/validation';

function StatusBadges({ user }: { user: UserListItem }) {
  return (
    <div className="flex flex-wrap gap-1">
      {!user.isActive ? <Badge tone="gray">Inactivo</Badge> : <Badge tone="green">Activo</Badge>}
      {user.isLockedOut && <Badge tone="red">Bloqueado</Badge>}
      {!user.emailConfirmed && <Badge tone="amber">Sin verificar</Badge>}
    </div>
  );
}

type Dialog =
  | { kind: 'create' }
  | { kind: 'edit'; user: UserListItem }
  | { kind: 'roles'; user: UserListItem }
  | { kind: 'lock'; user: UserListItem }
  | { kind: 'revoke'; user: UserListItem }
  | null;

export function UsersPage() {
  const { can, user: currentUser } = useAuth();
  const query = usePagedQuery<UserListItem, { status?: string; role?: string }>(usersApi.list, {
    initialSort: { sortBy: 'createdAt', sortDir: 'desc' },
  });
  const [roles, setRoles] = useState<Role[]>([]);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [notice, setNotice] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    if (can(PERMISSIONS.roles.leer)) {
      rolesApi.all().then((r) => setRoles(r.items)).catch(() => setRoles([]));
    }
  }, [can]);

  const done = (text: string) => {
    setDialog(null);
    setNotice({ tone: 'success', text });
    query.reload();
  };

  const runAction = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action();
      done(success);
    } catch (e) {
      setDialog(null);
      setNotice({ tone: 'error', text: errorMessage(e) });
    }
  };

  const columns: Column<UserListItem>[] = [
    {
      key: 'user',
      header: 'Usuario',
      sortKey: 'fullName',
      render: (u) => (
        <div className="min-w-[12rem]">
          <p className="font-medium">{u.fullName}</p>
          <p className="text-xs text-slate-500">{u.email}</p>
        </div>
      ),
    },
    { key: 'roles', header: 'Roles', render: (u) => <div className="flex flex-wrap gap-1">{u.roles.map((r) => <Badge key={r} tone="brand">{r}</Badge>)}</div> },
    { key: 'status', header: 'Estado', render: (u) => <StatusBadges user={u} /> },
    { key: 'lastLogin', header: 'Último acceso', sortKey: 'lastLoginAt', className: 'whitespace-nowrap', render: (u) => formatDateTime(u.lastLoginAt) },
    { key: 'created', header: 'Creado', sortKey: 'createdAt', className: 'whitespace-nowrap', render: (u) => formatDateTime(u.createdAt) },
    {
      key: 'actions',
      header: 'Acciones',
      className: 'text-right',
      render: (u) => (
        <div className="flex justify-end gap-1">
          <Can permission={PERMISSIONS.usuarios.editar}>
            <Button size="sm" variant="ghost" title="Editar" aria-label={`Editar ${u.email}`} onClick={() => setDialog({ kind: 'edit', user: u })}>
              <Pencil className="h-4 w-4" />
            </Button>
          </Can>
          <Can permission={PERMISSIONS.usuarios.asignarRoles}>
            <Button size="sm" variant="ghost" title="Roles" aria-label={`Roles de ${u.email}`} onClick={() => setDialog({ kind: 'roles', user: u })}>
              <ShieldCheck className="h-4 w-4" />
            </Button>
          </Can>
          <Can permission={PERMISSIONS.usuarios.bloquear}>
            {u.id !== currentUser?.id &&
              (u.isLockedOut ? (
                <Button size="sm" variant="ghost" title="Desbloquear" aria-label={`Desbloquear ${u.email}`} onClick={() => runAction(() => usersApi.unlock(u.id), `${u.email} desbloqueado.`)}>
                  <Unlock className="h-4 w-4" />
                </Button>
              ) : (
                <Button size="sm" variant="ghost" title="Bloquear" aria-label={`Bloquear ${u.email}`} onClick={() => setDialog({ kind: 'lock', user: u })}>
                  <Lock className="h-4 w-4" />
                </Button>
              ))}
            <Button size="sm" variant="ghost" title="Cerrar sesiones" aria-label={`Cerrar sesiones de ${u.email}`} onClick={() => setDialog({ kind: 'revoke', user: u })}>
              <LogOut className="h-4 w-4" />
            </Button>
          </Can>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Usuarios"
        description="Gestiona cuentas, estados, roles y sesiones."
        actions={
          <Can permission={PERMISSIONS.usuarios.crear}>
            <Button onClick={() => setDialog({ kind: 'create' })}>
              <Plus className="h-4 w-4" /> Nuevo usuario
            </Button>
          </Can>
        }
      />

      {notice && (
        <Alert tone={notice.tone} className="mb-4">
          {notice.text}
        </Alert>
      )}

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto_auto]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input className="input pl-9" placeholder="Buscar por nombre o email…" aria-label="Buscar usuarios" value={query.search} onChange={(e) => query.setSearch(e.target.value)} />
        </div>
        <Select label="Estado" hideLabel value={query.filters.status ?? ''} onChange={(e) => query.setFilter('status', e.target.value || undefined)}>
          <option value="">Todos los estados</option>
          <option value="active">Activos</option>
          <option value="inactive">Inactivos</option>
          <option value="locked">Bloqueados</option>
          <option value="unverified">Sin verificar</option>
        </Select>
        <Select label="Rol" hideLabel value={query.filters.role ?? ''} onChange={(e) => query.setFilter('role', e.target.value || undefined)}>
          <option value="">Todos los roles</option>
          {roles.map((r) => (
            <option key={r.id} value={r.name}>{r.name}</option>
          ))}
        </Select>
      </div>

      <DataTable
        columns={columns}
        rows={query.data?.items}
        rowKey={(u) => u.id}
        loading={query.loading}
        error={query.error}
        onRetry={query.reload}
        emptyTitle="No se encontraron usuarios"
        emptyDescription="Prueba con otros filtros o términos de búsqueda."
        sortBy={query.sortBy}
        sortDir={query.sortDir}
        onSort={query.toggleSort}
        footer={
          query.data && query.data.totalCount > 0 && (
            <Pagination
              page={query.data.page}
              totalPages={query.data.totalPages}
              totalCount={query.data.totalCount}
              pageSize={query.pageSize}
              onPageChange={query.setPage}
              onPageSizeChange={query.setPageSize}
            />
          )
        }
      />

      {dialog?.kind === 'create' && <CreateUserModal roles={roles} onClose={() => setDialog(null)} onSaved={done} />}
      {dialog?.kind === 'edit' && <EditUserModal user={dialog.user} onClose={() => setDialog(null)} onSaved={done} />}
      {dialog?.kind === 'roles' && <UserRolesModal user={dialog.user} roles={roles} onClose={() => setDialog(null)} onSaved={done} />}
      <ConfirmDialog
        open={dialog?.kind === 'lock'}
        title="Bloquear usuario"
        danger
        confirmLabel="Bloquear"
        message={<>Se bloqueará a <strong>{dialog && 'user' in dialog ? dialog.user.email : ''}</strong> hasta que lo desbloquees y se cerrarán sus sesiones.</>}
        onClose={() => setDialog(null)}
        onConfirm={() => dialog && 'user' in dialog && runAction(() => usersApi.lock(dialog.user.id), 'Usuario bloqueado y sesiones cerradas.')}
      />
      <ConfirmDialog
        open={dialog?.kind === 'revoke'}
        title="Cerrar sesiones"
        confirmLabel="Cerrar sesiones"
        message={<>Se revocarán todos los refresh tokens de <strong>{dialog && 'user' in dialog ? dialog.user.email : ''}</strong>. Deberá iniciar sesión de nuevo cuando expire su access token.</>}
        onClose={() => setDialog(null)}
        onConfirm={() => dialog && 'user' in dialog && runAction(() => usersApi.revokeSessions(dialog.user.id), 'Sesiones revocadas.')}
      />
    </div>
  );
}

type CreateField = 'fullName' | 'email' | 'password';

function CreateUserModal({ roles, onClose, onSaved }: { roles: Role[]; onClose: () => void; onSaved: (msg: string) => void }) {
  const [form, setForm] = useState({ fullName: '', email: '', password: '', emailConfirmed: true });
  const [roleIds, setRoleIds] = useState<string[]>(() => roles.filter((r) => r.name === 'usuario').map((r) => r.id));
  const [errors, setErrors] = useState<FieldErrors<CreateField>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = compactErrors<CreateField>({
      fullName: validateFullName(form.fullName),
      email: validateEmail(form.email),
      password: validateNewPassword(form.password, form.email),
    });
    setErrors(validation);
    if (hasErrors(validation)) return;

    setSaving(true);
    setServerError(null);
    try {
      await usersApi.create({ ...form, roleIds });
      onSaved(`Usuario ${form.email} creado.`);
    } catch (e) {
      if (e instanceof ApiError) {
        setErrors({ email: e.fieldError('email'), password: e.fieldError('password'), fullName: e.fieldError('fullName') });
      }
      setServerError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      title="Nuevo usuario"
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="create-user" loading={saving}>Crear</Button>
        </>
      }
    >
      <form id="create-user" className="space-y-4" onSubmit={submit} noValidate>
        {serverError && <Alert tone="error">{serverError}</Alert>}
        <Input label="Nombre completo" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} error={errors.fullName} />
        <Input label="Email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} error={errors.email} />
        <div>
          <PasswordInput label="Contraseña inicial" autoComplete="new-password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} error={errors.password} />
          <PasswordStrengthMeter password={form.password} />
        </div>
        <Checkbox
          label="Email ya verificado"
          description="Si lo desmarcas, el usuario deberá verificar su email (puede pedir el reenvío desde el login)."
          checked={form.emailConfirmed}
          onChange={(e) => setForm({ ...form, emailConfirmed: e.target.checked })}
        />
        {roles.length > 0 && (
          <fieldset>
            <legend className="label">Roles</legend>
            <div className="grid grid-cols-2 gap-2">
              {roles.map((role) => (
                <Checkbox
                  key={role.id}
                  label={role.name}
                  checked={roleIds.includes(role.id)}
                  onChange={(e) => setRoleIds((ids) => (e.target.checked ? [...ids, role.id] : ids.filter((id) => id !== role.id)))}
                />
              ))}
            </div>
          </fieldset>
        )}
      </form>
    </Modal>
  );
}

function EditUserModal({ user, onClose, onSaved }: { user: UserListItem; onClose: () => void; onSaved: (msg: string) => void }) {
  const [fullName, setFullName] = useState(user.fullName);
  const [isActive, setIsActive] = useState(user.isActive);
  const [error, setError] = useState<string>();
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = validateFullName(fullName);
    setError(validation);
    if (validation) return;
    setSaving(true);
    try {
      await usersApi.update(user.id, { fullName: fullName.trim(), isActive });
      onSaved('Usuario actualizado.');
    } catch (e) {
      setServerError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      title={`Editar ${user.email}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="edit-user" loading={saving}>Guardar</Button>
        </>
      }
    >
      <form id="edit-user" className="space-y-4" onSubmit={submit} noValidate>
        {serverError && <Alert tone="error">{serverError}</Alert>}
        <Input label="Nombre completo" value={fullName} onChange={(e) => setFullName(e.target.value)} error={error} />
        <Checkbox
          label="Cuenta activa"
          description="Una cuenta inactiva no puede iniciar sesión; al desactivarla se cierran sus sesiones."
          checked={isActive}
          onChange={(e) => setIsActive(e.target.checked)}
        />
      </form>
    </Modal>
  );
}

function UserRolesModal({ user, roles, onClose, onSaved }: { user: UserListItem; roles: Role[]; onClose: () => void; onSaved: (msg: string) => void }) {
  const [selected, setSelected] = useState<string[]>(() => roles.filter((r) => user.roles.includes(r.name)).map((r) => r.id));
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    setSaving(true);
    try {
      await usersApi.setRoles(user.id, selected);
      onSaved(`Roles de ${user.email} actualizados. Se aplicarán cuando renueve su sesión.`);
    } catch (e) {
      setServerError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      title={`Roles de ${user.email}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button onClick={save} loading={saving}>Guardar roles</Button>
        </>
      }
    >
      <div className="space-y-3">
        {serverError && <Alert tone="error">{serverError}</Alert>}
        {roles.length === 0 && <p className="text-sm text-slate-500">Necesitas el permiso roles.leer para ver los roles disponibles.</p>}
        {roles.map((role) => (
          <Checkbox
            key={role.id}
            label={role.name}
            description={role.description ?? undefined}
            checked={selected.includes(role.id)}
            onChange={(e) => setSelected((ids) => (e.target.checked ? [...ids, role.id] : ids.filter((id) => id !== role.id)))}
          />
        ))}
      </div>
    </Modal>
  );
}
