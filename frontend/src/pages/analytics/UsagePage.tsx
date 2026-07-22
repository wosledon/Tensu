import { useState, useEffect, useCallback } from 'react';
import { Card, Col, Row, Select, DatePicker, Typography, Space, Button, Statistic, Alert, Table } from 'antd';
import ReactECharts from 'echarts-for-react';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../../hooks/useThemeMode';
import { useAuth } from '../../hooks/useAuth';
import { analyticsApi } from '../../api';
import { PageSkeleton } from '../../components';
import type { ApiKeyUsageStat, OrgUsageStat } from '../../types';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

const chartColors = ['#007AFF', '#34C759', '#FF9500', '#AF52DE', '#5AC8FA', '#FF3B30', '#30B0C7', '#FF2D55'];

export default function UsagePage() {
  const { t } = useTranslation();
  const { isDark } = useThemeMode();
  const { user } = useAuth();
  const [data, setData] = useState<any>(null);
  const [keyStats, setKeyStats] = useState<ApiKeyUsageStat[]>([]);
  const [orgStats, setOrgStats] = useState<OrgUsageStat[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [granularity, setGranularity] = useState('day');
  const [dates, setDates] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().subtract(7, 'day'), dayjs()]);

  const isSuperAdmin = user?.role === 'SuperAdmin';

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [result, keyResult, orgResult] = await Promise.all([
        analyticsApi.usage(dates[0].toISOString(), dates[1].toISOString(), granularity),
        analyticsApi.byApiKey(dates[0].toISOString(), dates[1].toISOString()),
        isSuperAdmin
          ? analyticsApi.byOrganization(dates[0].toISOString(), dates[1].toISOString()).catch(() => null)
          : Promise.resolve(null),
      ]);
      setData(result);
      setKeyStats(keyResult.items);
      setOrgStats(orgResult?.items ?? []);
    } catch (err: any) {
      setError(err?.message || t('analytics.loadError'));
    } finally {
      setLoading(false);
    }
  }, [dates, granularity, isSuperAdmin, t]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const textColor = isDark ? 'rgba(235,235,245,0.6)' : 'rgba(60,60,67,0.6)';
  const gridColor = isDark ? 'rgba(84,84,88,0.2)' : 'rgba(60,60,67,0.08)';

  const tokenOption = {
    color: chartColors,
    tooltip: { trigger: 'axis' as const },
    legend: { data: [t('analytics.inputTokens'), t('analytics.outputTokens'), t('analytics.totalTokens')], textStyle: { color: textColor } },
    grid: { left: 48, right: 24, top: 48, bottom: 24 },
    xAxis: { type: 'category' as const, data: data?.map((d: any) => d.time) || [], axisLabel: { color: textColor }, axisLine: { lineStyle: { color: gridColor } } },
    yAxis: { type: 'value' as const, name: 'Tokens', axisLabel: { color: textColor }, splitLine: { lineStyle: { color: gridColor } } },
    series: [
      { name: t('analytics.inputTokens'), type: 'bar' as const, stack: 'tokens', data: data?.map((d: any) => d.inputTokens) || [], itemStyle: { borderRadius: [2, 2, 0, 0] } },
      { name: t('analytics.outputTokens'), type: 'bar' as const, stack: 'tokens', data: data?.map((d: any) => d.outputTokens) || [], itemStyle: { borderRadius: [2, 2, 0, 0] } },
      { name: t('analytics.totalTokens'), type: 'line' as const, yAxisIndex: 0, data: data?.map((d: any) => d.totalTokens) || [], smooth: true, lineStyle: { width: 2 } },
    ],
  };

  const requestOption = {
    color: chartColors,
    tooltip: { trigger: 'axis' as const },
    legend: { data: [t('analytics.requests'), t('analytics.success'), t('analytics.failed'), t('analytics.cacheHits')], textStyle: { color: textColor } },
    grid: { left: 48, right: 24, top: 48, bottom: 24 },
    xAxis: { type: 'category' as const, data: data?.map((d: any) => d.time) || [], axisLabel: { color: textColor }, axisLine: { lineStyle: { color: gridColor } } },
    yAxis: { type: 'value' as const, name: t('analytics.requests'), axisLabel: { color: textColor }, splitLine: { lineStyle: { color: gridColor } } },
    series: [
      { name: t('analytics.requests'), type: 'bar' as const, data: data?.map((d: any) => d.requests) || [], itemStyle: { borderRadius: [6, 6, 0, 0] } },
      { name: t('analytics.success'), type: 'line' as const, data: data?.map((d: any) => d.success) || [], smooth: true },
      { name: t('analytics.failed'), type: 'line' as const, data: data?.map((d: any) => d.failed) || [], smooth: true, lineStyle: { type: 'dashed' as const } },
      { name: t('analytics.cacheHits'), type: 'line' as const, data: data?.map((d: any) => d.cacheHits) || [], smooth: true, lineStyle: { type: 'dotted' as const } },
    ],
  };

  const totals = data?.reduce((acc: any, d: any) => ({
    inputTokens: (acc.inputTokens || 0) + (d.inputTokens || 0),
    outputTokens: (acc.outputTokens || 0) + (d.outputTokens || 0),
    totalTokens: (acc.totalTokens || 0) + (d.totalTokens || 0),
    requests: (acc.requests || 0) + (d.requests || 0),
    cacheHits: (acc.cacheHits || 0) + (d.cacheHits || 0),
    compressionSaved: (acc.compressionSaved || 0) + (d.compressionSaved || 0),
  }), {}) || {};

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
    { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
    { title: t('analytics.inputTokens'), dataIndex: 'inputTokens', key: 'inputTokens', align: 'right' as const },
    { title: t('analytics.outputTokens'), dataIndex: 'outputTokens', key: 'outputTokens', align: 'right' as const },
    { title: t('analytics.totalTokens'), dataIndex: 'totalTokens', key: 'totalTokens', align: 'right' as const },
    { title: t('analytics.successRate'), dataIndex: 'successRate', key: 'successRate', align: 'right' as const, render: (v: number) => `${v}%` },
  ];

  return (
    <div>
      <Title level={4} style={{ marginBottom: 24 }}>{t('analytics.usage')}</Title>

      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space size={12} wrap>
          <RangePicker
            value={dates}
            onChange={(vals) => vals && setDates([vals[0]!, vals[1]!])}
            format="YYYY-MM-DD"
          />
          <Select value={granularity} onChange={setGranularity} style={{ width: 120 }}>
            <Select.Option value="minute">{t('analytics.minute')}</Select.Option>
            <Select.Option value="hour">{t('analytics.hour')}</Select.Option>
            <Select.Option value="day">{t('analytics.day')}</Select.Option>
            <Select.Option value="week">{t('analytics.week')}</Select.Option>
            <Select.Option value="month">{t('analytics.month')}</Select.Option>
          </Select>
          <Button type="primary" onClick={fetchData}>{t('common.search')}</Button>
        </Space>
      </Card>

      {error && <Alert type="error" message={error} style={{ marginBottom: 24 }} />}
      {loading ? <PageSkeleton /> : (
        <>
          <Row gutter={[24, 24]}>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.totalTokens')} value={totals.totalTokens || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.requests')} value={totals.requests || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.cacheHits')} value={totals.cacheHits || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.compressionSaved')} value={totals.compressionSaved || 0} />
              </Card>
            </Col>
          </Row>

          <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
            <Col xs={24} lg={16}>
              <Card title={t('analytics.totalTokens') + ' Trend'} style={{ borderRadius: 18 }}>
                <ReactECharts option={tokenOption} style={{ height: 320 }} />
              </Card>
            </Col>
            <Col xs={24} lg={8}>
              <Card title={t('analytics.requests') + ' Trend'} style={{ borderRadius: 18 }}>
                <ReactECharts option={requestOption} style={{ height: 320 }} />
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

          {isSuperAdmin && orgStats.length > 0 && (
            <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
              <Col xs={24}>
                <Card title={t('analytics.byOrganization')} style={{ borderRadius: 18 }}>
                  <Table
                    rowKey="organizationId"
                    size="small"
                    pagination={false}
                    dataSource={orgStats}
                    columns={[
                      { title: t('organization.name'), dataIndex: 'organizationName', key: 'organizationName' },
                      { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
                      { title: t('analytics.totalTokens'), dataIndex: 'totalTokens', key: 'totalTokens', align: 'right' as const },
                      { title: t('analytics.totalCost'), dataIndex: 'totalCost', key: 'totalCost', align: 'right' as const },
                      { title: t('analytics.cacheHitRate'), dataIndex: 'cacheHitRate', key: 'cacheHitRate', align: 'right' as const, render: (v: number) => `${v}%` },
                    ]}
                  />
                </Card>
              </Col>
            </Row>
          )}
        </>
      )}
    </div>
  );
}
