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
const historia = {
  requirementCode: 'REQ-001', storyIndex: 0, rol: 'cajero', quiero: 'pagar', para: 'cerrar',
  criteriosAceptacion: ['dado A entonces B'],
};
const caso = {
  requirementCode: 'REQ-001', storyIndex: 0, titulo: 'Pago ok', precondiciones: [], pasos: ['ir'],
  resultadoEsperado: 'pagado',
};

describe('useAnalysisStore.applyEvent', () => {
  beforeEach(() => useAnalysisStore.getState().reset());

  it('arma el árbol requerimiento → evaluación → historia → caso', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'requirement', data: { requerimiento: req } });
    applyEvent({ event: 'evaluation', data: { evaluacion: evalOk } });
    applyEvent({ event: 'story', data: { historia } });
    applyEvent({ event: 'testcase', data: { caso } });
    applyEvent({ event: 'done', data: { analysisId: 'abc' } });

    const state = useAnalysisStore.getState();
    expect(state.status).toBe('done');
    expect(state.requirements).toHaveLength(1);
    expect(state.requirements[0].evaluacion?.pasa).toBe(true);
    expect(state.requirements[0].historias[0].caso?.titulo).toBe('Pago ok');
  });

  it('error marca el estado y guarda el mensaje', () => {
    const { applyEvent } = useAnalysisStore.getState();
    applyEvent({ event: 'error', data: { mensaje: 'falló el LLM' } });
    const state = useAnalysisStore.getState();
    expect(state.status).toBe('error');
    expect(state.error).toBe('falló el LLM');
  });
});
