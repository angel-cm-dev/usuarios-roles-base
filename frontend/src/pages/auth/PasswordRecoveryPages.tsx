import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/services';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { Button } from '../../components/ui/Button';
import { Input, PasswordInput } from '../../components/ui/Field';
import { Alert } from '../../components/ui/Feedback';
import { PasswordStrengthMeter } from '../../components/ui/PasswordStrengthMeter';
import { errorMessage } from '../../hooks/usePagedQuery';
import { compactErrors, hasErrors, validateConfirmation, validateEmail, validateNewPassword, type FieldErrors } from '../../lib/validation';

const backToLogin = (
  <Link to="/login" className="font-medium text-brand-600 hover:underline dark:text-brand-400">
    Volver a iniciar sesión
  </Link>
);

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string>();
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = validateEmail(email);
    setError(validation);
    if (validation) return;

    setSubmitting(true);
    try {
      const response = await authApi.forgotPassword(email.trim());
      setMessage(response.message);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthLayout title="Recuperar contraseña" subtitle="Te enviaremos un enlace de un solo uso válido por 30 minutos." footer={backToLogin}>
      {message ? (
        <Alert tone="success" title="Solicitud recibida">
          {message}
        </Alert>
      ) : (
        <form className="space-y-4" onSubmit={handleSubmit} noValidate>
          <Input label="Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} error={error} />
          <Button type="submit" className="w-full" loading={submitting}>
            Enviar enlace
          </Button>
        </form>
      )}
    </AuthLayout>
  );
}

type ResetField = 'password' | 'confirm';

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState<FieldErrors<ResetField>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = compactErrors<ResetField>({
      password: validateNewPassword(password),
      confirm: validateConfirmation(password, confirm),
    });
    setErrors(validation);
    if (hasErrors(validation)) return;

    setSubmitting(true);
    setServerError(null);
    try {
      const response = await authApi.resetPassword(token, password);
      setDone(response.message);
    } catch (e) {
      if (e instanceof ApiError && e.fieldError('newPassword')) {
        setErrors({ password: e.fieldError('newPassword') });
      }
      setServerError(errorMessage(e));
    } finally {
      setSubmitting(false);
    }
  };

  if (!token) {
    return (
      <AuthLayout title="Enlace inválido" footer={backToLogin}>
        <Alert tone="error">El enlace no contiene un token. Solicita uno nuevo.</Alert>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title="Nueva contraseña" subtitle="Al cambiarla se cerrarán todas tus sesiones abiertas." footer={backToLogin}>
      {done ? (
        <Alert tone="success" title="¡Listo!">
          {done}
        </Alert>
      ) : (
        <form className="space-y-4" onSubmit={handleSubmit} noValidate>
          {serverError && <Alert tone="error">{serverError}</Alert>}
          <div>
            <PasswordInput label="Nueva contraseña" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} error={errors.password} />
            <PasswordStrengthMeter password={password} />
          </div>
          <PasswordInput label="Confirmar contraseña" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} error={errors.confirm} />
          <Button type="submit" className="w-full" loading={submitting}>
            Guardar contraseña
          </Button>
        </form>
      )}
    </AuthLayout>
  );
}
