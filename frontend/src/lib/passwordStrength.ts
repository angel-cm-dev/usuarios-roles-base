/**
 * Reglas de contraseña del lado del cliente. Replican la política del backend (Security:Password)
 * para dar feedback inmediato; el backend sigue siendo la fuente de verdad y vuelve a validar.
 */

export interface PasswordRule {
  id: 'length' | 'upper' | 'lower' | 'digit' | 'symbol';
  label: string;
  met: boolean;
}

export interface PasswordStrength {
  /** 0 (muy débil) a 4 (muy fuerte). */
  score: 0 | 1 | 2 | 3 | 4;
  label: string;
  rules: PasswordRule[];
  /** true si cumple TODAS las reglas obligatorias (lo mismo que exige la API). */
  meetsPolicy: boolean;
}

export const MIN_PASSWORD_LENGTH = 8;

const COMMON_PATTERNS = ['password', 'contraseña', '123456', 'qwerty', 'admin', 'letmein', 'welcome', 'abc123'];

const LABELS = ['Muy débil', 'Débil', 'Aceptable', 'Fuerte', 'Muy fuerte'] as const;

export function getPasswordRules(password: string): PasswordRule[] {
  return [
    { id: 'length', label: `Al menos ${MIN_PASSWORD_LENGTH} caracteres`, met: password.length >= MIN_PASSWORD_LENGTH },
    { id: 'upper', label: 'Una letra mayúscula', met: /\p{Lu}/u.test(password) },
    { id: 'lower', label: 'Una letra minúscula', met: /\p{Ll}/u.test(password) },
    { id: 'digit', label: 'Un número', met: /\d/.test(password) },
    { id: 'symbol', label: 'Un símbolo (! @ # $ …)', met: /[^\p{L}\p{N}]/u.test(password) },
  ];
}

export function evaluatePassword(password: string): PasswordStrength {
  const rules = getPasswordRules(password);
  const meetsPolicy = rules.every((r) => r.met);

  if (!password) {
    return { score: 0, label: LABELS[0], rules, meetsPolicy };
  }

  // Puntaje base: reglas cumplidas (0..5) + bonus por longitud.
  let points = rules.filter((r) => r.met).length;
  if (password.length >= 12) points += 1;
  if (password.length >= 16) points += 1;

  // Penalizaciones: patrones comunes y repeticiones ("aaaa", "1111").
  const lower = password.toLowerCase();
  if (COMMON_PATTERNS.some((p) => lower.includes(p))) points -= 2;
  if (/(.)\1{2,}/.test(password)) points -= 1;

  let score: PasswordStrength['score'];
  if (points <= 2) score = 0;
  else if (points === 3) score = 1;
  else if (points === 4) score = 2;
  else if (points <= 6) score = 3;
  else score = 4;

  // Sin cumplir la política nunca puede considerarse "fuerte".
  if (!meetsPolicy && score > 1) score = 1;

  return { score, label: LABELS[score], rules, meetsPolicy };
}
