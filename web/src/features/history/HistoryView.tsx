import { useEffect, useState } from 'react';
import { getAnalyses } from '../../shared/api/client';
import type { AnalysisSummary } from '../../shared/types';

export function HistoryView({ onSelect }: { onSelect: (id: string) => void }) {
  const [items, setItems] = useState<AnalysisSummary[]>([]);
  const [error, setError] = useState<string>();

  useEffect(() => {
    getAnalyses().then(setItems).catch((e) => setError(e.message));
  }, []);

  if (error) return <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>;
  if (items.length === 0) return <p className="text-sm text-slate-500">Sin análisis todavía.</p>;

  return (
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-slate-200 text-left text-xs uppercase text-slate-500">
          <th className="py-2">Archivo</th>
          <th>Fecha</th>
          <th>Estado</th>
          <th>Requerimientos</th>
          <th>Aprobados</th>
        </tr>
      </thead>
      <tbody>
        {items.map((a) => (
          <tr
            key={a.id}
            className="cursor-pointer border-b border-slate-100 hover:bg-indigo-50"
            onClick={() => onSelect(a.id)}
          >
            <td className="py-2 font-medium text-slate-700">{a.fileName}</td>
            <td>{new Date(a.createdAt).toLocaleString()}</td>
            <td>{a.status}</td>
            <td>{a.totalRequerimientos}</td>
            <td>{a.aprobados}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
