import type { AnalysisSummary, ConversationDetailDto, ConversationSummary, ProjectDetail, ProjectSummary } from '../types';

export const API_BASE: string = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100';

// ---- Autenticación (ADR-0010): token en memoria + localStorage; viaja en cada request. ----

let authToken: string | null = localStorage.getItem('rc_token');

export function setAuthToken(token: string | null) {
  authToken = token;
  if (token) localStorage.setItem('rc_token', token);
  else localStorage.removeItem('rc_token');
}

async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers);
  if (authToken) headers.set('Authorization', `Bearer ${authToken}`);
  const response = await fetch(input, { ...init, headers });
  if (response.status === 401 && authToken) {
    // Token vencido o revocado: la app vuelve al login.
    window.dispatchEvent(new Event('auth:expired'));
  }
  return response;
}

export interface AuthUser { email: string; nombre: string; rol: string }

export async function getAuthStatus(): Promise<{ habilitado: boolean }> {
  const response = await fetch(`${API_BASE}/api/auth/status`);
  if (!response.ok) throw new Error('No se pudo consultar el estado de autenticación.');
  return response.json();
}

export async function login(email: string, password: string): Promise<AuthUser & { token: string }> {
  const response = await fetch(`${API_BASE}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  return readOrThrow(response);
}

export async function getUsers(): Promise<{ email: string; nombre: string; rol: string; createdAt: string }[]> {
  return readOrThrow(await apiFetch(`${API_BASE}/api/users`));
}

export async function upsertUser(email: string, nombre: string, rol: string, password: string): Promise<void> {
  await readOrThrow(await apiFetch(`${API_BASE}/api/users`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, nombre, rol, password }),
  }));
}

export async function deleteUser(email: string): Promise<void> {
  const response = await apiFetch(`${API_BASE}/api/users/${encodeURIComponent(email)}`, { method: 'DELETE' });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
}

export async function getUsage(dias = 30): Promise<any> {
  return readOrThrow(await apiFetch(`${API_BASE}/api/admin/usage?dias=${dias}`));
}

export async function analyzeFile(file: File, proyecto?: string): Promise<Response> {
  const form = new FormData();
  form.append('file', file);
  if (proyecto) form.append('proyecto', proyecto);
  const response = await apiFetch(`${API_BASE}/api/analyses`, { method: 'POST', body: form });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
  return response;
}

export async function getAnalyses(): Promise<AnalysisSummary[]> {
  const response = await apiFetch(`${API_BASE}/api/analyses`);
  if (!response.ok) throw new Error('No se pudo cargar el historial.');
  return response.json();
}

export async function getAnalysis(id: string): Promise<any> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}`);
  if (!response.ok) throw new Error('No se pudo cargar el análisis.');
  return response.json();
}

export async function deleteAnalysis(id: string): Promise<void> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}`, { method: 'DELETE' });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
}

async function readOrThrow(response: Response): Promise<any> {
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.mensaje ?? `Error ${response.status}`);
  return body;
}

export async function answerClarifications(id: string, codigo: string, respuestas: string[]): Promise<any> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/clarifications`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ respuestas }),
  });
  return readOrThrow(response);
}

export async function generateStories(id: string, codigo: string, regenerar = false): Promise<any> {
  const query = regenerar ? '?regenerar=true' : '';
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/stories${query}`, { method: 'POST' });
  return readOrThrow(response);
}

export async function generateUserStories(id: string, codigo: string, regenerar = false): Promise<any> {
  const query = regenerar ? '?regenerar=true' : '';
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/user-stories${query}`, { method: 'POST' });
  return readOrThrow(response);
}

export async function setRequirementStage(id: string, codigo: string, etapa: string): Promise<any> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/stage`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ etapa }),
  });
  return readOrThrow(response);
}

export async function updateUseCase(id: string, codigo: string, caso: unknown): Promise<any> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/use-case`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(caso),
  });
  return readOrThrow(response);
}

