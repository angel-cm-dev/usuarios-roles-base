import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { FullPageSpinner } from '../components/ui/Spinner';
import { useAuth } from './AuthContext';

/** Rutas privadas: espera a recuperar la sesión y redirige a /login si no hay usuario. */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'loading') return <FullPageSpinner />;
  if (status === 'anonymous') return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}

/** Rutas públicas de autenticación: si ya hay sesión, se va al panel. */
export function GuestOnly() {
  const { status } = useAuth();
  if (status === 'loading') return <FullPageSpinner />;
  if (status === 'authenticated') return <Navigate to="/" replace />;
  return <Outlet />;
}

/** Protege una página por permiso (la API también lo valida: esto es solo UX). */
export function RequirePermission({ permission, children }: { permission: string | string[]; children: ReactNode }) {
  const { can } = useAuth();
  if (!can(permission)) return <Navigate to="/sin-permiso" replace />;
  return <>{children}</>;
}

/** Renderiza su contenido solo si el usuario tiene el permiso. Útil para ocultar botones. */
export function Can({ permission, children, fallback = null }: { permission: string | string[]; children: ReactNode; fallback?: ReactNode }) {
  const { can } = useAuth();
  return <>{can(permission) ? children : fallback}</>;
}
