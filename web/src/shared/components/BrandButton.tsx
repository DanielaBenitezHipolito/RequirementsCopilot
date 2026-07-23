import type { ButtonHTMLAttributes } from 'react';

const BRAND = '#1e2a5a';
const GOLD = '#f59e0b';

interface Props extends ButtonHTMLAttributes<HTMLButtonElement> {
  loading?: boolean;
  loadingText?: string;
  icon?: string;
  sparkle?: boolean;
  variant?: 'primary' | 'secondary';
}

function SparkleIcon() {
  return (
    <svg viewBox="0 0 24 24" fill={GOLD} className="h-4 w-4 shrink-0">
      <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
    </svg>
  );
}

export function BrandButton({
  loading,
  loadingText,
  icon,
  sparkle,
  variant = 'primary',
  children,
  disabled,
  className,
  ...rest
}: Props) {
  const isDisabled = disabled || loading;
  const isSecondary = variant === 'secondary';

  const variantClass = isSecondary
    ? 'border border-slate-300 bg-white text-slate-800 shadow-sm hover:bg-slate-50'
    : `shadow-md ${isDisabled ? 'bg-slate-300 text-slate-500' : 'text-white'}`;

  return (
    <button
      type="button"
      {...rest}
      disabled={isDisabled}
      className={`rounded-lg px-6 py-3 text-sm font-semibold hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-100 ${variantClass} ${className ?? ''}`}
      style={!isSecondary && !isDisabled ? { backgroundColor: BRAND } : undefined}
    >
      {loading ? (
        <span className="inline-flex items-center gap-2">
          <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-slate-400 border-t-transparent" />
          {loadingText ?? '…con IA…'}
        </span>
      ) : (
        <span className="inline-flex items-center gap-1.5">
          {sparkle && <SparkleIcon />}
          {icon && <span>{icon}</span>}
          {children}
        </span>
      )}
    </button>
  );
}
