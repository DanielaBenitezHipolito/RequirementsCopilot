import { create } from 'zustand';
import type { SseEvent } from '../../shared/types';

interface Message {
  role: 'user' | 'agent';
  text: string;
}

interface Draft {
  texto: string;
  area: string;
}

interface ConversationDetail {
  id: string;
  lastResponseId?: string;
  messages: Message[];
}

interface InterviewState {
  messages: Message[];
  draft?: Draft;
  previousResponseId?: string;
  conversationId?: string;
  status: 'idle' | 'sending' | 'error';
  error?: string;
  addUserMessage: (text: string) => void;
  applyEvent: (evt: SseEvent) => void;
  hydrate: (detail: ConversationDetail) => void;
  reset: () => void;
}

const initial = {
  messages: [] as Message[],
  draft: undefined,
  previousResponseId: undefined,
  conversationId: undefined as string | undefined,
  status: 'idle' as const,
  error: undefined,
};

export const useInterviewStore = create<InterviewState>((set) => ({
  ...initial,
  addUserMessage: (text) =>
    set((state) => ({
      messages: [...state.messages, { role: 'user', text }],
      draft: undefined,
      status: 'sending',
      error: undefined,
    })),
  reset: () => set({ ...initial }),
  hydrate: (detail) =>
    set({
      messages: detail.messages,
      previousResponseId: detail.lastResponseId,
      conversationId: detail.id,
      draft: undefined,
      status: 'idle',
      error: undefined,
    }),
  applyEvent: (evt) =>
    set((state) => {
      switch (evt.event) {
        case 'token': {
          const last = state.messages[state.messages.length - 1];
          if (last?.role === 'agent') {
            return {
              messages: [...state.messages.slice(0, -1), { role: 'agent', text: last.text + evt.data.texto }],
            };
          }
          return { messages: [...state.messages, { role: 'agent', text: evt.data.texto }] };
        }
        case 'draft':
          return { draft: evt.data.requerimiento };
        case 'conversation':
          return { conversationId: evt.data.conversationId };
        case 'done':
          return { previousResponseId: evt.data.responseId, status: 'idle' };
        case 'error':
          return { status: 'error', error: evt.data.mensaje ?? 'Error desconocido' };
        default:
          return {};
      }
    }),
}));
