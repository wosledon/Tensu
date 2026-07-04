import { useCallback } from 'react';
import { App } from 'antd';
import { useTranslation } from 'react-i18next';

export function useConfirmDelete(deleteFn: (id: number) => Promise<void>, onSuccess?: () => void) {
  const { t } = useTranslation();
  const { message, modal } = App.useApp();

  const handleDelete = useCallback(
    (id: number) => {
      modal.confirm({
        title: t('common.deleteConfirm'),
        okType: 'danger',
        okText: t('common.confirm'),
        cancelText: t('common.cancel'),
        onOk: async () => {
          try {
            await deleteFn(id);
            message.success(t('common.success'));
            onSuccess?.();
          } catch (e: any) {
            message.error(e?.message || t('common.error'));
          }
        },
      });
    },
    [deleteFn, onSuccess, t]
  );

  return { handleDelete };
}
