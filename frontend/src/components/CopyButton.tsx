import { Button, App, Tooltip } from 'antd';
import { CopyOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

interface CopyButtonProps {
  text: string;
  size?: 'small' | 'middle' | 'large';
  type?: 'text' | 'link' | 'default';
  tooltip?: string;
}

/**
 * One-click copy with toast feedback (design spec §14.3).
 */
export default function CopyButton({ text, size = 'small', type = 'text', tooltip }: CopyButtonProps) {
  const { t } = useTranslation();
  const { message } = App.useApp();

  const handleCopy = async (e: React.MouseEvent) => {
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(text);
      message.success(t('common.copied'));
    } catch {
      // Clipboard API unavailable (non-secure context): fallback.
      const ta = document.createElement('textarea');
      ta.value = text;
      document.body.appendChild(ta);
      ta.select();
      document.execCommand('copy');
      document.body.removeChild(ta);
      message.success(t('common.copied'));
    }
  };

  return (
    <Tooltip title={tooltip ?? t('common.copy')}>
      <Button aria-label={t('common.copy')} type={type} size={size} icon={<CopyOutlined />} onClick={handleCopy} />
    </Tooltip>
  );
}
