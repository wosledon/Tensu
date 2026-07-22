import { Card, theme } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useCountUp } from '../hooks/useCountUp';
import Sparkline from './Sparkline';

interface StatCardProps {
  title: string;
  value: number;
  icon?: React.ReactNode;
  /** Accent color for icon badge, value and sparkline. */
  color?: string;
  prefix?: string;
  suffix?: string;
  precision?: number;
  /** Previous period value for the trend badge (e.g. yesterday vs today). */
  compareValue?: number;
  /** Mini trend series rendered at the bottom. */
  sparkData?: number[];
  onClick?: () => void;
}

function formatNumber(v: number, precision?: number): string {
  if (precision != null) return v.toFixed(precision);
  if (Math.abs(v) >= 1_000_000) return `${(v / 1_000_000).toFixed(2)}M`;
  if (Math.abs(v) >= 10_000) return `${(v / 1_000).toFixed(1)}K`;
  return Math.round(v).toLocaleString();
}

/**
 * Unified stat card (design spec §7.5): icon badge + title + animated big
 * number + period-over-period trend + sparkline.
 */
export default function StatCard({
  title, value, icon, color = '#007AFF', prefix, suffix, precision,
  compareValue, sparkData, onClick,
}: StatCardProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const animated = useCountUp(value);

  const hasCompare = compareValue != null && compareValue !== 0;
  const delta = hasCompare ? ((value - compareValue!) / Math.abs(compareValue!)) * 100 : 0;
  const trendUp = delta >= 0;

  return (
    <Card
      hoverable={!!onClick}
      onClick={onClick}
      style={{ borderRadius: token.borderRadiusLG, height: '100%', cursor: onClick ? 'pointer' : undefined }}
      styles={{ body: { padding: '18px 20px' } }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ fontSize: 13, color: token.colorTextSecondary, marginBottom: 6, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {title}
          </div>
          <div style={{
            fontSize: 26, fontWeight: 600, lineHeight: 1.2, color,
            fontFamily: '"SF Mono", ui-monospace, monospace',
            letterSpacing: '-0.01em',
          }}>
            {prefix}{formatNumber(animated, precision)}{suffix}
          </div>
          {hasCompare && (
            <div style={{ marginTop: 6, fontSize: 12, display: 'flex', alignItems: 'center', gap: 4 }}>
              <span style={{ color: trendUp ? token.colorSuccess : token.colorError, display: 'inline-flex', alignItems: 'center', gap: 2, fontWeight: 500 }}>
                {trendUp ? <ArrowUpOutlined style={{ fontSize: 10 }} /> : <ArrowDownOutlined style={{ fontSize: 10 }} />}
                {Math.abs(delta).toFixed(1)}%
              </span>
              <span style={{ color: token.colorTextSecondary }}>{t('common.vsYesterday')}</span>
            </div>
          )}
        </div>
        {icon && (
          <div style={{
            width: 40, height: 40, borderRadius: 12, flexShrink: 0,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            backgroundColor: `${color}1A`, color, fontSize: 18,
          }}>
            {icon}
          </div>
        )}
      </div>
      {sparkData && sparkData.length > 1 && (
        <div style={{ marginTop: 10 }}>
          <Sparkline data={sparkData} color={color} width={180} height={30} />
        </div>
      )}
    </Card>
  );
}
