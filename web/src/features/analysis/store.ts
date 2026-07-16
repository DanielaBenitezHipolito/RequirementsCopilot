import { create } from 'zustand';
import type { RequirementView, SseEvent } from '../../shared/types';

interface AnalysisState {
  status: 'idle' | 'running' | 'done' | 'error';
  statusMessage: string;
  error?: string;
  analysisId?: string;
  requirements: RequirementView[];
  start: () => void;
  applyEvent: (evt: SseEvent) => void;
  reset: () => void;
}

const initial = {
  status: 'idle' as const,
  statusMessage: '',
  error: undefined,
  analysisId: undefined,
  requirements: [] as RequirementView[],
};

export const useAnalysisStore = create<AnalysisState>((set) => ({
  ...initial,
  start: () => set({ ...initial, status: 'running' }),
  reset: () => set({ ...initial }),
  applyEvent: (evt) =>
    set((state) => {
      switch (evt.event) {
        case 'status':
          return { statusMessage: evt.data.mensaje ?? '' };
        case 'requirement':
          return {
            requirements: [...state.requirements, { ...evt.data.requerimiento, aclaraciones: [], historias: [] }],
          };
        case 'evaluation':
          return {
            requirements: state.requirements.map((r) =>
              r.codigo === evt.data.evaluacion.requirementCode ? { ...r, evaluacion: evt.data.evaluacion } : r,
            ),
          };
        case 'clarification':
          return {
            requirements: state.requirements.map((r) =>
              r.codigo === evt.data.aclaracion.requirementCode
                ? { ...r, aclaraciones: evt.data.aclaracion.preguntas.map((pregunta: string) => ({ pregunta })) }
                : r,
            ),
          };
        case 'done':
          return { status: 'done', analysisId: evt.data.analysisId, statusMessage: '' };
        case 'error':
          return { status: 'error', error: evt.data.mensaje ?? 'Error desconocido' };
        default:
          return {};
      }
    }),
}));
