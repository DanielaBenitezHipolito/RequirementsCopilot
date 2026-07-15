import type { RequirementView } from '../../shared/types';

export function RequirementCard({ requirement }: { requirement: RequirementView }) {
  const evaluacion = requirement.evaluacion;
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
