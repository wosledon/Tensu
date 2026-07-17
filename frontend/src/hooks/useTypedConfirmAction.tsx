import { useCallback } from 'react';
import { App, Input } from 'antd';
import { useTranslation } from 'react-i18next';

interface TypedConfirmOptions {
  title: string;
  expectedText: string;
  action: () => Promise<void>;
  onSuccess?: () => void;
}

/**
 * Double confirmation for sensitive operations (delete provider, revoke key, ...).
 * The user must type the entity name exactly before the action executes.
 */
export function useTypedConfirmAction() {
  const { t } = useTranslation();
  const { message, modal } = App.useApp();

  const confirmAction = useCallback(
    ({ title, expectedText, action, onSuccess }: TypedConfirmOptions) => {
      let typed = '';
      modal.confirm({
        title,
        content: (
          <div>
            <p>{t('common.typedConfirmHint', { name: expectedText })}</p>
            <Input
              placeholder={expectedText}
              onChange={(e) => { typed = e.target.value; }}
            />
          </div>
        ),
        okType: 'danger',
        okText: t('common.confirm'),
        cancelText: t('common.cancel'),
        onOk: async () => {
          if (typed.trim() !== expectedText) {
            message.error(t('common.typedConfirmMismatch'));
            return Promise.reject(new Error('typed confirmation mismatch'));
          }
          try {
            await action();
            message.success(t('common.success'));
            onSuccess?.();
          } catch (e: any) {
            message.error(e?.message || t('common.error'));
            return Promise.reject(e);
          }
        },
      });
    },
    [t, modal, message]
  );

  return { confirmAction };
}

