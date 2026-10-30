import { useEffect, useRef, useState } from 'react';
import {
  appendProjectDocument,
  createEmptyProject,
  deleteProject,
  getProject,
  getProjects,
  getProjectUseCases,
  updateProject,
  uploadProject,
} from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';
import { mapRequirement } from '../../shared/components/StoriesBanner';
import { downloadUseCasesDocx, downloadUseCasesPdf } from '../../shared/lib/useCaseDoc';
import type { ProjectSummary } from '../../shared/types';

const BRAND = '#1e2a5a';

function FolderIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
    </svg>
  );
}

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
  const [useCaseCount, setUseCaseCount] = useState(0);
  const fileRef = useRef<HTMLInputElement>(null);
  const docRef = useRef<HTMLInputElement>(null);

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
      setUseCaseCount(detail.casosDeUso ?? 0);
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

  async function createEmpty() {
    const nombre = newName.trim();
    if (!nombre) return;
    setBusy(true);
    try {
      await createEmptyProject(nombre);
      setNewName('');
      await load();
      await open(nombre);
    } catch (e) {
      setError(message(e, 'No se pudo crear el proyecto.'));
    } finally {
      setBusy(false);
    }
  }

  async function appendDocuments(files: File[]) {
    if (!selected) return;
    setBusy(true);
    try {
      // Secuencial a propósito: cada anexo parte del contenido ya guardado; en paralelo se pisarían.
      for (const file of files) {
        await appendProjectDocument(selected, file);
      }
      await open(selected);
      await load();
    } catch (e) {
      setError(message(e, 'No se pudo anexar el documento.'));
      await open(selected);
    } finally {
      setBusy(false);
    }
  }

  async function exportProject(format: 'docx' | 'pdf') {
    if (!selected) return;
    setBusy(true);
    try {
      const casos = (await getProjectUseCases(selected)).map(mapRequirement);
      if (casos.length === 0) {
        setError('El proyecto todavía no tiene casos de uso generados.');
        return;
      }
      const download = format === 'docx' ? downloadUseCasesDocx : downloadUseCasesPdf;
      await download(casos, `casos-de-uso-${selected}`);
    } catch (e) {
      setError(message(e, 'No se pudo exportar.'));
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
    <div className="grid h-full min-h-[28rem] grid-cols-1 gap-4 lg:grid-cols-3">
      <div className="flex min-h-0 flex-col gap-4 overflow-y-auto lg:col-span-1">
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Nuevo proyecto</h2>
          <p className="mt-1 text-xs text-slate-500">
            Suba un archivo .md con la descripción del sistema existente (módulos, roles, reglas), o cree un proyecto
            vacío para un sistema nuevo: su contexto se irá construyendo con los casos de uso que genere.
          </p>
          <input
            className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Nombre (opcional con .md; obligatorio para proyecto vacío)"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
          />
          <BrandButton className="mt-3 w-full" loading={busy} onClick={() => fileRef.current?.click()}>
            Subir contexto .md
          </BrandButton>
          <BrandButton
            variant="secondary"
            className="mt-2 w-full"
            loading={busy}
            disabled={!newName.trim()}
            onClick={() => void createEmpty()}
          >
            Crear proyecto vacío
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
                className={`flex items-center gap-2 rounded-lg border px-3 py-2 transition-colors ${
                  selected === p.nombre ? 'border-slate-300 bg-slate-50' : 'border-slate-100 hover:bg-slate-50'
                }`}
                style={selected === p.nombre ? { borderLeft: `3px solid ${BRAND}` } : undefined}
              >
                <span
                  className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${
                    selected === p.nombre ? 'text-white' : 'bg-slate-100 text-slate-500'
                  }`}
                  style={selected === p.nombre ? { backgroundColor: BRAND } : undefined}
                >
                  <FolderIcon />
                </span>
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
          <div className="flex flex-1 flex-col items-center justify-center gap-2 p-6 text-center">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-400">
              <FolderIcon />
            </span>
            <p className="text-sm font-semibold text-slate-600">Seleccione un proyecto</p>
            <p className="max-w-xs text-xs text-slate-400">
              Vea y edite su contexto, anexe casos de uso existentes o exporte el documento consolidado.
            </p>
          </div>
        )}
        {selected && (
          <>
            <div className="flex shrink-0 flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-3">
              <div>
                <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">{selected}</h2>
                <p className="text-xs text-slate-400">
                  {content.length} caracteres · {useCaseCount} caso{useCaseCount === 1 ? '' : 's'} de uso asociado
                  {useCaseCount === 1 ? '' : 's'}
                </p>
              </div>
              <div className="flex gap-2">
                <input
                  ref={docRef}
                  type="file"
                  accept=".pdf,.docx,.txt,.md"
                  multiple
                  className="hidden"
                  onChange={(e) => {
                    const files = Array.from(e.target.files ?? []);
                    if (files.length) void appendDocuments(files);
                    e.target.value = '';
                  }}
                />
                <BrandButton
                  variant="secondary"
                  className="!px-4 !py-2 !text-xs"
                  loading={busy}
                  onClick={() => docRef.current?.click()}
                >
                  Anexar documento
                </BrandButton>
                {useCaseCount > 0 && (
                  <>
                    <BrandButton variant="secondary" className="!px-4 !py-2 !text-xs" loading={busy} onClick={() => void exportProject('docx')}>
                      Word del proyecto
                    </BrandButton>
                    <BrandButton variant="secondary" className="!px-4 !py-2 !text-xs" loading={busy} onClick={() => void exportProject('pdf')}>
                      PDF del proyecto
                    </BrandButton>
                  </>
                )}
                <BrandButton loading={busy} disabled={!dirty} onClick={() => void save()}>
                  Guardar cambios
                </BrandButton>
              </div>
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
