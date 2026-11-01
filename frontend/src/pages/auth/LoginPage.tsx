import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/services';
import { useAuth } from '../../auth/AuthContext';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { Button } from '../../components/ui/Button';
import { Input, PasswordInput } from '../../components/ui/Field';
import { Alert } from '../../components/ui/Feedback';
import { compactErrors, hasErrors, validateEmail, type FieldErrors } from '../../lib/validation';

type Field = 'email' | 'password';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FieldErrors<Field>>({});
  const [serverError, setServerError] = useState<ApiError | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [resent, setResent] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = compactErrors<Field>({
      email: validateEmail(email),
      password: password ? undefined : 'La contraseña es obligatoria.',
    });
    setErrors(validation);
    if (hasErrors(validation)) return;

    setSubmitting(true);
    setServerError(null);
    try {
      await login(email, password);
      navigate(from, { replace: true });
    } catch (error) {
      setServerError(error instanceof ApiError ? error : new ApiError(0, 'Error inesperado'));
    } finally {
      setSubmitting(false);
    }
  };

  const resendVerification = async () => {
    await authApi.resendVerification(email);
    setResent(true);
  };

  return (
    <AuthLayout
      title="Iniciar sesión"
      subtitle="Accede al panel con tu cuenta."
      footer={
        <>
          ¿No tienes cuenta?{' '}
          <Link to="/registro" className="font-medium text-brand-600 hover:underline dark:text-brand-400">
            Regístrate
          </Link>
        </>
      }
    >
      <form className="space-y-4" onSubmit={handleSubmit} noValidate>
        {serverError && <LoginError error={serverError} onResend={resendVerification} resent={resent} />}
        <Input label="Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
        <PasswordInput
          label="Contraseña"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={errors.password}
        />
        <div className="flex justify-end">
          <Link to="/olvide-password" className="text-sm text-brand-600 hover:underline dark:text-brand-400">
            ¿Olvidaste tu contraseña?
          </Link>
        </div>
        <Button type="submit" className="w-full" loading={submitting}>
          Ingresar
        </Button>
      </form>
      <div className="mt-6 rounded-lg border border-dashed border-slate-300 p-3 text-xs text-slate-500 dark:border-slate-700 dark:text-slate-400">
        <p className="font-medium text-slate-600 dark:text-slate-300">Cuentas demo (datos ficticios)</p>
        <p>admin@demo.pe / Demo123! · usuario@demo.pe / Demo123!</p>
      </div>
    </AuthLayout>
  );
}

function LoginError({ error, onResend, resent }: { error: ApiError; onResend: () => void; resent: boolean }) {
  if (error.code === 'email_not_verified') {
    return (
      <Alert tone="warning" title="Email sin verificar">
        {error.message}{' '}
        {resent ? (
          <span>Te enviamos un nuevo enlace.</span>
        ) : (
          <button type="button" onClick={onResend} className="font-medium underline">
            Reenviar correo
          </button>
        )}
      </Alert>
    );
  }

  if (error.code === 'account_locked') {
    return (
      <Alert tone="error" title="Cuenta bloqueada temporalmente">
        {error.message}
      </Alert>
    );
  }

  if (error.status === 429) {
    return (
      <Alert tone="warning" title="Demasiados intentos">
        {error.message}
      </Alert>
    );
  }

  return <Alert tone="error">{error.message}</Alert>;
}
