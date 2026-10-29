import { useState } from 'react';
import type { CasoDeUso, RequirementView } from '../../shared/types';
import { BrandButton } from '../../shared/components/BrandButton';
import { UseCaseEditor } from './UseCaseEditor';

interface Props {
  requirement: RequirementView;
  onReevaluate?: (respuestas: string[]) => Promise<void>;
  onGenerateUserStories?: () => Promise<void>;
  onRegenerateUseCase?: () => Promise<void>;
  onEditUseCase?: (caso: CasoDeUso) => Promise<void>;
  onSetStage?: (etapa: string) => Promise<void>;
}

const STAGES: { value: string; label: string; classes: string }[] = [
  { value: 'Draft', label: 'Borrador', classes: 'bg-slate-100 text-slate-600' },
  { value: 'Approved', label: 'Aprobado', classes: 'bg-emerald-50 text-emerald-700' },
  { value: 'InDevelopment', label: 'En desarrollo', classes: 'bg-blue-50 text-blue-700' },
  { value: 'Implemented', label: 'Implementado', classes: 'bg-violet-50 text-violet-700' },
];

const BRAND = '#1e2a5a';

function barColor(score: number) {
  if (score >= 4) return 'bg-emerald-500';
  if (score === 3) return 'bg-amber-400';
  return 'bg-rose-500';
}

function ChevronIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      className={`h-4 w-4 shrink-0 transition-transform ${open ? 'rotate-180' : ''}`}
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 9l6 6 6-6" />
    </svg>
  );
}

