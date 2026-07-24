import type { CasoDeUso, RequirementView } from '../types';

function esc(s: string): string {
  return (s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br/>');
}

/** Una sección "CASO DE USO N" con la estructura de la plantilla corporativa. */
function useCaseSection(caso: CasoDeUso, codigo: string, area: string, n: number, pageBreak: boolean): string {
  const row = (label: string, value: string) =>
    `<tr><td class="lbl">${label}</td><td>${value}</td></tr>`;

  const actores = caso.actores.length
    ? `<ul>${caso.actores.map((a) => `<li><b>${esc(a.nombre)}:</b> ${esc(a.descripcion)}</li>`).join('')}</ul>`
    : '—';
  const precond = caso.precondiciones.length
    ? `<ul>${caso.precondiciones.map((p) => `<li>${esc(p)}</li>`).join('')}</ul>`
    : '—';
  const extens = caso.extensiones.length
    ? `<ul>${caso.extensiones.map((e) => `<li>${esc(e)}</li>`).join('')}</ul>`
    : '—';
  const coment = caso.comentarios.length
    ? `<ul>${caso.comentarios.map((c) => `<li>${esc(c)}</li>`).join('')}</ul>`
    : '—';

  const flujos = caso.flujos
    .map(
      (f) => `
      <p class="sub">Flujo del proceso — ${esc(f.titulo)}</p>
      <table class="flow" border="1">
        <thead><tr><th style="width:8%">Paso</th><th style="width:46%">Acción</th><th style="width:46%">Resultado esperado</th></tr></thead>
        <tbody>${f.pasos
          .map((p) => `<tr><td>${p.numero}</td><td>${esc(p.accion)}</td><td>${esc(p.resultadoEsperado)}</td></tr>`)
          .join('')}</tbody>
      </table>`,
    )
    .join('');

  return `
  <div ${pageBreak ? 'style="page-break-before:always"' : ''}>
    <h2>CASO DE USO ${n} — ${esc(caso.nombre)}</h2>
    <table class="meta" border="1">
      ${row('CÓDIGO', esc(codigo))}
      ${row('ÁREA', esc(area))}
      ${row('OBJETIVO', esc(caso.objetivo))}
      ${row('DESCRIPCIÓN', esc(caso.descripcion))}
      ${row('ACTORES', actores)}
      ${row('PRECONDICIONES', precond)}
      ${row('TRIGGER', esc(caso.trigger))}
    </table>
    ${flujos}
    <table class="meta" border="1">
      ${row('EXTENSIONES', extens)}
      ${row('FRECUENCIA', esc(caso.frecuencia))}
      ${row('IMPORTANCIA', esc(caso.importancia))}
      ${row('URGENCIA', esc(caso.urgencia))}
      ${row('COMENTARIOS', coment)}
    </table>
  </div>`;
}

/** Documento Word (.doc via HTML) con TODOS los casos de uso generados del análisis. */
export function useCasesToWordHtml(requirements: RequirementView[], fileName: string): string {
  const conCaso = requirements.filter((r) => r.caso);
  const secciones = conCaso
    .map((r, i) => useCaseSection(r.caso!, r.codigo, r.area, i + 1, i > 0))
    .join('\n');

  return `<!doctype html>
<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word">
<head><meta charset="utf-8"/>
<title>Casos de uso — ${esc(fileName)}</title>
<style>
  body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; color: #222; }
  h1 { color: #1e2a5a; font-size: 18pt; }
  h2 { color: #1e2a5a; font-size: 14pt; margin-top: 18pt; }
  .sub { font-weight: bold; color: #1e2a5a; margin: 10pt 0 4pt; }
  table { border-collapse: collapse; width: 100%; margin: 6pt 0 10pt; }
  td, th { border: 1px solid #b8c0d0; padding: 5pt 7pt; vertical-align: top; }
  th { background: #1e2a5a; color: #fff; text-align: left; }
  .meta td.lbl { width: 22%; background: #eef1f7; font-weight: bold; color: #1e2a5a; }
  ul { margin: 0; padding-left: 16pt; }
</style></head>
<body>
  <h1>Especificación de Casos de Uso</h1>
  <p><b>Documento origen:</b> ${esc(fileName)} &nbsp;|&nbsp; <b>Casos de uso:</b> ${conCaso.length}</p>
  ${secciones}
</body></html>`;
}

/** Cuántos casos de uso hay listos para exportar. */
export function generatedUseCaseCount(requirements: RequirementView[]): number {
  return requirements.filter((r) => r.caso).length;
}

/** Descarga el documento Word (.doc) con todos los casos de uso. */
export function downloadUseCasesWord(requirements: RequirementView[], fileName: string): void {
  const html = useCasesToWordHtml(requirements, fileName);
  const base = (fileName || 'casos-de-uso').replace(/\.[^.]+$/, '');
  const blob = new Blob(['﻿', html], { type: 'application/msword;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${base} - Casos de Uso.doc`;
  a.click();
  URL.revokeObjectURL(url);
}
