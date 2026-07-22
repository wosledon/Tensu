import { useMemo } from 'react';

interface SparklineProps {
  data: number[];
  width?: number;
  height?: number;
  color?: string;
  /** Fill the area under the line with a translucent gradient. */
  area?: boolean;
}

/**
 * Lightweight SVG sparkline (no chart library) for stat cards.
 */
export default function Sparkline({ data, width = 96, height = 32, color = 'var(--ant-color-primary)', area = true }: SparklineProps) {
  const { path, areaPath, gradientId } = useMemo(() => {
    const id = `spark-${Math.random().toString(36).slice(2, 8)}`;
    if (!data.length) return { path: '', areaPath: '', gradientId: id };

    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;
    const stepX = data.length > 1 ? width / (data.length - 1) : width;
    const points = data.map((v, i) => [
      i * stepX,
      height - 2 - ((v - min) / range) * (height - 4),
    ] as const);

    // Smooth path via simple cubic segments.
    let d = `M ${points[0][0]},${points[0][1]}`;
    for (let i = 1; i < points.length; i++) {
      const [x0, y0] = points[i - 1];
      const [x1, y1] = points[i];
      const cx = (x0 + x1) / 2;
      d += ` C ${cx},${y0} ${cx},${y1} ${x1},${y1}`;
    }
    const areaD = `${d} L ${points[points.length - 1][0]},${height} L ${points[0][0]},${height} Z`;
    return { path: d, areaPath: areaD, gradientId: id };
  }, [data, width, height]);

  if (!data.length) return null;

  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} style={{ display: 'block', overflow: 'visible' }}>
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.28} />
          <stop offset="100%" stopColor={color} stopOpacity={0.02} />
        </linearGradient>
      </defs>
      {area && <path d={areaPath} fill={`url(#${gradientId})`} />}
      <path d={path} fill="none" stroke={color} strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
