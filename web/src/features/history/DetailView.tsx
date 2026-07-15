import { useEffect, useMemo, useState } from 'react';
import { getAnalysis } from '../../shared/api/client';
import type { RequirementView } from '../../shared/types';
import { RequirementCard } from '../analysis/RequirementCard';

export function DetailView({ id, onBack }: { id: string; onBack: () => void }) {
  const [fileName, setFileName] = useState('');
  const [requirements, setRequirements] = useState<RequirementView[]>([]);
  const [area, setArea] = useState<string>('Todas');
  const [error, setError] = useState<string>();

  useEffect(() => {
    getAnalysis(id)
      .then((detail) => {
        setFileName(detail.fileName);
        setRequirements(
          detail.requerimientos.map((r: any) => ({
            codigo: r.codigo,
            texto: r.texto,
            area: r.area,
            evaluacion: r.evaluacion ?? undefined,
            historias: (r.historias ?? []).map((h: any) => ({ ...h, caso: h.caso ?? undefined })),
          })),
        );
      })
      .catch((e) => setError(e.message));
  }, [id]);

  const areas = useMemo(() => ['Todas', ...new Set(requirements.map((r) => r.area))], [requirements]);
  const visible = area === 'Todas' ? requirements : requirements.filter((r) => r.area === area);

  if (error) return <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>;

  return (
    <div className="space-y-4">
      <button className="text-sm text-indigo-600 hover:underline" onClick={onBack}>
        ← Volver al historial
      </button>
      <h2 className="text-lg font-semibold text-slate-800">{fileName}</h2>
      <div className="flex flex-wrap gap-2">
        {areas.map((a) => (
          <button
            key={a}
            onClick={() => setArea(a)}
            className={`rounded-full px-3 py-1 text-xs font-medium ${
              area === a ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
            }`}
          >
            {a}
          </button>
        ))}
      </div>
      <div className="space-y-3">
        {visible.map((r) => (
          <RequirementCard key={r.codigo} requirement={r} />
        ))}
      </div>
    </div>
  );
}
