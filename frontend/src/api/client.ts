import type { AuthResponse } from './types';

/**
 * Cliente HTTP de la aplicación.
 *
 * - El access token vive SOLO en memoria (no en localStorage): un XSS no puede robarlo de un almacenamiento persistente.
 * - El refresh token está en una cookie HttpOnly que el navegador envía solo a /api/auth.
 * - Ante un 401 se intenta UN refresh y se reintenta la petición original.
 * - El refresh es "single-flight": varias peticiones que fallen a la vez comparten la misma renovación.
 *   Esto es clave porque el backend detecta el reuso de refresh tokens y revocaría la sesión
 *   si enviáramos el mismo token dos veces en paralelo.
 */

export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '/api';

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly errors: Record<string, string[]>;
  readonly title?: string;

  constructor(status: number, message: string, code = 'error', errors: Record<string, string[]> = {}, title?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.errors = errors;
    this.title = title;
  }

  /** Primer mensaje de error de un campo (para mostrar bajo el input). */
  fieldError(field: string): string | undefined {
    return this.errors[field]?.[0];
  }
}

let accessToken: string | null = null;
let refreshInFlight: Promise<AuthResponse | null> | null = null;
const sessionListeners = new Set<(session: AuthResponse | null) => void>();

export const tokenStore = {
  get: () => accessToken,
  set: (token: string | null) => {
    accessToken = token;
  },
  clear: () => {
    accessToken = null;
  },
};

/** Suscribe a cambios de sesión (refresh exitoso o sesión expirada). Devuelve la función para desuscribirse. */
export function onSessionChange(listener: (session: AuthResponse | null) => void): () => void {
  sessionListeners.add(listener);
  return () => sessionListeners.delete(listener);
}

function notify(session: AuthResponse | null) {
  sessionListeners.forEach((listener) => listener(session));
}

async function doRefresh(): Promise<AuthResponse | null> {
  try {
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, { method: 'POST', credentials: 'include' });
    if (!response.ok) {
      tokenStore.clear();
      notify(null);
      return null;
    }

    const session = (await response.json()) as AuthResponse;
    tokenStore.set(session.accessToken);
    notify(session);
    return session;
  } catch {
    return null;
  }
}

/** Renueva la sesión usando la cookie de refresh. Comparte la promesa si ya hay una renovación en curso. */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!refreshInFlight) {
    // Web Locks serializa la renovación también entre pestañas del mismo navegador.
    const locks = typeof navigator !== 'undefined' ? navigator.locks : undefined;
    const run = locks ? locks.request('urb-refresh', () => doRefresh()) : doRefresh();
    refreshInFlight = run.finally(() => {
      refreshInFlight = null;
    });
  }

  return refreshInFlight;
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  /** false para endpoints públicos (login, registro...): no adjunta token ni intenta refresh. */
  auth?: boolean;
  signal?: AbortSignal;
}

export function buildQuery(query?: RequestOptions['query']): string {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.append(key, String(value));
    }
  }

  const text = params.toString();
  return text ? `?${text}` : '';
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: { title?: string; detail?: string; code?: string; errors?: Record<string, string[]> } = {};
  try {
    problem = await response.json();
  } catch {
    // Respuesta sin cuerpo JSON (p. ej. error del proxy).
  }

  const message =
    problem.detail ??
    problem.title ??
    (response.status === 0 ? 'No se pudo conectar con el servidor.' : `Error ${response.status}`);
  return new ApiError(response.status, message, problem.code ?? 'error', problem.errors ?? {}, problem.title);
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}, isRetry = false): Promise<T> {
  const { method = 'GET', body, query, auth = true, signal } = options;
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const token = tokenStore.get();
  if (auth && token) headers.Authorization = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}${buildQuery(query)}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      credentials: 'include',
      signal,
    });
  } catch (error) {
    if ((error as Error).name === 'AbortError') throw error;
    throw new ApiError(0, 'No se pudo conectar con el servidor. Revisa tu conexión.', 'network_error');
  }

  if (response.status === 401 && auth && !isRetry) {
    const session = await refreshSession();
    if (session) {
      return apiRequest<T>(path, options, true);
    }
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(path: string, query?: RequestOptions['query'], signal?: AbortSignal) => apiRequest<T>(path, { query, signal }),
  post: <T>(path: string, body?: unknown, auth = true) => apiRequest<T>(path, { method: 'POST', body, auth }),
  put: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'PUT', body }),
  delete: <T>(path: string) => apiRequest<T>(path, { method: 'DELETE' }),
};
