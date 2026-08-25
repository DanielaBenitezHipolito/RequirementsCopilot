import { useEffect, useState } from 'react';
import { getProjects } from '../api/client';
import type { ProjectSummary } from '../types';

interface Props {
  value?: string;
  onChange: (proyecto?: string) => void;
  disabled?: boolean;
}

/**
 * Selector de proyecto existente. Sin selección = proyecto nuevo (los agentes no reciben contexto).
 * La gestión (subir/editar/eliminar .md) vive en la pestaña Proyectos.
 */
export function ProjectPicker({ value, onChange, disabled }: Props) {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    getProjects()
      .then(setProjects)
      .catch((e) => setError(e instanceof Error ? e.message : 'No se pudieron cargar los proyectos.'));
  }, []);

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="text-[11px] font-bold tracking-wide text-slate-500 uppercase">Proyecto</label>
      <select
        className="min-w-[12rem] rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 focus:border-slate-400 focus:outline-none disabled:bg-slate-100"
        value={value ?? ''}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value || undefined)}
      >
        <option value="">Proyecto nuevo (sin contexto)</option>
        {projects.map((p) => (
          <option key={p.nombre} value={p.nombre}>
            {p.nombre}
          </option>
        ))}
      </select>
      {error && <span className="text-xs text-rose-600">{error}</span>}
    </div>
  );
}
