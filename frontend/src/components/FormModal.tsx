import { Modal, Form, type FormInstance } from 'antd';
import { useTranslation } from 'react-i18next';

interface FormModalProps {
  title: string;
  header?: React.ReactNode;
  open: boolean;
  form: FormInstance;
  editing: boolean;
  submitting?: boolean;
  width?: number;
  onOk: () => void;
  onCancel: () => void;
  children: React.ReactNode;
}

export default function FormModal({
  title, header, open, form, editing, submitting, width = 560, onOk, onCancel, children,
}: FormModalProps) {
  const { t } = useTranslation();

  const modalTitle = header ?? (editing ? `${t('common.edit')} - ${title}` : `${t('common.create')} - ${title}`);

  return (
    <Modal
      title={modalTitle}
      open={open}
      onOk={onOk}
      onCancel={onCancel}
      confirmLoading={submitting}
      width={width}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
        {children}
      </Form>
    </Modal>
  );
}
