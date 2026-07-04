import { useState, useEffect } from 'react';
import { Card, Col, Row, DatePicker, Typography, Spin, Space, Button, Statistic, Table } from 'antd';
import { useTranslation } from 'react-i18next';
import { analyticsApi } from '../../api';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

export default function CachePage() {
  const { t } = useTranslation();
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [dates, setDates] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().subtract(7, 'day'), dayjs()]);

  useEffect(() => {
    setLoading(true);
    analyticsApi.cache(dates[0].toISOString(), dates[1].toISOString())
      .then(setData)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const columns = [
    { title: t('analytics.model'), dataIndex: 'model', key: 'model' },
    { title: t('analytics.totalRequests'), dataIndex: 'total', key: 'total', align: 'right' as const },
    { title: t('analytics.cacheHits'), dataIndex: 'hits', key: 'hits', align: 'right' as const },
    { title: t('analytics.hitRate'), dataIndex: 'hitRate', key: 'hitRate', align: 'right' as const, render: (v: number) => `${v}%` },
  ];

  return (
    <div>
      <Title level={4} style={{ marginBottom: 24 }}>{t('analytics.cache')}</Title>

      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space size={12} wrap>
          <RangePicker
            value={dates}
            onChange={(vals) => vals && setDates([vals[0]!, vals[1]!])}
            format="YYYY-MM-DD"
          />
          <Button type="primary" onClick={() => {
            setLoading(true);
            analyticsApi.cache(dates[0].toISOString(), dates[1].toISOString())
              .then(setData).catch(() => {}).finally(() => setLoading(false));
          }}>{t('common.search')}</Button>
        </Space>
      </Card>

      {loading ? <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 120 }}><Spin size="large" /></div> : (
        <>
          <Row gutter={[24, 24]}>
            <Col xs={24} sm={12} lg={8}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.totalRequests')} value={data?.totalRequests || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={8}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.cacheHits')} value={data?.cacheHits || 0} />
              </Card>
            </Col>
            <Col xs={24} sm={12} lg={8}>
              <Card style={{ borderRadius: 18 }}>
                <Statistic title={t('analytics.hitRate')} value={data?.hitRate || 0} suffix="%" />
              </Card>
            </Col>
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
