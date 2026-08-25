import { useEffect, useRef, useState } from 'react';
import { getProjects, uploadProject } from '../api/client';
import type { ProjectSummary } from '../types';

interface Props {
  value?: string;
  onChange: (proyecto?: string) => void;
  disabled?: boolean;
}

/**
 * Selector de proyecto existente + carga de su .md. Sin selección = proyecto nuevo
 * (los agentes no reciben contexto y preguntan todo desde cero).
 */
export function ProjectPicker({ value, onChange, disabled }: Props) {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string>();
  const fileRef = useRef<HTMLInputElement>(null);

  async function load() {
    try {
      setProjects(await getProjects());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron cargar los proyectos.');
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function upload(file: File) {
    setUploading(true);
    setError(undefined);
    try {
      const { nombre } = await uploadProject(file);
      await load();
      onChange(nombre);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo subir el proyecto.');
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="text-[11px] font-bold tracking-wide text-slate-500 uppercase">Proyecto</label>
      <select
        className="min-w-[12rem] rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-700 focus:border-slate-400 focus:outline-none disabled:bg-slate-100"
        value={value ?? ''}
        disabled={disabled || uploading}
        onChange={(e) => onChange(e.target.value || undefined)}
      >
        <option value="">Proyecto nuevo (sin contexto)</option>
        {projects.map((p) => (
          <option key={p.nombre} value={p.nombre}>
            {p.nombre}
          </option>
        ))}
      </select>
      <button
        type="button"
        disabled={disabled || uploading}
        onClick={() => fileRef.current?.click()}
        className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
      >
        {uploading ? 'Subiendo…' : 'Subir contexto .md'}
      </button>
      <input
        ref={fileRef}
        type="file"
        accept=".md"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) void upload(file);
          e.target.value = '';
        }}
      />
      {error && <span className="text-xs text-rose-600">{error}</span>}
    </div>
  );
}
