import { Tag, Space } from 'antd';
import { useTranslation } from 'react-i18next';

interface CapabilityTagsProps {
  vision?: boolean;
  reasoning?: boolean;
  toolUse?: boolean;
  thinking?: boolean;
}

export default function CapabilityTags({ vision, reasoning, toolUse, thinking }: CapabilityTagsProps) {
  const { t } = useTranslation();

  const items = [
    { active: vision, label: t('model.vision'), color: '#007AFF' },
    { active: reasoning, label: t('model.reasoning'), color: '#AF52DE' },
    { active: toolUse, label: t('model.toolUse'), color: '#34C759' },
    { active: thinking, label: t('model.thinking'), color: '#FF9500' },
  ];

  return (
    <Space size={4} wrap>
      {items.map((item) =>
        item.active ? (
          <Tag key={item.label} color="blue" style={{ borderRadius: 8, fontSize: 12 }}>{item.label}</Tag>
        ) : (
          <Tag key={item.label} style={{ borderRadius: 8, fontSize: 12, opacity: 0.45 }}>{item.label}</Tag>
        )
      )}
    </Space>
  );
}
