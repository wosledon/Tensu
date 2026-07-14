import { useState } from 'react';
import { Card, Input, Button, Typography, Space, Table, Tag, App, Modal } from 'antd';
import { SearchOutlined, CopyOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { compressionApi } from '../../api';
import { PageHeader } from '../../components';
import type { ColumnsType } from 'antd/es/table';

const { Text } = Typography;

interface CompressionMapping {
  id: number;
  decompressionKey: string;
  strategy: string;
  createdAt: string;
  hasOriginalBody: boolean;
  originalBodyLength?: number;
  compressedBodyLength: number;
}

export default function CompressionRestorePage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [searchKey, setSearchKey] = useState('');
  const [mappings, setMappings] = useState<CompressionMapping[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [restoringKey, setRestoringKey] = useState<string | null>(null);
  const [restoredBody, setRestoredBody] = useState<string | null>(null);
  const [restoreModalOpen, setRestoreModalOpen] = useState(false);

  const fetchMappings = async (key?: string, p = 1, ps = 20) => {
    setLoading(true);
    try {
      const res = await compressionApi.mappings({ requestId: key, page: p, pageSize: ps });
      const items = (res as any).items.map((m: any) => ({
        ...m,
        hasOriginalBody: m.hasOriginalBody ?? false,
      }));
      setMappings(items);
      setTotal((res as any).total);
      setPage((res as any).page);
      setPageSize((res as any).pageSize);
    } catch {
      message.error(t('common.error'));
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => {
    fetchMappings(searchKey || undefined, 1, pageSize);
  };

  const handleRestore = async (decompressionKey: string) => {
    setRestoringKey(decompressionKey);
    try {
      const res = await compressionApi.restore(decompressionKey) as any;
      setRestoredBody(res.originalBody);
      setRestoreModalOpen(true);
    } catch {
      message.error(t('common.error'));
    } finally {
      setRestoringKey(null);
    }
  };

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    message.success(t('common.copied'));
  };

  const handleDelete = async (decompressionKey: string) => {
    try {
      await compressionApi.deleteMapping(decompressionKey);
      message.success(t('common.success'));
      fetchMappings(searchKey || undefined, page, pageSize);
    } catch {
      message.error(t('common.error'));
    }
  };

  const columns: ColumnsType<CompressionMapping> = [
    {
      title: t('compression.id'),
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: t('compression.decompressionKey'),
      dataIndex: 'decompressionKey',
      key: 'decompressionKey',
      ellipsis: true,
      width: 280,
    },
    {
      title: t('compression.strategy'),
      dataIndex: 'strategy',
      key: 'strategy',
      width: 140,
      render: (strategy: string) => <Tag>{strategy}</Tag>,
    },
    {
      title: t('compression.originalBody'),
      dataIndex: 'hasOriginalBody',
      key: 'hasOriginalBody',
      width: 130,
      render: (has: boolean, record) => (
        <Tag color={has ? 'green' : 'default'}>
          {has ? `${record.originalBodyLength ?? 0} ${t('compression.chars')}` : t('compression.notStored')}
        </Tag>
      ),
    },
    {
      title: t('compression.compressedLength'),
      dataIndex: 'compressedBodyLength',
      key: 'compressedBodyLength',
      width: 150,
      render: (len: number) => <Text code>{len.toLocaleString()}</Text>,
    },
    {
      title: t('compression.createdAt'),
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 180,
    },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 200,
      render: (_, record) => (
        <Space>
          <Button
            type="link"
            icon={<SearchOutlined />}
            onClick={() => handleRestore(record.decompressionKey)}
            loading={restoringKey === record.decompressionKey}
            disabled={!record.hasOriginalBody}
          >
            {t('compression.restore')}
          </Button>
          <Button type="link" danger icon={<DeleteOutlined />} onClick={() => handleDelete(record.decompressionKey)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('compression.restoreTitle')}
        onSearch={(keyword) => { setSearchKey(keyword); fetchMappings(keyword || undefined, 1, pageSize); }}
        searchPlaceholder={t('compression.decompressionKey')}
      />
      <Card>
        <Space style={{ marginBottom: 16 }}>
          <Input
            placeholder={t('compression.decompressionKey')}
            value={searchKey}
            onChange={(e) => setSearchKey(e.target.value)}
            onPressEnter={handleSearch}
            style={{ width: 320 }}
          />
          <Button type="primary" icon={<SearchOutlined />} onClick={handleSearch}>
            {t('common.search')}
          </Button>
          <Button onClick={() => { setSearchKey(''); fetchMappings(undefined, 1, pageSize); }}>
            {t('common.reset')}
          </Button>
        </Space>
        <Table
          columns={columns}
          dataSource={mappings}
          rowKey="decompressionKey"
          loading={loading}
          pagination={{
            current: page,
            pageSize,
            total,
            onChange: (p, ps) => fetchMappings(searchKey || undefined, p, ps),
            showSizeChanger: true,
            showTotal: (count) => t('common.totalItems', { total: count }),
          }}
          scroll={{ x: 1000 }}
        />
      </Card>

      <Modal
        title={t('compression.restoreModalTitle')}
        open={restoreModalOpen}
        onCancel={() => setRestoreModalOpen(false)}
        width={800}
        footer={
          <Space>
            <Button onClick={() => setRestoreModalOpen(false)}>{t('compression.close')}</Button>
            {restoredBody && (
              <Button icon={<CopyOutlined />} onClick={() => handleCopy(restoredBody)}>
                {t('common.copy')}
              </Button>
            )}
          </Space>
        }
      >
        {restoredBody && (
          <pre style={{ background: '#f5f5f5', padding: 12, borderRadius: 6, maxHeight: 500, overflow: 'auto', whiteSpace: 'pre-wrap', wordBreak: 'break-all' }}>
            {restoredBody}
          </pre>
        )}
      </Modal>
    </div>
  );
}
