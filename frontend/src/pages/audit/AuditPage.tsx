import { useCallback, useState } from 'react';
import { Table, Card, Tag, Descriptions, Modal } from 'antd';
import { useTranslation } from 'react-i18next';
import { auditApi } from '../../api';
import { useCrudList } from '../../hooks';
import { PageHeader, StatusDot } from '../../components';
import type { RequestLog } from '../../types';
import dayjs from 'dayjs';

export default function AuditPage() {
  const { t } = useTranslation();
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<RequestLog | null>(null);

  const fetchFn = useCallback((params: any) => auditApi.list(params), []);
  const { data, total, loading, params, setPage, setKeyword } = useCrudList<RequestLog, any>({ fetchFn });

  const statusColors: Record<string, 'success' | 'error' | 'warning' | 'default'> = {
    Success: 'success', Failed: 'error', Timeout: 'warning', Interrupted: 'default', RateLimited: 'warning',
  };

  const columns = [
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
    { title: t('audit.status'), dataIndex: 'status', key: 'status', render: (v: string) => <StatusDot color={statusColors[v] || 'default'} text={v} /> },
    { title: t('audit.cacheHit'), dataIndex: 'cacheHit', key: 'cacheHit', render: (v: boolean) => v ? <Tag color="purple">HIT</Tag> : <Tag>MISS</Tag> },
  ];

  return (
    <div>
      <PageHeader title={t('audit.title')} onSearch={(v) => setKeyword(v || undefined)} searchPlaceholder={t('audit.requestId')} />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          onRow={(record) => ({ onClick: () => { setSelected(record); setDetailOpen(true); }, style: { cursor: 'pointer' } })}
          scroll={{ x: 1200 }} size="small"
        />
      </Card>

      <Modal title={t('audit.requestId')} open={detailOpen} onCancel={() => setDetailOpen(false)} footer={null} width={700}>
        {selected && (
          <Descriptions bordered column={2} size="small">
            <Descriptions.Item label={t('audit.requestId')} span={2}>
              <span style={{ fontFamily: 'monospace' }}>{selected.requestId}</span>
            </Descriptions.Item>
            <Descriptions.Item label={t('audit.timestamp')}>{dayjs(selected.timestamp).format('YYYY-MM-DD HH:mm:ss')}</Descriptions.Item>
            <Descriptions.Item label={t('audit.model')}>{selected.modelName}</Descriptions.Item>
            <Descriptions.Item label={t('audit.provider')}>{selected.providerName}</Descriptions.Item>
            <Descriptions.Item label={t('common.status')}><StatusDot color={statusColors[selected.status] || 'default'} text={selected.status} /></Descriptions.Item>
            <Descriptions.Item label={t('audit.inputTokens')}>{selected.inputTokens?.toLocaleString() ?? '-'}</Descriptions.Item>
            <Descriptions.Item label={t('audit.outputTokens')}>{selected.outputTokens?.toLocaleString() ?? '-'}</Descriptions.Item>
            <Descriptions.Item label={t('audit.latency')}>{selected.totalDurationMs ? `${selected.totalDurationMs}ms` : '-'}</Descriptions.Item>
            <Descriptions.Item label={t('audit.ttft')}>{selected.timeToFirstTokenMs ? `${selected.timeToFirstTokenMs}ms` : '-'}</Descriptions.Item>
            <Descriptions.Item label={t('audit.speed')}>{selected.outputTokensPerSecond ? `${selected.outputTokensPerSecond.toFixed(2)} t/s` : '-'}</Descriptions.Item>
            <Descriptions.Item label={t('audit.cacheHit')}>{selected.cacheHit ? <Tag color="purple">HIT</Tag> : <Tag>MISS</Tag>}</Descriptions.Item>
            {selected.errorCode && <Descriptions.Item label="Error" span={2}><Tag color="error">{selected.errorCode}</Tag> {selected.errorMessage}</Descriptions.Item>}
          </Descriptions>
        )}
      </Modal>
    </div>
  );
}
