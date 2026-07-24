import { useEffect, useRef, useState } from 'react';
import { completeConversation, getConversation, getConversations, sendConversationMessage } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { BrandButton } from '../../shared/components/BrandButton';
import type { ConversationSummary } from '../../shared/types';
import { useInterviewStore } from './store';

const BRAND = '#1e2a5a';

function BotIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth={2} className="h-4 w-4">
      <rect x="4" y="8" width="16" height="12" rx="2" />
      <path strokeLinecap="round" d="M12 4v4M8 14v.01M16 14v.01" />
    </svg>
  );
}

function SendIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path strokeLinecap="round" strokeLinejoin="round" d="M22 2L11 13" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M22 2l-7 20-4-9-9-4 20-7z" />
    </svg>
  );
}

export function InterviewView({ onAnalyzed }: { onAnalyzed?: (analysisId: string) => void }) {
  const { messages, draft, previousResponseId, conversationId, status, error, addUserMessage, applyEvent, hydrate, reset } =
    useInterviewStore();
  const [text, setText] = useState('');
  const [approving, setApproving] = useState(false);
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [historyError, setHistoryError] = useState<string>();
  const [loadingConversations, setLoadingConversations] = useState(true);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  function loadConversations() {
    getConversations()
      .then(setConversations)
      .catch((e) => setHistoryError(e.message))
      .finally(() => setLoadingConversations(false));
  }

  useEffect(() => {
    loadConversations();
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ block: 'end' });
  }, [messages.length, status]);

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
    <div className="grid h-full grid-cols-1 gap-4 lg:grid-cols-3">
      <div className="flex min-h-0 flex-col gap-4 overflow-y-auto lg:col-span-1">
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Conversaciones</h2>
          </div>
          <BrandButton className="mt-3 w-full" onClick={() => reset()}>
            Nueva conversación
          </BrandButton>

          <div className="mt-3 space-y-2">
            {loadingConversations && (
              <p className="flex items-center gap-2 text-sm text-slate-400">
                <span className="h-4 w-4 animate-spin rounded-full border-2 border-slate-200 border-t-slate-500" />
                Cargando conversaciones…
              </p>
            )}
            {!loadingConversations && historyError && (
              <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{historyError}</p>
            )}
            {!loadingConversations && !historyError && conversations.length === 0 && (
              <p className="text-sm text-slate-400">Todavía no hay conversaciones.</p>
            )}
            {conversations.map((c) => (
              <button
                key={c.id}
                className="flex w-full flex-col gap-1 rounded-lg border border-slate-100 px-3 py-2 text-left hover:bg-slate-50"
                onClick={() => void openConversation(c)}
              >
                <div className="flex items-center justify-between gap-2">
                  <p className="truncate text-sm text-slate-700">{c.preview || '(sin mensajes)'}</p>
                  <span
                    className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-bold tracking-wide uppercase ${
                      c.status === 'Abierta' ? 'bg-amber-50 text-amber-700' : 'bg-emerald-50 text-emerald-700'
                    }`}
                  >
                    {c.status}
                  </span>
                </div>
                <p className="text-xs text-slate-400">{new Date(c.updatedAt).toLocaleString()}</p>
              </button>
            ))}
          </div>
        </div>

        {draft && (
          <div className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
            <p className="text-[11px] font-bold tracking-wide text-slate-400 uppercase">Contexto Activo</p>
            <p className="text-sm text-slate-700">{draft.texto}</p>
            <p className="text-xs text-slate-500">Área: {draft.area}</p>
          </div>
        )}
      </div>

      <div className="flex h-full min-h-0 flex-col rounded-2xl border border-slate-200 bg-white shadow-sm lg:col-span-2">
        <div className="shrink-0 border-b border-slate-100 px-4 py-3">
          <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Copilot de Requerimientos</h2>
          <p className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" /> En línea · Howden AI
          </p>
        </div>

        <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
          {messages.length === 0 && (
            <p className="text-sm text-slate-400">Cuéntale al agente qué necesitas y lo convertirá en un requerimiento.</p>
          )}
          {messages.map((m, i) => (
            <div key={i} className={`flex items-start gap-2 ${m.role === 'user' ? 'justify-end' : 'justify-start'}`}>
              {m.role === 'agent' && (
                <span
                  className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full"
                  style={{ backgroundColor: BRAND }}
                >
                  <BotIcon />
                </span>
              )}
              <p
                className={`max-w-[80%] whitespace-pre-wrap rounded-2xl px-3 py-2 text-sm ${
                  m.role === 'user'
                    ? 'text-white'
                    : 'border border-slate-200 bg-white text-slate-800'
                }`}
                style={m.role === 'user' ? { backgroundColor: BRAND } : undefined}
              >
                {m.text}
              </p>
            </div>
          ))}
          {status === 'sending' && <p className="text-sm text-slate-400">El agente está escribiendo…</p>}
          <div ref={messagesEndRef} />
        </div>

        {status === 'error' && <p className="mx-4 mb-3 shrink-0 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

        {draft && (
          <div className="mx-4 mb-3 shrink-0 space-y-2 rounded-xl border-2 p-4" style={{ borderColor: BRAND }}>
            <p className="text-[11px] font-bold tracking-wide uppercase" style={{ color: BRAND }}>
              Borrador de requerimiento
            </p>
            <p className="text-sm text-slate-800">{draft.texto}</p>
            <p className="text-xs text-slate-500">Área: {draft.area}</p>
            <BrandButton loading={approving} loadingText="Aprobando…" onClick={() => void approve()}>
              Aprobar y analizar
            </BrandButton>
          </div>
        )}

        <div className="flex shrink-0 gap-2 border-t border-slate-100 p-4">
          <input
            className="flex-1 rounded-full border border-slate-300 px-4 py-2.5 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Consúltale cualquier duda al Copilot…"
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void send();
            }}
          />
          <BrandButton
            className="!h-10 !w-10 !rounded-full !p-0"
            disabled={!text.trim()}
            loading={status === 'sending'}
            loadingText=""
            onClick={() => void send()}
          >
            <SendIcon />
          </BrandButton>
        </div>
      </div>
    </div>
  );
}
