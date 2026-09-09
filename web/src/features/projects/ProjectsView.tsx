import { useEffect, useRef, useState } from 'react';
import { deleteProject, getProject, getProjects, updateProject, uploadProject } from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';
import type { ProjectSummary } from '../../shared/types';

function message(e: unknown, fallback: string) {
  return e instanceof Error ? e.message : fallback;
}

/** Pantalla administrativa: proyectos existentes y su contexto Markdown (subir, ver/editar, eliminar). */
export function ProjectsView() {
  const [projects, setProjects] = useState<ProjectSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [selected, setSelected] = useState<string>();
  const [content, setContent] = useState('');
  const [savedContent, setSavedContent] = useState('');
  const [busy, setBusy] = useState(false);
  const [newName, setNewName] = useState('');
  const fileRef = useRef<HTMLInputElement>(null);

  async function load() {
    setLoading(true);
    try {
      setProjects(await getProjects());
      setError(undefined);
    } catch (e) {
      setError(message(e, 'No se pudieron cargar los proyectos.'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function open(nombre: string) {
    setBusy(true);
    try {
      const detail = await getProject(nombre);
      setSelected(detail.nombre);
      setContent(detail.contenido);
      setSavedContent(detail.contenido);
      setError(undefined);
    } catch (e) {
      setError(message(e, 'No se pudo abrir el proyecto.'));
    } finally {
      setBusy(false);
    }
  }

  async function upload(file: File) {
    setBusy(true);
    try {
      const { nombre } = await uploadProject(file, newName.trim() || undefined);
      setNewName('');
      await load();
      await open(nombre);
    } catch (e) {
      setError(message(e, 'No se pudo subir el proyecto.'));
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    if (!selected) return;
    setBusy(true);
    try {
      await updateProject(selected, content);
      setSavedContent(content);
      await load();
    } catch (e) {
      setError(message(e, 'No se pudo guardar.'));
    } finally {
      setBusy(false);
    }
  }

  async function remove(nombre: string) {
    if (!confirm(`¿Eliminar el proyecto «${nombre}»? Los análisis existentes dejarán de usar su contexto.`)) return;
    setBusy(true);
    try {
      await deleteProject(nombre);
      if (selected === nombre) setSelected(undefined);
      await load();
    } catch (e) {
      setError(message(e, 'No se pudo eliminar.'));
    } finally {
      setBusy(false);
    }
  }

  const dirty = content !== savedContent;

  return (
    <div className="grid min-h-[28rem] grid-cols-1 gap-4 lg:h-[calc(100vh-16rem)] lg:grid-cols-3">
      <div className="flex min-h-0 flex-col gap-4 overflow-y-auto lg:col-span-1">
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Nuevo proyecto</h2>
          <p className="mt-1 text-xs text-slate-500">
            Suba un archivo .md con la descripción del sistema existente (módulos, roles, reglas). Si ya existe uno con
            ese nombre, se reemplaza.
          </p>
          <input
            className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Nombre (opcional; por defecto el del archivo)"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
          />
          <BrandButton className="mt-3 w-full" loading={busy} onClick={() => fileRef.current?.click()}>
            Subir contexto .md
          </BrandButton>
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
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Proyectos</h2>
          <div className="mt-3 space-y-2">
            {loading && <p className="text-sm text-slate-400">Cargando…</p>}
            {!loading && projects.length === 0 && <p className="text-sm text-slate-400">Todavía no hay proyectos.</p>}
            {projects.map((p) => (
              <div
                key={p.nombre}
                className={`flex items-center gap-2 rounded-lg border px-3 py-2 ${
                  selected === p.nombre ? 'border-slate-400 bg-slate-50' : 'border-slate-100 hover:bg-slate-50'
                }`}
              >
                <button className="min-w-0 flex-1 text-left" onClick={() => void open(p.nombre)}>
                  <p className="truncate text-sm font-semibold text-slate-700">{p.nombre}</p>
                  <p className="text-xs text-slate-400">{new Date(p.updatedAt).toLocaleString()}</p>
                </button>
                <button
                  type="button"
                  onClick={() => void remove(p.nombre)}
                  className="rounded-md p-2 text-slate-400 hover:bg-slate-200 hover:text-rose-500"
                  aria-label={`Eliminar ${p.nombre}`}
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="flex h-full min-h-0 flex-col rounded-2xl border border-slate-200 bg-white shadow-sm lg:col-span-2">
        {error && <p className="m-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
        {!selected && (
          <p className="p-6 text-sm text-slate-400">Seleccione un proyecto para ver o editar su contexto.</p>
        )}
        {selected && (
          <>
            <div className="flex shrink-0 items-center justify-between border-b border-slate-100 px-4 py-3">
              <div>
                <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">{selected}</h2>
                <p className="text-xs text-slate-400">{content.length} caracteres</p>
              </div>
              <BrandButton loading={busy} disabled={!dirty} onClick={() => void save()}>
                Guardar cambios
              </BrandButton>
            </div>
            <textarea
              className="min-h-[24rem] flex-1 resize-none p-4 font-mono text-sm text-slate-700 focus:outline-none"
              value={content}
              onChange={(e) => setContent(e.target.value)}
            />
          </>
        )}
      </div>
    </div>
  );
}
