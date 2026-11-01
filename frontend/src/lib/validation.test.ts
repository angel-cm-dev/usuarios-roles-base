import { describe, expect, it } from 'vitest';
import { compactErrors, hasErrors, isEmail, validateConfirmation, validateNewPassword, validateRoleName } from './validation';
import { hasPermission } from '../auth/permissions';
import { parseUtc } from './format';

describe('validación de formularios', () => {
  it.each(['ana@demo.pe', 'a.b+c@sub.dominio.com'])('acepta el email %s', (email) => expect(isEmail(email)).toBe(true));
  it.each(['ana', 'ana@', '@demo.pe', 'ana@demo'])('rechaza el email %s', (email) => expect(isEmail(email)).toBe(false));

  it('la nueva contraseña no puede contener el email', () => {
    expect(validateNewPassword('Mariela#2026', 'mariela@demo.pe')).toMatch(/email/);
    expect(validateNewPassword('Segura#2026', 'mariela@demo.pe')).toBeUndefined();
  });

  it('valida que la confirmación coincida', () => {
    expect(validateConfirmation('abc', 'abd')).toBe('Las contraseñas no coinciden.');
    expect(validateConfirmation('abc', 'abc')).toBeUndefined();
  });

  it('valida nombres de rol sin espacios', () => {
    expect(validateRoleName('soporte-nivel-1')).toBeUndefined();
    expect(validateRoleName('rol con espacios')).toBeDefined();
  });

  it('compactErrors elimina los campos sin error', () => {
    const errors = compactErrors({ email: undefined, password: 'Obligatoria' });
    expect(errors).toEqual({ password: 'Obligatoria' });
    expect(hasErrors(errors)).toBe(true);
    expect(hasErrors(compactErrors({ email: undefined }))).toBe(false);
  });
});

describe('hasPermission', () => {
  it('concede si tiene alguno de los permisos pedidos', () => {
    expect(hasPermission(['usuarios.leer'], ['roles.leer', 'usuarios.leer'])).toBe(true);
  });

  it('deniega si no hay usuario o no tiene el permiso', () => {
    expect(hasPermission(undefined, 'usuarios.leer')).toBe(false);
    expect(hasPermission(['usuarios.leer'], 'usuarios.crear')).toBe(false);
  });
});

describe('parseUtc', () => {
  it('interpreta como UTC las fechas sin zona horaria', () => {
    expect(parseUtc('2026-01-15T10:00:00').toISOString()).toBe('2026-01-15T10:00:00.000Z');
    expect(parseUtc('2026-01-15T10:00:00Z').toISOString()).toBe('2026-01-15T10:00:00.000Z');
  });
});
