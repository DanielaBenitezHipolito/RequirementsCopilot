import type { CasoDeUso, RequirementView } from '../types';

// Las librerías de export (docx, jspdf) se cargan con import() dinámico SOLO al descargar,
// para no engordar el bundle inicial (la app abre más rápido; el chunk pesado va bajo demanda).

const NAVY = '1e2a5a';
const NAVY_RGB: [number, number, number] = [30, 42, 90];

/** Cuántos casos de uso hay listos para exportar. */
export function generatedUseCaseCount(requirements: RequirementView[]): number {
  return requirements.filter((r) => r.caso).length;
}

interface Seccion {
  titulo: string;
  meta1: [string, string][];
  flujos: { titulo: string; filas: [string, string, string][] }[];
  meta2: [string, string][];
}

function joinList(items: string[]): string {
  return items.length ? items.map((i) => `• ${i}`).join('\n') : '—';
}

/** Normaliza cada caso de uso a la estructura de secciones de la plantilla. */
function toSections(requirements: RequirementView[]): Seccion[] {
  return requirements
    .filter((r) => r.caso)
    .map((r) => {
      const c = r.caso as CasoDeUso;
      return {
        titulo: c.nombre,
        meta1: [
          ['Código', r.codigo],
          ['Área', r.area],
          ['Objetivo', c.objetivo],
          ['Descripción', c.descripcion],
          ['Actores', c.actores.length ? c.actores.map((a) => `• ${a.nombre}: ${a.descripcion}`).join('\n') : '—'],
          ['Precondiciones', joinList(c.precondiciones)],
          ['Trigger', c.trigger],
        ] as [string, string][],
        flujos: c.flujos.map((f) => ({
          titulo: f.titulo,
          filas: f.pasos.map((p) => [String(p.numero), p.accion, p.resultadoEsperado] as [string, string, string]),
        })),
        meta2: [
          ['Extensiones', joinList(c.extensiones)],
          ['Frecuencia', c.frecuencia],
          ['Importancia', c.importancia],
          ['Urgencia', c.urgencia],
          ['Comentarios', joinList(c.comentarios)],
        ] as [string, string][],
      };
    });
}

function baseName(fileName: string): string {
  return (fileName || 'casos-de-uso').replace(/\.[^.]+$/, '');
}

function triggerDownload(filename: string, blob: Blob): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

// ---------- Word (.docx nativo) ----------

/** Descarga un .docx nativo con todos los casos de uso. */
export async function downloadUseCasesDocx(requirements: RequirementView[], fileName: string): Promise<void> {
  const D = await import('docx');
  const secciones = toSections(requirements);
  const border = { style: D.BorderStyle.SINGLE, size: 4, color: 'B8C0D0' };
  const allBorders = {
    top: border, bottom: border, left: border, right: border, insideHorizontal: border, insideVertical: border,
  };

  const metaTable = (rows: [string, string][]) =>
    new D.Table({
      width: { size: 100, type: D.WidthType.PERCENTAGE },
      borders: allBorders,
      rows: rows.map(
        ([label, value]) =>
          new D.TableRow({
            children: [
              new D.TableCell({
                width: { size: 24, type: D.WidthType.PERCENTAGE },
                shading: { fill: 'EEF1F7' },
                children: [new D.Paragraph({ children: [new D.TextRun({ text: label, bold: true, color: NAVY })] })],
              }),
              new D.TableCell({
                children: value.split('\n').map((line) => new D.Paragraph({ children: [new D.TextRun(line)] })),
              }),
            ],
          }),
      ),
    });

  const flowTable = (filas: [string, string, string][]) => {
    const header = (t: string) =>
      new D.TableCell({
        shading: { fill: NAVY },
        children: [new D.Paragraph({ children: [new D.TextRun({ text: t, bold: true, color: 'FFFFFF' })] })],
      });
    return new D.Table({
      width: { size: 100, type: D.WidthType.PERCENTAGE },
      borders: allBorders,
      rows: [
        new D.TableRow({ tableHeader: true, children: [header('Paso'), header('Acción'), header('Resultado esperado')] }),
        ...filas.map(
          ([n, accion, res]) =>
            new D.TableRow({
              children: [
                new D.TableCell({ width: { size: 8, type: D.WidthType.PERCENTAGE }, children: [new D.Paragraph(n)] }),
                new D.TableCell({ children: [new D.Paragraph(accion)] }),
                new D.TableCell({ children: [new D.Paragraph(res)] }),
              ],
            }),
        ),
      ],
    });
  };

  const children: (InstanceType<typeof D.Paragraph> | InstanceType<typeof D.Table>)[] = [
    new D.Paragraph({ heading: D.HeadingLevel.TITLE, children: [new D.TextRun({ text: 'Especificación de Casos de Uso', color: NAVY })] }),
    new D.Paragraph({ children: [new D.TextRun({ text: `Documento origen: ${fileName}  |  Casos de uso: ${secciones.length}`, italics: true })] }),
  ];

  secciones.forEach((s, i) => {
    children.push(
      new D.Paragraph({
        heading: D.HeadingLevel.HEADING_1,
        pageBreakBefore: i > 0,
        children: [new D.TextRun({ text: `CASO DE USO ${i + 1} — ${s.titulo}`, color: NAVY })],
      }),
    );
    children.push(metaTable(s.meta1));
    s.flujos.forEach((f) => {
      children.push(new D.Paragraph({ children: [new D.TextRun({ text: `Flujo del proceso — ${f.titulo}`, bold: true, color: NAVY })], spacing: { before: 160, after: 60 } }));
      children.push(flowTable(f.filas));
    });
    children.push(new D.Paragraph({ text: '', spacing: { after: 60 } }));
    children.push(metaTable(s.meta2));
  });

  const doc = new D.Document({ sections: [{ children }] });
  const blob = await D.Packer.toBlob(doc);
  triggerDownload(`${baseName(fileName)} - Casos de Uso.docx`, blob);
}