export function RequirementCard({ requirement, onReevaluate, onGenerateUserStories, onRegenerateUseCase, onEditUseCase, onSetStage }: Props) {
  const evaluacion = requirement.evaluacion;
  // El SSE arma requerimientos parciales: nunca asumir que historias viene definida.
  const historias = requirement.historias ?? [];
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [open, setOpen] = useState(true);
  const [editing, setEditing] = useState(false);

  const pending = requirement.aclaraciones.filter((a) => !a.respuesta);
  const answered = requirement.aclaraciones.filter((a) => a.respuesta);
  const stage = STAGES.find((s) => s.value === requirement.etapa);
  // Acento lateral: el veredicto de la evaluación de un vistazo, antes de abrir la tarjeta.
  const accent = !evaluacion ? '#cbd5e1' : evaluacion.pasa ? '#10b981' : '#f43f5e';

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    try {
      await action();
      // Tras re-evaluar, las preguntas pendientes son otras: los inputs arrancan vacíos.
      setDrafts({});
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error inesperado');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm"
      style={{ borderLeft: `4px solid ${accent}` }}
    >
      {/* div clickeable (no <button>): el header contiene el <select> de etapa y un botón no puede anidar interactivos. */}
      <div
        role="button"
        tabIndex={0}
        className="flex w-full cursor-pointer items-center gap-2 px-4 py-3 text-left"
        onClick={() => setOpen((o) => !o)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') setOpen((o) => !o);
        }}
      >
        <span
          className="rounded-full bg-blue-50 px-2.5 py-0.5 text-[11px] font-bold tracking-wide"
          style={{ color: BRAND }}
        >
          {requirement.codigo}
        </span>
        <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-bold tracking-wide text-slate-600 uppercase">
          {requirement.area}
        </span>
        {/* Con etapa visible, el chip de evaluación es redundante (el acento lateral ya da el veredicto). */}
        {evaluacion && !stage && (
          <span
            className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold tracking-wide uppercase ${
              evaluacion.pasa ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-600'
            }`}
          >
            {evaluacion.pasa ? 'Aprobado' : 'No aprobado'}
          </span>
        )}
        {stage &&
          (onSetStage ? (
            <select
              className={`rounded-full border-0 px-2.5 py-0.5 text-[11px] font-bold tracking-wide uppercase ${stage.classes}`}
              value={stage.value}
              disabled={busy}
              onClick={(e) => e.stopPropagation()}
              onChange={(e) => void run(() => onSetStage(e.target.value))}
            >
              {STAGES.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          ) : (
            <span className={`rounded-full px-2.5 py-0.5 text-[11px] font-bold tracking-wide uppercase ${stage.classes}`}>
              {stage.label}
            </span>
          ))}
        {evaluacion && (
          <span className="ml-auto flex items-baseline gap-1.5">
            <span className="text-lg leading-none font-black tracking-tight text-slate-800">
              {evaluacion.promedio.toFixed(2)}
            </span>
            <span className="text-[11px] text-slate-400">/ 5 · umbral {evaluacion.umbral.toFixed(2)}</span>
          </span>
        )}
        <ChevronIcon open={open} />
      </div>

      {open && (
        <div className="space-y-4 border-t border-slate-100 px-4 pb-4 pt-3">
          {requirement.solapamiento && (
            <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              ⚠️ {requirement.solapamiento}
            </p>
          )}

          <div>
            <p className="mb-1 text-[11px] font-bold tracking-wide text-slate-400 uppercase">
              Enunciado / Descripción
            </p>
            <div className="rounded-lg bg-slate-50 p-3 text-sm text-slate-700">{requirement.texto}</div>
          </div>

          {evaluacion && (
            <div>
              <p className="mb-2 text-[11px] font-bold tracking-wide text-slate-400 uppercase">
                Análisis por Dimensión del Framework
              </p>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {evaluacion.criterios.map((c) => (
                  <div key={c.nombre} className="rounded-lg border border-slate-100 p-3">
                    <div className="flex items-center justify-between text-xs font-semibold text-slate-700">
                      <span>{c.nombre}</span>
                      <span className="font-mono">{c.score}/5</span>
                    </div>
                    <div className="mt-1.5 h-1.5 w-full rounded-full bg-slate-100">
                      <div
                        className={`h-1.5 rounded-full ${barColor(c.score)}`}
                        style={{ width: `${(c.score / 5) * 100}%` }}
                      />
                    </div>
                    <p className="mt-1.5 text-xs text-slate-500">{c.observacion}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {requirement.aclaraciones.length > 0 && (
            <div className="rounded-lg border border-slate-200 p-3">
              <p className="text-[11px] font-bold tracking-wide text-slate-500 uppercase">
                Preguntas de Clarificación Corporativas
              </p>

              {answered.length > 0 && (
                <div className="mt-2 space-y-2 border-b border-slate-100 pb-3">
                  {answered.map((a, i) => (
                    <div key={i} className="text-xs text-slate-500">
                      <p className="font-semibold text-slate-600">P: {a.pregunta}</p>
                      <p className="mt-0.5">R: {a.respuesta}</p>
                    </div>
                  ))}
                </div>
              )}

              <div className="mt-3 space-y-3">
                {pending.map((a, i) => (
                  <div key={i} className="rounded-xl border border-slate-200 p-4">
                    <p className="text-sm font-bold text-slate-800">
                      <span style={{ color: BRAND }}>Q{i + 1}:</span> {a.pregunta}
                    </p>
                    {onReevaluate ? (
                      <input
                        className="mt-2 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                        placeholder="Escriba su respuesta aclarando este requerimiento…"
                        value={drafts[i] ?? ''}
                        onChange={(e) => setDrafts({ ...drafts, [i]: e.target.value })}
                        disabled={busy}
                      />
                    ) : (
                      <p className="mt-1 text-xs italic text-slate-500">Pendiente de respuesta (ver Detalle)</p>
                    )}
                  </div>
                ))}
              </div>
              {onReevaluate && pending.length > 0 && (
                <div className="mt-3 flex justify-end">
                  <BrandButton
                    sparkle
                    className="!px-4 !py-2 !text-xs"
                    loading={busy}
                    loadingText="Re-evaluando con IA…"
                    disabled={pending.some((_, i) => !(drafts[i] ?? '').trim())}
                    onClick={() => run(() => onReevaluate(pending.map((_, i) => (drafts[i] ?? '').trim())))}
                  >
                    ↻ Responder y Re-evaluar Requerimiento
                  </BrandButton>
                </div>
              )}
            </div>
          )}

          {error && <p className="rounded-lg bg-rose-50 p-2 text-xs text-rose-700">{error}</p>}

          {requirement.caso && editing && onEditUseCase && (
            <UseCaseEditor
              caso={requirement.caso}
              onCancel={() => setEditing(false)}
              onSave={async (caso) => {
                await onEditUseCase(caso);
                setEditing(false);
              }}
            />
          )}

          {requirement.caso && !editing && (
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-widest uppercase" style={{ color: BRAND }}>
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-3.5 w-3.5">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                      <path strokeLinecap="round" strokeLinejoin="round" d="M14 2v6h6" />
                    </svg>
                    Caso de uso
                  </p>
                  <p className="mt-1 text-base font-black tracking-tight text-slate-900">{requirement.caso.nombre}</p>
                </div>
                <div className="flex gap-2">
                  {onEditUseCase && (
                    <BrandButton variant="secondary" className="!px-3 !py-1.5 !text-xs" disabled={busy} onClick={() => setEditing(true)}>
                      ✏️ Editar
                    </BrandButton>
                  )}
                  {onRegenerateUseCase && (
                    <BrandButton
                      variant="secondary"
                      className="!px-3 !py-1.5 !text-xs"
                      loading={busy}
                      loadingText="Regenerando…"
                      onClick={() => {
                        if (confirm('¿Regenerar el caso de uso? El actual se reemplaza por uno nuevo de la IA.')) {
                          void run(onRegenerateUseCase);
                        }
                      }}
                    >
                      ↻ Regenerar
                    </BrandButton>
                  )}
                </div>
              </div>

              <div className="mt-3 space-y-3">
                <div>
                  <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Objetivo</p>
                  <p className="mt-1 text-sm text-slate-700">{requirement.caso.objetivo}</p>
                </div>

                <div>
                  <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Descripción</p>
                  <p className="mt-1 text-sm text-slate-700">{requirement.caso.descripcion}</p>
                </div>

                {requirement.caso.actores.length > 0 && (
                  <div>
                    <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Actores</p>
                    <ul className="mt-1 list-disc pl-5 text-sm text-slate-700">
                      {requirement.caso.actores.map((a, i) => (
                        <li key={i}>
                          <span className="font-semibold">{a.nombre}</span> — {a.descripcion}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {requirement.caso.precondiciones.length > 0 && (
                  <div>
                    <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Precondiciones</p>
                    <ul className="mt-1 list-disc pl-5 text-sm text-slate-700">
                      {requirement.caso.precondiciones.map((p, i) => (
                        <li key={i}>{p}</li>
                      ))}
                    </ul>
                  </div>
                )}

                <div>
                  <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Trigger</p>
                  <p className="mt-1 text-sm text-slate-700">{requirement.caso.trigger}</p>
                </div>

                {requirement.caso.flujos.map((f, i) => (
                  <div key={i} className="rounded-lg border border-slate-200 bg-white p-3">
                    <p className="text-xs font-bold tracking-wide text-slate-600 uppercase">{f.titulo}</p>
                    <table className="mt-2 w-full text-xs text-slate-700">
                      <thead>
                        <tr className="border-b border-slate-200 text-left text-[11px] uppercase text-slate-400">
                          <th className="py-1 pr-2">Paso</th>
                          <th className="py-1 pr-2">Acción</th>
                          <th className="py-1">Resultado esperado</th>
                        </tr>
                      </thead>
                      <tbody>
                        {f.pasos.map((p) => (
                          <tr key={p.numero} className="border-b border-slate-100 last:border-0">
                            <td className="py-1 pr-2 font-mono">{p.numero}</td>
                            <td className="py-1 pr-2">{p.accion}</td>
                            <td className="py-1">{p.resultadoEsperado}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ))}

                {requirement.caso.extensiones.length > 0 && (
                  <div>
                    <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Extensiones</p>
                    <ul className="mt-1 list-disc pl-5 text-sm text-slate-700">
                      {requirement.caso.extensiones.map((e, i) => (
                        <li key={i}>{e}</li>
                      ))}
                    </ul>
                  </div>
                )}

                <div className="flex flex-wrap gap-2">
                  <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-bold uppercase text-slate-600">
                    Frecuencia: {requirement.caso.frecuencia}
                  </span>
                  <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-bold uppercase text-slate-600">
                    Importancia: {requirement.caso.importancia}
                  </span>
                  <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[11px] font-bold uppercase text-slate-600">
                    Urgencia: {requirement.caso.urgencia}
                  </span>
                </div>

                {requirement.caso.comentarios.length > 0 && (
                  <div>
                    <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Comentarios</p>
                    <ul className="mt-1 list-disc pl-5 text-sm text-slate-700">
                      {requirement.caso.comentarios.map((c, i) => (
                        <li key={i}>{c}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            </div>
          )}

          {historias.length > 0 && (
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-bold text-slate-800">HISTORIAS DE USUARIO</p>
                <span className="rounded-full bg-slate-200 px-2.5 py-0.5 text-[11px] font-bold text-slate-600">
                  {historias.reduce((sum, h) => sum + h.puntos, 0)} pts totales
                </span>
              </div>
              <p className="mt-1 text-[11px] italic text-slate-400">
                Estimación orientativa generada por IA (complejidad relativa); no es un compromiso de entrega.
              </p>
              <div className="mt-3 space-y-3">
                {historias.map((h, i) => (
                  <div key={i} className="rounded-lg border border-slate-200 bg-white p-3">
                    <div className="flex items-center justify-between gap-2">
                      <p className="text-sm font-bold text-slate-800">{h.titulo}</p>
                      <span className="shrink-0 rounded-full bg-blue-50 px-2.5 py-0.5 font-mono text-[11px] font-bold" style={{ color: BRAND }}>
                        {h.puntos} pts
                      </span>
                    </div>
                    <p className="mt-1.5 text-sm text-slate-700">
                      <span className="font-semibold">Como</span> {h.como}, <span className="font-semibold">quiero</span>{' '}
                      {h.quiero}, <span className="font-semibold">para</span> {h.para}.
                    </p>
                    {h.criteriosAceptacion.length > 0 && (
                      <div className="mt-2">
                        <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Criterios de aceptación</p>
                        <ul className="mt-1 list-disc pl-5 text-xs text-slate-600">
                          {h.criteriosAceptacion.map((c, j) => (
                            <li key={j}>{c}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {h.dependencias.length > 0 && (
                      <p className="mt-2 text-xs text-slate-400">Depende de: {h.dependencias.join(', ')}</p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {onGenerateUserStories && requirement.listoParaHistorias && historias.length === 0 && (
            <div className="flex justify-end">
              <BrandButton
                sparkle
                variant="secondary"
                className="!px-4 !py-2 !text-xs"
                loading={busy}
                loadingText="Generando historias…"
                onClick={() => run(onGenerateUserStories)}
              >
                📋 Generar Historias de Usuario (backlog)
              </BrandButton>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
