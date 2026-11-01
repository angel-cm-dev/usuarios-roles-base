import { Check, X } from 'lucide-react';
import { evaluatePassword } from '../../lib/passwordStrength';
import { cn } from '../../lib/format';

const barColors = ['bg-red-500', 'bg-orange-500', 'bg-amber-400', 'bg-lime-500', 'bg-emerald-500'];

/** Medidor visual de fortaleza + checklist de la política de contraseñas. */
export function PasswordStrengthMeter({ password }: { password: string }) {
  const strength = evaluatePassword(password);

  return (
    <div className="mt-2 space-y-2" aria-live="polite">
      <div className="flex gap-1" aria-hidden>
        {[0, 1, 2, 3].map((index) => (
          <div
            key={index}
            className={cn(
              'h-1.5 flex-1 rounded-full transition-colors',
              password && index < Math.max(1, strength.score) ? barColors[strength.score] : 'bg-slate-200 dark:bg-slate-700',
            )}
          />
        ))}
      </div>
      {password && (
        <p className="text-xs text-slate-600 dark:text-slate-300">
          Fortaleza: <strong data-testid="strength-label">{strength.label}</strong>
        </p>
      )}
      <ul className="grid grid-cols-1 gap-x-4 gap-y-0.5 text-xs sm:grid-cols-2">
        {strength.rules.map((rule) => (
          <li key={rule.id} className={cn('flex items-center gap-1', rule.met ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-500 dark:text-slate-400')}>
            {rule.met ? <Check className="h-3 w-3" aria-hidden /> : <X className="h-3 w-3" aria-hidden />}
            <span>
              {rule.label}
              <span className="sr-only">{rule.met ? ' (cumplido)' : ' (pendiente)'}</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
