import { useRef, useState, type CSSProperties } from 'react';
import { analyzeFile } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { BrandButton } from '../../shared/components/BrandButton';
import { StoriesBanner } from '../../shared/components/StoriesBanner';
import { useAnalysisStore } from './store';
import { RequirementCard } from './RequirementCard';

const BRAND = '#1e2a5a';

function UploadIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-6 w-6">
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 16V4m0 0l-4 4m4-4l4 4" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v3a2 2 0 002 2h12a2 2 0 002-2v-3" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3} className="h-3 w-3 text-white">
      <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
    </svg>
  );
}

function SparkleIcon({ className, style }: { className?: string; style?: CSSProperties }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} style={style}>
      <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
    </svg>
  );
}

function StepDot({ state }: { state: 'done' | 'active' | 'pending' }) {
  if (state === 'done') {
    return (
      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-emerald-500">
        <CheckIcon />
      </span>
    );
  }
  if (state === 'active') {
    return (
      <span className="relative flex h-5 w-5 shrink-0 items-center justify-center">
        <span className="absolute h-5 w-5 animate-ping rounded-full opacity-40" style={{ backgroundColor: BRAND }} />
        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: BRAND }} />
      </span>
    );
  }
  return (
    <span className="flex h-5 w-5 shrink-0 items-center justify-center">
      <span className="h-1.5 w-1.5 rounded-full bg-slate-300" />
    </span>
  );
}

function StepLabel({ text, state }: { text: string; state: 'done' | 'active' | 'pending' }) {
  if (state === 'done') return <span className="text-sm text-slate-400 line-through">{text}</span>;
  if (state === 'active') return <span className="text-sm font-semibold" style={{ color: BRAND }}>{text}</span>;
  return <span className="text-sm text-slate-400">{text}</span>;
}

const PREVIEWABLE = /\.(txt|md)$/i;

function isPreviewable(file: File) {
  return PREVIEWABLE.test(file.name);
}

