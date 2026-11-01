import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Mail, RefreshCw, ShieldOff, Trash2 } from 'lucide-react';
import { devApi } from '../api/services';
import type { DevEmail } from '../api/types';
import { Button } from '../components/ui/Button';
import { Alert, EmptyState, ErrorState, PageHeader } from '../components/ui/Feedback';
import { Spinner } from '../components/ui/Spinner';
import { errorMessage } from '../hooks/usePagedQuery';
import { formatDateTime } from '../lib/format';

export function ForbiddenPage() {
  return (
    <div className="flex flex-col items-center gap-3 py-20 text-center">
      <ShieldOff className="h-12 w-12 text-amber-500" aria-hidden />
      <h1 className="text-xl font-semibold">No tienes permiso para ver esta sección</h1>
      <p className="max-w-md text-sm text-slate-500">Pide a un administrador que asigne el permiso necesario a alguno de tus roles. Los cambios se aplican al renovar tu sesión.</p>
      <Link to="/" className="text-sm font-medium text-brand-600 hover:underline dark:text-brand-400">Volver al inicio</Link>
    </div>
  );
}

export function NotFoundPage() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-3 text-center">
      <p className="text-5xl font-bold text-brand-600">404</p>
      <h1 className="text-xl font-semibold">Página no encontrada</h1>
      <Link to="/" className="text-sm font-medium text-brand-600 hover:underline dark:text-brand-400">Ir al inicio</Link>
    </div>
  );
}

/**
 * Buzón de desarrollo: muestra los correos simulados por el backend (IEmailSender de desarrollo).
 * Solo funciona si la API tiene Features:DevMailbox = true. Es público a propósito para poder
 * verificar el email antes de tener sesión.
 */
export function DevMailboxPage() {
  const [emails, setEmails] = useState<DevEmail[]>();
  const [selected, setSelected] = useState<DevEmail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  const load = useCallback(() => {
    setError(null);
    devApi
      .mailbox()
      .then((list) => {
        setEmails(list);
        setSelected((current) => list.find((m) => m.id === current?.id) ?? list[0] ?? null);
      })
      .catch((e) => setError(errorMessage(e)));
  }, []);

  useEffect(load, [load]);

  const visible = emails?.filter((m) => !filter || m.to.toLowerCase().includes(filter.toLowerCase())) ?? [];

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-6">
      <PageHeader
        title="Buzón de desarrollo"
        description="Correos simulados (verificación, recuperación). Nada se envía realmente."
        actions={
          <>
            <Link to="/login" className="self-center text-sm text-brand-600 hover:underline dark:text-brand-400">Ir al login</Link>
            <Button variant="secondary" onClick={load}><RefreshCw className="h-4 w-4" /> Actualizar</Button>
            <Button variant="secondary" onClick={() => devApi.clear().then(load)}><Trash2 className="h-4 w-4" /> Vaciar</Button>
          </>
        }
      />
      <Alert tone="warning" className="mb-4">
        Solo para desarrollo/demo. En producción desactiva <code>Features:DevMailbox</code> y registra un IEmailSender real (SMTP).
      </Alert>
      {error && <ErrorState message={`${error} ¿Está activo Features:DevMailbox en la API?`} onRetry={load} />}
      {!emails && !error && <div className="flex justify-center py-12"><Spinner className="h-8 w-8" /></div>}
      {emails && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[22rem_1fr]">
          <div className="card overflow-hidden">
            <div className="border-b border-slate-200 p-3 dark:border-slate-800">
              <input className="input" placeholder="Filtrar por destinatario…" aria-label="Filtrar por destinatario" value={filter} onChange={(e) => setFilter(e.target.value)} />
            </div>
            {visible.length === 0 ? (
              <EmptyState title="Buzón vacío" description="Regístrate o solicita recuperar tu contraseña para ver correos aquí." />
            ) : (
              <ul className="max-h-[60vh] divide-y divide-slate-100 overflow-y-auto dark:divide-slate-800">
                {visible.map((mail) => (
                  <li key={mail.id}>
                    <button
                      className={`w-full px-4 py-3 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-800/50 ${selected?.id === mail.id ? 'bg-brand-50 dark:bg-brand-900/30' : ''}`}
                      onClick={() => setSelected(mail)}
                    >
                      <p className="flex items-center gap-2 font-medium"><Mail className="h-3.5 w-3.5 shrink-0" aria-hidden />{mail.subject}</p>
                      <p className="truncate text-xs text-slate-500">{mail.to}</p>
                      <p className="text-[11px] text-slate-400">{formatDateTime(mail.sentAt)}</p>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div className="card min-h-[20rem] p-5">
            {selected ? (
              <article>
                <h2 className="text-lg font-semibold">{selected.subject}</h2>
                <p className="mb-4 text-xs text-slate-500">Para: {selected.to} · {formatDateTime(selected.sentAt)}</p>
                {/* Se muestra la versión de texto (sin HTML) para no inyectar marcado; los enlaces se hacen clicables. */}
                <MailText text={selected.textBody} />
              </article>
            ) : (
              <EmptyState title="Selecciona un correo" />
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function MailText({ text }: { text: string }) {
  const parts = text.split(/(https?:\/\/\S+)/g);
  return (
    <p className="whitespace-pre-wrap break-words text-sm leading-relaxed">
      {parts.map((part, index) => {
        if (!/^https?:\/\//.test(part)) return <span key={index}>{part}</span>;
        // Enlace al frontend: se navega dentro de la SPA conservando la ruta y el token.
        const url = new URL(part);
        return (
          <a key={index} href={`${url.pathname}${url.search}`} className="font-medium text-brand-600 underline dark:text-brand-400">
            {part}
          </a>
        );
      })}
    </p>
  );
}
