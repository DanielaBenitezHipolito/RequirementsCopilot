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
            requirements: [...state.requirements, { ...evt.data.requerimiento, historias: [] }],
          };
        case 'evaluation':
          return {
            requirements: state.requirements.map((r) =>
              r.codigo === evt.data.evaluacion.requirementCode ? { ...r, evaluacion: evt.data.evaluacion } : r,
            ),
          };
        case 'story': {
          const { requirementCode, storyIndex, ...historia } = evt.data.historia;
          return {
            requirements: state.requirements.map((r) => {
              if (r.codigo !== requirementCode) return r;
              const historias = [...r.historias];
              historias[storyIndex] = { ...historia };
              return { ...r, historias };
            }),
          };
        }
        case 'testcase': {
          const { requirementCode, storyIndex, ...caso } = evt.data.caso;
          return {
            requirements: state.requirements.map((r) => {
              if (r.codigo !== requirementCode) return r;
              const historias = r.historias.map((h, i) => (i === storyIndex ? { ...h, caso } : h));
              return { ...r, historias };
            }),
          };
        }
        case 'done':
          return { status: 'done', analysisId: evt.data.analysisId, statusMessage: '' };
        case 'error':
          return { status: 'error', error: evt.data.mensaje ?? 'Error desconocido' };
        default:
          return {};
      }
    }),
}));
