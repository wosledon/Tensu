import { useState } from 'react';
import { Form } from 'antd';

interface UseFormModalOptions<T> {
  createFn: (data: Partial<T>) => Promise<T>;
  updateFn?: (id: number, data: Partial<T>) => Promise<T>;
  onSuccess?: () => void;
}

export function useFormModal<T extends { id?: number }>({
  createFn,
  updateFn,
  onSuccess,
}: UseFormModalOptions<T>) {
  const [form] = Form.useForm();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<T | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    setOpen(true);
  };

  const openEdit = (record: T) => {
    setEditing(record);
    form.setFieldsValue(record);
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
      setSubmitting(true);
      if (editing?.id && updateFn) {
        await updateFn(editing.id, values);
      } else {
        await createFn(values);
      }
      close();
      onSuccess?.();
    } catch {
      // validation error or API error
    } finally {
      setSubmitting(false);
    }
  };

  return {
    form, open, editing, submitting,
    openCreate, openEdit, close, submit,
  };
}
