import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  ClipboardList,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Mail,
  Menu,
  Moon,
  ShieldCheck,
  Sun,
  UserCircle,
  Users,
  X,
} from 'lucide-react';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { useTheme } from '../../hooks/useTheme';
import { cn } from '../../lib/format';

interface NavItem {
  to: string;
  label: string;
  icon: typeof Users;
  permission?: string;
}

const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Inicio', icon: LayoutDashboard },
  { to: '/usuarios', label: 'Usuarios', icon: Users, permission: PERMISSIONS.usuarios.leer },
  { to: '/roles', label: 'Roles', icon: ShieldCheck, permission: PERMISSIONS.roles.leer },
  { to: '/permisos', label: 'Permisos', icon: KeyRound, permission: PERMISSIONS.permisos.leer },
  { to: '/auditoria', label: 'Auditoría', icon: ClipboardList, permission: PERMISSIONS.auditoria.leer },
  { to: '/perfil', label: 'Mi perfil', icon: UserCircle },
];

const SHOW_DEV_MAILBOX = import.meta.env.VITE_SHOW_DEV_MAILBOX !== 'false';

export function AppLayout() {
  const { user, logout, can } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);

  // Las opciones sin permiso se OCULTAN (la API igualmente respondería 403).
  const items = NAV_ITEMS.filter((item) => !item.permission || can(item.permission));

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  const sidebar = (
    <nav className="flex h-full flex-col gap-1 p-4" aria-label="Navegación principal">
      <div className="mb-6 flex items-center gap-2 px-2">
        <img src="/favicon.svg" alt="" className="h-8 w-8" />
        <div>
          <p className="text-sm font-semibold leading-tight">usuarios-roles-base</p>
          <p className="text-xs text-slate-500 dark:text-slate-400">Auth & RBAC</p>
        </div>
      </div>
      {items.map(({ to, label, icon: Icon }) => (
        <NavLink
          key={to}
          to={to}
          end={to === '/'}
          onClick={() => setMobileOpen(false)}
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'bg-brand-50 text-brand-700 dark:bg-brand-900/40 dark:text-brand-200'
                : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800',
            )
          }
        >
          <Icon className="h-4 w-4" aria-hidden />
          {label}
        </NavLink>
      ))}
      {SHOW_DEV_MAILBOX && (
        <NavLink
          to="/dev/buzon"
          onClick={() => setMobileOpen(false)}
          className="mt-auto flex items-center gap-3 rounded-lg px-3 py-2 text-sm text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
        >
          <Mail className="h-4 w-4" aria-hidden /> Buzón de desarrollo
        </NavLink>
      )}
    </nav>
  );

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[16rem_1fr]">
      <aside className="hidden border-r border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900 lg:block">
        <div className="sticky top-0 h-screen">{sidebar}</div>
      </aside>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-900/50" onClick={() => setMobileOpen(false)} aria-hidden />
          <aside className="relative h-full w-64 bg-white shadow-xl dark:bg-slate-900">
            <button className="absolute right-3 top-3 p-1" onClick={() => setMobileOpen(false)} aria-label="Cerrar menú">
              <X className="h-5 w-5" />
            </button>
            {sidebar}
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-slate-200 bg-white/80 px-4 backdrop-blur dark:border-slate-800 dark:bg-slate-900/80">
          <button className="rounded p-1.5 lg:hidden" onClick={() => setMobileOpen(true)} aria-label="Abrir menú">
            <Menu className="h-5 w-5" />
          </button>
          <div className="ml-auto flex items-center gap-2">
            <button
              onClick={toggle}
              className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
              aria-label={theme === 'dark' ? 'Activar modo claro' : 'Activar modo oscuro'}
            >
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </button>
            <div className="hidden text-right sm:block">
              <p className="text-sm font-medium leading-tight">{user?.fullName}</p>
              <p className="text-xs text-slate-500 dark:text-slate-400">{user?.roles.join(', ')}</p>
            </div>
            <button
              onClick={handleLogout}
              className="flex items-center gap-1 rounded-lg px-2 py-2 text-sm text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              <LogOut className="h-4 w-4" aria-hidden />
              <span className="hidden sm:inline">Salir</span>
            </button>
          </div>
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 p-4 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
