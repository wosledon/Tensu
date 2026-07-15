import { Tag, Space, theme } from 'antd';
import { useTranslation } from 'react-i18next';

interface CapabilityTagsProps {
  vision?: boolean;
  reasoning?: boolean;
  toolUse?: boolean;
  thinking?: boolean;
}

export default function CapabilityTags({ vision, reasoning, toolUse, thinking }: CapabilityTagsProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();

  const items = [
    { active: vision, label: t('model.vision'), color: token.colorInfo },
    { active: reasoning, label: t('model.reasoning'), color: token.colorPrimary },
    { active: toolUse, label: t('model.toolUse'), color: token.colorSuccess },
    { active: thinking, label: t('model.thinking'), color: token.colorWarning },
  ];

  return (
    <Space size={4} wrap>
      {items.map((item) => (
        <Tag
          key={item.label}
          style={{
            borderRadius: 8,
            fontSize: 12,
            color: item.active ? item.color : token.colorTextDisabled,
            borderColor: item.active ? item.color : token.colorBorder,
            background: item.active ? `${item.color}15` : 'transparent',
            opacity: item.active ? 1 : 0.65,
          }}
        >
          {item.label}
        </Tag>
      ))}
    </Space>
  );
}
