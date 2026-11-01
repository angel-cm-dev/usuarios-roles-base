import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { KeyRound, Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { permissionsApi, rolesApi } from '../api/services';
import type { Permission, Role } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Can } from '../auth/guards';
import { PERMISSIONS } from '../auth/permissions';
import { Button } from '../components/ui/Button';
import { DataTable, type Column } from '../components/ui/DataTable';
import { Checkbox, Input } from '../components/ui/Field';
import { Alert, Badge, PageHeader } from '../components/ui/Feedback';
import { ConfirmDialog, Modal } from '../components/ui/Modal';
import { Pagination } from '../components/ui/Pagination';
import { errorMessage, usePagedQuery } from '../hooks/usePagedQuery';
import { formatDate } from '../lib/format';
import { validateRoleName } from '../lib/validation';

type Dialog = { kind: 'create' } | { kind: 'edit'; role: Role } | { kind: 'permissions'; role: Role } | { kind: 'delete'; role: Role } | null;

export function RolesPage() {
  const { can } = useAuth();
  const query = usePagedQuery<Role>(rolesApi.list, { initialSort: { sortBy: 'name', sortDir: 'asc' } });
  const [catalog, setCatalog] = useState<Permission[]>([]);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [notice, setNotice] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (can(PERMISSIONS.permisos.leer)) {
      permissionsApi.all().then((r) => setCatalog(r.items)).catch(() => setCatalog([]));
    }
  }, [can]);

  const done = (text: string) => {
    setDialog(null);
    setNotice({ tone: 'success', text });
    query.reload();
  };

  const remove = async (role: Role) => {
    setDeleting(true);
    try {
      await rolesApi.remove(role.id);
      done(`Rol ${role.name} eliminado.`);
    } catch (e) {
      setDialog(null);
      setNotice({ tone: 'error', text: errorMessage(e) });
    } finally {
      setDeleting(false);
    }
  };

  const columns: Column<Role>[] = [
    {
      key: 'name',
      header: 'Rol',
      sortKey: 'name',
      render: (r) => (
        <div>
          <p className="flex items-center gap-2 font-medium">
            {r.name} {r.isSystem && <Badge tone="sky">sistema</Badge>}
          </p>
          <p className="text-xs text-slate-500">{r.description ?? '—'}</p>
        </div>
      ),
    },
    {
      key: 'permissions',
      header: 'Permisos',
      render: (r) => (
        <div className="flex max-w-md flex-wrap gap-1">
          {r.permissions.length === 0 && <span className="text-xs text-slate-400">Sin permisos</span>}
          {r.permissions.slice(0, 6).map((p) => <Badge key={p}>{p}</Badge>)}
          {r.permissions.length > 6 && <Badge tone="brand">+{r.permissions.length - 6}</Badge>}
        </div>
      ),
    },
    { key: 'users', header: 'Usuarios', sortKey: 'userCount', render: (r) => <span className="tabular-nums">{r.userCount}</span> },
    { key: 'created', header: 'Creado', sortKey: 'createdAt', className: 'whitespace-nowrap', render: (r) => formatDate(r.createdAt) },
    {
      key: 'actions',
      header: 'Acciones',
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <Can permission={PERMISSIONS.roles.editar}>
            <Button size="sm" variant="ghost" title="Editar" aria-label={`Editar ${r.name}`} onClick={() => setDialog({ kind: 'edit', role: r })}>
              <Pencil className="h-4 w-4" />
            </Button>
            {r.name !== 'admin' && (
              <Button size="sm" variant="ghost" title="Permisos" aria-label={`Permisos de ${r.name}`} onClick={() => setDialog({ kind: 'permissions', role: r })}>
                <KeyRound className="h-4 w-4" />
              </Button>
            )}
          </Can>
          <Can permission={PERMISSIONS.roles.eliminar}>
            {!r.isSystem && (
              <Button size="sm" variant="ghost" title="Eliminar" aria-label={`Eliminar ${r.name}`} onClick={() => setDialog({ kind: 'delete', role: r })}>
                <Trash2 className="h-4 w-4 text-red-500" />
              </Button>
            )}
          </Can>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Roles"
        description="Un rol agrupa permisos. El rol admin siempre tiene el catálogo completo."
        actions={
          <Can permission={PERMISSIONS.roles.crear}>
            <Button onClick={() => setDialog({ kind: 'create' })}>
              <Plus className="h-4 w-4" /> Nuevo rol
            </Button>
          </Can>
        }
      />
      {notice && <Alert tone={notice.tone} className="mb-4">{notice.text}</Alert>}
      <div className="relative mb-4">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
        <input className="input pl-9" placeholder="Buscar rol…" aria-label="Buscar roles" value={query.search} onChange={(e) => query.setSearch(e.target.value)} />
      </div>
      <DataTable
        columns={columns}
        rows={query.data?.items}
        rowKey={(r) => r.id}
        loading={query.loading}
        error={query.error}
        onRetry={query.reload}
        emptyTitle="No hay roles que coincidan"
        sortBy={query.sortBy}
        sortDir={query.sortDir}
        onSort={query.toggleSort}
        footer={
          query.data && query.data.totalCount > 0 && (
            <Pagination page={query.data.page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} pageSize={query.pageSize} onPageChange={query.setPage} onPageSizeChange={query.setPageSize} />
          )
        }
      />

      {(dialog?.kind === 'create' || dialog?.kind === 'edit') && (
        <RoleFormModal role={dialog.kind === 'edit' ? dialog.role : undefined} catalog={catalog} onClose={() => setDialog(null)} onSaved={done} />
      )}
      {dialog?.kind === 'permissions' && <RolePermissionsModal role={dialog.role} catalog={catalog} onClose={() => setDialog(null)} onSaved={done} />}
      <ConfirmDialog
        open={dialog?.kind === 'delete'}
        title="Eliminar rol"
        danger
        loading={deleting}
        confirmLabel="Eliminar"
        message={<>¿Eliminar el rol <strong>{dialog?.kind === 'delete' ? dialog.role.name : ''}</strong>? Solo es posible si no tiene usuarios asignados.</>}
        onClose={() => setDialog(null)}
        onConfirm={() => dialog?.kind === 'delete' && remove(dialog.role)}
      />
    </div>
  );
}

