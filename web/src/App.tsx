import { useState } from 'react';
import { AnalyzeView } from './features/analysis/AnalyzeView';
import { HistoryView } from './features/history/HistoryView';
import { DetailView } from './features/history/DetailView';
import { InterviewView } from './features/interview/InterviewView';

type View = 'analyze' | 'interview' | 'history' | 'detail';

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

export default function App() {
  const [view, setView] = useState<View>('analyze');
  const [selectedId, setSelectedId] = useState<string>();

  const tab = (target: View, label: string, icon: React.ReactNode) => (
    <button
      onClick={() => setView(target)}
      className={`flex items-center justify-center gap-2 rounded-xl px-4 py-3 text-sm font-bold tracking-wide uppercase transition-colors ${
        view === target ? 'text-white' : 'text-slate-600 hover:bg-slate-50'
      }`}
      style={view === target ? { backgroundColor: BRAND } : undefined}
    >
      {icon}
      {label}
    </button>
  );

  return (
    <div className="flex min-h-screen flex-col bg-slate-100">
      <header className="border-b border-slate-200 bg-white">
        <div className="flex w-full items-center gap-4 px-6 py-3 lg:px-10">
          <div className="flex items-center gap-3">
            <span className="text-2xl font-black tracking-tight" style={{ color: BRAND }}>
              HOWDEN
            </span>
            <span className="h-8 w-px bg-slate-200" />
            <div className="leading-tight">
              <p className="text-[10px] font-semibold tracking-widest text-slate-500 uppercase">AI Requirements</p>
              <p className="text-[10px] font-semibold tracking-widest text-slate-500 uppercase">Copilot</p>
            </div>
          </div>
          <div className="ml-auto flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1.5 text-xs text-slate-600">
            <ShieldIcon />
            Ambiente Interno Seguro · Howden
          </div>
        </div>
      </header>

      <main className="flex w-full flex-1 flex-col px-6 py-6 lg:px-10">
        <div className="mb-6 grid shrink-0 grid-cols-1 gap-1 rounded-2xl border border-slate-200 bg-white p-1.5 shadow-sm sm:grid-cols-3">
          {tab('analyze', 'Auditar Requerimientos', <AuditIcon />)}
          {tab('interview', 'Copilot Chat de IA', <ChatIcon />)}
          {tab('history', 'Historial de Auditorías', <HistoryIcon />)}
        </div>

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
          {view === 'detail' && selectedId && <DetailView id={selectedId} onBack={() => setView('history')} />}
        </div>
      </main>

      <footer className="border-t border-slate-200">
        <div className="flex w-full flex-col items-center gap-1 px-6 py-3 text-xs text-slate-500 sm:flex-row sm:justify-between lg:px-10">
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
