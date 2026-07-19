import { useState, useEffect, useCallback } from 'react';
import { Card, Col, Row, DatePicker, Typography, Spin, Space, Button, Statistic, Table, Alert } from 'antd';
import { useTranslation } from 'react-i18next';
import { analyticsApi } from '../../api';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

export default function PerformancePage() {
  const { t } = useTranslation();
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dates, setDates] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().subtract(7, 'day'), dayjs()]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await analyticsApi.performance(dates[0].toISOString(), dates[1].toISOString());
      setData(result);
    } catch (err: any) {
      setError(err?.message || t('analytics.loadError'));
    } finally {
      setLoading(false);
    }
  }, [dates, t]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const columns = [
    { title: t('analytics.model'), dataIndex: 'model', key: 'model' },
    { title: t('analytics.avgLatency'), dataIndex: 'avgLatency', key: 'avgLatency', align: 'right' as const, render: (v: number) => `${v} ms` },
    { title: 'P95 ' + t('analytics.avgLatency'), dataIndex: 'p95Latency', key: 'p95Latency', align: 'right' as const, render: (v: number) => `${v} ms` },
    { title: t('analytics.avgSpeed'), dataIndex: 'avgSpeed', key: 'avgSpeed', align: 'right' as const, render: (v: number) => `${v} t/s` },
    { title: t('analytics.requests'), dataIndex: 'requests', key: 'requests', align: 'right' as const },
  ];

  return (
    <div>
      <Title level={4} style={{ marginBottom: 24 }}>{t('analytics.performance')}</Title>

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
      {loading ? <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 120 }}><Spin size="large" /></div> : (
        <>
          <Row gutter={[24, 24]}>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.avgLatency')} value={data?.overall?.avgLatency || 0} suffix="ms" />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={'P95 ' + t('analytics.avgLatency')} value={data?.overall?.p95Latency || 0} suffix="ms" />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.avgTtft')} value={data?.overall?.avgTtft || 0} suffix="ms" />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={6}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.avgSpeed')} value={data?.overall?.avgSpeed || 0} suffix="t/s" />
              </Card>
            </Col>
            {data?.overall?.reasoningShare != null && (
              <Col xs={24} sm={12} lg={6}>
                <Card style={{ borderRadius: 18 }}>
                  <Statistic
                    title={t('analytics.reasoningShare')}
                    value={data.overall.reasoningShare}
                    suffix="%"
                  />
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {(data.overall.reasoningTokensTotal ?? 0).toLocaleString()} / {(data.overall.cachedInputTokensTotal ?? 0).toLocaleString()} {t('analytics.reasoningCachedNote')}
                  </Typography.Text>
                </Card>
              </Col>
            )}
          </Row>

          <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
            <Col xs={24}>
              <Card title={t('analytics.byModel')} style={{ borderRadius: 18 }}>
                <Table rowKey="model" size="small" pagination={false} dataSource={data?.byModel || []} columns={columns} />
              </Card>
            </Col>
          </Row>
        </>
      )}
    </div>
  );
}
