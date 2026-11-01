import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, apiRequest, buildQuery, refreshSession, tokenStore } from './client';

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const session = (token: string) => ({
  accessToken: token,
  tokenType: 'Bearer',
  expiresIn: 900,
  expiresAt: '2026-01-01T00:15:00Z',
  user: { id: '1', email: 'a@demo.pe', fullName: 'A', emailConfirmed: true, roles: [], permissions: [], createdAt: '', lastLoginAt: null },
});

describe('apiRequest', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    tokenStore.clear();
  });

  afterEach(() => {
    fetchMock.mockReset();
    vi.unstubAllGlobals();
  });

  it('adjunta el access token en memoria como Bearer', async () => {
    tokenStore.set('token-1');
    fetchMock.mockResolvedValueOnce(json(200, { ok: true }));

    await apiRequest('/users');

    const [, init] = fetchMock.mock.calls[0];
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer token-1');
    expect(init?.credentials).toBe('include');
  });

  it('ante un 401 renueva la sesión y reintenta la petición original', async () => {
    tokenStore.set('expirado');
    fetchMock
      .mockResolvedValueOnce(json(401, { code: 'unauthorized' }))
      .mockResolvedValueOnce(json(200, session('nuevo')))
      .mockResolvedValueOnce(json(200, { items: [] }));

    const result = await apiRequest<{ items: unknown[] }>('/users');

    expect(result).toEqual({ items: [] });
    expect(fetchMock.mock.calls[1][0]).toBe('/api/auth/refresh');
    const retryHeaders = fetchMock.mock.calls[2][1]?.headers as Record<string, string>;
    expect(retryHeaders.Authorization).toBe('Bearer nuevo');
    expect(tokenStore.get()).toBe('nuevo');
  });

  it('si el refresh falla, propaga el 401 y limpia el token', async () => {
    tokenStore.set('expirado');
    fetchMock
      .mockResolvedValueOnce(json(401, { code: 'unauthorized', detail: 'No autenticado' }))
      .mockResolvedValueOnce(json(401, { code: 'invalid_refresh_token' }));

    await expect(apiRequest('/users')).rejects.toMatchObject({ status: 401 });
    expect(tokenStore.get()).toBeNull();
  });

  it('varias renovaciones simultáneas comparten una sola llamada (evita falsos reusos)', async () => {
    fetchMock.mockImplementation(async () => json(200, session('compartido')));

    const [a, b, c] = await Promise.all([refreshSession(), refreshSession(), refreshSession()]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(a?.accessToken).toBe('compartido');
    expect(b).toBe(a);
    expect(c).toBe(a);
  });

  it('convierte ProblemDetails en ApiError con errores por campo', async () => {
    fetchMock.mockResolvedValueOnce(
      json(400, { title: 'Datos inválidos', detail: 'Uno o más campos no son válidos.', code: 'validation_error', errors: { password: ['Muy corta'] } }),
    );

    const error = (await apiRequest('/auth/register', { method: 'POST', body: {}, auth: false }).catch((e: unknown) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('validation_error');
    expect(error.fieldError('password')).toBe('Muy corta');
  });

  it('los endpoints públicos no intentan refresh ante 401', async () => {
    fetchMock.mockResolvedValueOnce(json(401, { code: 'invalid_credentials', detail: 'Email o contraseña incorrectos.' }));

    await expect(apiRequest('/auth/login', { method: 'POST', body: {}, auth: false })).rejects.toMatchObject({ code: 'invalid_credentials' });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('un fallo de red se informa como ApiError legible', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    await expect(apiRequest('/users')).rejects.toMatchObject({ code: 'network_error', status: 0 });
  });
});

describe('buildQuery', () => {
  it('omite valores vacíos y codifica el resto', () => {
    expect(buildQuery({ page: 1, search: 'ana maría', role: undefined, status: '' })).toBe('?page=1&search=ana+mar%C3%ADa');
    expect(buildQuery({})).toBe('');
  });
});
