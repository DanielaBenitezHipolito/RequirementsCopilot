import { useEffect, useRef, useState } from 'react';
import {
  appendProjectDocument,
  createEmptyProject,
  deleteProject,
  deleteProjectExample,
  getProject,
  getProjects,
  getProjectUseCases,
  updateProject,
  uploadProject,
} from '../../shared/api/client';
import { BrandButton } from '../../shared/components/BrandButton';
import { mapRequirement } from '../../shared/components/StoriesBanner';
import { downloadUseCasesDocx, downloadUseCasesPdf } from '../../shared/lib/useCaseDoc';
import type { ProjectExample, ProjectSummary } from '../../shared/types';

const BRAND = '#1e2a5a';

function FolderIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
    </svg>
  );
}

/** Trozos con **negrita** y `código` sin HTML crudo (seguro por construcción). */
function inline(text: string) {
  return text.split(/(\*\*[^*]+\*\*|`[^`]+`)/g).map((part, i) => {
    if (part.startsWith('**') && part.endsWith('**')) {
      return (
        <strong key={i} className="font-bold text-slate-800">
          {part.slice(2, -2)}
        </strong>
      );
    }
    if (part.startsWith('`') && part.endsWith('`')) {
      return (
        <code key={i} className="rounded bg-slate-100 px-1 py-0.5 font-mono text-[0.85em] text-slate-700">
          {part.slice(1, -1)}
        </code>
      );
    }
    return part;
  });
}

/** Vista previa Markdown mínima (títulos, listas, negrita, código) sin dependencias. */
function MarkdownPreview({ text }: { text: string }) {
  const lines = text.split('\n');
  const blocks: React.ReactNode[] = [];
  let bullets: string[] = [];

  const flushBullets = (key: number) => {
    if (bullets.length === 0) return;
    blocks.push(
      <ul key={`ul-${key}`} className="list-disc space-y-1 pl-5 text-sm text-slate-700">
        {bullets.map((b, i) => (
          <li key={i}>{inline(b)}</li>
        ))}
      </ul>,
    );
    bullets = [];
  };

  lines.forEach((raw, i) => {
    const line = raw.trimEnd();
    if (/^\s*[-*]\s+/.test(line)) {
      bullets.push(line.replace(/^\s*[-*]\s+/, ''));
      return;
    }
    flushBullets(i);
    if (line.startsWith('### ')) {
      blocks.push(<h4 key={i} className="pt-2 text-sm font-bold text-slate-800">{inline(line.slice(4))}</h4>);
    } else if (line.startsWith('## ')) {
      blocks.push(
        <h3 key={i} className="border-b border-slate-100 pt-3 pb-1 text-base font-black tracking-tight text-slate-900">
          {inline(line.slice(3))}
        </h3>,
      );
    } else if (line.startsWith('# ')) {
      blocks.push(
        <h2 key={i} className="pt-2 text-lg font-black tracking-tight" style={{ color: BRAND }}>
          {inline(line.slice(2))}
        </h2>,
      );
    } else if (line.trim() !== '') {
      blocks.push(<p key={i} className="text-sm leading-relaxed text-slate-700">{inline(line)}</p>);
    }
  });
  flushBullets(lines.length);

  return <div className="space-y-2">{blocks}</div>;
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
  const [examples, setExamples] = useState<ProjectExample[]>([]);
  const [asExample, setAsExample] = useState(false);
  const [editing, setEditing] = useState(false);
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
      setExamples(detail.ejemplos ?? []);
      // Sin contenido no hay nada que previsualizar: se abre directo en edición.
      setEditing(detail.contenido.trim() === '');
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
        await appendProjectDocument(selected, file, asExample);
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

  async function removeExample(documento: string) {
    if (!selected) return;
    if (!confirm(`¿Quitar el documento de ejemplo «${documento}»?`)) return;
    setBusy(true);
    try {
      await deleteProjectExample(selected, documento);
      await open(selected);
    } catch (e) {
      setError(message(e, 'No se pudo quitar el ejemplo.'));
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
                <label className="flex cursor-pointer items-center gap-1.5 text-xs text-slate-500 select-none">
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-[#1e2a5a]"
                    checked={asExample}
                    onChange={(e) => setAsExample(e.target.checked)}
                  />
                  Documento de ejemplo (solo lectura)
                </label>
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
            {examples.length > 0 && (
              <div className="shrink-0 border-b border-slate-100 px-4 py-3">
                <p className="text-[11px] font-bold tracking-widest text-slate-400 uppercase">
                  Documentos de ejemplo (solo lectura)
                </p>
                <div className="mt-2 flex flex-wrap gap-2">
                  {examples.map((ejemplo) => (
                    <span
                      key={ejemplo.nombre}
                      className="flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-3 py-1 text-xs text-slate-600"
                    >
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-3.5 w-3.5 text-slate-400">
                        <path strokeLinecap="round" strokeLinejoin="round" d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                        <path strokeLinecap="round" strokeLinejoin="round" d="M14 2v6h6" />
                      </svg>
                      <span className="font-semibold">{ejemplo.nombre}</span>
                      <span className="text-slate-400">· {ejemplo.caracteres.toLocaleString('es-CO')} caracteres</span>
                      <button
                        type="button"
                        aria-label={`Quitar ejemplo ${ejemplo.nombre}`}
                        className="ml-0.5 text-slate-400 hover:text-rose-500"
                        onClick={() => void removeExample(ejemplo.nombre)}
                      >
                        ✕
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}
            <div className="flex shrink-0 gap-1 border-b border-slate-100 px-4 py-2">
              {([['preview', 'Vista previa'], ['edit', 'Editar']] as const).map(([mode, label]) => {
                const active = (mode === 'edit') === editing;
                return (
                  <button
                    key={mode}
                    type="button"
                    className={`rounded-full px-3 py-1 text-[11px] font-bold tracking-wide uppercase transition-colors ${
                      active ? 'text-white' : 'text-slate-500 hover:bg-slate-100'
                    }`}
                    style={active ? { backgroundColor: BRAND } : undefined}
                    onClick={() => setEditing(mode === 'edit')}
                  >
                    {label}
                  </button>
                );
              })}
            </div>
            {editing ? (
              <textarea
                className="min-h-[24rem] flex-1 resize-none p-4 font-mono text-sm text-slate-700 focus:outline-none"
                value={content}
                onChange={(e) => setContent(e.target.value)}
              />
            ) : (
              <div className="min-h-[24rem] flex-1 overflow-y-auto p-5">
                {content.trim() === '' ? (
                  <p className="text-sm text-slate-400">
                    Sin contenido todavía: edite el contexto o anexe documentos.
                  </p>
                ) : (
                  <MarkdownPreview text={content} />
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