export function AnalyzeView({ onOpenDetail }: { onOpenDetail?: (id: string) => void }) {
  const { status, requirements, analysisId, resumen, error, start, applyEvent, updateRequirement, reset } =
    useAnalysisStore();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [staged, setStaged] = useState<File | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [editedText, setEditedText] = useState<string | null>(null);

  function clearStaged() {
    setStaged(null);
    setPreviewOpen(false);
    setEditedText(null);
  }

  async function togglePreview() {
    if (!staged) return;
    if (previewOpen) {
      setPreviewOpen(false);
      return;
    }
    if (editedText === null) {
      const text = await staged.text();
      setEditedText(text);
    }
    setPreviewOpen(true);
  }

  function startAudit() {
    if (!staged) return;
    const file =
      editedText !== null ? new File([editedText], staged.name, { type: 'text/plain' }) : staged;
    setStaged(null);
    setPreviewOpen(false);
    setEditedText(null);
    void analyze(file);
  }

  async function analyze(file: File) {
    start();
    try {
      const response = await analyzeFile(file);
      for await (const evt of parseSse(response.body!)) applyEvent(evt);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  // Pasos de progreso derivados del contenido del store (sin tocar el store).
  const hasRequirement = requirements.length > 0;
  const hasEvaluation = requirements.some((r) => r.evaluacion);
  const hasClarification = requirements.some((r) => r.aclaraciones.length > 0);
  const hasSummary = !!resumen;
  const isDone = status === 'done';

  const steps: { label: string; state: 'done' | 'active' | 'pending' }[] = [
    {
      label: 'Estableciendo conexión con el servidor Howden…',
      state: hasRequirement || isDone ? 'done' : 'active',
    },
    {
      label: 'Extrayendo y mapeando requerimientos individuales…',
      state: hasRequirement || isDone ? 'done' : 'active',
    },
    {
      label: 'Evaluando claridad, completitud y consistencia técnica…',
      state: hasEvaluation || isDone ? 'done' : hasRequirement ? 'active' : 'pending',
    },
    {
      label: 'Calculando puntajes en base al framework de 5 dimensiones…',
      state: hasEvaluation || isDone ? 'done' : hasRequirement ? 'active' : 'pending',
    },
    {
      label: 'Redactando preguntas de clarificación corporativas…',
      state: hasClarification || isDone ? 'done' : hasEvaluation ? 'active' : 'pending',
    },
    {
      label: 'Compilando reporte de calidad ejecutivo…',
      state: hasSummary || isDone ? 'done' : hasClarification ? 'active' : 'pending',
    },
  ];

  const evaluados = requirements.filter((r) => r.evaluacion);
  const promedioGeneral =
    evaluados.length > 0 ? evaluados.reduce((sum, r) => sum + r.evaluacion!.promedio, 0) / evaluados.length : 0;
  const umbral = evaluados[0]?.evaluacion?.umbral ?? 0;
  const aprobadoGeneral = evaluados.length > 0 && promedioGeneral >= umbral;
  const aprobados = requirements.filter((r) => r.evaluacion?.pasa).length;
  const requierenAclaracion = requirements.filter((r) => r.aclaraciones.some((a) => !a.respuesta)).length;

  const ringColor = aprobadoGeneral ? '#10b981' : '#f43f5e';
  const circumference = 2 * Math.PI * 54;
  const ringOffset = circumference * (1 - promedioGeneral / 5);

  return (
    <div className="space-y-6">
      {status === 'idle' && (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">
            Cargar Especificación de Requerimientos
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Cargue un documento de requerimientos para auditarlo técnicamente.
          </p>
          {!staged && (
          <div
            className={`mt-4 flex cursor-pointer flex-col items-center rounded-xl border-2 border-dashed px-8 py-12 text-center transition-colors ${
              dragging ? 'border-slate-400 bg-slate-50' : 'border-slate-300'
            }`}
            onClick={() => inputRef.current?.click()}
            onDragOver={(e) => {
              e.preventDefault();
              setDragging(true);
            }}
            onDragLeave={() => setDragging(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragging(false);
              const file = e.dataTransfer.files[0];
              if (file) setStaged(file);
            }}
          >
            <span className="flex h-14 w-14 items-center justify-center rounded-full bg-slate-100 text-slate-500">
              <UploadIcon />
            </span>
            <p className="mt-4 font-semibold text-slate-700">Arrastre su documento de requerimientos aquí</p>
            <p className="mt-1 text-xs text-slate-500">Soporta archivos .pdf, .docx, .txt y .md · máximo 10 MB</p>
            <button
              type="button"
              className="mt-5 rounded-lg px-5 py-2.5 text-sm font-semibold text-white hover:opacity-90"
              style={{ backgroundColor: BRAND }}
              onClick={(e) => {
                e.stopPropagation();
                inputRef.current?.click();
              }}
            >
              Explorar Archivos
            </button>
            <input
              ref={inputRef}
              type="file"
              accept=".pdf,.docx,.txt,.md"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) setStaged(file);
                e.target.value = '';
              }}
            />
          </div>
          )}

          {staged && (
            <>
              <div className="mt-4 flex items-center gap-4 rounded-xl border border-slate-200 bg-slate-50 p-4">
                <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-blue-50 text-slate-500">
                  <UploadIcon />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-bold tracking-wide text-slate-800 uppercase">{staged.name}</p>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Contenido cargado con éxito · {(staged.size / 1024).toFixed(1)} KB · Listo para auditar
                  </p>
                </div>
                <BrandButton
                  variant="secondary"
                  sparkle
                  className="!px-4 !py-2 !text-xs"
                  disabled={!isPreviewable(staged)}
                  title={
                    isPreviewable(staged)
                      ? undefined
                      : 'Vista previa disponible solo para archivos de texto'
                  }
                  onClick={() => void togglePreview()}
                >
                  {previewOpen ? 'Ocultar Vista Previa' : 'Ver / Editar Contenido'}
                </BrandButton>
                <button
                  type="button"
                  onClick={clearStaged}
                  className="rounded-md p-2 text-slate-400 hover:bg-slate-200 hover:text-rose-500"
                  aria-label="Quitar archivo"
                >
                  ✕
                </button>
              </div>

              {previewOpen && editedText !== null && (
                <div className="mt-3 rounded-xl border border-slate-200 p-4">
                  <div className="flex items-center justify-between">
                    <p className="text-[11px] font-bold tracking-wide text-slate-500 uppercase">
                      Edición Opcional del Documento:
                    </p>
                    <p className="text-xs text-slate-400">{editedText.length} caracteres</p>
                  </div>
                  <textarea
                    className="mt-2 w-full rounded-lg border border-slate-300 p-3 font-mono text-sm text-slate-700 focus:border-slate-400 focus:outline-none"
                    rows={12}
                    value={editedText}
                    onChange={(e) => setEditedText(e.target.value)}
                  />
                </div>
              )}

              <div className="mt-4 flex justify-end">
                <BrandButton sparkle onClick={startAudit}>
                  Iniciar Auditoría de Calidad
                </BrandButton>
              </div>
            </>
          )}
        </div>
      )}

      {status === 'error' && <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

      {status === 'running' && (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <div className="flex flex-col items-center text-center">
            <span className="relative flex h-14 w-14 items-center justify-center">
              <span className="absolute h-14 w-14 animate-spin rounded-full border-4 border-dashed border-slate-300" />
              <SparkleIcon className="h-6 w-6" style={{ color: BRAND }} />
            </span>
            <h2 className="mt-4 text-lg font-bold text-slate-800">Análisis de Requerimientos en Progreso</h2>
            <p className="mt-1 text-sm text-slate-500">
              Nuestros agentes de auditoría están procesando la información.
            </p>
          </div>

          <div className="mt-6 rounded-xl bg-slate-50 p-4">
            <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-wide text-slate-400 uppercase">
              <span aria-hidden>ⓘ</span> Registro de Tareas Activas
            </p>
            <div className="mt-3 space-y-3">
              {steps.map((s) => (
                <div key={s.label} className="flex items-center gap-3">
                  <StepDot state={s.state} />
                  <StepLabel text={s.label} state={s.state} />
                </div>
              ))}
            </div>
          </div>

          {requirements.length > 0 && (
            <div className="mt-6 space-y-3">
              {requirements.map((r) => (
                <RequirementCard key={r.codigo} requirement={r} />
              ))}
            </div>
          )}
        </div>
      )}

      {status === 'done' && (
        <>
          <div className="flex justify-end">
            <BrandButton onClick={reset}>Analizar otro documento</BrandButton>
          </div>
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
            <div className="flex flex-col items-center rounded-2xl border border-slate-200 bg-white p-6 text-center shadow-sm">
              <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">
                Puntaje de Calidad General
              </p>
              <svg viewBox="0 0 120 120" className="mt-4 h-32 w-32">
                <circle cx="60" cy="60" r="54" fill="none" stroke="#e2e8f0" strokeWidth="10" />
                <circle
                  cx="60"
                  cy="60"
                  r="54"
                  fill="none"
                  stroke={ringColor}
                  strokeWidth="10"
                  strokeLinecap="round"
                  strokeDasharray={circumference}
                  strokeDashoffset={ringOffset}
                  transform="rotate(-90 60 60)"
                />
                <text x="60" y="56" textAnchor="middle" fontSize="22" fontWeight="bold" fill="#1e293b">
                  {promedioGeneral.toFixed(2)}
                </text>
                <text x="60" y="74" textAnchor="middle" fontSize="10" fill="#64748b">
                  de 5.00 puntos
                </text>
              </svg>
              <p className="mt-1 text-xs text-slate-400">Umbral de aprobación: {umbral.toFixed(2)}</p>
              <span
                className={`mt-3 rounded-full px-4 py-1 text-sm font-bold tracking-wide uppercase ${
                  aprobadoGeneral ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-600'
                }`}
              >
                {aprobadoGeneral ? 'Aprobado' : 'No Aprobado'}
              </span>
            </div>

            <div className="flex flex-col justify-center gap-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm lg:col-span-2">
              {resumen && (
                <div>
                  <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">
                    ✦ Resumen Ejecutivo de Auditoría
                  </p>
                  <p className="mt-1.5 text-sm text-slate-700">{resumen}</p>
                </div>
              )}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <div className="text-center">
                  <p className="text-3xl font-black" style={{ color: BRAND }}>
                    {requirements.length}
                  </p>
                  <p className="mt-1 text-xs text-slate-500">Requerimientos detectados</p>
                </div>
                <div className="text-center">
                  <p className="text-3xl font-black text-emerald-600">{aprobados}</p>
                  <p className="mt-1 text-xs text-slate-500">Aprobados</p>
                </div>
                <div className="text-center">
                  <p className="text-3xl font-black text-rose-600">{requierenAclaracion}</p>
                  <p className="mt-1 text-xs text-slate-500">Requieren aclaración</p>
                </div>
              </div>
              {analysisId && onOpenDetail && (
                <button
                  className="self-center rounded-lg px-5 py-2.5 text-sm font-semibold text-white hover:opacity-90"
                  style={{ backgroundColor: BRAND }}
                  onClick={() => onOpenDetail(analysisId)}
                >
                  Responder preguntas y generar historias →
                </button>
              )}
            </div>
          </div>

          {analysisId && (
            <StoriesBanner analysisId={analysisId} requirements={requirements} onUpdate={updateRequirement} />
          )}

          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Requerimientos Auditados</h2>
          <div className="space-y-3">
            {requirements.map((r) => (
              <RequirementCard key={r.codigo} requirement={r} />
            ))}
          </div>
        </>
      )}
    </div>
  );
}