/** Selector de permisos agrupado por módulo. */
function PermissionPicker({ catalog, selected, onChange }: { catalog: Permission[]; selected: string[]; onChange: (codes: string[]) => void }) {
  const modules = useMemo(() => {
    const groups = new Map<string, Permission[]>();
    catalog.forEach((p) => groups.set(p.module, [...(groups.get(p.module) ?? []), p]));
    return [...groups.entries()];
  }, [catalog]);

  if (catalog.length === 0) {
    return <p className="text-sm text-slate-500">Necesitas el permiso permisos.leer para ver el catálogo.</p>;
  }

  const toggle = (code: string, checked: boolean) => onChange(checked ? [...selected, code] : selected.filter((c) => c !== code));

  return (
    <div className="space-y-4">
      {modules.map(([module, permissions]) => (
        <fieldset key={module} className="rounded-lg border border-slate-200 p-3 dark:border-slate-800">
          <legend className="px-1 text-xs font-semibold uppercase tracking-wide text-slate-500">{module}</legend>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {permissions.map((p) => (
              <Checkbox key={p.code} label={p.code} description={p.description} checked={selected.includes(p.code)} onChange={(e) => toggle(p.code, e.target.checked)} />
            ))}
          </div>
        </fieldset>
      ))}
    </div>
  );
}

function RoleFormModal({ role, catalog, onClose, onSaved }: { role?: Role; catalog: Permission[]; onClose: () => void; onSaved: (msg: string) => void }) {
  const [name, setName] = useState(role?.name ?? '');
  const [description, setDescription] = useState(role?.description ?? '');
  const [permissions, setPermissions] = useState<string[]>([]);
  const [error, setError] = useState<string>();
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = validateRoleName(name);
    setError(validation);
    if (validation) return;
    setSaving(true);
    try {
      if (role) {
        await rolesApi.update(role.id, { name: name.trim(), description: description.trim() });
        onSaved('Rol actualizado.');
      } else {
        await rolesApi.create({ name: name.trim(), description: description.trim(), permissions });
        onSaved(`Rol ${name.trim().toLowerCase()} creado.`);
      }
    } catch (e) {
      setServerError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      size="lg"
      title={role ? `Editar rol ${role.name}` : 'Nuevo rol'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="role-form" loading={saving}>Guardar</Button>
        </>
      }
    >
      <form id="role-form" className="space-y-4" onSubmit={submit} noValidate>
        {serverError && <Alert tone="error">{serverError}</Alert>}
        <Input label="Nombre" value={name} onChange={(e) => setName(e.target.value)} error={error} disabled={role?.isSystem} hint="Se guarda en minúsculas y viaja como claim 'role' en el JWT." />
        <Input label="Descripción" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={250} />
        {!role && (
          <div>
            <p className="label">Permisos iniciales</p>
            <PermissionPicker catalog={catalog} selected={permissions} onChange={setPermissions} />
          </div>
        )}
      </form>
    </Modal>
  );
}

function RolePermissionsModal({ role, catalog, onClose, onSaved }: { role: Role; catalog: Permission[]; onClose: () => void; onSaved: (msg: string) => void }) {
  const [selected, setSelected] = useState<string[]>(role.permissions);
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    setSaving(true);
    try {
      await rolesApi.setPermissions(role.id, selected);
      onSaved(`Permisos de ${role.name} actualizados. Los usuarios los verán al renovar su sesión.`);
    } catch (e) {
      setServerError(errorMessage(e));
      setSaving(false);
    }
  };

  return (
    <Modal
      open
      size="lg"
      title={`Permisos del rol ${role.name}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button onClick={save} loading={saving} disabled={catalog.length === 0}>Guardar permisos</Button>
        </>
      }
    >
      {serverError && <Alert tone="error" className="mb-3">{serverError}</Alert>}
      <PermissionPicker catalog={catalog} selected={selected} onChange={setSelected} />
    </Modal>
  );
}
