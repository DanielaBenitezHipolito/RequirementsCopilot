import { beforeEach, describe, expect, it } from 'vitest';
import { useInterviewStore } from './store';

describe('useInterviewStore', () => {
  beforeEach(() => useInterviewStore.getState().reset());

  it('addUserMessage agrega un mensaje del usuario', () => {
    useInterviewStore.getState().addUserMessage('quiero pagar con tarjeta');
    const state = useInterviewStore.getState();
    expect(state.messages).toEqual([{ role: 'user', text: 'quiero pagar con tarjeta' }]);
    expect(state.status).toBe('sending');
  });

  it('token crea el mensaje del agente y acumula texto en tokens siguientes', () => {
    const { applyEvent } = useInterviewStore.getState();
    applyEvent({ event: 'token', data: { texto: 'Hola' } });
    applyEvent({ event: 'token', data: { texto: ' ¿en qué área?' } });
    const state = useInterviewStore.getState();
    expect(state.messages).toEqual([{ role: 'agent', text: 'Hola ¿en qué área?' }]);
  });

  it('draft setea el borrador', () => {
    const { applyEvent } = useInterviewStore.getState();
    applyEvent({ event: 'draft', data: { requerimiento: { texto: 'debe X', area: 'Pagos' } } });
    expect(useInterviewStore.getState().draft).toEqual({ texto: 'debe X', area: 'Pagos' });
  });

  it('addUserMessage limpia un draft existente al iniciar un nuevo turno', () => {
    useInterviewStore.getState().applyEvent({ event: 'draft', data: { requerimiento: { texto: 'debe X', area: 'Pagos' } } });
    expect(useInterviewStore.getState().draft).toEqual({ texto: 'debe X', area: 'Pagos' });
    useInterviewStore.getState().addUserMessage('otra cosa');
    expect(useInterviewStore.getState().draft).toBeUndefined();
  });

  it('done guarda previousResponseId y vuelve a idle', () => {
    const { applyEvent } = useInterviewStore.getState();
    applyEvent({ event: 'done', data: { responseId: 'resp-1' } });
    const state = useInterviewStore.getState();
    expect(state.previousResponseId).toBe('resp-1');
    expect(state.status).toBe('idle');
  });

  it('error marca el estado y guarda el mensaje', () => {
    const { applyEvent } = useInterviewStore.getState();
    applyEvent({ event: 'error', data: { mensaje: 'falló el LLM' } });
    const state = useInterviewStore.getState();
    expect(state.status).toBe('error');
    expect(state.error).toBe('falló el LLM');
  });

  it('reset vuelve al estado inicial', () => {
    useInterviewStore.getState().addUserMessage('hola');
    useInterviewStore.getState().applyEvent({ event: 'draft', data: { requerimiento: { texto: 'x', area: 'y' } } });
    useInterviewStore.getState().reset();
    const state = useInterviewStore.getState();
    expect(state.messages).toEqual([]);
    expect(state.draft).toBeUndefined();
    expect(state.status).toBe('idle');
  });

  it('conversation guarda el conversationId', () => {
    const { applyEvent } = useInterviewStore.getState();
    applyEvent({ event: 'conversation', data: { conversationId: 'conv-1' } });
    expect(useInterviewStore.getState().conversationId).toBe('conv-1');
  });

  it('hydrate monta mensajes, lastResponseId y conversationId de una conversación existente', () => {
    useInterviewStore.getState().hydrate({
      id: 'conv-1',
      lastResponseId: 'resp-1',
      messages: [
        { role: 'user', text: 'quiero X' },
        { role: 'agent', text: '¿en qué área?' },
      ],
    });
    const state = useInterviewStore.getState();
    expect(state.messages).toEqual([
      { role: 'user', text: 'quiero X' },
      { role: 'agent', text: '¿en qué área?' },
    ]);
    expect(state.previousResponseId).toBe('resp-1');
    expect(state.conversationId).toBe('conv-1');
    expect(state.draft).toBeUndefined();
    expect(state.status).toBe('idle');
  });

  it('reset limpia también el conversationId', () => {
    useInterviewStore.getState().applyEvent({ event: 'conversation', data: { conversationId: 'conv-1' } });
    useInterviewStore.getState().reset();
    expect(useInterviewStore.getState().conversationId).toBeUndefined();
  });

  it('setProyecto fija el proyecto y hydrate lo recupera de la conversación', () => {
    useInterviewStore.getState().setProyecto('Hotelería');
    expect(useInterviewStore.getState().proyecto).toBe('Hotelería');
    useInterviewStore.getState().hydrate({ id: 'c1', messages: [], proyecto: 'Reservas' });
    expect(useInterviewStore.getState().proyecto).toBe('Reservas');
    useInterviewStore.getState().reset();
    expect(useInterviewStore.getState().proyecto).toBeUndefined();
  });
});
