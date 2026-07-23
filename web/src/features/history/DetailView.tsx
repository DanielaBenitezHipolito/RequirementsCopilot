import { useCallback, useEffect, useMemo, useState } from 'react';
import { getAnalysis, reevaluateRequirement } from '../../shared/api/client';
import { mapRequirement, StoriesBanner } from '../../shared/components/StoriesBanner';
import type { RequirementView } from '../../shared/types';
import { RequirementCard } from '../analysis/RequirementCard';

const BRAND = '#1e2a5a';

export function DetailView({ id, onBack }: { id: string; onBack: () => void }) {
  const [fileName, setFileName] = useState('');
  const [resumen, setResumen] = useState<string>();
  const [requirements, setRequirements] = useState<RequirementView[]>([]);
  const [area, setArea] = useState<string>('Todas');
  const [error, setError] = useState<string>();

  const load = useCallback(() => {
    getAnalysis(id)
      .then((detail) => {
        setFileName(detail.fileName);
        setResumen(detail.resumen ?? undefined);
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
      {resumen && (
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">
            ✦ Resumen Ejecutivo de Auditoría
          </p>
          <p className="mt-1.5 text-sm text-slate-700">{resumen}</p>
        </div>
      )}
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
      <StoriesBanner analysisId={id} requirements={requirements} onUpdate={replace} />
      <div className="space-y-3">
        {visible.map((r) => (
          <RequirementCard
            key={r.codigo}
            requirement={r}
            onReevaluate={async (respuestas) => replace(await reevaluateRequirement(id, r.codigo, respuestas))}
          />
        ))}
      </div>
    </div>
  );
}
