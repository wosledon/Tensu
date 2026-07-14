import { useState } from 'react';
import { Form, App } from 'antd';
import { useTranslation } from 'react-i18next';

interface UseFormModalOptions<T> {
  createFn: (data: Partial<T>) => Promise<T>;
  updateFn?: (id: number, data: Partial<T>) => Promise<T>;
  onSuccess?: () => void;
  transformRecord?: (record: T) => T;
  transformSubmit?: (values: Partial<T>) => Partial<T>;
}

export function useFormModal<T extends { id?: number }>({
  createFn,
  updateFn,
  onSuccess,
  transformRecord,
  transformSubmit,
}: UseFormModalOptions<T>) {
  const [form] = Form.useForm();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<T | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const { message } = App.useApp();
  const { t } = useTranslation();

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    setOpen(true);
  };

  const openEdit = (record: T) => {
    const transformed = transformRecord ? transformRecord(record) : record;
    setEditing(transformed);
    form.setFieldsValue(transformed);
    setOpen(true);
  };

  const close = () => {
    setOpen(false);
    setEditing(null);
    form.resetFields();
  };

  const submit = async () => {
    try {
      const values = await form.validateFields();
      const payload = transformSubmit ? transformSubmit(values) : values;
      setSubmitting(true);
      if (editing?.id && updateFn) {
        await updateFn(editing.id, payload);
      } else {
        await createFn(payload);
      }
      close();
      onSuccess?.();
    } catch (err: any) {
      if (err?.errorFields) return;
      message.error(err?.message || t('common.error'));
    } finally {
      setSubmitting(false);
    }
  };

  return {
    form, open, editing, submitting,
    openCreate, openEdit, close, submit,
  };
}
