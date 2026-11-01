import { Loader2 } from 'lucide-react';
import { cn } from '../../lib/format';

export function Spinner({ className, label = 'Cargando…' }: { className?: string; label?: string }) {
  return (
    <span role="status" className="inline-flex items-center gap-2 text-slate-500 dark:text-slate-400">
      <Loader2 className={cn('h-5 w-5 animate-spin', className)} aria-hidden />
      <span className="sr-only">{label}</span>
    </span>
  );
}

export function FullPageSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spinner className="h-8 w-8" label="Recuperando sesión…" />
    </div>
  );
}
