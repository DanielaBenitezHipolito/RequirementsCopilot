import type { ButtonHTMLAttributes } from 'react';

const BRAND = '#1e2a5a';

interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  loading?: boolean;
  loadingText?: string;
  icon?: string;
}

export function BrandButton({ loading, loadingText, icon, children, disabled, className, ...rest }: Props) {
  const isDisabled = disabled || loading;
  return (
    <button
      type="button"
      {...rest}
      disabled={isDisabled}
      className={`rounded-lg px-5 py-2.5 text-sm font-semibold hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-100 ${
        isDisabled ? 'bg-slate-300 text-slate-500' : 'text-white'
      } ${className ?? ''}`}
      style={isDisabled ? undefined : { backgroundColor: BRAND }}
    >
      {loading ? (
        <span className="inline-flex items-center gap-2">
          <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-slate-400 border-t-transparent" />
          {loadingText ?? '…con IA…'}
        </span>
      ) : (
        <span className="inline-flex items-center gap-1.5">
          {icon && <span>{icon}</span>}
          {children}
        </span>
      )}
    </button>
  );
}
