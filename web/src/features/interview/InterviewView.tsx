import { useState } from 'react';
import { completeConversation, sendConversationMessage } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { useInterviewStore } from './store';

export function InterviewView({ onAnalyzed }: { onAnalyzed?: (analysisId: string) => void }) {
  const { messages, draft, previousResponseId, status, error, addUserMessage, applyEvent } = useInterviewStore();
  const [text, setText] = useState('');
  const [approving, setApproving] = useState(false);

  async function send() {
    const mensaje = text.trim();
    if (!mensaje || status === 'sending') return;
    setText('');
    addUserMessage(mensaje);
    try {
      const response = await sendConversationMessage(mensaje, previousResponseId);
      for await (const evt of parseSse(response.body!)) applyEvent(evt);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    }
  }

  async function approve() {
    if (!draft) return;
    setApproving(true);
    try {
      const { analysisId } = await completeConversation(draft.texto, draft.area);
      onAnalyzed?.(analysisId);
    } catch (e) {
      applyEvent({ event: 'error', data: { mensaje: e instanceof Error ? e.message : 'Error inesperado' } });
    } finally {
      setApproving(false);
    }
  }

  return (
    <div className="space-y-4">
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
    </div>
  );
}