// ---------- PDF ----------

/** Descarga un PDF con todos los casos de uso (tablas auto-paginadas). */
export async function downloadUseCasesPdf(requirements: RequirementView[], fileName: string): Promise<void> {
  const { jsPDF } = await import('jspdf');
  // Importar por efecto secundario: engancha el método autoTable al prototipo de jsPDF.
  await import('jspdf-autotable');
  const secciones = toSections(requirements);
  const doc = new jsPDF({ unit: 'pt', format: 'a4' });
  const docAny = doc as unknown as { autoTable?: (o: Record<string, unknown>) => void; lastAutoTable: { finalY: number } };
  if (typeof docAny.autoTable !== 'function') {
    throw new Error('No se pudo cargar jspdf-autotable. Verifica que la dependencia esté instalada.');
  }
  // El método vive en la instancia tras el import; se accede vía cast (no está tipado por defecto).
  const table = (options: Record<string, unknown>) => docAny.autoTable!(options);
  const finalY = () => docAny.lastAutoTable.finalY;
  const margin = 40;
  const pageWidth = doc.internal.pageSize.getWidth();
  const pageHeight = doc.internal.pageSize.getHeight();

  doc.setTextColor(...NAVY_RGB);
  doc.setFontSize(18);
  doc.text('Especificación de Casos de Uso', margin, 50);
  doc.setTextColor(90);
  doc.setFontSize(10);
  doc.text(`Documento origen: ${fileName}  |  Casos de uso: ${secciones.length}`, margin, 68);

  let y = 90;

  const heading = (text: string, size: number) => {
    if (y > pageHeight - 80) {
      doc.addPage();
      y = 50;
    }
    doc.setTextColor(...NAVY_RGB);
    doc.setFontSize(size);
    doc.text(text, margin, y);
    y += size + 6;
  };

  const metaTable = (rows: [string, string][]) => {
    table({
      startY: y,
      margin: { left: margin, right: margin },
      theme: 'grid',
      styles: { fontSize: 9, cellPadding: 4, valign: 'top' },
      columnStyles: { 0: { cellWidth: (pageWidth - margin * 2) * 0.24, fontStyle: 'bold', textColor: NAVY_RGB, fillColor: [238, 241, 247] } },
      body: rows,
    });
    y = finalY() + 12;
  };

  const flowTable = (filas: [string, string, string][]) => {
    table({
      startY: y,
      margin: { left: margin, right: margin },
      theme: 'grid',
      headStyles: { fillColor: NAVY_RGB, textColor: [255, 255, 255] },
      styles: { fontSize: 9, cellPadding: 4, valign: 'top' },
      columnStyles: { 0: { cellWidth: 36 } },
      head: [['Paso', 'Acción', 'Resultado esperado']],
      body: filas,
    });
    y = finalY() + 12;
  };

  secciones.forEach((s, i) => {
    if (i > 0) {
      doc.addPage();
      y = 50;
    }
    heading(`CASO DE USO ${i + 1} — ${s.titulo}`, 14);
    metaTable(s.meta1);
    s.flujos.forEach((f) => {
      heading(`Flujo del proceso — ${f.titulo}`, 11);
      flowTable(f.filas);
    });
    metaTable(s.meta2);
  });

  doc.save(`${baseName(fileName)} - Casos de Uso.pdf`);
}
