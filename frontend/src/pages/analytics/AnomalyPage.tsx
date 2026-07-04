import { useState, useEffect, useCallback } from 'react';
import { Card, DatePicker, Typography, Spin, Space, Button, Table, Tag, Select, Row, Col, Statistic, Modal, Descriptions } from 'antd';
import { ExportOutlined, ReloadOutlined, EyeOutlined, CopyOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { analyticsApi } from '../../api';
import { exportTableToCsv } from '../../utils/export';
import { useNavigate } from 'react-router-dom';
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
  const [copied, setCopied] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(false);
  const [refreshInterval, setRefreshInterval] = useState(30);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);
  const navigate = useNavigate();

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await analyticsApi.anomalies(dates[0].toISOString(), dates[1].toISOString(), undefined, severity, type, page, 20);
      setData(result.items);
      setTotal(result.total);
      setLastRefreshed(new Date());
    } catch (err) {
      setError(err instanceof Error ? err.message : t('analytics.loadError', 'Failed to load anomalies'));
    } finally {
      setLoading(false);
    }
  }, [dates, severity, type, page]);

  const copyDetails = async () => {
    if (!selectedRow) return;
    const text = `${t('analytics.type', 'Type')}: ${selectedRow.type}\n${t('common.status', 'Severity')}: ${selectedRow.severity}\n${t('analytics.dimension', 'Dimension')}: ${selectedRow.dimension}\n${t('analytics.message', 'Message')}: ${selectedRow.message}\n${t('analytics.currentValue', 'Current')}: ${selectedRow.currentValue}\n${t('analytics.baselineValue', 'Baseline')}: ${selectedRow.baselineValue}\n${t('analytics.timestamp', 'Time')}: ${new Date(selectedRow.detectedAt).toLocaleString()}`;
    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  useEffect(() => { fetchData(); }, [fetchData]);

  useEffect(() => {
    if (!autoRefresh) return;
    const id = setInterval(fetchData, refreshInterval * 1000);
    return () => clearInterval(id);
  }, [autoRefresh, refreshInterval, fetchData]);

  const severityColor = (severity: string) => {
    if (severity === 'Critical') return 'red';
    if (severity === 'High') return 'orange';
    if (severity === 'Medium') return 'gold';
    return 'default';
  };

  const renderDimension = (dimension: string) => {
    if (dimension.startsWith('model:')) {
      const modelName = dimension.slice('model:'.length);
      return <a onClick={(e) => { e.stopPropagation(); navigate(`/models?keyword=${encodeURIComponent(modelName)}`); }} style={{ fontFamily: 'monospace', cursor: 'pointer' }}>{dimension}</a>;
    }
    if (dimension.startsWith('provider:')) {
      const providerName = dimension.slice('provider:'.length);
      return <a onClick={(e) => { e.stopPropagation(); navigate(`/providers?keyword=${encodeURIComponent(providerName)}`); }} style={{ fontFamily: 'monospace', cursor: 'pointer' }}>{dimension}</a>;
    }
    return <span style={{ fontFamily: 'monospace' }}>{dimension}</span>;
  };

  const formatValue = (type: string, value: number) => {
    if (type === 'ErrorRateSpike' || type === 'RateLimitSpike') {
      return `${(value * 100).toFixed(1)}%`;
    }
    if (typeof value === 'number' && value < 1) {
      return value.toFixed(4);
    }
    return value.toString();
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
    { title: t('analytics.dimension', 'Dimension'), dataIndex: 'dimension', key: 'dimension', width: 240, render: (v: string) => renderDimension(v) },
    { title: t('analytics.message', 'Message'), dataIndex: 'message', key: 'message', ellipsis: true },
    {
      title: t('analytics.currentValue', 'Current'),
      dataIndex: 'currentValue',
      key: 'currentValue',
      align: 'right' as const,
      width: 120,
      render: (v: number, r: any) => <span style={{ fontFamily: 'monospace' }}>{formatValue(r.type, v)}</span>,
    },
    {
      title: t('analytics.baselineValue', 'Baseline'),
      dataIndex: 'baselineValue',
      key: 'baselineValue',
      align: 'right' as const,
      width: 120,
      render: (v: number, r: any) => <span style={{ fontFamily: 'monospace' }}>{formatValue(r.type, v)}</span>,
    },
    {
      title: t('common.actions', 'Actions'),
      key: 'actions',
      width: 100,
      fixed: 'right' as const,
      render: (_: any, r: any) => (
        <Button type="text" icon={<EyeOutlined />} title={t('common.show', 'View')} onClick={() => { setSelectedRow(r); setDetailOpen(true); }} />
      ),
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
              { value: 'Critical', label: t('analytics.severityLevel.Critical') },
              { value: 'High', label: t('analytics.severityLevel.High') },
              { value: 'Medium', label: t('analytics.severityLevel.Medium') },
            ]}
          />
          <Select
            value={type}
            onChange={(v) => { setType(v); setPage(1); }}
            allowClear
            style={{ width: 160 }}
            placeholder={t('analytics.type')}
            options={[
              { value: 'UsageSpike', label: t('analytics.anomalyType.UsageSpike') },
              { value: 'UsageDrop', label: t('analytics.anomalyType.UsageDrop') },
              { value: 'CostSpike', label: t('analytics.anomalyType.CostSpike') },
              { value: 'TokenSpike', label: t('analytics.anomalyType.TokenSpike') },
              { value: 'TokenDrop', label: t('analytics.anomalyType.TokenDrop') },
              { value: 'LatencySpike', label: t('analytics.anomalyType.LatencySpike') },
              { value: 'ErrorRateSpike', label: t('analytics.anomalyType.ErrorRateSpike') },
              { value: 'RateLimitSpike', label: t('analytics.anomalyType.RateLimitSpike') },
              { value: 'ProviderDegraded', label: t('analytics.anomalyType.ProviderDegraded') },
            ]}
          />
          <Button type="primary" onClick={fetchData}>{t('common.search')}</Button>
          <Button onClick={() => { setDates([dayjs().subtract(1, 'day'), dayjs()]); setSeverity(undefined); setType(undefined); setPage(1); }}>{t('common.reset')}</Button>
          <Button icon={<ReloadOutlined />} onClick={fetchData}>{t('common.refresh', 'Refresh')}</Button>
          <Select
            value={autoRefresh ? refreshInterval : undefined}
            onChange={(v) => { setRefreshInterval(v || 30); setAutoRefresh(!!v); }}
            allowClear
            style={{ width: 120 }}
            placeholder={t('common.refresh', 'Refresh')}
            options={[
              { value: 15, label: t('analytics.refreshInterval.15') },
              { value: 30, label: t('analytics.refreshInterval.30') },
              { value: 60, label: t('analytics.refreshInterval.60') },
              { value: 120, label: t('analytics.refreshInterval.120') },
            ]}
          />
          <Button icon={<ExportOutlined />} onClick={handleExport}>{t('common.export', 'Export')}</Button>
          {lastRefreshed && <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>{t('analytics.lastRefreshed', 'Last refreshed')}: {lastRefreshed.toLocaleTimeString()}</span>}
        </Space>
      </Card>

      {loading ? <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 120 }}><Spin size="large" /></div> : (
        <>
          {error && <Card style={{ borderRadius: 18, marginBottom: 24, borderColor: '#ff4d4f' }}>{error}</Card>}
          <Row gutter={[24, 24]} style={{ marginBottom: 24 }}>
            {['Critical', 'High', 'Medium'].map((sev) => (
              <Col key={sev} xs={24} sm={8}>
                <Card style={{ borderRadius: 18, cursor: 'pointer' }} onClick={() => { setSeverity(sev); setPage(1); }}>
                  <Statistic title={<Tag color={severityColor(sev)}>{t(`analytics.severityLevel.${sev}`)}</Tag>} value={severityCounts[sev] || 0} />
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
                scroll={{ x: 1100 }}
              />
            )}
          </Card>
        </>
      )}

      <Modal
        title={t('analytics.anomalies', 'Anomaly Detection')}
        open={detailOpen}
        onCancel={() => setDetailOpen(false)}
        width={720}
        footer={
          <Button icon={<CopyOutlined />} onClick={copyDetails}>
            {copied ? t('common.copied') : t('common.copy')}
          </Button>
        }
      >
        {selectedRow && (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label={t('analytics.type', 'Type')}>{selectedRow.type}</Descriptions.Item>
            <Descriptions.Item label={t('common.status', 'Severity')}>
              <Tag color={severityColor(selectedRow.severity)}>{selectedRow.severity}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('analytics.dimension', 'Dimension')}>
              {renderDimension(selectedRow.dimension)}
            </Descriptions.Item>
            <Descriptions.Item label={t('analytics.message', 'Message')}>{selectedRow.message}</Descriptions.Item>
            <Descriptions.Item label={t('analytics.currentValue', 'Current')}>
              <span style={{ fontFamily: 'monospace' }}>{formatValue(selectedRow.type, selectedRow.currentValue)}</span>
            </Descriptions.Item>
            <Descriptions.Item label={t('analytics.baselineValue', 'Baseline')}>
              <span style={{ fontFamily: 'monospace' }}>{formatValue(selectedRow.type, selectedRow.baselineValue)}</span>
            </Descriptions.Item>
            <Descriptions.Item label={t('analytics.timestamp', 'Timestamp')}>{new Date(selectedRow.detectedAt).toLocaleString()}</Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
    </div>
  );
}