export async function reevaluateRequirement(id: string, codigo: string, respuestas: string[]): Promise<any> {
  const response = await apiFetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/reevaluate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ respuestas }),
  });
  return readOrThrow(response);
}

export async function sendConversationMessage(
  mensaje: string,
  previousResponseId?: string,
  conversationId?: string,
  proyecto?: string,
  imagenes?: string[],
): Promise<Response> {
  const response = await apiFetch(`${API_BASE}/api/conversations/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      mensaje,
      previousResponseId: previousResponseId ?? null,
      conversationId: conversationId ?? null,
      proyecto: proyecto ?? null,
      imagenes: imagenes && imagenes.length > 0 ? imagenes : null,
    }),
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
  return response;
}

export async function completeConversation(
  texto: string,
  area: string,
  conversationId?: string,
  proyecto?: string,
): Promise<{ analysisId: string }> {
  const response = await apiFetch(`${API_BASE}/api/conversations/complete`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ texto, area, conversationId: conversationId ?? null, proyecto: proyecto ?? null }),
  });
  return readOrThrow(response);
}

export async function getConversations(): Promise<ConversationSummary[]> {
  const response = await apiFetch(`${API_BASE}/api/conversations`);
  if (!response.ok) throw new Error('No se pudo cargar el historial de conversaciones.');
  return response.json();
}

export async function getConversation(id: string): Promise<ConversationDetailDto> {
  const response = await apiFetch(`${API_BASE}/api/conversations/${id}`);
  return readOrThrow(response);
}

export async function extractDocumentText(file: File): Promise<{ texto: string }> {
  const form = new FormData();
  form.append('file', file);
  const response = await apiFetch(`${API_BASE}/api/documents/extract`, { method: 'POST', body: form });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
  return response.json();
}

export async function getProjects(): Promise<ProjectSummary[]> {
  const response = await apiFetch(`${API_BASE}/api/projects`);
  if (!response.ok) throw new Error('No se pudieron cargar los proyectos.');
  return response.json();
}

export async function uploadProject(file: File, nombre?: string): Promise<{ nombre: string }> {
  const form = new FormData();
  form.append('file', file);
  if (nombre) form.append('nombre', nombre);
  const response = await apiFetch(`${API_BASE}/api/projects`, { method: 'POST', body: form });
  return readOrThrow(response);
}

export async function createEmptyProject(nombre: string): Promise<{ nombre: string }> {
  const form = new FormData();
  form.append('nombre', nombre);
  const response = await apiFetch(`${API_BASE}/api/projects`, { method: 'POST', body: form });
  return readOrThrow(response);
}

export async function appendProjectDocument(nombre: string, file: File): Promise<{ nombre: string }> {
  const form = new FormData();
  form.append('file', file);
  const response = await apiFetch(`${API_BASE}/api/projects/${encodeURIComponent(nombre)}/documents`, {
    method: 'POST',
    body: form,
  });
  return readOrThrow(response);
}

export async function getProjectUseCases(nombre: string): Promise<any[]> {
  const response = await apiFetch(`${API_BASE}/api/projects/${encodeURIComponent(nombre)}/use-cases`);
  return readOrThrow(response);
}

export async function getProject(nombre: string): Promise<ProjectDetail> {
  const response = await apiFetch(`${API_BASE}/api/projects/${encodeURIComponent(nombre)}`);
  return readOrThrow(response);
}

export async function updateProject(nombre: string, contenido: string): Promise<{ nombre: string }> {
  const response = await apiFetch(`${API_BASE}/api/projects/${encodeURIComponent(nombre)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ contenido }),
  });
  return readOrThrow(response);
}

export async function deleteProject(nombre: string): Promise<void> {
  const response = await apiFetch(`${API_BASE}/api/projects/${encodeURIComponent(nombre)}`, { method: 'DELETE' });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
}
