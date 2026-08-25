export interface Criterion { nombre: string; score: number; observacion: string }
export interface Evaluacion {
  requirementCode: string; criterios: Criterion[]; promedio: number; umbral: number; pasa: boolean;
}
export interface Aclaracion { pregunta: string; respuesta?: string }
export interface Actor { nombre: string; descripcion: string }
export interface PasoFlujo { numero: number; accion: string; resultadoEsperado: string }
export interface Flujo { titulo: string; pasos: PasoFlujo[] }
export interface CasoDeUso {
  nombre: string; objetivo: string; descripcion: string; actores: Actor[]; precondiciones: string[];
  trigger: string; flujos: Flujo[]; extensiones: string[]; frecuencia: string; importancia: string;
  urgencia: string; comentarios: string[];
}
export interface RequirementView {
  codigo: string; texto: string; area: string; evaluacion?: Evaluacion;
  aclaraciones: Aclaracion[]; listoParaHistorias?: boolean; caso?: CasoDeUso;
}
export interface AnalysisSummary {
  id: string; fileName: string; createdAt: string; status: string;
  totalRequerimientos: number; aprobados: number;
}
export interface SseEvent { event: string; data: any }
export interface ConversationSummary {
  id: string; createdAt: string; updatedAt: string; status: 'Abierta' | 'Completada';
  analysisId?: string; preview: string;
}
export interface ProjectSummary { nombre: string; updatedAt: string }
export interface ConversationDetailDto {
  id: string; status: 'Abierta' | 'Completada'; analysisId?: string; lastResponseId?: string; proyecto?: string;
  messages: { role: 'user' | 'agent'; text: string }[];
}
