import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { MailCheck } from 'lucide-react';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/services';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { Button } from '../../components/ui/Button';
import { Input, PasswordInput } from '../../components/ui/Field';
import { Alert } from '../../components/ui/Feedback';
import { PasswordStrengthMeter } from '../../components/ui/PasswordStrengthMeter';
import {
  compactErrors,
  hasErrors,
  validateConfirmation,
  validateEmail,
  validateFullName,
  validateNewPassword,
  type FieldErrors,
} from '../../lib/validation';

type Field = 'fullName' | 'email' | 'password' | 'confirm';

export function RegisterPage() {
  const [form, setForm] = useState({ fullName: '', email: '', password: '', confirm: '' });
  const [errors, setErrors] = useState<FieldErrors<Field>>({});
  const [serverError, setServerError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const update = (field: Field) => (event: ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current, [field]: event.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const validation = compactErrors<Field>({
      fullName: validateFullName(form.fullName),
      email: validateEmail(form.email),
      password: validateNewPassword(form.password, form.email),
      confirm: validateConfirmation(form.password, form.confirm),
    });
    setErrors(validation);
    if (hasErrors(validation)) return;

    setSubmitting(true);
    setServerError(null);
    try {
      await authApi.register({ fullName: form.fullName.trim(), email: form.email.trim(), password: form.password });
      setDone(true);
    } catch (error) {
      if (error instanceof ApiError) {
        // Errores por campo devueltos por la API (ValidationProblemDetails).
        setErrors({ fullName: error.fieldError('fullName'), email: error.fieldError('email'), password: error.fieldError('password') });
        setServerError(error.message);
      } else {
        setServerError('No se pudo completar el registro.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (done) {
    return (
      <AuthLayout title="Revisa tu correo">
        <div className="space-y-4 text-center">
          <MailCheck className="mx-auto h-12 w-12 text-brand-500" aria-hidden />
          <p className="text-sm text-slate-600 dark:text-slate-300">
            Si el email <strong>{form.email}</strong> es válido, recibirás un enlace para verificar tu cuenta.
          </p>
          <p className="text-xs text-slate-500">
            En desarrollo los correos se simulan: ábrelos en el{' '}
            <Link to="/dev/buzon" className="text-brand-600 underline dark:text-brand-400">
              buzón de desarrollo
            </Link>
            .
          </p>
          <Link to="/login" className="inline-block text-sm font-medium text-brand-600 hover:underline dark:text-brand-400">
            Ir a iniciar sesión
          </Link>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title="Crear cuenta"
      subtitle="Te enviaremos un correo para verificar tu email."
      footer={
        <>
          ¿Ya tienes cuenta?{' '}
          <Link to="/login" className="font-medium text-brand-600 hover:underline dark:text-brand-400">
            Inicia sesión
          </Link>
        </>
      }
    >
      <form className="space-y-4" onSubmit={handleSubmit} noValidate>
        {serverError && <Alert tone="error">{serverError}</Alert>}
        <Input label="Nombre completo" autoComplete="name" value={form.fullName} onChange={update('fullName')} error={errors.fullName} />
        <Input label="Email" type="email" autoComplete="email" value={form.email} onChange={update('email')} error={errors.email} />
        <div>
          <PasswordInput label="Contraseña" autoComplete="new-password" value={form.password} onChange={update('password')} error={errors.password} />
          <PasswordStrengthMeter password={form.password} />
        </div>
        <PasswordInput label="Confirmar contraseña" autoComplete="new-password" value={form.confirm} onChange={update('confirm')} error={errors.confirm} />
        <Button type="submit" className="w-full" loading={submitting}>
          Crear cuenta
        </Button>
      </form>
    </AuthLayout>
  );
}
