import { useCallback } from 'react';
import { Modal, message } from 'antd';
import { useTranslation } from 'react-i18next';

export function useConfirmDelete(deleteFn: (id: number) => Promise<void>, onSuccess?: () => void) {
  const { t } = useTranslation();

  const handleDelete = useCallback(
    (id: number) => {
      Modal.confirm({
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
