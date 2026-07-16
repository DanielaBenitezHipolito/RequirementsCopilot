import { useRef, useState } from 'react';
import { analyzeFile } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { useAnalysisStore } from './store';
import { RequirementCard } from './RequirementCard';

export function AnalyzeView({ onOpenDetail }: { onOpenDetail?: (id: string) => void }) {
  const { status, statusMessage, error, requirements, analysisId, start, applyEvent } = useAnalysisStore();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);

  async function analyze(file: File) {
    start();
    try {
      const response = await analyzeFile(file);
      for await (const evt of parseSse(response.body!)) applyEvent(evt);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  return (
    <div className="space-y-4">
      <div
        className={`flex cursor-pointer flex-col items-center rounded-xl border-2 border-dashed p-8 text-slate-500 ${
          dragging ? 'border-indigo-400 bg-indigo-50' : 'border-slate-300'
        }`}
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          const file = e.dataTransfer.files[0];
          if (file) void analyze(file);
        }}
      >
        <p className="font-medium">Arrastra un documento o haz clic para seleccionarlo</p>
        <p className="mt-1 text-xs">PDF, DOCX, TXT o MD · máximo 10 MB</p>
        <input
          ref={inputRef}
          type="file"
          accept=".pdf,.docx,.txt,.md"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) void analyze(file);
            e.target.value = '';
          }}
        />
      </div>

      {status === 'running' && (
        <p className="animate-pulse text-sm text-indigo-600">{statusMessage || 'Analizando…'}</p>
      )}
      {status === 'error' && <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
      {status === 'done' && (
        <div className="flex items-center gap-3">
          <p className="text-sm text-emerald-700">Análisis completado.</p>
          {analysisId && onOpenDetail && (
            <button
              className="rounded bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700"
              onClick={() => onOpenDetail(analysisId)}
            >
              Responder preguntas y generar historias →
            </button>
          )}
        </div>
      )}

      <div className="space-y-3">
        {requirements.map((r) => (
          <RequirementCard key={r.codigo} requirement={r} />
        ))}
      </div>
    </div>
  );
}
