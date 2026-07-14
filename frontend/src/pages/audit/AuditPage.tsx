import { useCallback, useState } from 'react';
import { Table, Card, Tag, Descriptions, Modal, Switch, Space, Typography, Button, Tabs } from 'antd';
import { ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { auditApi } from '../../api';
import { useCrudList } from '../../hooks';
import { PageHeader, StatusDot } from '../../components';
import type { RequestLog, ArchivedRequestLog } from '../../types';
import dayjs from 'dayjs';
import { exportTableToCsv } from '../../utils/export';
import { useAuth } from '../../hooks/useAuth';

const { Paragraph } = Typography;

function desensitizeText(text: string) {
  if (!text) return text;
  let result = text;

  result = result.replace(/"api[_-]?key"\s*:\s*"[^"]{8,}"/gi, '"api_key":"***"');
  result = result.replace(/"token"\s*:\s*"[^"]{8,}"/gi, '"token":"***"');
  result = result.replace(/"authorization"\s*:\s*"[^"]{8,}"/gi, '"authorization":"***"');
  result = result.replace(/sk-[A-Za-z0-9]{20,}/g, 'sk-***');
  result = result.replace(/pk-[A-Za-z0-9]{20,}/g, 'pk-***');
  result = result.replace(/[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}/g, '***@***.***');
  result = result.replace(/\b(\d{1,3}\.){3}\d{1,3}\b/g, '***.***.***.***');
  result = result.replace(/\b([0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4}\b/g, '***');

  return result;
}

export default function AuditPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<RequestLog | ArchivedRequestLog | null>(null);
  const [desensitize, setDesensitize] = useState(true);
  const [activeTab, setActiveTab] = useState('current');

  const currentFetchFn = useCallback((params: any) => auditApi.list(params), []);
  const { data: currentData, total: currentTotal, loading: currentLoading, params: currentParams, setPage: setCurrentPage, setKeyword: setCurrentKeyword } = useCrudList<RequestLog, any>({ fetchFn: currentFetchFn });

  const archivedFetchFn = useCallback((params: any) => auditApi.listArchived(params), []);
  const { data: archivedData, total: archivedTotal, loading: archivedLoading, params: archivedParams, setPage: setArchivedPage, setKeyword: setArchivedKeyword } = useCrudList<ArchivedRequestLog, any>({ fetchFn: archivedFetchFn });

  const statusColors: Record<string, 'success' | 'error' | 'warning' | 'default'> = {
    Success: 'success', Failed: 'error', Timeout: 'warning', Interrupted: 'default', RateLimited: 'warning',
  };

  const statusLabelMap: Record<string, string> = {
    Success: t('audit.statusSuccess'),
    Failed: t('audit.statusFailed'),
    Timeout: t('audit.statusTimeout'),
    Interrupted: t('audit.statusInterrupted'),
    RateLimited: t('audit.statusRateLimited'),
  };

  const currentColumns = [
    {
      title: t('audit.requestId'), dataIndex: 'requestId', key: 'requestId', width: 200,
      render: (v: string) => <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{v.substring(0, 16)}...</span>,
    },
    { title: t('audit.timestamp'), dataIndex: 'timestamp', key: 'timestamp', sorter: true, render: (v: string) => dayjs(v).format('MM-DD HH:mm:ss') },
    { title: t('audit.model'), dataIndex: 'modelName', key: 'modelName', render: (v: string) => <Tag>{v}</Tag> },
    { title: t('audit.provider'), dataIndex: 'providerName', key: 'providerName' },
    { title: t('audit.inputTokens'), dataIndex: 'inputTokens', key: 'inputTokens', align: 'right' as const, render: (v: number) => v?.toLocaleString() ?? '-' },
    { title: t('audit.outputTokens'), dataIndex: 'outputTokens', key: 'outputTokens', align: 'right' as const, render: (v: number) => v?.toLocaleString() ?? '-' },
    { title: t('audit.latency'), key: 'latency', align: 'right' as const, render: (_: any, r: RequestLog) => r.totalDurationMs ? `${r.totalDurationMs}ms` : '-' },
    { title: t('audit.ttft'), key: 'ttft', align: 'right' as const, render: (_: any, r: RequestLog) => r.timeToFirstTokenMs ? `${r.timeToFirstTokenMs}ms` : '-' },
    { title: t('audit.speed'), key: 'speed', align: 'right' as const, render: (_: any, r: RequestLog) => r.outputTokensPerSecond ? `${r.outputTokensPerSecond.toFixed(1)} t/s` : '-' },
    { title: t('audit.status'), dataIndex: 'status', key: 'status', render: (v: string) => <StatusDot color={statusColors[v] || 'default'} text={statusLabelMap[v] || v} /> },
    { title: t('audit.cacheHit'), dataIndex: 'cacheHit', key: 'cacheHit', render: (v: boolean) => v ? <Tag color="purple">{t('audit.cacheHitHit')}</Tag> : <Tag>{t('audit.cacheHitMiss')}</Tag> },
  ];

  const archivedColumns = [
    {
      title: t('audit.requestId'), dataIndex: 'requestId', key: 'requestId', width: 200,
      render: (v: string) => <span style={{ fontFamily: 'monospace', fontSize: 12 }}>{v.substring(0, 16)}...</span>,
    },
    { title: t('audit.timestamp'), dataIndex: 'timestamp', key: 'timestamp', sorter: true, render: (v: string) => dayjs(v).format('MM-DD HH:mm:ss') },
    { title: t('audit.model'), dataIndex: 'modelName', key: 'modelName', render: (v: string) => <Tag>{v}</Tag> },
    { title: t('audit.provider'), dataIndex: 'providerName', key: 'providerName' },
    { title: t('audit.inputTokens'), dataIndex: 'inputTokens', key: 'inputTokens', align: 'right' as const, render: (v: number) => v?.toLocaleString() ?? '-' },
    { title: t('audit.outputTokens'), dataIndex: 'outputTokens', key: 'outputTokens', align: 'right' as const, render: (v: number) => v?.toLocaleString() ?? '-' },
    { title: t('audit.latency'), key: 'latency', align: 'right' as const, render: (_: any, r: ArchivedRequestLog) => r.totalDurationMs ? `${r.totalDurationMs}ms` : '-' },
    { title: t('audit.ttft'), key: 'ttft', align: 'right' as const, render: (_: any, r: ArchivedRequestLog) => r.timeToFirstTokenMs ? `${r.timeToFirstTokenMs}ms` : '-' },
    { title: t('audit.speed'), key: 'speed', align: 'right' as const, render: (_: any, r: ArchivedRequestLog) => r.outputTokensPerSecond ? `${r.outputTokensPerSecond.toFixed(1)} t/s` : '-' },
    { title: t('audit.status'), dataIndex: 'status', key: 'status', render: (v: string) => <StatusDot color={statusColors[v] || 'default'} text={statusLabelMap[v] || v} /> },
    { title: t('audit.cacheHit'), dataIndex: 'cacheHit', key: 'cacheHit', render: (v: boolean) => v ? <Tag color="purple">{t('audit.cacheHitHit')}</Tag> : <Tag>{t('audit.cacheHitMiss')}</Tag> },
  ];

  const handleExportCurrent = () => {
    exportTableToCsv('audit-logs', currentColumns, currentData);
  };

  const handleExportArchived = () => {
    exportTableToCsv('archived-logs', archivedColumns, archivedData);
  };

  const renderContentBlock = (label: string, value?: string) => {
    if (!value) return null;
    const display = desensitize ? desensitizeText(value) : value;
    return (
      <Descriptions.Item label={label} span={2}>
        <Paragraph copyable style={{ fontFamily: 'monospace', fontSize: 12, whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
          {display}
        </Paragraph>
      </Descriptions.Item>
    );
  };

  const renderDetail = (record: RequestLog | ArchivedRequestLog) => {
    return (
      <>
        <Space style={{ marginBottom: 16 }}>
          <span>{t('audit.desensitization')}</span>
          <Switch checked={desensitize} onChange={setDesensitize} />
        </Space>
        <Descriptions bordered column={2} size="small">
          <Descriptions.Item label={t('audit.requestId')} span={2}>
            <span style={{ fontFamily: 'monospace' }}>{record.requestId}</span>
          </Descriptions.Item>
          <Descriptions.Item label={t('audit.timestamp')}>{dayjs(record.timestamp).format('YYYY-MM-DD HH:mm:ss')}</Descriptions.Item>
          <Descriptions.Item label={t('audit.model')}>{record.modelName}</Descriptions.Item>
          <Descriptions.Item label={t('audit.provider')}>{record.providerName || '-'}</Descriptions.Item>
          <Descriptions.Item label={t('common.status')}><StatusDot color={statusColors[record.status] || 'default'} text={record.status} /></Descriptions.Item>
          <Descriptions.Item label={t('audit.inputTokens')}>{record.inputTokens?.toLocaleString() ?? '-'}</Descriptions.Item>
          <Descriptions.Item label={t('audit.outputTokens')}>{record.outputTokens?.toLocaleString() ?? '-'}</Descriptions.Item>
          <Descriptions.Item label={t('audit.latency')}>{record.totalDurationMs ? `${record.totalDurationMs}ms` : '-'}</Descriptions.Item>
          <Descriptions.Item label={t('audit.ttft')}>{record.timeToFirstTokenMs ? `${record.timeToFirstTokenMs}ms` : '-'}</Descriptions.Item>
          <Descriptions.Item label={t('audit.speed')}>{record.outputTokensPerSecond ? `${record.outputTokensPerSecond.toFixed(2)} t/s` : '-'}</Descriptions.Item>
          <Descriptions.Item label={t('audit.cacheHit')}>{record.cacheHit ? <Tag color="purple">{t('audit.cacheHitHit')}</Tag> : <Tag>{t('audit.cacheHitMiss')}</Tag>}</Descriptions.Item>
          {'semanticCacheHit' in record && <Descriptions.Item label={t('audit.semanticCacheHit')}>{record.semanticCacheHit ? <Tag color="cyan">{t('audit.cacheHitHit')}</Tag> : <Tag>{t('audit.cacheHitMiss')}</Tag>}</Descriptions.Item>}
          {'compressionApplied' in record && <Descriptions.Item label={t('audit.compression')}>{record.compressionApplied ? <Tag color="green">{t('audit.cacheHitHit')}</Tag> : <Tag>{t('audit.cacheHitMiss')}</Tag>}</Descriptions.Item>}
          {'compressionStrategy' in record && record.compressionStrategy && <Descriptions.Item label={t('audit.compressionStrategy')}>{record.compressionStrategy}</Descriptions.Item>}
          {'retryCount' in record && record.retryCount !== undefined && <Descriptions.Item label={t('audit.retryCount')}>{record.retryCount}</Descriptions.Item>}
          {'currency' in record && record.currency && <Descriptions.Item label={t('audit.currency')}>{record.currency}</Descriptions.Item>}
          {'inputCost' in record && record.inputCost !== undefined && <Descriptions.Item label={t('audit.inputCost')}>{record.inputCost?.toFixed(4)}</Descriptions.Item>}
          {'outputCost' in record && record.outputCost !== undefined && <Descriptions.Item label={t('audit.outputCost')}>{record.outputCost?.toFixed(4)}</Descriptions.Item>}
          {record.errorCode && <Descriptions.Item label={t('audit.error')} span={2}><Tag color="error">{record.errorCode}</Tag> {record.errorMessage}</Descriptions.Item>}
          {renderContentBlock(t('audit.requestContent'), record.requestContent)}
          {renderContentBlock(t('audit.responseContent'), record.responseContent)}
        </Descriptions>
      </>
    );
  };

  return (
    <div>
      <PageHeader
        title={t('audit.title')}
        onSearch={(v) => {
          if (activeTab === 'current') setCurrentKeyword(v || undefined);
          else setArchivedKeyword(v || undefined);
        }}
        searchPlaceholder={t('audit.requestId')}
        extra={
          <Space>
            {activeTab === 'current' ? (
              <Button icon={<ExportOutlined />} onClick={handleExportCurrent}>
                {t('common.export', 'Export')}
              </Button>
            ) : (
              <Button icon={<ExportOutlined />} onClick={handleExportArchived}>
                {t('common.export', 'Export')}
              </Button>
            )}
          </Space>
        }
      />
      <Card style={{ borderRadius: 18 }}>
        <Tabs
          activeKey={activeTab}
          onChange={setActiveTab}
          items={[
            {
              key: 'current',
              label: t('audit.current'),
              children: (
                <Table
                  columns={currentColumns} dataSource={currentData} rowKey="id" loading={currentLoading}
                  pagination={{ current: currentParams.page, pageSize: currentParams.pageSize, total: currentTotal, showSizeChanger: true, onChange: setCurrentPage }}
                  onRow={(record) => ({ onClick: () => { setSelected(record); setDetailOpen(true); }, style: { cursor: 'pointer' } })}
                  scroll={{ x: 1200 }} size="small"
                />
              ),
            },
            ...(user?.role === 'SuperAdmin' ? [{
              key: 'archived',
              label: t('audit.archived'),
              children: (
                <Table
                  columns={archivedColumns} dataSource={archivedData} rowKey="requestId" loading={archivedLoading}
                  pagination={{ current: archivedParams.page, pageSize: archivedParams.pageSize, total: archivedTotal, showSizeChanger: true, onChange: setArchivedPage }}
                  onRow={(record) => ({ onClick: () => { setSelected(record); setDetailOpen(true); }, style: { cursor: 'pointer' } })}
                  scroll={{ x: 1200 }} size="small"
                />
              ),
            }] : []),
          ]}
        />
      </Card>

      <Modal title={t('audit.requestId')} open={detailOpen} onCancel={() => setDetailOpen(false)} footer={null} width={800}>
        {selected && renderDetail(selected)}
      </Modal>
    </div>
  );
}
