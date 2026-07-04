import { message } from 'antd';
import { useTranslation } from 'react-i18next';

export interface ApiError {
  code: number;
  message: string;
  data: unknown;
}

export function getApiErrorMessage(error: unknown, fallback?: string): string {
  if (typeof error === 'string') return error;

  if (error && typeof error === 'object' && 'response' in error) {
    const axiosError = error as { response?: { data?: ApiError } };
    const apiError = axiosError.response?.data;
    if (apiError?.message) {
      return apiError.message;
    }
  }

  if (error && typeof error === 'object' && 'message' in error) {
    return (error as Error).message;
  }

  return fallback || 'Operation failed';
}

export function showApiError(error: unknown, fallback?: string) {
  const msg = getApiErrorMessage(error, fallback);
  message.error(msg);
}

export function useApiError() {
  const { t } = useTranslation();

  const handleError = (error: unknown, fallback?: string) => {
    const msg = getApiErrorMessage(error, fallback || t('common.error'));
    message.error(msg);
  };

  return { handleError, getApiErrorMessage };
}
