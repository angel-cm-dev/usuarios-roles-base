import type { ReactNode } from 'react';
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import { cn } from '../../lib/format';
import { EmptyState, ErrorState } from './Feedback';
import { Spinner } from './Spinner';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  /** Si se define, la columna es ordenable por este campo de la API. */
  sortKey?: string;
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[] | undefined;
  rowKey: (row: T) => string;
  loading: boolean;
  error?: string | null;
  onRetry?: () => void;
  emptyTitle?: string;
  emptyDescription?: string;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  onSort?: (key: string) => void;
  footer?: ReactNode;
}

export function DataTable<T>({
  columns,
  rows,
  rowKey,
  loading,
  error,
  onRetry,
  emptyTitle,
  emptyDescription,
  sortBy,
  sortDir,
  onSort,
  footer,
}: DataTableProps<T>) {
  const isEmpty = !loading && !error && rows?.length === 0;

  return (
    <div className="card overflow-hidden">
      <div className="relative overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
          <thead className="bg-slate-50 dark:bg-slate-900/60">
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  scope="col"
                  className={cn('whitespace-nowrap px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500', column.className)}
                  aria-sort={column.sortKey && sortBy === column.sortKey ? (sortDir === 'desc' ? 'descending' : 'ascending') : undefined}
                >
                  {column.sortKey && onSort ? (
                    <button type="button" className="inline-flex items-center gap-1 hover:text-slate-800 dark:hover:text-slate-200" onClick={() => onSort(column.sortKey!)}>
                      {column.header}
                      {sortBy === column.sortKey ? (
                        sortDir === 'desc' ? <ArrowDown className="h-3 w-3" /> : <ArrowUp className="h-3 w-3" />
                      ) : (
                        <ArrowUpDown className="h-3 w-3 opacity-40" />
                      )}
                    </button>
                  ) : (
                    column.header
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
            {!error &&
              rows?.map((row) => (
                <tr key={rowKey(row)} className="hover:bg-slate-50/70 dark:hover:bg-slate-800/40">
                  {columns.map((column) => (
                    <td key={column.key} className={cn('px-4 py-3 align-middle', column.className)}>
                      {column.render(row)}
                    </td>
                  ))}
                </tr>
              ))}
          </tbody>
        </table>
        {loading && (
          <div className={cn('flex items-center justify-center', rows?.length ? 'absolute inset-0 bg-white/60 dark:bg-slate-900/60' : 'py-12')}>
            <Spinner className="h-6 w-6" />
          </div>
        )}
      </div>
      {error && <ErrorState message={error} onRetry={onRetry} />}
      {isEmpty && <EmptyState title={emptyTitle} description={emptyDescription} />}
      {footer}
    </div>
  );
}
