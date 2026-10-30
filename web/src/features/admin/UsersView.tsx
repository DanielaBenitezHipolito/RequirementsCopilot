import { useEffect, useState } from 'react';
import { deleteUser, getUsers, upsertUser } from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';

const BRAND = '#1e2a5a';

function initials(nombre: string) {
  return nombre
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((parte) => parte[0]!.toUpperCase())
    .join('');
}

interface UserRow {
  email: string;
  nombre: string;
  rol: string;
  createdAt: string;
}

/** Administración de usuarios (solo rol Admin). */
export function UsersView({ currentEmail }: { currentEmail: string }) {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [email, setEmail] = useState('');
  const [nombre, setNombre] = useState('');
  const [rol, setRol] = useState('Analista');
  const [password, setPassword] = useState('');

  async function load() {
    try {
      setUsers(await getUsers());
      setError(undefined);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los usuarios.');
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function create() {
    setBusy(true);
    try {
      await upsertUser(email.trim(), nombre.trim(), rol, password);
      setEmail('');
      setNombre('');
      setPassword('');
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar el usuario.');
    } finally {
      setBusy(false);
    }
  }

  async function remove(target: string) {
    if (!confirm(`¿Eliminar el usuario «${target}»?`)) return;
    try {
      await deleteUser(target);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo eliminar.');
    }
  }

  const field = 'rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none';

  return (
    <div className="space-y-4">
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Nuevo usuario</h2>
        <p className="mt-1 text-xs text-slate-500">
          Si el email ya existe, se reemplaza (incluida la contraseña): úselo también para resetear accesos.
        </p>
        <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-5">
          <input className={field} placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} />
          <input className={field} placeholder="Nombre" value={nombre} onChange={(e) => setNombre(e.target.value)} />
          <select className={field} value={rol} onChange={(e) => setRol(e.target.value)}>
            <option value="Analista">Analista</option>
            <option value="Admin">Admin</option>
          </select>
          <input
            className={field}
            placeholder="Contraseña"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <BrandButton loading={busy} disabled={!email.trim() || !nombre.trim() || !password} onClick={() => void create()}>
            Guardar
          </BrandButton>
        </div>
      </div>

      {error && <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Usuarios</h2>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-left text-[11px] tracking-wide text-slate-400 uppercase">
                <th className="py-2">Email</th>
                <th>Nombre</th>
                <th>Rol</th>
                <th>Creado</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.email} className="border-b border-slate-100">
                  <td className="py-2.5">
                    <span className="flex items-center gap-2.5 font-semibold text-slate-700">
                      <span
                        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-[11px] font-black text-white"
                        style={{ background: `linear-gradient(135deg, #0c1430, ${BRAND})` }}
                      >
                        {initials(u.nombre) || '?'}
                      </span>
                      {u.email}
                    </span>
                  </td>
                  <td className="text-slate-600">{u.nombre}</td>
                  <td>
                    <span
                      className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold uppercase ${
                        u.rol === 'Admin' ? 'bg-violet-50 text-violet-700' : 'bg-slate-100 text-slate-600'
                      }`}
                    >
                      {u.rol}
                    </span>
                  </td>
                  <td className="text-slate-500">{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td className="text-right">
                    {u.email !== currentEmail && (
                      <button
                        type="button"
                        className="rounded-md p-1.5 text-slate-400 hover:bg-slate-200 hover:text-rose-500"
                        aria-label={`Eliminar ${u.email}`}
                        onClick={() => void remove(u.email)}
                      >
                        ✕
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
