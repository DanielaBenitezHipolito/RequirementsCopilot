import { useCallback, useEffect, useMemo, useState } from 'react';
import { answerClarifications, generateStories, getAnalysis } from '../../shared/api/client';
import type { RequirementView } from '../../shared/types';
import { RequirementCard } from '../analysis/RequirementCard';

const BRAND = '#1e2a5a';

function mapRequirement(r: any): RequirementView {
  return {
    codigo: r.codigo,
    texto: r.texto,
    area: r.area,
    evaluacion: r.evaluacion ?? undefined,
    aclaraciones: r.aclaraciones ?? [],
    listoParaHistorias: r.listoParaHistorias,
    historias: (r.historias ?? []).map((h: any) => ({ ...h, caso: h.caso ?? undefined })),
  };
}

export function DetailView({ id, onBack }: { id: string; onBack: () => void }) {
  const [fileName, setFileName] = useState('');
  const [requirements, setRequirements] = useState<RequirementView[]>([]);
  const [area, setArea] = useState<string>('Todas');
  const [error, setError] = useState<string>();

  const load = useCallback(() => {
    getAnalysis(id)
      .then((detail) => {
        setFileName(detail.fileName);
        setRequirements(detail.requerimientos.map(mapRequirement));
      })
      .catch((e) => setError(e.message));
  }, [id]);

  useEffect(() => load(), [load]);

  const replace = (updated: any) =>
    setRequirements((prev) => prev.map((r) => (r.codigo === updated.codigo ? mapRequirement(updated) : r)));

  const areas = useMemo(() => ['Todas', ...new Set(requirements.map((r) => r.area))], [requirements]);
  const visible = area === 'Todas' ? requirements : requirements.filter((r) => r.area === area);

  if (error) return <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>;

  return (
    <div className="space-y-4">
      <button
        className="text-sm font-semibold hover:underline"
        style={{ color: BRAND }}
        onClick={onBack}
      >
        ← Volver al historial
      </button>
      <h2 className="text-lg font-bold text-slate-800">{fileName}</h2>
      <div className="flex flex-wrap gap-2">
        {areas.map((a) => (
          <button
            key={a}
            onClick={() => setArea(a)}
            className={`rounded-full px-3 py-1 text-xs font-bold tracking-wide uppercase ${
              area === a ? 'text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
            style={area === a ? { backgroundColor: BRAND } : undefined}
          >
            {a}
          </button>
        ))}
      </div>
      <div className="space-y-3">
        {visible.map((r) => (
          <RequirementCard
            key={r.codigo}
            requirement={r}
            onAnswer={async (respuestas) => replace(await answerClarifications(id, r.codigo, respuestas))}
            onGenerate={async () => replace(await generateStories(id, r.codigo))}
          />
        ))}
      </div>
    </div>
  );
}
