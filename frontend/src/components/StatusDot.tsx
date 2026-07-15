import { Tag, theme } from 'antd';

interface StatusDotProps {
  color: 'success' | 'warning' | 'error' | 'default' | 'processing';
  text: string;
}

export default function StatusDot({ color, text }: StatusDotProps) {
  const { token } = theme.useToken();
  const colorMap: Record<StatusDotProps['color'], string> = {
    success: token.colorSuccess,
    warning: token.colorWarning,
    error: token.colorError,
    processing: token.colorPrimary,
    default: token.colorTextTertiary,
  };

  return (
    <Tag style={{ borderRadius: 8, display: 'inline-flex', alignItems: 'center', gap: 6, paddingInline: 8, borderColor: 'transparent', background: token.colorFillTertiary }}>
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
