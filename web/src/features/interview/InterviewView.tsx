import { useEffect, useState } from 'react';
import { completeConversation, getConversation, getConversations, sendConversationMessage } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import type { ConversationSummary } from '../../shared/types';
import { useInterviewStore } from './store';

export function InterviewView({ onAnalyzed }: { onAnalyzed?: (analysisId: string) => void }) {
  const { messages, draft, previousResponseId, conversationId, status, error, addUserMessage, applyEvent, hydrate, reset } =
    useInterviewStore();
  const [text, setText] = useState('');
  const [approving, setApproving] = useState(false);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [historyError, setHistoryError] = useState<string>();

  function loadConversations() {
    getConversations().then(setConversations).catch((e) => setHistoryError(e.message));
  }

  useEffect(() => {
    loadConversations();
  }, []);

  async function send() {
    const mensaje = text.trim();
    if (!mensaje || status === 'sending') return;
    setText('');
    addUserMessage(mensaje);
    try {
      const response = await sendConversationMessage(mensaje, previousResponseId, conversationId);
      for await (const evt of parseSse(response.body!)) applyEvent(evt);
      loadConversations();
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  async function approve() {
    if (!draft) return;
    setApproving(true);
    try {
      const { analysisId } = await completeConversation(draft.texto, draft.area, conversationId);
      reset();
      loadConversations();
      onAnalyzed?.(analysisId);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    } finally {
      setApproving(false);
    }
  }

  async function openConversation(item: ConversationSummary) {
    if (item.status === 'Completada') {
      if (item.analysisId) onAnalyzed?.(item.analysisId);
      return;
    }
    try {
      const detail = await getConversation(item.id);
      hydrate(detail);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-700">Conversar</h2>
        <button
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-600 hover:bg-slate-100"
          onClick={() => reset()}
        >
          Nueva conversación
        </button>
      </div>

      <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        {messages.length === 0 && (
          <p className="text-sm text-slate-400">Cuéntale al agente qué necesitas y lo convertirá en un requerimiento.</p>
        )}
        {messages.map((m, i) => (
          <div key={i} className={`flex ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <p
              className={`max-w-[80%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm ${
                m.role === 'user' ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-800'
              }`}
            >
              {m.text}
            </p>
          </div>
        ))}
        {status === 'sending' && <p className="text-sm text-slate-400">El agente está escribiendo…</p>}
      </div>

      {status === 'error' && <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

      <div className="flex gap-2">
        <input
          className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none"
          placeholder="Escribe tu mensaje…"
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') void send();
          }}
        />
        <button
          className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          disabled={status === 'sending' || !text.trim()}
          onClick={() => void send()}
        >
          Enviar
        </button>
      </div>

      {draft && (
        <div className="space-y-2 rounded-xl border border-emerald-200 bg-emerald-50 p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700">Borrador de requerimiento</p>
          <p className="text-sm text-slate-800">{draft.texto}</p>
          <p className="text-xs text-slate-500">Área: {draft.area}</p>
          <button
            className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            disabled={approving}
            onClick={() => void approve()}
          >
            {approving ? 'Aprobando…' : 'Aprobar y analizar'}
          </button>
        </div>
      )}

      <div className="space-y-2 rounded-xl border border-slate-200 bg-white p-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Conversaciones anteriores</p>
        {historyError && <p className="rounded bg-rose-50 p-3 text-sm text-rose-700">{historyError}</p>}
        {!historyError && conversations.length === 0 && (
          <p className="text-sm text-slate-400">Todavía no hay conversaciones.</p>
        )}
        {conversations.map((c) => (
          <div
            key={c.id}
            className="flex items-center justify-between gap-2 rounded-lg border border-slate-100 px-3 py-2 hover:bg-slate-50"
          >
            <button className="flex-1 text-left" onClick={() => void openConversation(c)}>
              <p className="truncate text-sm text-slate-700">{c.preview || '(sin mensajes)'}</p>
              <p className="text-xs text-slate-400">{new Date(c.updatedAt).toLocaleString()}</p>
            </button>
            <span
              className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                c.status === 'Abierta' ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700'
              }`}
            >
              {c.status}
            </span>
            {c.status === 'Completada' && c.analysisId && (
              <button
                className="rounded-lg border border-indigo-200 px-2 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-50"
                onClick={() => onAnalyzed?.(c.analysisId!)}
              >
                Ver análisis
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
