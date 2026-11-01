import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { CheckCircle2, XCircle } from 'lucide-react';
import { authApi } from '../../api/services';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { Spinner } from '../../components/ui/Spinner';
import { errorMessage } from '../../hooks/usePagedQuery';

type State = { status: 'loading' } | { status: 'success'; message: string } | { status: 'error'; message: string };

export function VerifyEmailPage() {
  const [params] = useSearchParams();
  const token = params.get('token');
  const [state, setState] = useState<State>({ status: 'loading' });
  // El token es de un solo uso: en StrictMode los efectos se ejecutan dos veces en desarrollo,
  // así que se protege para no "gastarlo" dos veces.
  const sent = useRef(false);

  useEffect(() => {
    if (!token) {
      setState({ status: 'error', message: 'El enlace no contiene un token.' });
      return;
    }

    if (sent.current) return;
    sent.current = true;
    authApi
      .verifyEmail(token)
      .then((r) => setState({ status: 'success', message: r.message }))
      .catch((e) => setState({ status: 'error', message: errorMessage(e) }));
  }, [token]);

  return (
    <AuthLayout title="Verificación de email">
      <div className="flex flex-col items-center gap-4 py-4 text-center">
        {state.status === 'loading' && <Spinner className="h-8 w-8" label="Verificando…" />}
        {state.status === 'success' && (
          <>
            <CheckCircle2 className="h-12 w-12 text-emerald-500" aria-hidden />
            <p className="text-sm">{state.message}</p>
          </>
        )}
        {state.status === 'error' && (
          <>
            <XCircle className="h-12 w-12 text-red-500" aria-hidden />
            <p className="text-sm">{state.message}</p>
            <p className="text-xs text-slate-500">Puedes solicitar un nuevo enlace desde la pantalla de inicio de sesión.</p>
          </>
        )}
        {state.status !== 'loading' && (
          <Link to="/login" className="text-sm font-medium text-brand-600 hover:underline dark:text-brand-400">
            Ir a iniciar sesión
          </Link>
        )}
      </div>
    </AuthLayout>
  );
}
