import { useState, useEffect } from 'react';
import { Card, Col, Row, Typography } from 'antd';
import { ApiOutlined, ThunderboltOutlined, DollarOutlined, CloudOutlined, AlertOutlined } from '@ant-design/icons';
import ReactECharts from 'echarts-for-react';
import * as echarts from 'echarts';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../../hooks/useThemeMode';
import { analyticsApi } from '../../api';
import { useNavigate } from 'react-router-dom';
import { StatCard, PageSkeleton } from '../../components';

const { Title } = Typography;

const chartColors = ['#007AFF', '#34C759', '#FF9500', '#AF52DE', '#5AC8FA', '#FF3B30', '#30B0C7', '#FF2D55'];

export default function DashboardPage() {
  const { t } = useTranslation();
  const { isDark } = useThemeMode();
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [anomalyCount, setAnomalyCount] = useState(0);
  const navigate = useNavigate();

  useEffect(() => {
    const fetchData = () => {
      analyticsApi.dashboard()
        .then(setData)
        .catch(() => {})
        .finally(() => setLoading(false));

      const from = new Date(); from.setHours(0, 0, 0, 0);
      const to = new Date();
      analyticsApi.anomalies(from.toISOString(), to.toISOString())
        .then((res) => setAnomalyCount(res?.total ?? 0))
        .catch(() => {});
    };

    fetchData();
    const interval = setInterval(fetchData, 60000);
    return () => clearInterval(interval);
  }, []);

  if (loading) return <PageSkeleton cards={5} />;

  const textColor = isDark ? 'rgba(235,235,245,0.6)' : 'rgba(60,60,67,0.6)';
  const gridColor = isDark ? 'rgba(84,84,88,0.2)' : 'rgba(60,60,67,0.08)';

  const trend: any[] = data?.dailyTrend ?? [];
  const prev = trend[trend.length - 2];

  const areaGradient = (color: string) => new echarts.graphic.LinearGradient(0, 0, 0, 1, [
    { offset: 0, color: `${color}33` },
    { offset: 1, color: `${color}05` },
  ]);

  const trendOption = {
    color: chartColors,
    tooltip: { trigger: 'axis' as const },
    legend: { data: [t('audit.totalRequests'), t('audit.totalTokens')], textStyle: { color: textColor } },
    grid: { left: 48, right: 24, top: 48, bottom: 24 },
    xAxis: { type: 'category' as const, data: trend.map((d: any) => d.date), axisLabel: { color: textColor }, axisLine: { lineStyle: { color: gridColor } } },
    yAxis: [
      { type: 'value' as const, name: t('audit.totalRequests'), axisLabel: { color: textColor }, splitLine: { lineStyle: { color: gridColor } } },
      { type: 'value' as const, name: t('audit.totalTokens'), axisLabel: { color: textColor }, splitLine: { show: false } },
    ],
    series: [
      {
        name: t('audit.totalRequests'), type: 'bar' as const,
        data: trend.map((d: any) => d.requests),
        itemStyle: { borderRadius: [6, 6, 0, 0] },
      },
      {
        name: t('audit.totalTokens'), type: 'line' as const, yAxisIndex: 1,
        data: trend.map((d: any) => d.tokens),
        smooth: true, lineStyle: { width: 2 },
        areaStyle: { color: areaGradient('#34C759') },
      },
    ],
  };

  const modelPieOption = {
    color: chartColors,
    tooltip: { trigger: 'item' as const, formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical' as const, right: 8, top: 'center', textStyle: { color: textColor } },
    series: [{
      type: 'pie' as const, radius: ['45%', '70%'], center: ['35%', '50%'],
      avoidLabelOverlap: false, itemStyle: { borderRadius: 10, borderColor: isDark ? '#1C1C1E' : '#fff', borderWidth: 2 },
      label: { show: false }, emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
      data: data?.modelDistribution?.map((d: any) => ({ name: d.name, value: d.value })) || [],
    }],
  };

  const latencyOption = {
    color: ['#007AFF', '#5AC8FA'],
    tooltip: { trigger: 'axis' as const },
    legend: { data: [t('audit.avgLatency'), t('audit.successRate')], textStyle: { color: textColor } },
    grid: { left: 48, right: 48, top: 48, bottom: 24 },
    xAxis: { type: 'category' as const, data: trend.map((d: any) => d.date), axisLabel: { color: textColor }, axisLine: { lineStyle: { color: gridColor } } },
    yAxis: [
      { type: 'value' as const, name: 'ms', axisLabel: { color: textColor }, splitLine: { lineStyle: { color: gridColor } } },
      { type: 'value' as const, name: '%', min: 0, max: 100, axisLabel: { color: textColor }, splitLine: { show: false } },
    ],
    series: [
      {
        name: t('audit.avgLatency'), type: 'line' as const, smooth: true,
        data: trend.map((d: any) => Math.round(d.avgLatency)),
        areaStyle: { color: areaGradient('#007AFF') },
      },
      {
        name: t('audit.successRate'), type: 'line' as const, yAxisIndex: 1, smooth: true,
        data: trend.map((d: any) => d.successRate),
        lineStyle: { type: 'dashed' as const },
      },
    ],
  };

  const stats = [
    {
      title: t('dashboard.todayRequests'), value: data?.today?.requests || 0,
      icon: <ApiOutlined />, color: '#007AFF',
      compareValue: prev?.requests, sparkData: trend.map((d: any) => d.requests),
    },
    {
      title: t('dashboard.todayTokens'), value: data?.today?.tokens || 0,
      icon: <ThunderboltOutlined />, color: '#34C759',
      compareValue: prev?.tokens, sparkData: trend.map((d: any) => d.tokens),
    },
    {
      title: t('dashboard.todayCost'), value: data?.today?.cost || 0, prefix: '$', precision: 4,
      icon: <DollarOutlined />, color: '#FF9500',
      compareValue: prev?.cost, sparkData: trend.map((d: any) => d.cost ?? 0),
    },
    {
      title: t('dashboard.cacheHitRate'), value: data?.today?.cacheHitRate || 0, suffix: '%',
      icon: <CloudOutlined />, color: '#AF52DE',
      sparkData: trend.map((d: any) => d.cacheHitRate ?? 0),
    },
    {
      title: t('analytics.anomalies'), value: anomalyCount,
      icon: <AlertOutlined />, color: '#FF3B30',
      onClick: () => navigate('/analytics/anomalies'),
    },
  ];

  return (
    <div>
      <Title level={4} style={{ margin: 0, marginBottom: 32 }}>{t('dashboard.title')}</Title>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16, marginBottom: 24 }}>
        {stats.map((s, i) => (
          <StatCard key={i} {...s} />
        ))}
      </div>

      <Row gutter={[24, 24]}>
        <Col xs={24} lg={16}>
          <Card title={t('dashboard.requestTrend')} style={{ borderRadius: 18 }}>
            <ReactECharts option={trendOption} style={{ height: 300 }} notMerge />
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card title={t('dashboard.modelDistribution')} style={{ borderRadius: 18 }}>
            <ReactECharts option={modelPieOption} style={{ height: 300 }} notMerge />
          </Card>
        </Col>
      </Row>

      <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
        <Col xs={24} lg={14}>
          <Card title={t('audit.latency') + ' & ' + t('audit.status')} style={{ borderRadius: 18 }}>
            <ReactECharts option={latencyOption} style={{ height: 300 }} notMerge />
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card title={t('dashboard.providerHealth')} style={{ borderRadius: 18 }}>
            {data?.providers?.length ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                {data.providers.map((p: any) => {
                  const statusLabel = p.healthStatus === 'Healthy' ? t('provider.healthy') : p.healthStatus === 'Degraded' ? t('provider.degraded') : p.healthStatus === 'Unhealthy' ? t('provider.unhealthy') : t('provider.unknown');
                  const statusColor = p.healthStatus === 'Healthy' ? '#34C759' : p.healthStatus === 'Unhealthy' ? '#FF3B30' : '#FF9500';
                  return (
                    <div key={p.name} style={{
                      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                      padding: '12px 16px', borderRadius: 12,
                      background: 'var(--ant-color-bg-elevated)', transition: 'background 0.2s, transform 0.2s',
                      cursor: 'pointer',
                    }}
                      onMouseEnter={(e) => { e.currentTarget.style.transform = 'translateX(4px)'; }}
                      onMouseLeave={(e) => { e.currentTarget.style.transform = 'none'; }}
                    >
                      <span style={{ fontWeight: 500 }}>{p.name}</span>
                      <span style={{ color: statusColor, display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 500 }}>
                        <span style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: 'currentColor' }} />
                        {statusLabel}
                      </span>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: 60, color: 'var(--ant-color-text-secondary)' }}>{t('common.noData')}</div>
            )}
          </Card>
        </Col>
      </Row>
    </div>
  );
}
