import { useState } from 'react';
import { login, setAuthToken, type AuthUser } from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';

const BRAND = '#1e2a5a';

/** Pantalla de inicio de sesión (solo cuando la autenticación está habilitada en el backend). */
export function LoginView({ onLogin }: { onLogin: (user: AuthUser) => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  async function submit() {
    if (!email.trim() || !password) return;
    setBusy(true);
    setError(undefined);
    try {
      const result = await login(email.trim(), password);
      setAuthToken(result.token);
      onLogin({ email: result.email, nombre: result.nombre, rol: result.rol });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo iniciar sesión.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex h-screen items-center justify-center bg-slate-100 px-4">
      <div className="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <p className="text-center text-2xl font-black tracking-tight" style={{ color: BRAND }}>
          HOWDEN
        </p>
        <p className="mt-1 text-center text-[10px] font-semibold tracking-widest text-slate-500 uppercase">
          AI Requirements Copilot
        </p>
        <div className="mt-6 space-y-3">
          <input
            className="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            className="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Contraseña"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void submit();
            }}
          />
          {error && <p className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700">{error}</p>}
          <BrandButton className="w-full" loading={busy} loadingText="Entrando…" disabled={!email.trim() || !password} onClick={() => void submit()}>
            Iniciar sesión
          </BrandButton>
        </div>
      </div>
    </div>
  );
}
