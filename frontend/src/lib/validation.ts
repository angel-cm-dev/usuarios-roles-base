import { evaluatePassword } from './passwordStrength';

export type FieldErrors<T extends string = string> = Partial<Record<T, string>>;

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;

export function isEmail(value: string): boolean {
  return EMAIL_REGEX.test(value.trim());
}

export function validateEmail(value: string): string | undefined {
  if (!value.trim()) return 'El email es obligatorio.';
  if (!isEmail(value)) return 'El email no tiene un formato válido.';
  return undefined;
}

export function validateFullName(value: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) return 'El nombre es obligatorio.';
  if (trimmed.length < 2) return 'El nombre debe tener al menos 2 caracteres.';
  if (trimmed.length > 100) return 'El nombre no puede superar 100 caracteres.';
  return undefined;
}

export function validateNewPassword(value: string, email?: string): string | undefined {
  if (!value) return 'La contraseña es obligatoria.';
  const strength = evaluatePassword(value);
  if (!strength.meetsPolicy) return 'La contraseña no cumple los requisitos.';
  const local = email?.split('@')[0];
  if (local && local.length >= 3 && value.toLowerCase().includes(local.toLowerCase())) {
    return 'La contraseña no debe contener tu email.';
  }
  return undefined;
}

export function validateConfirmation(password: string, confirmation: string): string | undefined {
  if (!confirmation) return 'Confirma la contraseña.';
  return password === confirmation ? undefined : 'Las contraseñas no coinciden.';
}

export function validateRoleName(value: string): string | undefined {
  if (!value.trim()) return 'El nombre es obligatorio.';
  if (!/^[a-zA-Z0-9][a-zA-Z0-9_.-]{1,49}$/.test(value.trim())) {
    return 'De 2 a 50 caracteres: letras, números, guion, punto o guion bajo (sin espacios).';
  }
  return undefined;
}

/** Devuelve solo los campos con error (útil para saber si el formulario es válido). */
export function compactErrors<T extends string>(errors: Record<T, string | undefined>): FieldErrors<T> {
  const result: FieldErrors<T> = {};
  for (const key of Object.keys(errors) as T[]) {
    if (errors[key]) result[key] = errors[key];
  }
  return result;
}

export function hasErrors(errors: FieldErrors): boolean {
  return Object.values(errors).some(Boolean);
}
