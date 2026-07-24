import type { CasoDeUso } from '../types';

/** Convierte un caso de uso al formato Markdown de la plantilla corporativa. */
export function useCaseToMarkdown(caso: CasoDeUso, codigo: string, area: string): string {
  const l: string[] = [];
  l.push(`# ${caso.nombre}`, '');
  l.push(`**Código:** ${codigo}  |  **Área:** ${area}`, '');
  l.push('## Objetivo', caso.objetivo, '');
  l.push('## Descripción', caso.descripcion, '');

  if (caso.actores.length) {
    l.push('## Actores');
    caso.actores.forEach((a) => l.push(`- **${a.nombre}:** ${a.descripcion}`));
    l.push('');
  }
  if (caso.precondiciones.length) {
    l.push('## Precondiciones');
    caso.precondiciones.forEach((p) => l.push(`- ${p}`));
    l.push('');
  }
  l.push('## Trigger', caso.trigger, '');

  caso.flujos.forEach((f) => {
    l.push(`## Flujo del proceso — ${f.titulo}`, '');
    l.push('| Paso | Acción | Resultado esperado |', '|---|---|---|');
    f.pasos.forEach((p) =>
      l.push(`| ${p.numero} | ${escapeCell(p.accion)} | ${escapeCell(p.resultadoEsperado)} |`),
    );
    l.push('');
  });

  if (caso.extensiones.length) {
    l.push('## Extensiones');
    caso.extensiones.forEach((e) => l.push(`- ${e}`));
    l.push('');
  }
  l.push('## Clasificación');
  l.push(`- **Frecuencia:** ${caso.frecuencia}`);
  l.push(`- **Importancia:** ${caso.importancia}`);
  l.push(`- **Urgencia:** ${caso.urgencia}`, '');

  if (caso.comentarios.length) {
    l.push('## Comentarios');
    caso.comentarios.forEach((c) => l.push(`- ${c}`));
    l.push('');
  }
  return l.join('\n');
}

function escapeCell(s: string): string {
  return s.replace(/\|/g, '\\|').replace(/\n/g, ' ');
}

/** Dispara la descarga de un texto como archivo. */
export function downloadText(filename: string, content: string, mime = 'text/markdown'): void {
  const blob = new Blob([content], { type: `${mime};charset=utf-8` });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
