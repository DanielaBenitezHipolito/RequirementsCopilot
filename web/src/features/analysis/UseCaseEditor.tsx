import { useState } from 'react';
import type { CasoDeUso } from '../../shared/types';
import { BrandButton } from '../../shared/components/BrandButton';

interface Props {
  caso: CasoDeUso;
  onSave: (caso: CasoDeUso) => Promise<void>;
  onCancel: () => void;
}

const lines = (t: string) => t.split('\n').map((l) => l.trim()).filter(Boolean);

function parseActores(t: string) {
  // Una línea por actor: "Nombre — Descripción" (también acepta "Nombre - Descripción").
  return lines(t).map((l) => {
    const sep = l.includes(' — ') ? ' — ' : ' - ';
    const i = l.indexOf(sep);
    return i < 0 ? { nombre: l, descripcion: '' } : { nombre: l.slice(0, i).trim(), descripcion: l.slice(i + sep.length).trim() };
  });
}

function parseFlujos(t: string) {
  // "## Título" abre un flujo; cada paso es "acción | resultado esperado".
  const flujos: { titulo: string; pasos: { numero: number; accion: string; resultadoEsperado: string }[] }[] = [];
  for (const l of lines(t)) {
    if (l.startsWith('## ')) {
      flujos.push({ titulo: l.slice(3).trim(), pasos: [] });
      continue;
    }
    if (flujos.length === 0) flujos.push({ titulo: 'Flujo principal', pasos: [] });
    const flujo = flujos[flujos.length - 1];
    const i = l.indexOf('|');
    flujo.pasos.push({
      numero: flujo.pasos.length + 1,
      accion: (i < 0 ? l : l.slice(0, i)).trim(),
      resultadoEsperado: i < 0 ? '' : l.slice(i + 1).trim(),
    });
  }
  return flujos;
}

/** Edición manual del caso de uso: campos simples + listas de a una línea; flujos como "acción | resultado". */
export function UseCaseEditor({ caso, onSave, onCancel }: Props) {
  const [nombre, setNombre] = useState(caso.nombre);
  const [objetivo, setObjetivo] = useState(caso.objetivo);
  const [descripcion, setDescripcion] = useState(caso.descripcion);
  const [trigger, setTrigger] = useState(caso.trigger);
  const [frecuencia, setFrecuencia] = useState(caso.frecuencia);
  const [importancia, setImportancia] = useState(caso.importancia);
  const [urgencia, setUrgencia] = useState(caso.urgencia);
  const [actores, setActores] = useState(caso.actores.map((a) => `${a.nombre} — ${a.descripcion}`).join('\n'));
  const [precondiciones, setPrecondiciones] = useState(caso.precondiciones.join('\n'));
  const [extensiones, setExtensiones] = useState(caso.extensiones.join('\n'));
  const [comentarios, setComentarios] = useState(caso.comentarios.join('\n'));
  const [flujos, setFlujos] = useState(
    caso.flujos.map((f) => `## ${f.titulo}\n${f.pasos.map((p) => `${p.accion} | ${p.resultadoEsperado}`).join('\n')}`).join('\n'),
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string>();

  async function save() {
    setSaving(true);
    setError(undefined);
    try {
      await onSave({
        nombre: nombre.trim(),
        objetivo: objetivo.trim(),
        descripcion: descripcion.trim(),
        actores: parseActores(actores),
        precondiciones: lines(precondiciones),
        trigger: trigger.trim(),
        flujos: parseFlujos(flujos),
        extensiones: lines(extensiones),
        frecuencia: frecuencia.trim(),
        importancia: importancia.trim(),
        urgencia: urgencia.trim(),
        comentarios: lines(comentarios),
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  }

  const field = 'mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-slate-400 focus:outline-none';
  const label = 'text-[11px] font-bold tracking-wide text-slate-500 uppercase';

  return (
    <div className="space-y-3 rounded-lg border-2 border-slate-300 bg-white p-4">
      <p className="text-sm font-bold text-slate-800">EDITAR CASO DE USO</p>
      <div>
        <p className={label}>Nombre</p>
        <input className={field} value={nombre} onChange={(e) => setNombre(e.target.value)} />
      </div>
      <div>
        <p className={label}>Objetivo</p>
        <textarea className={field} rows={2} value={objetivo} onChange={(e) => setObjetivo(e.target.value)} />
      </div>
      <div>
        <p className={label}>Descripción</p>
        <textarea className={field} rows={2} value={descripcion} onChange={(e) => setDescripcion(e.target.value)} />
      </div>
      <div>
        <p className={label}>Actores (uno por línea: Nombre — Descripción)</p>
        <textarea className={field} rows={3} value={actores} onChange={(e) => setActores(e.target.value)} />
      </div>
      <div>
        <p className={label}>Precondiciones (una por línea)</p>
        <textarea className={field} rows={2} value={precondiciones} onChange={(e) => setPrecondiciones(e.target.value)} />
      </div>
      <div>
        <p className={label}>Trigger</p>
        <input className={field} value={trigger} onChange={(e) => setTrigger(e.target.value)} />
      </div>
      <div>
        <p className={label}>Flujos («## Título» abre un flujo; cada paso: acción | resultado esperado)</p>
        <textarea className={`${field} font-mono`} rows={6} value={flujos} onChange={(e) => setFlujos(e.target.value)} />
      </div>
      <div>
        <p className={label}>Extensiones (una por línea)</p>
        <textarea className={field} rows={2} value={extensiones} onChange={(e) => setExtensiones(e.target.value)} />
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div>
          <p className={label}>Frecuencia</p>
          <input className={field} value={frecuencia} onChange={(e) => setFrecuencia(e.target.value)} />
        </div>
        <div>
          <p className={label}>Importancia</p>
          <input className={field} value={importancia} onChange={(e) => setImportancia(e.target.value)} />
        </div>
        <div>
          <p className={label}>Urgencia</p>
          <input className={field} value={urgencia} onChange={(e) => setUrgencia(e.target.value)} />
        </div>
      </div>
      <div>
        <p className={label}>Comentarios (uno por línea)</p>
        <textarea className={field} rows={2} value={comentarios} onChange={(e) => setComentarios(e.target.value)} />
      </div>
      {error && <p className="rounded-lg bg-rose-50 p-2 text-xs text-rose-700">{error}</p>}
      <div className="flex justify-end gap-2">
        <BrandButton variant="secondary" className="!px-4 !py-2 !text-xs" onClick={onCancel}>
          Cancelar
        </BrandButton>
        <BrandButton className="!px-4 !py-2 !text-xs" loading={saving} disabled={!nombre.trim() || !objetivo.trim()} onClick={() => void save()}>
          Guardar caso de uso
        </BrandButton>
      </div>
    </div>
  );
}
