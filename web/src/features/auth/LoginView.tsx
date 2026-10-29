import { useState } from 'react';
import { login, setAuthToken, type AuthUser } from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';

const BRAND = '#1e2a5a';
const GOLD = '#f59e0b';

const CRITERIOS = ['Claridad', 'Completitud', 'Verificabilidad', 'Consistencia', 'Factibilidad'];

/** Anchos (en %) de las líneas del documento ficticio que se audita en la viñeta. */
const DOC_LINES = [92, 78, 85, 60, 88, 72, 40];

/**
 * Viñeta de marca: un requerimiento siendo auditado — el haz dorado recorre el documento
 * y los cinco criterios del framework se van marcando. Estático con reduced-motion.
 */
function AuditVignette() {
  return (
    <div className="relative rounded-2xl border border-white/10 bg-white/5 p-5 backdrop-blur-sm">
      <div className="flex items-center gap-2 border-b border-white/10 pb-3">
        <span className="h-2.5 w-2.5 rounded-full bg-white/20" />
        <span className="h-2.5 w-2.5 rounded-full bg-white/20" />
        <p className="ml-2 text-[10px] font-semibold tracking-widest text-white/50 uppercase">
          requerimiento-001.docx
        </p>
      </div>

      <div className="relative mt-4 space-y-2.5" aria-hidden="true">
        {DOC_LINES.map((width, i) => (
          <div key={i} className="h-2 rounded-full bg-white/15" style={{ width: `${width}%` }} />
        ))}
        {/* Haz de escaneo */}
        <div
          className="login-scan-beam pointer-events-none absolute -left-2 -right-2 h-7 rounded-md"
          style={{
            background: `linear-gradient(180deg, transparent, ${GOLD}26 45%, ${GOLD}40 50%, ${GOLD}26 55%, transparent)`,
            boxShadow: `0 0 24px ${GOLD}33`,
          }}
        />
      </div>

      <div className="mt-5 flex flex-wrap gap-2">
        {CRITERIOS.map((criterio, i) => (
          <span
            key={criterio}
            className="login-tick flex items-center gap-1.5 rounded-full border border-white/15 px-2.5 py-1 text-[10px] font-semibold tracking-wide text-white/90 uppercase"
            style={{ animationDelay: `${0.8 + i * 1.1}s` }}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke={GOLD} strokeWidth={3} className="h-3 w-3">
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
            {criterio}
          </span>
        ))}
      </div>
    </div>
  );
}

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

  const field =
    'w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-800 placeholder-slate-400 focus:border-slate-500 focus:outline-none focus:ring-2 focus:ring-slate-200';
  const label = 'mb-1.5 block text-[11px] font-bold tracking-widest text-slate-500 uppercase';

  return (
    <div className="flex min-h-screen bg-slate-100">
      {/* Panel de marca: navy profundo con la viñeta de auditoría. */}
      <div
        className="relative hidden w-[55%] flex-col justify-between overflow-hidden p-12 lg:flex"
        style={{ background: `linear-gradient(160deg, #0c1430 0%, ${BRAND} 70%, #27367a 100%)` }}
      >
        <div>
          <p className="text-3xl font-black tracking-tight text-white">HOWDEN</p>
          <p className="mt-1 text-[11px] font-semibold tracking-[0.25em] text-white/50 uppercase">
            AI Requirements Copilot
          </p>
        </div>

        <div className="max-w-md">
          <h1 className="text-4xl leading-tight font-black tracking-tight text-white">
            Requerimientos claros
            <br />
            antes de la primera
            <br />
            línea de código.
          </h1>
          <div className="mt-8">
            <AuditVignette />
          </div>
        </div>

        <p className="text-xs text-white/40">
          Estándares IEEE-830 · Ambiente interno Howden
        </p>
      </div>

      {/* Panel del formulario. */}
      <div className="flex flex-1 items-center justify-center px-4 py-10">
        <div className="w-full max-w-sm">
          {/* Marca compacta para pantallas sin panel izquierdo. */}
          <div className="mb-8 lg:hidden">
            <p className="text-2xl font-black tracking-tight" style={{ color: BRAND }}>
              HOWDEN
            </p>
            <p className="mt-0.5 text-[10px] font-semibold tracking-[0.25em] text-slate-500 uppercase">
              AI Requirements Copilot
            </p>
          </div>

          <h2 className="text-2xl font-black tracking-tight text-slate-900">Iniciar sesión</h2>
          <p className="mt-1.5 text-sm text-slate-500">
            Use las credenciales asignadas por su administrador.
          </p>

          <div className="mt-8 space-y-5">
            <div>
              <label htmlFor="login-email" className={label}>
                Email
              </label>
              <input
                id="login-email"
                className={field}
                placeholder="nombre@howdengroup.com"
                type="email"
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="login-password" className={label}>
                Contraseña
              </label>
              <input
                id="login-password"
                className={field}
                placeholder="••••••••"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void submit();
                }}
              />
            </div>
            {error && <p className="rounded-lg bg-rose-50 p-3 text-xs text-rose-700">{error}</p>}
            <BrandButton
              className="w-full"
              loading={busy}
              loadingText="Entrando…"
              disabled={!email.trim() || !password}
              onClick={() => void submit()}
            >
              Iniciar sesión
            </BrandButton>
          </div>

          <p className="mt-10 text-center text-xs text-slate-400">
            Acceso restringido al equipo de Howden.
          </p>
        </div>
      </div>
    </div>
  );
}
