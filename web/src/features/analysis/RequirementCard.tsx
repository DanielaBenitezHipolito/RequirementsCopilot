import { useState } from 'react';
import type { RequirementView } from '../../shared/types';

interface Props {
  requirement: RequirementView;
  onAnswer?: (respuestas: string[]) => Promise<void>;
  onGenerate?: () => Promise<void>;
}

export function RequirementCard({ requirement, onAnswer, onGenerate }: Props) {
  const evaluacion = requirement.evaluacion;
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  const pending = requirement.aclaraciones.filter((a) => !a.respuesta);
  const canGenerate =
    !!onGenerate &&
    requirement.historias.length === 0 &&
    !!evaluacion &&
    (requirement.listoParaHistorias ?? (evaluacion.pasa || (requirement.aclaraciones.length > 0 && pending.length === 0)));

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(undefined);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error inesperado');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center gap-2">
        <span className="font-mono text-sm font-bold text-slate-700">{requirement.codigo}</span>
        <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-xs text-indigo-700">{requirement.area}</span>
        {evaluacion && (
          <span
            className={`ml-auto rounded-full px-2 py-0.5 text-xs font-semibold ${
              evaluacion.pasa ? 'bg-emerald-100 text-emerald-700' : 'bg-rose-100 text-rose-700'
            }`}
          >
            {evaluacion.pasa ? 'Aprobado' : 'No aprobado'} · {evaluacion.promedio.toFixed(2)} / {evaluacion.umbral}
          </span>
        )}
      </div>
      <p className="mt-2 text-sm text-slate-700">{requirement.texto}</p>

      {evaluacion && (
        <table className="mt-3 w-full text-xs">
          <tbody>
            {evaluacion.criterios.map((c) => (
              <tr key={c.nombre} className="border-t border-slate-100">
                <td className="py-1 font-medium text-slate-600">{c.nombre}</td>
                <td className="py-1 text-center font-mono">{c.score}/5</td>
                <td className="py-1 text-slate-500">{c.observacion}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {requirement.aclaraciones.length > 0 && (
        <div className="mt-3 rounded-lg bg-amber-50 p-3">
          <p className="text-xs font-semibold text-amber-800">Preguntas de clarificación</p>
          <div className="mt-2 space-y-2">
            {requirement.aclaraciones.map((a, i) => (
              <div key={i}>
                <p className="text-xs text-slate-700">{a.pregunta}</p>
                {a.respuesta ? (
                  <p className="mt-0.5 text-xs text-emerald-700">R: {a.respuesta}</p>
                ) : onAnswer ? (
                  <input
                    className="mt-1 w-full rounded border border-amber-200 bg-white px-2 py-1 text-xs"
                    placeholder="Tu respuesta…"
                    value={drafts[i] ?? ''}
                    onChange={(e) => setDrafts({ ...drafts, [i]: e.target.value })}
                    disabled={busy}
                  />
                ) : (
                  <p className="mt-0.5 text-xs italic text-amber-700">Pendiente de respuesta (ver Detalle)</p>
                )}
              </div>
            ))}
          </div>
          {onAnswer && pending.length > 0 && (
            <button
              className="mt-2 rounded bg-amber-600 px-3 py-1 text-xs font-medium text-white hover:bg-amber-700 disabled:opacity-50"
              disabled={busy || requirement.aclaraciones.every((a, i) => a.respuesta || !(drafts[i] ?? '').trim())}
              onClick={() =>
                run(() => onAnswer(requirement.aclaraciones.map((a, i) => a.respuesta ?? (drafts[i] ?? '').trim())))
              }
            >
              Guardar respuestas
            </button>
          )}
        </div>
      )}

      {canGenerate && (
        <button
          className="mt-3 rounded bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          disabled={busy}
          onClick={() => run(onGenerate!)}
        >
          {busy ? 'Generando…' : 'Generar historias de usuario'}
        </button>
      )}
      {error && <p className="mt-2 rounded bg-rose-50 p-2 text-xs text-rose-700">{error}</p>}

      {requirement.historias.map((h, i) => (
        <div key={i} className="mt-3 rounded-lg bg-slate-50 p-3">
          <p className="text-sm">
            <span className="font-semibold">Como</span> {h.rol}, <span className="font-semibold">quiero</span>{' '}
            {h.quiero}, <span className="font-semibold">para</span> {h.para}.
          </p>
          <ul className="mt-1 list-disc pl-5 text-xs text-slate-600">
            {h.criteriosAceptacion.map((c, j) => (
              <li key={j}>{c}</li>
            ))}
          </ul>
          {h.caso && (
            <div className="mt-2 rounded border border-slate-200 bg-white p-2 text-xs">
              <p className="font-semibold text-slate-700">Caso de prueba: {h.caso.titulo}</p>
              {h.caso.precondiciones.length > 0 && <p className="mt-1">Precondiciones: {h.caso.precondiciones.join('; ')}</p>}
              <ol className="mt-1 list-decimal pl-5">
                {h.caso.pasos.map((p, j) => (
                  <li key={j}>{p}</li>
                ))}
              </ol>
              <p className="mt-1 text-emerald-700">Resultado esperado: {h.caso.resultadoEsperado}</p>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
