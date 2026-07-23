import { useState } from 'react';
import { generateStories } from '../api/client';
import type { RequirementView } from '../types';
import { BrandButton } from './BrandButton';

export function mapRequirement(r: any): RequirementView {
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

interface Props {
  analysisId: string;
  requirements: RequirementView[];
  onUpdate: (updated: RequirementView) => void;
}

function SparkleBadge() {
  return (
    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-emerald-500 text-white">
      <svg viewBox="0 0 24 24" fill="currentColor" className="h-5 w-5">
        <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
      </svg>
    </span>
  );
}

export function StoriesBanner({ analysisId, requirements, onUpdate }: Props) {
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState({ i: 0, total: 0 });
  const [error, setError] = useState<string>();

  const pendientes = requirements.filter((r) => r.evaluacion?.pasa && r.historias.length === 0);
  const show = requirements.length > 0 && requirements.every((r) => r.evaluacion?.pasa === true) && pendientes.length > 0;

  if (!show) return null;

  async function generateAll() {
    setBusy(true);
    setError(undefined);
    setProgress({ i: 0, total: pendientes.length });
    for (let i = 0; i < pendientes.length; i++) {
      try {
        const updated = await generateStories(analysisId, pendientes[i].codigo);
        onUpdate(mapRequirement(updated));
        setProgress({ i: i + 1, total: pendientes.length });
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Error inesperado');
        break;
      }
    }
    setBusy(false);
  }

  return (
    <div className="flex flex-col gap-4 rounded-2xl border border-emerald-200 bg-emerald-50 p-5 sm:flex-row sm:items-center">
      <SparkleBadge />
      <div className="flex-1">
        <p className="font-bold text-slate-800">¡Todos los requerimientos cumplen con los estándares de calidad!</p>
        <p className="mt-0.5 text-sm text-emerald-700">
          La especificación cumple totalmente con el umbral de aprobación. Ahora puede autogenerar la especificación
          formal de Historias de Usuario para los desarrolladores.
        </p>
        {error && <p className="mt-2 rounded-lg bg-rose-50 p-2 text-xs text-rose-700">{error}</p>}
      </div>
      <BrandButton
        sparkle
        loading={busy}
        loadingText={`Generando historias… (${progress.i}/${progress.total})`}
        onClick={() => void generateAll()}
      >
        📖 Generar Historias de Usuario
      </BrandButton>
    </div>
  );
}
