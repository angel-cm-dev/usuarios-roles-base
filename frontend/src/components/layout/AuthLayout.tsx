import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Moon, Sun } from 'lucide-react';
import { useTheme } from '../../hooks/useTheme';

/** Layout centrado para login, registro y recuperación de contraseña. */
export function AuthLayout({ title, subtitle, children, footer }: { title: string; subtitle?: string; children: ReactNode; footer?: ReactNode }) {
  const { theme, toggle } = useTheme();
  return (
    <div className="relative flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-50 via-white to-slate-100 px-4 py-10 dark:from-slate-950 dark:via-slate-900 dark:to-slate-950">
      <button
        onClick={toggle}
        className="absolute right-4 top-4 rounded-lg p-2 text-slate-500 hover:bg-white/60 dark:text-slate-400 dark:hover:bg-slate-800"
        aria-label={theme === 'dark' ? 'Activar modo claro' : 'Activar modo oscuro'}
      >
        {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
      </button>
      <div className="w-full max-w-md">
        <Link to="/login" className="mb-6 flex items-center justify-center gap-2">
          <img src="/favicon.svg" alt="" className="h-10 w-10" />
          <span className="text-lg font-semibold">usuarios-roles-base</span>
        </Link>
        <div className="card p-6 sm:p-8">
          <h1 className="text-xl font-semibold">{title}</h1>
          {subtitle && <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{subtitle}</p>}
          <div className="mt-6">{children}</div>
        </div>
        {footer && <div className="mt-4 text-center text-sm text-slate-600 dark:text-slate-400">{footer}</div>}
      </div>
    </div>
  );
}
