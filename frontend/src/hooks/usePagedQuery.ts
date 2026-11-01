import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../api/client';
import type { PagedParams, PagedResult } from '../api/types';

export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return debounced;
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return 'Ocurrió un error inesperado.';
}

type Filters = Record<string, string | number | boolean | undefined>;

interface Options<F extends Filters> {
  initialFilters?: F;
  initialSort?: { sortBy?: string; sortDir?: 'asc' | 'desc' };
  pageSize?: number;
}

/**
 * Estado completo de una tabla paginada: página, tamaño, búsqueda (con debounce), filtros y orden.
 * Cancela la petición anterior si cambian los parámetros (AbortController) para evitar resultados desordenados.
 */
export function usePagedQuery<T, F extends Filters = Filters>(
  fetcher: (params: PagedParams, signal: AbortSignal) => Promise<PagedResult<T>>,
  options: Options<F> = {},
) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(options.pageSize ?? 10);
  const [search, setSearchState] = useState('');
  const [filters, setFilters] = useState<F>(options.initialFilters ?? ({} as F));
  const [sortBy, setSortBy] = useState(options.initialSort?.sortBy);
  const [sortDir, setSortDir] = useState<'asc' | 'desc' | undefined>(options.initialSort?.sortDir);
  const [data, setData] = useState<PagedResult<T>>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [version, setVersion] = useState(0);

  const debouncedSearch = useDebouncedValue(search);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    fetcher({ page, pageSize, search: debouncedSearch || undefined, sortBy, sortDir, ...filters }, controller.signal)
      .then((result) => setData(result))
      .catch((e: unknown) => {
        if ((e as Error).name !== 'AbortError') setError(errorMessage(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
    // fetcher es estable (función de módulo); se omite a propósito para no re-ejecutar en cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, pageSize, debouncedSearch, sortBy, sortDir, filters, version]);

  const setSearch = useCallback((value: string) => {
    setSearchState(value);
    setPage(1);
  }, []);

  const setFilter = useCallback(<K extends keyof F>(key: K, value: F[K]) => {
    setFilters((current) => ({ ...current, [key]: value }));
    setPage(1);
  }, []);

  const setPageSize = useCallback((size: number) => {
    setPageSizeState(size);
    setPage(1);
  }, []);

  const toggleSort = useCallback(
    (key: string) => {
      if (sortBy === key) {
        setSortDir((dir) => (dir === 'asc' ? 'desc' : 'asc'));
      } else {
        setSortBy(key);
        setSortDir('asc');
      }
    },
    [sortBy],
  );

  const reload = useCallback(() => setVersion((v) => v + 1), []);

  return { data, loading, error, page, setPage, pageSize, setPageSize, search, setSearch, filters, setFilter, sortBy, sortDir, toggleSort, reload };
}
