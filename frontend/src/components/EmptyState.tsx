import { Empty, Button } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

interface EmptyStateProps {
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
}

/**
 * Unified empty state with optional guiding action (design spec §7.2).
 */
export default function EmptyState({ description, actionLabel, onAction }: EmptyStateProps) {
  const { t } = useTranslation();

  return (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={description ?? t('common.noData')}
      style={{ padding: '48px 0' }}
    >
      {onAction && (
        <Button type="primary" icon={<PlusOutlined />} onClick={onAction}>
          {actionLabel ?? t('common.create')}
        </Button>
      )}
    </Empty>
  );
}
