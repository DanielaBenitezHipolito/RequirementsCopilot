export interface Criterion { nombre: string; score: number; observacion: string }
export interface Evaluacion {
  requirementCode: string; criterios: Criterion[]; promedio: number; umbral: number; pasa: boolean;
}
export interface Aclaracion { pregunta: string; respuesta?: string }
export interface Caso { titulo: string; precondiciones: string[]; pasos: string[]; resultadoEsperado: string }
export interface Historia {
  rol: string; quiero: string; para: string; criteriosAceptacion: string[]; caso?: Caso;
}
export interface RequirementView {
  codigo: string; texto: string; area: string; evaluacion?: Evaluacion;
  aclaraciones: Aclaracion[]; listoParaHistorias?: boolean; historias: Historia[];
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
export interface ConversationDetailDto {
  id: string; status: 'Abierta' | 'Completada'; analysisId?: string; lastResponseId?: string;
  messages: { role: 'user' | 'agent'; text: string }[];
}
