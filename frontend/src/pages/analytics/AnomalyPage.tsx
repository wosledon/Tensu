import { useState, useEffect } from 'react';
import { Card, DatePicker, Typography, Spin, Space, Button, Table, Tag, Select, Row, Col, Statistic, Modal } from 'antd';
import { ExportOutlined, ReloadOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { analyticsApi } from '../../api';
import { exportTableToCsv } from '../../utils/export';
import dayjs from 'dayjs';

const { Title } = Typography;
const { RangePicker } = DatePicker;

export default function AnomalyPage() {
  const { t } = useTranslation();
  const [data, setData] = useState<Array<{
    type: string;
    severity: string;
    dimension: string;
    message: string;
    currentValue: number;
    baselineValue: number;
    detectedAt: string;
  }>>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [dates, setDates] = useState<[dayjs.Dayjs, dayjs.Dayjs]>([dayjs().subtract(1, 'day'), dayjs()]);
  const [severity, setSeverity] = useState<string | undefined>(undefined);
  const [type, setType] = useState<string | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedRow, setSelectedRow] = useState<{
    type: string;
    severity: string;
    dimension: string;
    message: string;
    currentValue: number;
    baselineValue: number;
    detectedAt: string;
  } | null>(null);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await analyticsApi.anomalies(dates[0].toISOString(), dates[1].toISOString(), undefined, severity, type, page, 20);
      setData(result.items);
      setTotal(result.total);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load anomalies');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, [dates[0]?.toISOString(), dates[1]?.toISOString(), severity, type, page]);

  const severityColor = (severity: string) => {
    if (severity === 'Critical') return 'red';
    if (severity === 'High') return 'orange';
    if (severity === 'Medium') return 'gold';
    return 'default';
  };

  const severityCounts = data.reduce((acc, cur) => {
    acc[cur.severity] = (acc[cur.severity] || 0) + 1;
    return acc;
  }, {} as Record<string, number>);

  const columns = [
    { title: t('analytics.timestamp', 'Time'), dataIndex: 'detectedAt', key: 'detectedAt', width: 180, render: (v: string) => new Date(v).toLocaleString() },
    { title: t('analytics.type', 'Type'), dataIndex: 'type', key: 'type', width: 160 },
    {
      title: t('common.status', 'Severity'),
      dataIndex: 'severity',
      key: 'severity',
      width: 110,
      render: (v: string) => <Tag color={severityColor(v)}>{v}</Tag>,
    },
    { title: t('analytics.dimension', 'Dimension'), dataIndex: 'dimension', key: 'dimension', width: 220, render: (v: string) => <span style={{ fontFamily: 'monospace' }}>{v}</span> },
    { title: t('analytics.message', 'Message'), dataIndex: 'message', key: 'message', ellipsis: true },
    {
      title: t('analytics.currentValue', 'Current'),
      dataIndex: 'currentValue',
      key: 'currentValue',
      align: 'right' as const,
      width: 120,
      render: (v: number) => <span style={{ fontFamily: 'monospace' }}>{typeof v === 'number' && v < 1 ? v.toFixed(4) : v}</span>,
    },
    {
      title: t('analytics.baselineValue', 'Baseline'),
      dataIndex: 'baselineValue',
      key: 'baselineValue',
      align: 'right' as const,
      width: 120,
      render: (v: number) => <span style={{ fontFamily: 'monospace' }}>{typeof v === 'number' && v < 1 ? v.toFixed(4) : v}</span>,
    },
  ];

  const exportColumns = [
    { title: t('analytics.timestamp', 'Time'), dataIndex: 'detectedAt' },
    { title: t('analytics.type', 'Type'), dataIndex: 'type' },
    { title: t('common.status', 'Severity'), dataIndex: 'severity' },
    { title: t('analytics.dimension', 'Dimension'), dataIndex: 'dimension' },
    { title: t('analytics.message', 'Message'), dataIndex: 'message' },
    { title: t('analytics.currentValue', 'Current'), dataIndex: 'currentValue' },
    { title: t('analytics.baselineValue', 'Baseline'), dataIndex: 'baselineValue' },
  ];

  const handleExport = () => {
    exportTableToCsv('anomalies', exportColumns, data);
  };

  return (
    <div>
      <Title level={4} style={{ marginBottom: 24 }}>{t('analytics.anomalies', 'Anomaly Detection')}</Title>

      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space size={12} wrap>
          <RangePicker
            value={dates}
            onChange={(vals) => vals && setDates([vals[0]!, vals[1]!])}
            format="YYYY-MM-DD"
          />
          <Select
            value={severity}
            onChange={(v) => { setSeverity(v); setPage(1); }}
            allowClear
            style={{ width: 140 }}
            placeholder={t('analytics.severity')}
            options={[
              { value: 'Critical', label: 'Critical' },
              { value: 'High', label: 'High' },
              { value: 'Medium', label: 'Medium' },
            ]}
          />
          <Select
            value={type}
            onChange={(v) => { setType(v); setPage(1); }}
            allowClear
            style={{ width: 160 }}
            placeholder={t('analytics.type')}
            options={[
              { value: 'UsageSpike', label: 'UsageSpike' },
              { value: 'UsageDrop', label: 'UsageDrop' },
              { value: 'CostSpike', label: 'CostSpike' },
              { value: 'TokenSpike', label: 'TokenSpike' },
              { value: 'TokenDrop', label: 'TokenDrop' },
              { value: 'LatencySpike', label: 'LatencySpike' },
              { value: 'ErrorRateSpike', label: 'ErrorRateSpike' },
              { value: 'RateLimitSpike', label: 'RateLimitSpike' },
              { value: 'ProviderDegraded', label: 'ProviderDegraded' },
            ]}
          />
          <Button type="primary" onClick={fetchData}>{t('common.search')}</Button>
          <Button icon={<ReloadOutlined />} onClick={fetchData}>{t('common.refresh', 'Refresh')}</Button>
          <Button icon={<ExportOutlined />} onClick={handleExport}>{t('common.export', 'Export')}</Button>
        </Space>
      </Card>

      {loading ? <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 120 }}><Spin size="large" /></div> : (
        <>
          {error && <Card style={{ borderRadius: 18, marginBottom: 24, borderColor: '#ff4d4f' }}>{error}</Card>}
          <Row gutter={[24, 24]} style={{ marginBottom: 24 }}>
            {['Critical', 'High', 'Medium'].map((sev) => (
              <Col key={sev} xs={24} sm={8}>
                <Card style={{ borderRadius: 18 }}>
                  <Statistic title={<Tag color={severityColor(sev)}>{sev}</Tag>} value={severityCounts[sev] || 0} />
                </Card>
              </Col>
            ))}
          </Row>
          <Card style={{ borderRadius: 18 }}>
            {data.length === 0 && !severity && !type ? (
              <div style={{ textAlign: 'center', padding: 40, color: 'var(--ant-color-text-secondary)' }}>{t('common.noData')}</div>
            ) : data.length === 0 && (severity || type) ? (
              <div style={{ textAlign: 'center', padding: 40, color: 'var(--ant-color-text-secondary)' }}>{t('analytics.noMatch', 'No matching anomalies')}</div>
            ) : (
              <Table
                rowKey={(r) => `${r.detectedAt}-${r.dimension}-${r.type}-${r.message}`}
                size="small"
                pagination={{ current: page, pageSize: 20, total, showSizeChanger: false, onChange: setPage }}
                dataSource={data}
                columns={columns as any}
                scroll={{ x: 1000 }}
                onRow={(record) => ({
                  onClick: () => { setSelectedRow(record); setDetailOpen(true); },
                  style: { cursor: 'pointer' },
                })}
              />
            )}
          </Card>
        </>
      )}

      <Modal
        title={t('analytics.anomalies', 'Anomaly Detection')}
        open={detailOpen}
        onCancel={() => setDetailOpen(false)}
        footer={null}
      >
        {selectedRow && (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.type', 'Type')}: </span>{selectedRow.type}</div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('common.status', 'Severity')}: </span><Tag color={severityColor(selectedRow.severity)}>{selectedRow.severity}</Tag></div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.dimension', 'Dimension')}: </span><span style={{ fontFamily: 'monospace' }}>{selectedRow.dimension}</span></div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.message', 'Message')}: </span>{selectedRow.message}</div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.currentValue', 'Current')}: </span>{selectedRow.currentValue}</div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.baselineValue', 'Baseline')}: </span>{selectedRow.baselineValue}</div>
            <div><span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('analytics.timestamp', 'Timestamp')}: </span>{new Date(selectedRow.detectedAt).toLocaleString()}</div>
          </Space>
        )}
      </Modal>
    </div>
  );
}
