import { useEffect, useState } from 'react';
import { getUsage } from '../../shared/api/client';

interface UsageAgent {
  agente: string;
  llamadas: number;
  entrada: number;
  salida: number;
}

interface UsageDay {
  fecha: string;
  llamadas: number;
  entrada: number;
  salida: number;
}

interface Usage {
  llamadas: number;
  tokensEntrada: number;
  tokensSalida: number;
  porAgente: UsageAgent[];
  porDia: UsageDay[];
}

const fmt = (n: number) => n.toLocaleString('es-CO');

/** Tablero de control (solo Admin): consumo de tokens del LLM. */
export function AdminView() {
  const [dias, setDias] = useState(30);
  const [usage, setUsage] = useState<Usage>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    getUsage(dias)
      .then(setUsage)
      .catch((e) => setError(e instanceof Error ? e.message : 'No se pudo cargar el consumo.'));
  }, [dias]);

  const card = 'rounded-2xl border border-slate-200 bg-white p-5 shadow-sm';

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Tablero de Control · Consumo de IA</h2>
        <select
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm focus:outline-none"
          value={dias}
          onChange={(e) => setDias(Number(e.target.value))}
        >
          <option value={7}>Últimos 7 días</option>
          <option value={30}>Últimos 30 días</option>
          <option value={90}>Últimos 90 días</option>
        </select>
      </div>

      {error && <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

      {usage && (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div
              className="rounded-2xl p-5 text-white shadow-sm"
              style={{ background: 'linear-gradient(135deg, #0c1430, #1e2a5a 75%, #27367a)' }}
            >
              <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-wide text-white/60 uppercase">
                <svg viewBox="0 0 24 24" fill="#f59e0b" className="h-3.5 w-3.5">
                  <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
                </svg>
                Llamadas al LLM
              </p>
              <p className="mt-1 text-3xl font-black">{fmt(usage.llamadas)}</p>
            </div>
            <div className={card}>
              <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Tokens de entrada</p>
              <p className="mt-1 text-3xl font-black text-slate-800">{fmt(usage.tokensEntrada)}</p>
            </div>
            <div className={card}>
              <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Tokens de salida</p>
              <p className="mt-1 text-3xl font-black text-slate-800">{fmt(usage.tokensSalida)}</p>
            </div>
          </div>

          {usage.llamadas === 0 && (
            <p className="rounded-lg bg-slate-50 p-4 text-sm text-slate-500">
              Sin consumo registrado en el período. El registro se alimenta de las llamadas reales a Foundry
              (el modo demo no registra).
            </p>
          )}

          {usage.porAgente.length > 0 && (
            <div className={card}>
              <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Por agente</p>
              <div className="mt-2 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 text-left text-[11px] tracking-wide text-slate-400 uppercase">
                      <th className="py-2">Agente</th>
                      <th className="text-right">Llamadas</th>
                      <th className="text-right">Entrada</th>
                      <th className="text-right">Salida</th>
                    </tr>
                  </thead>
                  <tbody>
                    {usage.porAgente.map((a) => {
                      const maxTotal = Math.max(...usage.porAgente.map((x) => x.entrada + x.salida), 1);
                      const share = (a.entrada + a.salida) / maxTotal;
                      return (
                        <tr key={a.agente} className="border-b border-slate-100">
                          <td className="py-2.5 font-semibold text-slate-700">
                            {a.agente}
                            <span className="mt-1.5 block h-1 max-w-56 rounded-full bg-slate-100">
                              <span
                                className="block h-1 rounded-full"
                                style={{ width: `${Math.max(share * 100, 2)}%`, backgroundColor: '#1e2a5a' }}
                              />
                            </span>
                          </td>
                          <td className="text-right text-slate-600">{fmt(a.llamadas)}</td>
                          <td className="text-right font-mono text-slate-600">{fmt(a.entrada)}</td>
                          <td className="text-right font-mono text-slate-600">{fmt(a.salida)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {usage.porDia.length > 0 && (
            <div className={card}>
              <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Por día</p>
              <div className="mt-2 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 text-left text-[11px] tracking-wide text-slate-400 uppercase">
                      <th className="py-2">Fecha</th>
                      <th className="text-right">Llamadas</th>
                      <th className="text-right">Entrada</th>
                      <th className="text-right">Salida</th>
                    </tr>
                  </thead>
                  <tbody>
                    {usage.porDia.map((d) => (
                      <tr key={d.fecha} className="border-b border-slate-100">
                        <td className="py-2 font-semibold text-slate-700">{d.fecha}</td>
                        <td className="text-right text-slate-600">{fmt(d.llamadas)}</td>
                        <td className="text-right font-mono text-slate-600">{fmt(d.entrada)}</td>
                        <td className="text-right font-mono text-slate-600">{fmt(d.salida)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
