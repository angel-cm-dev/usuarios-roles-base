import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Activity, KeyRound, Lock, MailWarning, ShieldAlert, ShieldCheck, UserCheck, Users } from 'lucide-react';
import { dashboardApi } from '../api/services';
import type { DashboardStats } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Can } from '../auth/guards';
import { PERMISSIONS } from '../auth/permissions';
import { Badge, ErrorState, PageHeader } from '../components/ui/Feedback';
import { Spinner } from '../components/ui/Spinner';
import { errorMessage } from '../hooks/usePagedQuery';
import { eventLabel, formatDate, formatDateTime, parseUtc } from '../lib/format';

function Kpi({ label, value, icon: Icon, tone }: { label: string; value: number; icon: typeof Users; tone: string }) {
  return (
    <div className="card flex items-center gap-4 p-4">
      <div className={`rounded-lg p-2.5 ${tone}`}>
        <Icon className="h-5 w-5" aria-hidden />
      </div>
      <div>
        <p className="text-2xl font-semibold tabular-nums">{value}</p>
        <p className="text-xs text-slate-500 dark:text-slate-400">{label}</p>
      </div>
    </div>
  );
}

function LoginChart({ data }: { data: DashboardStats['loginsLast7Days'] }) {
  const max = Math.max(1, ...data.map((d) => d.succeeded + d.failed));
  const barHeight = 128; // px: alturas absolutas para que las barras no dependan de porcentajes en flex.
  return (
    <div className="card p-4">
      <h2 className="mb-4 text-sm font-semibold">Inicios de sesión (últimos 7 días)</h2>
      <div className="flex items-end gap-2" role="img" aria-label="Gráfico de inicios de sesión exitosos y fallidos por día">
        {data.map((day) => (
          <div key={day.date} className="flex flex-1 flex-col items-center gap-1">
            <span className="text-[10px] tabular-nums text-slate-500">{day.succeeded + day.failed || ''}</span>
            <div className="flex w-full flex-col justify-end overflow-hidden rounded-md bg-slate-100 dark:bg-slate-800" style={{ height: barHeight }}>
              <div className="bg-red-400" style={{ height: (day.failed / max) * barHeight }} title={`${day.failed} fallidos`} />
              <div className="bg-brand-500" style={{ height: (day.succeeded / max) * barHeight }} title={`${day.succeeded} exitosos`} />
            </div>
            <span className="text-[10px] text-slate-500">{new Date(`${day.date}T00:00:00`).toLocaleDateString('es-PE', { weekday: 'short' })}</span>
          </div>
        ))}
      </div>
      <div className="mt-3 flex gap-4 text-xs text-slate-500">
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-brand-500" /> Exitosos</span>
        <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-red-400" /> Fallidos</span>
      </div>
    </div>
  );
}

function AdminDashboard() {
  const [stats, setStats] = useState<DashboardStats>();
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setError(null);
    dashboardApi.stats().then(setStats).catch((e) => setError(errorMessage(e)));
  }, []);

  useEffect(load, [load]);

  if (error) return <ErrorState message={error} onRetry={load} />;
  if (!stats) return <div className="flex justify-center py-16"><Spinner className="h-8 w-8" /></div>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi label="Usuarios registrados" value={stats.totalUsers} icon={Users} tone="bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-200" />
        <Kpi label="Usuarios activos" value={stats.activeUsers} icon={UserCheck} tone="bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300" />
        <Kpi label="Cuentas bloqueadas" value={stats.lockedUsers} icon={Lock} tone="bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300" />
        <Kpi label="Emails sin verificar" value={stats.unverifiedUsers} icon={MailWarning} tone="bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300" />
        <Kpi label="Roles" value={stats.totalRoles} icon={ShieldCheck} tone="bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300" />
        <Kpi label="Sesiones activas" value={stats.activeSessions} icon={KeyRound} tone="bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300" />
        <Kpi label="Eventos (24 h)" value={stats.eventsLast24h} icon={Activity} tone="bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300" />
        <Kpi label="Logins fallidos (24 h)" value={stats.failedLoginsLast24h} icon={ShieldAlert} tone="bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-300" />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <LoginChart data={stats.loginsLast7Days} />
        <div className="card p-4">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-sm font-semibold">Eventos recientes</h2>
            <Can permission={PERMISSIONS.auditoria.leer}>
              <Link to="/auditoria" className="text-xs text-brand-600 hover:underline dark:text-brand-400">Ver todo</Link>
            </Can>
          </div>
          {stats.recentEvents.length === 0 ? (
            <p className="py-8 text-center text-sm text-slate-500">Aún no hay eventos.</p>
          ) : (
            <ul className="divide-y divide-slate-100 dark:divide-slate-800">
              {stats.recentEvents.map((event) => (
                <li key={event.id} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{eventLabel(event.eventType)}</p>
                    <p className="truncate text-xs text-slate-500">{event.email ?? event.details ?? '—'}</p>
                  </div>
                  <div className="shrink-0 text-right">
                    <Badge tone={event.succeeded ? 'green' : 'red'}>{event.succeeded ? 'OK' : 'Fallo'}</Badge>
                    <p className="mt-0.5 text-[11px] text-slate-500">{parseUtc(event.occurredAt).toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' })}</p>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

export function DashboardPage() {
  const { user } = useAuth();

  return (
    <div>
      <PageHeader title={`Hola, ${user?.fullName.split(' ')[0] ?? ''}`} description="Resumen de seguridad y actividad del sistema." />
      <Can
        permission={PERMISSIONS.dashboard.ver}
        fallback={
          <div className="card space-y-2 p-6 text-sm">
            <p>Tu cuenta tiene el rol <strong>{user?.roles.join(', ')}</strong>.</p>
            <p className="text-slate-500 dark:text-slate-400">
              Miembro desde {formatDate(user?.createdAt)} · Último acceso {formatDateTime(user?.lastLoginAt)}.
            </p>
            <p className="text-slate-500 dark:text-slate-400">
              Los indicadores del panel requieren el permiso <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">dashboard.ver</code>.
              Desde <Link to="/perfil" className="text-brand-600 underline dark:text-brand-400">Mi perfil</Link> puedes gestionar tu cuenta.
            </p>
          </div>
        }
      >
        <AdminDashboard />
      </Can>
    </div>
  );
}
