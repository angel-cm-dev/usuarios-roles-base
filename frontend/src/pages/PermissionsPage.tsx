import { useEffect, useState } from 'react';
import { Search } from 'lucide-react';
import { permissionsApi } from '../api/services';
import type { Permission } from '../api/types';
import { DataTable, type Column } from '../components/ui/DataTable';
import { Select } from '../components/ui/Field';
import { Badge, PageHeader } from '../components/ui/Feedback';
import { Pagination } from '../components/ui/Pagination';
import { usePagedQuery } from '../hooks/usePagedQuery';

const columns: Column<Permission>[] = [
  { key: 'code', header: 'Código', sortKey: 'code', render: (p) => <code className="rounded bg-slate-100 px-1.5 py-0.5 text-xs dark:bg-slate-800">{p.code}</code> },
  { key: 'module', header: 'Módulo', sortKey: 'module', render: (p) => <Badge tone="brand">{p.module}</Badge> },
  { key: 'description', header: 'Descripción', render: (p) => p.description },
  {
    key: 'roles',
    header: 'Roles que lo tienen',
    render: (p) => (
      <div className="flex flex-wrap gap-1">
        {p.roles.length ? p.roles.map((r) => <Badge key={r}>{r}</Badge>) : <span className="text-xs text-slate-400">Ninguno</span>}
      </div>
    ),
  },
];

export function PermissionsPage() {
  const query = usePagedQuery<Permission, { module?: string }>(permissionsApi.list, { initialSort: { sortBy: 'module', sortDir: 'asc' }, pageSize: 20 });
  const [modules, setModules] = useState<string[]>([]);

  useEffect(() => {
    permissionsApi.modules().then(setModules).catch(() => setModules([]));
  }, []);

  return (
    <div>
      <PageHeader
        title="Permisos"
        description="Catálogo definido en código (Domain/Security/Permissions.cs). Se asignan a roles, no directamente a usuarios."
      />
      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input className="input pl-9" placeholder="Buscar por código o descripción…" aria-label="Buscar permisos" value={query.search} onChange={(e) => query.setSearch(e.target.value)} />
        </div>
        <Select label="Módulo" hideLabel value={query.filters.module ?? ''} onChange={(e) => query.setFilter('module', e.target.value || undefined)}>
          <option value="">Todos los módulos</option>
          {modules.map((m) => (
            <option key={m} value={m}>{m}</option>
          ))}
        </Select>
      </div>
      <DataTable
        columns={columns}
        rows={query.data?.items}
        rowKey={(p) => p.id}
        loading={query.loading}
        error={query.error}
        onRetry={query.reload}
        sortBy={query.sortBy}
        sortDir={query.sortDir}
        onSort={query.toggleSort}
        emptyTitle="Sin permisos que coincidan"
        footer={
          query.data && query.data.totalCount > 0 && (
            <Pagination page={query.data.page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} pageSize={query.pageSize} onPageChange={query.setPage} onPageSizeChange={query.setPageSize} />
          )
        }
      />
    </div>
  );
}
