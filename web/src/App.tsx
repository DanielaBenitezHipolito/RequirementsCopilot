import { useEffect, useState } from 'react';
import { AnalyzeView } from './features/analysis/AnalyzeView';
import { HistoryView } from './features/history/HistoryView';
import { DetailView } from './features/history/DetailView';
import { InterviewView } from './features/interview/InterviewView';
import { ProjectsView } from './features/projects/ProjectsView';
import { LoginView } from './features/auth/LoginView';
import { UsersView } from './features/admin/UsersView';
import { AdminView } from './features/admin/AdminView';
import { getAuthStatus, setAuthToken, type AuthUser } from './shared/api/client';

type View = 'analyze' | 'interview' | 'history' | 'projects' | 'detail' | 'usuarios' | 'tablero';

const BRAND = '#1e2a5a';

function ShieldIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M12 3l7 3v5c0 4.5-3 8.5-7 10-4-1.5-7-5.5-7-10V6l7-3z"
      />
      <path strokeLinecap="round" strokeLinejoin="round" d="M9.5 12l1.8 1.8L14.5 10" />
    </svg>
  );
}

function AuditIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 11l3 3L22 4" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11" />
    </svg>
  );
}

function ChatIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2v10z"
      />
    </svg>
  );
}

function HistoryIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 3v5h5" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M3.05 13a9 9 0 106.24-9.53" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 7v5l3 3" />
    </svg>
  );
}

function FolderIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
    </svg>
  );
}

function UsersIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75" />
    </svg>
  );
}

function GaugeIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 15l3.5-3.5" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M20.3 17a9 9 0 10-16.6 0" />
    </svg>
  );
}

function loadStoredUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem('rc_user');
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
}

export default function App() {
  const [view, setView] = useState<View>('analyze');
  const [selectedId, setSelectedId] = useState<string>();
  // null = aún consultando; false = API abierta (sin login); true = requiere sesión.
  const [authRequired, setAuthRequired] = useState<boolean | null>(null);
  const [user, setUser] = useState<AuthUser | null>(loadStoredUser());

  useEffect(() => {
    getAuthStatus()
      .then((s) => setAuthRequired(s.habilitado))
      .catch(() => setAuthRequired(false));
  }, []);

  function logout() {
    setAuthToken(null);
    localStorage.removeItem('rc_user');
    setUser(null);
    setView('analyze');
  }

  useEffect(() => {
    const onExpired = () => logout();
    window.addEventListener('auth:expired', onExpired);
    return () => window.removeEventListener('auth:expired', onExpired);
  }, []);

  if (authRequired === null) {
    return <div className="flex h-screen items-center justify-center bg-slate-100 text-sm text-slate-400">Cargando…</div>;
  }

  if (authRequired && !user) {
    return (
      <LoginView
        onLogin={(u) => {
          localStorage.setItem('rc_user', JSON.stringify(u));
          setUser(u);
        }}
      />
    );
  }

  const isAdmin = authRequired && user?.rol === 'Admin';

  // 'detail' pertenece conceptualmente al historial: la pestaña Historial queda activa al verlo.
  const activeView = view === 'detail' ? 'history' : view;

  const tab = (target: View, label: string, icon: React.ReactNode) => {
    const active = activeView === target;
    return (
      <button
        key={target}
        onClick={() => setView(target)}
        className={`relative flex items-center gap-2 px-1 pb-3 text-[13px] font-bold tracking-wide uppercase transition-colors ${
          active ? 'text-white' : 'text-white/55 hover:text-white/85'
        }`}
      >
        {icon}
        {label}
        {/* El subrayado dorado: el mismo haz del login marcando la pestaña activa. */}
        <span
          className={`absolute right-0 -bottom-px left-0 h-0.5 rounded-full transition-opacity ${
            active ? 'opacity-100' : 'opacity-0'
          }`}
          style={{ backgroundColor: '#f59e0b', boxShadow: '0 0 10px #f59e0b88' }}
        />
      </button>
    );
  };

  return (
    <div className="flex h-screen flex-col bg-slate-100">
      <header
        className="shrink-0 text-white"
        style={{ background: `linear-gradient(135deg, #0c1430 0%, ${BRAND} 75%, #27367a 100%)` }}
      >
        <div className="flex w-full items-center gap-4 px-6 pt-4 lg:px-10">
          <div className="flex items-center gap-3">
            <span className="text-2xl font-black tracking-tight text-white">HOWDEN</span>
            <span className="h-8 w-px bg-white/15" />
            <div className="leading-tight">
              <p className="text-[10px] font-semibold tracking-widest text-white/50 uppercase">AI Requirements</p>
              <p className="text-[10px] font-semibold tracking-widest text-white/50 uppercase">Copilot</p>
            </div>
          </div>
          <div className="ml-auto flex items-center gap-3">
            <div className="hidden items-center gap-1.5 rounded-full border border-white/15 px-3 py-1.5 text-xs text-white/70 md:flex">
              <ShieldIcon />
              Ambiente Interno Seguro · Howden
            </div>
            {authRequired && user && (
              <div className="flex items-center gap-2 text-xs text-white/80">
                <span className="font-semibold">{user.nombre}</span>
                <span className="rounded-full bg-white/10 px-2 py-0.5 text-[10px] font-bold uppercase">{user.rol}</span>
                <button type="button" className="text-white/50 underline hover:text-white" onClick={logout}>
                  Salir
                </button>
              </div>
            )}
          </div>
        </div>
        <nav className="mt-4 flex w-full flex-wrap gap-x-7 gap-y-1 border-b border-white/10 px-6 lg:px-10">
          {tab('analyze', 'Auditar', <AuditIcon />)}
          {tab('interview', 'Copilot Chat', <ChatIcon />)}
          {tab('history', 'Historial', <HistoryIcon />)}
          {tab('projects', 'Proyectos', <FolderIcon />)}
          {isAdmin && tab('usuarios', 'Usuarios', <UsersIcon />)}
          {isAdmin && tab('tablero', 'Tablero', <GaugeIcon />)}
        </nav>
      </header>

      <main className="flex w-full min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6 lg:px-10">
        <div className="min-h-0 flex-1">
          {view === 'analyze' && (
            <AnalyzeView />
          )}
          {view === 'interview' && (
            <InterviewView
              onAnalyzed={(id) => {
                setSelectedId(id);
                setView('detail');
              }}
            />
          )}
          {view === 'history' && (
            <HistoryView
              onSelect={(id) => {
                setSelectedId(id);
                setView('detail');
              }}
            />
          )}
          {view === 'projects' && <ProjectsView />}
          {view === 'usuarios' && isAdmin && user && <UsersView currentEmail={user.email} />}
          {view === 'tablero' && isAdmin && <AdminView />}
          {view === 'detail' && selectedId && <DetailView id={selectedId} onBack={() => setView('history')} />}
        </div>
      </main>

      <footer className="shrink-0 border-t border-slate-200 bg-white">
        <div className="flex w-full flex-col items-center gap-1 px-6 py-2.5 text-[11px] text-slate-400 sm:flex-row sm:justify-between lg:px-10">
          <p>© 2026 Howden Corredores de Seguros SA. Todos los derechos reservados.</p>
          <p className="flex items-center gap-1.5">
            <span className="text-emerald-500">✓</span> Confidencialidad Asegurada
            <span className="text-slate-300">|</span>
            Estándares IEEE-830
          </p>
        </div>
      </footer>
    </div>
  );
}
