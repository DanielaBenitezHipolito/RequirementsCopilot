import { useState } from 'react';
import { AnalyzeView } from './features/analysis/AnalyzeView';
import { HistoryView } from './features/history/HistoryView';
import { DetailView } from './features/history/DetailView';

type View = 'analyze' | 'history' | 'detail';

export default function App() {
  const [view, setView] = useState<View>('analyze');
  const [selectedId, setSelectedId] = useState<string>();

  const tab = (target: View, label: string) => (
    <button
      onClick={() => setView(target)}
      className={`rounded-lg px-4 py-2 text-sm font-medium ${
        view === target ? 'bg-indigo-600 text-white' : 'text-slate-600 hover:bg-slate-100'
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-4xl items-center gap-4 px-4 py-3">
          <h1 className="text-lg font-bold text-slate-800">RequirementsCopilot</h1>
          <nav className="ml-auto flex gap-1">
            {tab('analyze', 'Analizar')}
            {tab('history', 'Historial')}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-4xl px-4 py-6">
        {view === 'analyze' && <AnalyzeView />}
        {view === 'history' && (
          <HistoryView
            onSelect={(id) => {
              setSelectedId(id);
              setView('detail');
            }}
          />
        )}
        {view === 'detail' && selectedId && <DetailView id={selectedId} onBack={() => setView('history')} />}
      </main>
    </div>
  );
}
