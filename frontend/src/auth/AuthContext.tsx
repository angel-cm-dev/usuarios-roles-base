import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { onSessionChange, refreshSession, tokenStore } from '../api/client';
import { authApi } from '../api/services';
import type { AuthResponse, UserProfile } from '../api/types';
import { hasPermission } from './permissions';

type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  user: UserProfile | null;
  login: (email: string, password: string) => Promise<UserProfile>;
  logout: () => Promise<void>;
  /** Aplica una sesión nueva (p. ej. tras cambiar la contraseña). */
  applySession: (session: AuthResponse) => void;
  setUser: (user: UserProfile) => void;
  can: (permission: string | string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUserState] = useState<UserProfile | null>(null);

  const applySession = useCallback((session: AuthResponse) => {
    tokenStore.set(session.accessToken);
    setUserState(session.user);
    setStatus('authenticated');
  }, []);

  useEffect(() => {
    // Cualquier refresh (automático o manual) actualiza el usuario; un refresh fallido cierra la sesión.
    const unsubscribe = onSessionChange((session) => {
      if (session) {
        setUserState(session.user);
        setStatus('authenticated');
      } else {
        setUserState(null);
        setStatus('anonymous');
      }
    });

    // Al cargar la app, el access token no existe (vive en memoria): se intenta recuperar la sesión con la cookie.
    refreshSession().then((session) => {
      if (!session) setStatus('anonymous');
    });

    return unsubscribe;
  }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      const session = await authApi.login(email, password);
      applySession(session);
      return session.user;
    },
    [applySession],
  );

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      tokenStore.clear();
      setUserState(null);
      setStatus('anonymous');
    }
  }, []);

  const can = useCallback((permission: string | string[]) => hasPermission(user?.permissions, permission), [user]);

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, logout, applySession, setUser: setUserState, can }),
    [status, user, login, logout, applySession, can],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  return context;
}

export { AuthContext };
