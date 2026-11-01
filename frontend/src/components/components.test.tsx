import type { ReactElement } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { UserProfile } from '../api/types';
import { AuthContext } from '../auth/AuthContext';
import { Can } from '../auth/guards';
import { Pagination } from './ui/Pagination';
import { PasswordStrengthMeter } from './ui/PasswordStrengthMeter';

function renderWithPermissions(ui: ReactElement, permissions: string[]) {
  const user: UserProfile = { id: '1', email: 'a@demo.pe', fullName: 'Ana', emailConfirmed: true, roles: ['usuario'], permissions, createdAt: '', lastLoginAt: null };
  const value = {
    status: 'authenticated' as const,
    user,
    login: vi.fn(),
    logout: vi.fn(),
    applySession: vi.fn(),
    setUser: vi.fn(),
    can: (p: string | string[]) => (Array.isArray(p) ? p : [p]).some((x) => permissions.includes(x)),
  };
  return render(
    <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <AuthContext.Provider value={value}>{ui}</AuthContext.Provider>
    </MemoryRouter>,
  );
}

describe('<Can>', () => {
  it('muestra el contenido si el usuario tiene el permiso', () => {
    renderWithPermissions(<Can permission="usuarios.crear"><button>Nuevo usuario</button></Can>, ['usuarios.crear']);
    expect(screen.getByRole('button', { name: 'Nuevo usuario' })).toBeInTheDocument();
  });

  it('oculta la acción (o muestra el fallback) si no tiene el permiso', () => {
    renderWithPermissions(
      <Can permission="usuarios.crear" fallback={<span>Sin acceso</span>}>
        <button>Nuevo usuario</button>
      </Can>,
      ['usuarios.leer'],
    );
    expect(screen.queryByRole('button', { name: 'Nuevo usuario' })).not.toBeInTheDocument();
    expect(screen.getByText('Sin acceso')).toBeInTheDocument();
  });
});

describe('<PasswordStrengthMeter>', () => {
  it('marca las reglas cumplidas y muestra la fortaleza', () => {
    render(<PasswordStrengthMeter password="Lima-Peru-Montañas-2026!" />);
    expect(screen.getByTestId('strength-label')).toHaveTextContent('Muy fuerte');
    expect(screen.getAllByText(/\(cumplido\)/)).toHaveLength(5);
  });

  it('indica las reglas pendientes', () => {
    render(<PasswordStrengthMeter password="abc" />);
    expect(screen.getByTestId('strength-label')).toHaveTextContent('Muy débil');
    expect(screen.getAllByText(/\(pendiente\)/).length).toBeGreaterThanOrEqual(3);
  });
});

describe('<Pagination>', () => {
  it('muestra el rango y navega entre páginas', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={2} totalPages={3} totalCount={25} pageSize={10} onPageChange={onPageChange} />);

    expect(screen.getByText(/Mostrando/)).toHaveTextContent('Mostrando 11–20 de 25');
    await userEvent.click(screen.getByRole('button', { name: 'Página siguiente' }));
    await userEvent.click(screen.getByRole('button', { name: 'Página anterior' }));
    expect(onPageChange.mock.calls).toEqual([[3], [1]]);
  });

  it('deshabilita "anterior" en la primera página', () => {
    render(<Pagination page={1} totalPages={1} totalCount={3} pageSize={10} onPageChange={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Página siguiente' })).toBeDisabled();
  });
});
