import { useEffect, useRef, useState } from 'react';
import { completeConversation, getConversation, getConversations, sendConversationMessage } from '../../shared/api/client';
import { parseSse } from '../../shared/api/sse';
import { BrandButton } from '../../shared/components/BrandButton';
import { ProjectPicker } from '../../shared/components/ProjectPicker';
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

function ClipIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M21.44 11.05l-9.19 9.19a6 6 0 01-8.49-8.49l9.19-9.19a4 4 0 015.66 5.66l-9.2 9.19a2 2 0 01-2.83-2.83l8.49-8.48"
      />
    </svg>
  );
}

const SUGERENCIAS = [
  'Necesito controlar los pagos de las reservas',
  'Quiero un reporte mensual de siniestros por ramo',
  'El sistema debe gestionar la renovación de pólizas',
];

const MAX_CHAT_IMAGES = 4;
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(new Error('No se pudo leer la imagen.'));
    reader.readAsDataURL(file);
  });
}

export function InterviewView({ onAnalyzed }: { onAnalyzed?: (analysisId: string) => void }) {
  const { messages, draft, previousResponseId, conversationId, proyecto, status, error, setProyecto, addUserMessage, applyEvent, hydrate, reset } =
    useInterviewStore();
  const [text, setText] = useState('');
  const [images, setImages] = useState<string[]>([]);
  const [imageError, setImageError] = useState<string>();
  const fileInputRef = useRef<HTMLInputElement>(null);
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

  async function attachFiles(files: Iterable<File>) {
    setImageError(undefined);
    for (const file of files) {
      if (!file.type.startsWith('image/')) continue;
      if (images.length >= MAX_CHAT_IMAGES) {
        setImageError(`Máximo ${MAX_CHAT_IMAGES} imágenes por mensaje.`);
        return;
      }
      if (file.size > MAX_IMAGE_BYTES) {
        setImageError('Cada imagen debe pesar máximo 5 MB.');
        return;
      }
      try {
        const dataUrl = await readAsDataUrl(file);
        setImages((prev) => (prev.length < MAX_CHAT_IMAGES ? [...prev, dataUrl] : prev));
      } catch (e) {
        setImageError(e instanceof Error ? e.message : 'No se pudo leer la imagen.');
      }
    }
  }

  async function send() {
    const mensaje = text.trim();
    const adjuntas = images;
    if ((!mensaje && adjuntas.length === 0) || status === 'sending') return;
    setText('');
    setImages([]);
    setImageError(undefined);
    addUserMessage(mensaje || '(imagen adjunta)', adjuntas.length > 0 ? adjuntas : undefined);
    try {
      const response = await sendConversationMessage(mensaje, previousResponseId, conversationId, proyecto, adjuntas);
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
      const { analysisId } = await completeConversation(draft.texto, draft.area, conversationId, proyecto);
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
    // Altura fija al viewport: los mensajes hacen scroll dentro del panel, no en la página.
    <div className="grid h-full min-h-[28rem] grid-cols-1 gap-4 lg:grid-cols-3">
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
        <div className="flex shrink-0 flex-wrap items-center gap-3 border-b border-slate-100 px-4 py-3">
          <span
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full shadow-sm"
            style={{ background: `linear-gradient(135deg, #0c1430, ${BRAND})` }}
          >
            <BotIcon />
          </span>
          <div>
            <h2 className="text-sm font-bold tracking-wide text-slate-800 uppercase">Copilot de Requerimientos</h2>
            <p className="mt-0.5 flex items-center gap-1.5 text-xs text-slate-500">
              <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-500" /> En línea · Howden AI
            </p>
          </div>
          {/* El proyecto se fija al iniciar el chat: el contexto viaja en el primer turno. */}
          <div className="ml-auto">
            <ProjectPicker value={proyecto} onChange={setProyecto} disabled={messages.length > 0} />
          </div>
        </div>

        <div className="flex-1 space-y-3 overflow-y-auto bg-slate-50/70 px-4 py-4">
          {messages.length === 0 && (
            <div className="flex h-full flex-col items-center justify-center gap-3 text-center">
              <span
                className="flex h-14 w-14 items-center justify-center rounded-full shadow-md"
                style={{ background: `linear-gradient(135deg, #0c1430, ${BRAND})` }}
              >
                <BotIcon />
              </span>
              <p className="text-base font-black tracking-tight text-slate-800">Cuénteme qué necesita</p>
              <p className="max-w-sm text-sm text-slate-500">
                Describa su idea (o pegue una captura con Ctrl+V) y la convierto en un requerimiento claro,
                completo y verificable.
              </p>
              <div className="mt-1 flex flex-wrap justify-center gap-2">
                {SUGERENCIAS.map((sugerencia) => (
                  <button
                    key={sugerencia}
                    type="button"
                    className="rounded-full border border-slate-300 bg-white px-3.5 py-1.5 text-xs text-slate-600 shadow-sm transition-colors hover:border-slate-400 hover:text-slate-800"
                    onClick={() => setText(sugerencia)}
                  >
                    {sugerencia}
                  </button>
                ))}
              </div>
            </div>
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
              <div
                className={`max-w-[80%] break-words whitespace-pre-wrap rounded-2xl px-3.5 py-2.5 text-sm shadow-sm ${
                  m.role === 'user'
                    ? 'rounded-br-md text-white'
                    : 'rounded-bl-md border border-slate-200 bg-white text-slate-800'
                }`}
                style={m.role === 'user' ? { background: `linear-gradient(135deg, ${BRAND}, #27367a)` } : undefined}
              >
                {m.images && m.images.length > 0 && (
                  <div className="mb-1.5 flex flex-wrap gap-1.5">
                    {m.images.map((src, j) => (
                      <img key={j} src={src} alt="imagen adjunta" className="h-24 max-w-40 rounded-lg object-cover" />
                    ))}
                  </div>
                )}
                {m.text}
              </div>
            </div>
          ))}
          {status === 'sending' && (
            <div className="flex items-center gap-2 text-sm text-slate-400">
              <span className="flex gap-1">
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400" />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400" style={{ animationDelay: '0.15s' }} />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400" style={{ animationDelay: '0.3s' }} />
              </span>
              El agente está escribiendo…
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        {status === 'error' && <p className="mx-4 mb-3 shrink-0 rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}

        {draft && (
          <div className="mx-4 mb-3 shrink-0 space-y-2 rounded-xl border-2 border-amber-400/80 bg-amber-50/50 p-4 shadow-sm">
            <p className="flex items-center gap-1.5 text-[11px] font-bold tracking-wide text-amber-700 uppercase">
              <svg viewBox="0 0 24 24" fill="#f59e0b" className="h-3.5 w-3.5">
                <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
              </svg>
              Borrador de requerimiento listo
            </p>
            <p className="text-sm text-slate-800">{draft.texto}</p>
            <p className="text-xs text-slate-500">Área: {draft.area}</p>
            <BrandButton sparkle loading={approving} loadingText="Aprobando…" onClick={() => void approve()}>
              Aprobar y analizar
            </BrandButton>
          </div>
        )}

        <div className="shrink-0 border-t border-slate-100 p-4">
          {imageError && <p className="mb-2 rounded-lg bg-rose-50 p-2 text-xs text-rose-700">{imageError}</p>}
          {images.length > 0 && (
            <div className="mb-2 flex flex-wrap gap-2">
              {images.map((src, i) => (
                <span key={i} className="relative">
                  <img src={src} alt={`adjunto ${i + 1}`} className="h-16 w-16 rounded-lg border border-slate-200 object-cover" />
                  <button
                    type="button"
                    aria-label="Quitar imagen"
                    className="absolute -top-1.5 -right-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-slate-700 text-[10px] text-white hover:bg-rose-600"
                    onClick={() => setImages((prev) => prev.filter((_, j) => j !== i))}
                  >
                    ✕
                  </button>
                </span>
              ))}
            </div>
          )}
          <div className="flex gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept="image/png,image/jpeg,image/webp,image/gif"
              multiple
              className="hidden"
              onChange={(e) => {
                if (e.target.files) void attachFiles(e.target.files);
                e.target.value = '';
              }}
            />
            <button
              type="button"
              aria-label="Adjuntar imagen"
              title="Adjuntar imagen (o pegue una captura con Ctrl+V)"
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full border border-slate-300 text-slate-500 hover:bg-slate-50"
              onClick={() => fileInputRef.current?.click()}
            >
              <ClipIcon />
            </button>
            <input
              className="flex-1 rounded-full border border-slate-300 px-4 py-2.5 text-sm focus:border-slate-400 focus:outline-none"
              placeholder="Consúltale cualquier duda al Copilot…"
              value={text}
              onChange={(e) => setText(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void send();
              }}
              onPaste={(e) => {
                const files = Array.from(e.clipboardData.files).filter((f) => f.type.startsWith('image/'));
                if (files.length > 0) {
                  e.preventDefault();
                  void attachFiles(files);
                }
              }}
            />
            <BrandButton
              className="!h-10 !w-10 !rounded-full !p-0"
              disabled={!text.trim() && images.length === 0}
              loading={status === 'sending'}
              loadingText=""
              onClick={() => void send()}
            >
              <SendIcon />
            </BrandButton>
          </div>
        </div>
      </div>
    </div>
  );
}
