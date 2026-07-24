const BRAND = '#1e2a5a';

/** Spinner centrado con texto opcional para estados de carga (GET lentos, cold start de Render). */
export function Loading({ text = 'Cargando…' }: { text?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-center">
      <span
        className="h-8 w-8 animate-spin rounded-full border-4 border-slate-200"
        style={{ borderTopColor: BRAND }}
      />
      <p className="text-sm text-slate-500">{text}</p>
    </div>
  );
}
