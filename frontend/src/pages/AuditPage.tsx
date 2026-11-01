import { useEffect, useState } from 'react';
import { Search } from 'lucide-react';
import { auditApi } from '../api/services';
import type { AuditEvent } from '../api/types';
import { DataTable, type Column } from '../components/ui/DataTable';
import { Select } from '../components/ui/Field';
import { Badge, PageHeader } from '../components/ui/Feedback';
import { Pagination } from '../components/ui/Pagination';
import { usePagedQuery } from '../hooks/usePagedQuery';
import { eventLabel, formatDateTime } from '../lib/format';

const columns: Column<AuditEvent>[] = [
  { key: 'date', header: 'Fecha', className: 'whitespace-nowrap', render: (e) => formatDateTime(e.occurredAt) },
  {
    key: 'event',
    header: 'Evento',
    render: (e) => (
      <div>
        <p className="font-medium">{eventLabel(e.eventType)}</p>
        <code className="text-[11px] text-slate-500">{e.eventType}</code>
      </div>
    ),
  },
  { key: 'result', header: 'Resultado', render: (e) => <Badge tone={e.succeeded ? 'green' : 'red'}>{e.succeeded ? 'Éxito' : 'Fallo'}</Badge> },
  { key: 'email', header: 'Usuario', render: (e) => e.email ?? <span className="text-slate-400">—</span> },
  { key: 'ip', header: 'IP', className: 'whitespace-nowrap', render: (e) => e.ipAddress ?? '—' },
  { key: 'details', header: 'Detalle', render: (e) => <span className="line-clamp-2 max-w-xs text-xs text-slate-500">{e.details ?? '—'}</span> },
];

type Filters = { eventType?: string; succeeded?: string; from?: string; to?: string };

export function AuditPage() {
  const query = usePagedQuery<AuditEvent, Filters>(auditApi.list, { pageSize: 20 });
  const [eventTypes, setEventTypes] = useState<string[]>([]);

  useEffect(() => {
    auditApi.eventTypes().then(setEventTypes).catch(() => setEventTypes([]));
  }, []);

  // Las fechas del filtro son locales: se envían como ISO (UTC) para que la API compare correctamente.
  const setDate = (key: 'from' | 'to', value: string) => {
    if (!value) return query.setFilter(key, undefined);
    const date = new Date(`${value}T${key === 'from' ? '00:00:00' : '23:59:59'}`);
    query.setFilter(key, date.toISOString());
  };

  return (
    <div>
      <PageHeader title="Auditoría" description="Registro inmutable de eventos de seguridad: logins, bloqueos, cambios de permisos, reuso de tokens…" />
      <div className="mb-4 grid grid-cols-1 gap-3 md:grid-cols-[1fr_auto_auto] lg:grid-cols-[1fr_auto_auto_auto_auto]">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input className="input pl-9" placeholder="Buscar por email, IP o detalle…" aria-label="Buscar eventos" value={query.search} onChange={(e) => query.setSearch(e.target.value)} />
        </div>
        <Select label="Tipo de evento" hideLabel value={query.filters.eventType ?? ''} onChange={(e) => query.setFilter('eventType', e.target.value || undefined)}>
          <option value="">Todos los eventos</option>
          {eventTypes.map((t) => (
            <option key={t} value={t}>{eventLabel(t)}</option>
          ))}
        </Select>
        <Select label="Resultado" hideLabel value={query.filters.succeeded ?? ''} onChange={(e) => query.setFilter('succeeded', e.target.value || undefined)}>
          <option value="">Todos</option>
          <option value="true">Éxito</option>
          <option value="false">Fallo</option>
        </Select>
        <input type="date" className="input" aria-label="Desde" onChange={(e) => setDate('from', e.target.value)} />
        <input type="date" className="input" aria-label="Hasta" onChange={(e) => setDate('to', e.target.value)} />
      </div>
      <DataTable
        columns={columns}
        rows={query.data?.items}
        rowKey={(e) => e.id}
        loading={query.loading}
        error={query.error}
        onRetry={query.reload}
        emptyTitle="Sin eventos"
        emptyDescription="No hay eventos que coincidan con los filtros."
        footer={
          query.data && query.data.totalCount > 0 && (
            <Pagination page={query.data.page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} pageSize={query.pageSize} onPageChange={query.setPage} onPageSizeChange={query.setPageSize} />
          )
        }
      />
    </div>
  );
}
