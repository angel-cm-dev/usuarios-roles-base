import { ChevronLeft, ChevronRight } from 'lucide-react';

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
}

export function Pagination({ page, totalPages, totalCount, pageSize, onPageChange, onPageSizeChange }: PaginationProps) {
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  const canPrev = page > 1;
  const canNext = page < totalPages;

  return (
    <nav aria-label="Paginación" className="flex flex-col items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm dark:border-slate-800 sm:flex-row">
      <p className="text-slate-500 dark:text-slate-400">
        Mostrando <strong>{from}</strong>–<strong>{to}</strong> de <strong>{totalCount}</strong>
      </p>
      <div className="flex items-center gap-2">
        {onPageSizeChange && (
          <select
            aria-label="Filas por página"
            className="input w-auto py-1"
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
          >
            {[10, 20, 50].map((size) => (
              <option key={size} value={size}>
                {size} / pág.
              </option>
            ))}
          </select>
        )}
        <button
          type="button"
          className="rounded-lg border border-slate-300 p-1.5 disabled:opacity-40 dark:border-slate-700"
          onClick={() => onPageChange(page - 1)}
          disabled={!canPrev}
          aria-label="Página anterior"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="min-w-[5rem] text-center">
          Página {totalPages === 0 ? 0 : page} de {totalPages}
        </span>
        <button
          type="button"
          className="rounded-lg border border-slate-300 p-1.5 disabled:opacity-40 dark:border-slate-700"
          onClick={() => onPageChange(page + 1)}
          disabled={!canNext}
          aria-label="Página siguiente"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>
    </nav>
  );
}
