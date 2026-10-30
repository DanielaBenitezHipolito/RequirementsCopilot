import { useEffect, useState } from 'react';
import { deleteAnalysis, getAnalyses } from '../../shared/api/client';
import { Loading } from '../../shared/components/Loading';
import type { AnalysisSummary } from '../../shared/types';

function DocIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4 text-slate-400">
      <path strokeLinecap="round" strokeLinejoin="round" d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M14 2v6h6" />
    </svg>
  );
}

function ClockIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4 text-slate-400">
      <circle cx="12" cy="12" r="9" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 7v5l3 3" />
    </svg>
  );
}

function ChevronRightIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4 text-slate-400">
      <path strokeLinecap="round" strokeLinejoin="round" d="M9 6l6 6-6 6" />
    </svg>
  );
}

function FolderIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-3.5 w-3.5">
      <path strokeLinecap="round" strokeLinejoin="round" d="M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" />
    </svg>
  );
}

interface Group {
  proyecto: string | null;
  items: AnalysisSummary[];
}

/** Agrupa por proyecto conservando el orden de recencia (los items llegan del más nuevo al más viejo). */
function groupByProject(items: AnalysisSummary[]): Group[] {
  const groups: Group[] = [];
  for (const item of items) {
    const key = item.proyecto ?? null;
    let group = groups.find((g) => g.proyecto === key);
    if (!group) {
      group = { proyecto: key, items: [] };
      groups.push(group);
    }
    group.items.push(item);
  }
  return groups;
}

export function HistoryView({ onSelect }: { onSelect: (id: string) => void }) {
  const [items, setItems] = useState<AnalysisSummary[]>([]);
  const [error, setError] = useState<string>();
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getAnalyses()
      .then(setItems)
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  async function remove(a: AnalysisSummary) {
    if (!confirm(`¿Eliminar el análisis «${a.fileName}»? Sus casos de uso dejarán de alimentar el contexto del proyecto.`)) {
      return;
    }
    try {
      await deleteAnalysis(a.id);
      setItems((prev) => prev.filter((x) => x.id !== a.id));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo eliminar.');
    }
  }

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Bitácora Histórica de Auditorías</h2>
      <p className="mt-1 text-sm text-slate-500">
        Acceda a los análisis procesados anteriormente por la plataforma.
      </p>

      {loading && <Loading text="Cargando historial de auditorías…" />}
      {!loading && error && <p className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
      {!loading && !error && items.length === 0 && (
        <div className="mt-6 flex flex-col items-center gap-2 py-10 text-center">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-400">
            <ClockIcon />
          </span>
          <p className="text-sm font-semibold text-slate-600">Sin análisis todavía</p>
          <p className="text-xs text-slate-400">Los documentos auditados aparecerán aquí, con su calificación y proyecto.</p>
        </div>
      )}

      {!loading && !error && items.length > 0 && (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-left text-[11px] tracking-wide text-slate-400 uppercase">
                <th className="py-2">Archivo / Documento</th>
                <th>Fecha</th>
                <th>Calificación</th>
                <th>Estatus</th>
                <th>Reqs Aprobados</th>
                <th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {groupByProject(items).map((group) => [
                <tr key={`g-${group.proyecto ?? 'sin-proyecto'}`}>
                  <td colSpan={6} className="pt-4 pb-1.5">
                    <span
                      className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-bold tracking-widest uppercase"
                      style={{ backgroundColor: '#1e2a5a12', color: '#1e2a5a' }}
                    >
                      <FolderIcon />
                      {group.proyecto ?? 'Sin proyecto'}
                      <span className="font-semibold text-slate-400">· {group.items.length}</span>
                    </span>
                  </td>
                </tr>,
                ...group.items.map((a) => {
                const total = a.totalRequerimientos;
                const aprobado = total > 0 && a.aprobados === total;
                const ratio = total > 0 ? a.aprobados / total : 0;
                return (
                  <tr
                    key={a.id}
                    className="cursor-pointer border-b border-slate-100 hover:bg-slate-50"
                    onClick={() => onSelect(a.id)}
                  >
                    <td className="py-3">
                      <span className="flex items-center gap-2.5 font-semibold text-slate-700">
                        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-slate-100">
                          <DocIcon />
                        </span>
                        {a.fileName}
                      </span>
                    </td>
                    <td>
                      <span className="flex items-center gap-1.5 text-slate-600">
                        <ClockIcon />
                        {new Date(a.createdAt).toLocaleString()}
                      </span>
                    </td>
                    <td>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-700">
                          {a.aprobados}/{total} aprobados
                        </span>
                        <span className="h-1.5 w-16 rounded-full bg-slate-100">
                          <span
                            className={`block h-1.5 rounded-full ${aprobado ? 'bg-emerald-500' : 'bg-rose-500'}`}
                            style={{ width: `${ratio * 100}%` }}
                          />
                        </span>
                      </div>
                    </td>
                    <td>
                      <span
                        className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold tracking-wide uppercase ${
                          aprobado ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-600'
                        }`}
                      >
                        {aprobado ? 'Aprobado' : 'No aprobado'}
                      </span>
                    </td>
                    <td className="text-slate-600">
                      {a.aprobados} / {total}
                    </td>
                    <td>
                      <span className="flex items-center gap-1">
                        <button
                          type="button"
                          aria-label={`Eliminar ${a.fileName}`}
                          className="rounded-md p-1.5 text-slate-400 hover:bg-slate-200 hover:text-rose-500"
                          onClick={(e) => {
                            e.stopPropagation();
                            void remove(a);
                          }}
                        >
                          ✕
                        </button>
                        <ChevronRightIcon />
                      </span>
                    </td>
                  </tr>
                );
              }),
              ])}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
