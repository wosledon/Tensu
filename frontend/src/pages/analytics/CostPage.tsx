import { useState, useEffect, useCallback } from 'react';
import { Card, Col, Row, DatePicker, Typography, Space, Button, Statistic, Table, Alert } from 'antd';
import ReactECharts from 'echarts-for-react';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../../hooks/useThemeMode';
import { analyticsApi } from '../../api';
import { PageSkeleton } from '../../components';
import type { ApiKeyUsageStat } from '../../types';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

const chartColors = ['#007AFF', '#34C759', '#FF9500', '#AF52DE', '#5AC8FA', '#FF3B30', '#30B0C7', '#FF2D55'];

export default function CostPage() {
  const { t } = useTranslation();
  const { isDark } = useThemeMode();
  const [data, setData] = useState<any>(null);
  const [keyStats, setKeyStats] = useState<ApiKeyUsageStat[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dates, setDates] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().subtract(30, 'day'), dayjs()]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [result, keyResult] = await Promise.all([
        analyticsApi.cost(dates[0].toISOString(), dates[1].toISOString()),
        analyticsApi.byApiKey(dates[0].toISOString(), dates[1].toISOString()),
      ]);
      setData(result);
      setKeyStats(keyResult.items);
    } catch (err: any) {
      setError(err?.message || t('analytics.loadError'));
    } finally {
      setLoading(false);
    }
  }, [dates, t]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const textColor = isDark ? 'rgba(235,235,245,0.6)' : 'rgba(60,60,67,0.6)';
  const gridColor = isDark ? 'rgba(84,84,88,0.2)' : 'rgba(60,60,67,0.08)';

  const dailyOption = {
    color: chartColors,
    tooltip: { trigger: 'axis' as const },
    grid: { left: 48, right: 24, top: 48, bottom: 24 },
    xAxis: { type: 'category' as const, data: data?.daily?.map((d: any) => d.date) || [], axisLabel: { color: textColor }, axisLine: { lineStyle: { color: gridColor } } },
    yAxis: { type: 'value' as const, name: 'USD', axisLabel: { color: textColor }, splitLine: { lineStyle: { color: gridColor } } },
    series: [{ type: 'bar' as const, data: data?.daily?.map((d: any) => d.cost) || [], itemStyle: { borderRadius: [6, 6, 0, 0] } }],
  };

  const modelColumns = [
    { title: t('analytics.model'), dataIndex: 'model', key: 'model' },
    { title: t('analytics.totalCost'), dataIndex: 'totalCost', key: 'totalCost', align: 'right' as const, render: (v: number) => `$${v.toFixed(6)}` },
    { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
    { title: t('analytics.avgCostPerRequest'), dataIndex: 'avgCostPerRequest', key: 'avgCostPerRequest', align: 'right' as const, render: (v: number) => `$${v.toFixed(6)}` },
  ];

  const providerColumns = [
    { title: t('analytics.provider'), dataIndex: 'provider', key: 'provider' },
    { title: t('analytics.totalCost'), dataIndex: 'totalCost', key: 'totalCost', align: 'right' as const, render: (v: number) => `$${v.toFixed(6)}` },
    { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
  ];

  const apiKeyColumns = [
    {
      title: t('apiKey.key'), key: 'name',
      render: (_: any, r: ApiKeyUsageStat) => (
        <Space size={4}>
          <span>{r.name}</span>
          {r.keyPrefix && <span style={{ fontFamily: 'monospace', opacity: 0.6 }}>{r.keyPrefix}...</span>}
        </Space>
      ),
    },
    { title: t('apiKey.user'), dataIndex: 'user', key: 'user', render: (v: string) => v || '-' },
    { title: t('analytics.totalCost'), dataIndex: 'totalCost', key: 'totalCost', align: 'right' as const, render: (v: number) => `$${v.toFixed(6)}` },
    { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
    {
      title: t('analytics.avgCostPerRequest'), key: 'avgCostPerRequest', align: 'right' as const,
      render: (_: any, r: ApiKeyUsageStat) => `$${(r.requests > 0 ? r.totalCost / r.requests : 0).toFixed(6)}`,
    },
  ];

  return (
    <div>
      <Title level={4} style={{ marginBottom: 24 }}>{t('analytics.cost')}</Title>

      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space size={12} wrap>
          <RangePicker
            value={dates}
            onChange={(vals) => vals && setDates([vals[0]!, vals[1]!])}
            format="YYYY-MM-DD"
          />
          <Button type="primary" onClick={fetchData}>{t('common.search')}</Button>
        </Space>
      </Card>

      {error && <Alert type="error" message={error} style={{ marginBottom: 24 }} />}
      {loading ? <PageSkeleton /> : (
        <>
          <Row gutter={[24, 24]}>
            <Col xs={24} sm={12} lg={5}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic
                  title={t('analytics.totalCost')}
                  value={data?.currencyConversionApplied ? data?.totalCostInDefaultCurrency ?? 0 : data?.totalCost || 0}
                  prefix={data?.currencyConversionApplied ? `${data?.defaultCurrency ?? 'USD'} ` : '$'}
                  precision={6}
                />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={5}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.requests')} value={data?.totalRequests || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={5}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.avgCostPerRequest')} value={data?.avgCostPerRequest || 0} prefix="$" precision={6} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={5}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.compressionSaved')} value={data?.compressionSavings || 0} prefix={data?.currencyConversionApplied ? `${data?.defaultCurrency ?? 'USD'} ` : '$'} precision={6} />
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {(data?.compressionSavedTokens ?? 0).toLocaleString()} tokens
                </Typography.Text>
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={4}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic
                  title={t('analytics.forecastNext7Days')}
                  value={data?.forecastNext7Days ?? 0}
                  prefix={data?.currencyConversionApplied ? `${data?.defaultCurrency ?? 'USD'} ` : '$'}
                  precision={2}
                />
              </Card>
            </Col>
          </Row>

          <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
            <Col xs={24} lg={16}>
              <Card title={t('analytics.daily') + ' ' + t('analytics.totalCost')} style={{ borderRadius: 18 }}>
                <ReactECharts option={dailyOption} style={{ height: 320 }} />
              </Card>
            </Col>
            <Col xs={24} lg={8}>
              <Card title={t('analytics.byProvider')} style={{ borderRadius: 18 }}>
                <Table rowKey="provider" size="small" pagination={false} dataSource={data?.byProvider || []} columns={providerColumns} />
              </Card>
            </Col>
          </Row>

          <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
            <Col xs={24}>
              <Card title={t('analytics.byModel')} style={{ borderRadius: 18 }}>
                <Table rowKey="model" size="small" pagination={false} dataSource={data?.byModel || []} columns={modelColumns} />
              </Card>
            </Col>
          </Row>

          <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
            <Col xs={24}>
              <Card title={t('analytics.byApiKey')} style={{ borderRadius: 18 }}>
                <Table rowKey="apiKeyId" size="small" pagination={false} dataSource={keyStats} columns={apiKeyColumns} />
              </Card>
            </Col>
          </Row>
        </>
      )}
    </div>
  );
}
