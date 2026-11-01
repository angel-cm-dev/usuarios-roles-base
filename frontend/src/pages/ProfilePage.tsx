import { useState, type FormEvent } from 'react';
import { ApiError } from '../api/client';
import { authApi } from '../api/services';
import { useAuth } from '../auth/AuthContext';
import { Can } from '../auth/guards';
import { PERMISSIONS } from '../auth/permissions';
import { Button } from '../components/ui/Button';
import { Input, PasswordInput } from '../components/ui/Field';
import { Alert, Badge, PageHeader } from '../components/ui/Feedback';
import { PasswordStrengthMeter } from '../components/ui/PasswordStrengthMeter';
import { errorMessage } from '../hooks/usePagedQuery';
import { formatDateTime } from '../lib/format';
import { compactErrors, hasErrors, validateConfirmation, validateFullName, validateNewPassword, type FieldErrors } from '../lib/validation';

type PasswordField = 'currentPassword' | 'newPassword' | 'confirm';

export function ProfilePage() {
  const { user, setUser, applySession, logout } = useAuth();
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [nameError, setNameError] = useState<string>();
  const [profileNotice, setProfileNotice] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);
  const [savingProfile, setSavingProfile] = useState(false);

  const [passwords, setPasswords] = useState({ currentPassword: '', newPassword: '', confirm: '' });
  const [passwordErrors, setPasswordErrors] = useState<FieldErrors<PasswordField>>({});
  const [passwordNotice, setPasswordNotice] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);
  const [savingPassword, setSavingPassword] = useState(false);
  const [loggingOutAll, setLoggingOutAll] = useState(false);

  if (!user) return null;

  const saveProfile = async (event: FormEvent) => {
    event.preventDefault();
    const validation = validateFullName(fullName);
    setNameError(validation);
    if (validation) return;
    setSavingProfile(true);
    try {
      setUser(await authApi.updateProfile(fullName.trim()));
      setProfileNotice({ tone: 'success', text: 'Perfil actualizado.' });
    } catch (e) {
      setProfileNotice({ tone: 'error', text: errorMessage(e) });
    } finally {
      setSavingProfile(false);
    }
  };

  const changePassword = async (event: FormEvent) => {
    event.preventDefault();
    const validation = compactErrors<PasswordField>({
      currentPassword: passwords.currentPassword ? undefined : 'Ingresa tu contraseña actual.',
      newPassword: validateNewPassword(passwords.newPassword, user.email),
      confirm: validateConfirmation(passwords.newPassword, passwords.confirm),
    });
    setPasswordErrors(validation);
    if (hasErrors(validation)) return;

    setSavingPassword(true);
    setPasswordNotice(null);
    try {
      applySession(await authApi.changePassword(passwords.currentPassword, passwords.newPassword));
      setPasswords({ currentPassword: '', newPassword: '', confirm: '' });
      setPasswordNotice({ tone: 'success', text: 'Contraseña actualizada. Se cerraron tus otras sesiones.' });
    } catch (e) {
      if (e instanceof ApiError) {
        setPasswordErrors({ currentPassword: e.fieldError('currentPassword'), newPassword: e.fieldError('newPassword') });
      }
      setPasswordNotice({ tone: 'error', text: errorMessage(e) });
    } finally {
      setSavingPassword(false);
    }
  };

  const logoutEverywhere = async () => {
    setLoggingOutAll(true);
    try {
      await authApi.logoutAll();
    } finally {
      await logout();
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader title="Mi perfil" description="Tus datos, permisos efectivos y seguridad de la cuenta." />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <section className="card space-y-3 p-5 lg:col-span-1">
          <h2 className="font-semibold">Cuenta</h2>
          <dl className="space-y-2 text-sm">
            <div><dt className="text-slate-500">Email</dt><dd className="font-medium">{user.email}</dd></div>
            <div><dt className="text-slate-500">Roles</dt><dd className="flex flex-wrap gap-1">{user.roles.map((r) => <Badge key={r} tone="brand">{r}</Badge>)}</dd></div>
            <div><dt className="text-slate-500">Último acceso</dt><dd>{formatDateTime(user.lastLoginAt)}</dd></div>
            <div><dt className="text-slate-500">Permisos efectivos</dt>
              <dd className="mt-1 flex flex-wrap gap-1">
                {user.permissions.length ? user.permissions.map((p) => <Badge key={p}>{p}</Badge>) : <span className="text-slate-400">Ninguno</span>}
              </dd>
            </div>
          </dl>
          <Button variant="secondary" className="w-full" loading={loggingOutAll} onClick={logoutEverywhere}>
            Cerrar sesión en todos los dispositivos
          </Button>
        </section>

        <div className="space-y-6 lg:col-span-2">
          <Can permission={PERMISSIONS.perfil.editar}>
            <section className="card p-5">
              <h2 className="mb-4 font-semibold">Datos personales</h2>
              <form className="space-y-4" onSubmit={saveProfile} noValidate>
                {profileNotice && <Alert tone={profileNotice.tone}>{profileNotice.text}</Alert>}
                <Input label="Nombre completo" value={fullName} onChange={(e) => setFullName(e.target.value)} error={nameError} />
                <Button type="submit" loading={savingProfile}>Guardar</Button>
              </form>
            </section>
          </Can>

          <section className="card p-5">
            <h2 className="mb-4 font-semibold">Cambiar contraseña</h2>
            <form className="space-y-4" onSubmit={changePassword} noValidate>
              {passwordNotice && <Alert tone={passwordNotice.tone}>{passwordNotice.text}</Alert>}
              <PasswordInput label="Contraseña actual" autoComplete="current-password" value={passwords.currentPassword} onChange={(e) => setPasswords({ ...passwords, currentPassword: e.target.value })} error={passwordErrors.currentPassword} />
              <div>
                <PasswordInput label="Nueva contraseña" autoComplete="new-password" value={passwords.newPassword} onChange={(e) => setPasswords({ ...passwords, newPassword: e.target.value })} error={passwordErrors.newPassword} />
                <PasswordStrengthMeter password={passwords.newPassword} />
              </div>
              <PasswordInput label="Confirmar nueva contraseña" autoComplete="new-password" value={passwords.confirm} onChange={(e) => setPasswords({ ...passwords, confirm: e.target.value })} error={passwordErrors.confirm} />
              <Button type="submit" loading={savingPassword}>Actualizar contraseña</Button>
            </form>
          </section>
        </div>
      </div>
    </div>
  );
}
