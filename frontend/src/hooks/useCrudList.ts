import { useState, useEffect, useCallback } from 'react';
import { App } from 'antd';
import { useTranslation } from 'react-i18next';
import type { PagedRequest, PagedResult } from '../types';

interface UseCrudListOptions<T, P extends PagedRequest> {
  fetchFn: (params: P) => Promise<PagedResult<T>>;
  defaultParams?: Partial<P>;
  autoFetch?: boolean;
}

export function useCrudList<T, P extends PagedRequest>({
  fetchFn,
  defaultParams,
  autoFetch = true,
}: UseCrudListOptions<T, P>) {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [data, setData] = useState<T[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [params, setParams] = useState<P>({
    page: 1,
    pageSize: 20,
    ...defaultParams,
  } as P);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await fetchFn(params);
      setData(res.items);
      setTotal(res.total);
    } catch {
      message.error(t('common.error'));
    } finally {
      setLoading(false);
    }
  }, [fetchFn, params, t, message]);

  useEffect(() => {
    if (autoFetch) fetchData();
  }, [fetchData, autoFetch]);

  const setPage = (page: number, pageSize?: number) => {
    setParams((prev) => ({ ...prev, page, pageSize: pageSize ?? prev.pageSize }));
  };

  const setSort = (sortBy: string, sortOrder: 'asc' | 'desc') => {
    setParams((prev) => ({ ...prev, sortBy, sortOrder, page: 1 }));
  };

  const setKeyword = (keyword?: string) => {
    setParams((prev) => ({ ...prev, keyword, page: 1 }));
  };

  const setExtra = (extra: Partial<P>) => {
    setParams((prev) => ({ ...prev, ...extra, page: 1 }));
  };

  return {
    data, total, loading, params,
    fetchData, setPage, setSort, setKeyword, setExtra, setParams,
  };
}
