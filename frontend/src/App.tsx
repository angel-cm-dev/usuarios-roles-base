import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import { GuestOnly, RequireAuth, RequirePermission } from './auth/guards';
import { PERMISSIONS } from './auth/permissions';
import { AppLayout } from './components/layout/AppLayout';
import { FullPageSpinner } from './components/ui/Spinner';
import { LoginPage } from './pages/auth/LoginPage';
import { ForgotPasswordPage, ResetPasswordPage } from './pages/auth/PasswordRecoveryPages';
import { RegisterPage } from './pages/auth/RegisterPage';
import { VerifyEmailPage } from './pages/auth/VerifyEmailPage';
import { DashboardPage } from './pages/DashboardPage';
import { DevMailboxPage, ForbiddenPage, NotFoundPage } from './pages/MiscPages';
import { ProfilePage } from './pages/ProfilePage';

// Las páginas de administración se cargan bajo demanda (code splitting): un usuario sin permisos no las descarga.
const UsersPage = lazy(() => import('./pages/UsersPage').then((m) => ({ default: m.UsersPage })));
const RolesPage = lazy(() => import('./pages/RolesPage').then((m) => ({ default: m.RolesPage })));
const PermissionsPage = lazy(() => import('./pages/PermissionsPage').then((m) => ({ default: m.PermissionsPage })));
const AuditPage = lazy(() => import('./pages/AuditPage').then((m) => ({ default: m.AuditPage })));

export function App() {
  return (
    <Suspense fallback={<FullPageSpinner />}>
      <Routes>
        <Route element={<GuestOnly />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/registro" element={<RegisterPage />} />
          <Route path="/olvide-password" element={<ForgotPasswordPage />} />
        </Route>

        {/* Accesibles con o sin sesión (llegan desde un enlace del correo). */}
        <Route path="/verificar-email" element={<VerifyEmailPage />} />
        <Route path="/restablecer-password" element={<ResetPasswordPage />} />
        <Route path="/dev/buzon" element={<DevMailboxPage />} />

        <Route element={<RequireAuth />}>
          <Route element={<AppLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path="/usuarios" element={<RequirePermission permission={PERMISSIONS.usuarios.leer}><UsersPage /></RequirePermission>} />
            <Route path="/roles" element={<RequirePermission permission={PERMISSIONS.roles.leer}><RolesPage /></RequirePermission>} />
            <Route path="/permisos" element={<RequirePermission permission={PERMISSIONS.permisos.leer}><PermissionsPage /></RequirePermission>} />
            <Route path="/auditoria" element={<RequirePermission permission={PERMISSIONS.auditoria.leer}><AuditPage /></RequirePermission>} />
            <Route path="/perfil" element={<ProfilePage />} />
            <Route path="/sin-permiso" element={<ForbiddenPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Route>
      </Routes>
    </Suspense>
  );
}
