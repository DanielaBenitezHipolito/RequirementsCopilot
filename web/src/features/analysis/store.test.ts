import { beforeEach, describe, expect, it } from 'vitest';
import { useAnalysisStore } from './store';

const req = { codigo: 'REQ-001', texto: 'debe X', area: 'Pagos' };
const evalOk = {
  requirementCode: 'REQ-001',
  criterios: [{ nombre: 'Claridad', score: 5, observacion: 'ok' }],
  promedio: 5,
  umbral: 3.5,
  pasa: true,
};
const aclaracion = {
  requirementCode: 'REQ-001',
  preguntas: ['¿Qué significa rápido?', '¿Para qué operaciones?'],
};

describe('useAnalysisStore.applyEvent', () => {
  beforeEach(() => useAnalysisStore.getState().reset());

  it('arma requerimiento → evaluación → preguntas de clarificación', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'requirement', data: { requerimiento: req } });
    applyEvent({ event: 'evaluation', data: { evaluacion: evalOk } });
    applyEvent({ event: 'clarification', data: { aclaracion } });
    applyEvent({ event: 'done', data: { analysisId: 'abc' } });

    const state = useAnalysisStore.getState();
    expect(state.status).toBe('done');
    expect(state.analysisId).toBe('abc');
    expect(state.requirements).toHaveLength(1);
    expect(state.requirements[0].evaluacion?.pasa).toBe(true);
    expect(state.requirements[0].aclaraciones).toHaveLength(2);
    expect(state.requirements[0].aclaraciones[0].pregunta).toBe('¿Qué significa rápido?');
    expect(state.requirements[0].historias).toHaveLength(0); // ya no llegan por SSE
  });

  it('error marca el estado y guarda el mensaje', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'error', data: { mensaje: 'falló el LLM' } });
    const state = useAnalysisStore.getState();
    expect(state.status).toBe('error');
    expect(state.error).toBe('falló el LLM');
  });

  it('summary guarda el resumen ejecutivo', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'summary', data: { resumen: 'El documento está en buen estado general.' } });
    const state = useAnalysisStore.getState();
    expect(state.resumen).toBe('El documento está en buen estado general.');
  });
});
