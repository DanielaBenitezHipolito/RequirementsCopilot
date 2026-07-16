import type { AnalysisSummary } from '../types';

export const API_BASE: string = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100';

export async function analyzeFile(file: File): Promise<Response> {
  const form = new FormData();
  form.append('file', file);
  const response = await fetch(`${API_BASE}/api/analyses`, { method: 'POST', body: form });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje ?? `Error ${response.status}`);
  }
  return response;
}

export async function getAnalyses(): Promise<AnalysisSummary[]> {
  const response = await fetch(`${API_BASE}/api/analyses`);
  if (!response.ok) throw new Error('No se pudo cargar el historial.');
  return response.json();
}

export async function getAnalysis(id: string): Promise<any> {
  const response = await fetch(`${API_BASE}/api/analyses/${id}`);
  if (!response.ok) throw new Error('No se pudo cargar el análisis.');
  return response.json();
}

async function readOrThrow(response: Response): Promise<any> {
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.mensaje ?? `Error ${response.status}`);
  return body;
}

export async function answerClarifications(id: string, codigo: string, respuestas: string[]): Promise<any> {
  const response = await fetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/clarifications`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ respuestas }),
  });
  return readOrThrow(response);
}

export async function generateStories(id: string, codigo: string): Promise<any> {
  const response = await fetch(`${API_BASE}/api/analyses/${id}/requirements/${codigo}/stories`, { method: 'POST' });
  return readOrThrow(response);
}
