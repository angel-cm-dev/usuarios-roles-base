import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { AuthContext } from '../../auth/AuthContext';
import { LoginPage } from './LoginPage';

function renderLogin(login = vi.fn()) {
  const value = { status: 'anonymous' as const, user: null, login, logout: vi.fn(), applySession: vi.fn(), setUser: vi.fn(), can: () => false };
  render(
    <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <AuthContext.Provider value={value}>
        <LoginPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
  return login;
}

describe('<LoginPage>', () => {
  it('valida los campos antes de llamar a la API', async () => {
    const login = renderLogin();
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    expect(screen.getByText('El email es obligatorio.')).toBeInTheDocument();
    expect(screen.getByText('La contraseña es obligatoria.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('muestra un aviso específico si la cuenta está bloqueada', async () => {
    const login = vi.fn().mockRejectedValue(new ApiError(423, 'La cuenta está bloqueada temporalmente.', 'account_locked'));
    renderLogin(login);

    await userEvent.type(screen.getByLabelText('Email'), 'ana@demo.pe');
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Incorrecta#1');
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    expect(await screen.findByText('Cuenta bloqueada temporalmente')).toBeInTheDocument();
    expect(login).toHaveBeenCalledWith('ana@demo.pe', 'Incorrecta#1');
  });

  it('ofrece reenviar la verificación si el email no está verificado', async () => {
    const login = vi.fn().mockRejectedValue(new ApiError(403, 'Debes verificar tu email.', 'email_not_verified'));
    renderLogin(login);

    await userEvent.type(screen.getByLabelText('Email'), 'ana@demo.pe');
    await userEvent.type(screen.getByLabelText('Contraseña'), 'Segura#2026');
    await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

    expect(await screen.findByRole('button', { name: 'Reenviar correo' })).toBeInTheDocument();
  });
});
