import { Tag } from 'antd';

interface StatusDotProps {
  color: 'success' | 'warning' | 'error' | 'default' | 'processing';
  text: string;
}

const colorMap: Record<string, string> = {
  success: '#34C759',
  warning: '#FF9500',
  error: '#FF3B30',
  processing: '#AF52DE',
  default: '#8E8E93',
};

export default function StatusDot({ color, text }: StatusDotProps) {
  return (
    <Tag style={{ borderRadius: 8, display: 'inline-flex', alignItems: 'center', gap: 6, paddingInline: 8 }}>
      <span
        style={{
          width: 7, height: 7, borderRadius: '50%',
          backgroundColor: colorMap[color] || colorMap.default,
          display: 'inline-block', flexShrink: 0,
        }}
      />
      <span>{text}</span>
    </Tag>
  );
}
