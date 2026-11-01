import { describe, expect, it } from 'vitest';
import { evaluatePassword, getPasswordRules } from './passwordStrength';

describe('evaluatePassword', () => {
  it('una contraseña vacía es muy débil y no cumple la política', () => {
    const result = evaluatePassword('');
    expect(result.score).toBe(0);
    expect(result.meetsPolicy).toBe(false);
  });

  it('cumple la política cuando tiene longitud, mayúscula, minúscula, número y símbolo', () => {
    expect(evaluatePassword('Segura#2026').meetsPolicy).toBe(true);
  });

  it.each([
    ['segura#2026', 'upper'],
    ['SEGURA#2026', 'lower'],
    ['Segura#Lima', 'digit'],
    ['Segura2026x', 'symbol'],
    ['Ab1!', 'length'],
  ])('detecta la regla incumplida en "%s" (%s)', (password, ruleId) => {
    const rule = getPasswordRules(password).find((r) => r.id === ruleId);
    expect(rule?.met).toBe(false);
  });

  it('nunca califica como fuerte una contraseña que no cumple la política', () => {
    expect(evaluatePassword('abcdefghijklmnopqrstuvwxyz').score).toBeLessThanOrEqual(1);
  });

  it('las contraseñas largas y variadas son muy fuertes', () => {
    const result = evaluatePassword('Lima-Peru-Montañas-2026!');
    expect(result.score).toBe(4);
    expect(result.label).toBe('Muy fuerte');
  });

  it('penaliza patrones comunes', () => {
    expect(evaluatePassword('Password123!').score).toBeLessThan(evaluatePassword('Tucan#Verde42').score);
  });

  it('reconoce mayúsculas acentuadas (Unicode)', () => {
    expect(getPasswordRules('Ñandú').find((r) => r.id === 'upper')?.met).toBe(true);
  });
});
