import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, DatePicker, Space, Card, Button, App } from 'antd';
import { DeleteOutlined, StopOutlined, CopyOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiKeyApi, modelApi, orgApi } from '../../api';
import { useCrudList, useConfirmDelete } from '../../hooks';
import { PageHeader, StatusDot, FormModal } from '../../components';
import type { ApiKey, Organization } from '../../types';
import dayjs from 'dayjs';
import { exportTableToCsv } from '../../utils/export';

function isValidIpOrCidr(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return true;

  const ipv4Cidr = /^(\d{1,3}\.){3}\d{1,3}(\/\d{1,2})?$/;
  const ipv6Cidr = /^([0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4}(\/\d{1,3})?$/;
  const ipv6Bracketed = /^\[([0-9a-fA-F:]+)\](?:\/\d{1,3})?$/;

  if (ipv4Cidr.test(trimmed)) {
    const parts = trimmed.split('/');
    const ip = parts[0];
    const cidr = parts[1];
    const octets = ip.split('.');
    if (octets.length !== 4) return false;
    if (octets.some((o) => Number(o) > 255)) return false;
    if (cidr !== undefined && (Number(cidr) < 0 || Number(cidr) > 32)) return false;
    return true;
  }

  if (trimmed.startsWith('[')) {
    if (ipv6Bracketed.test(trimmed)) return true;
    return false;
  }

  if (ipv6Cidr.test(trimmed)) {
    const parts = trimmed.split('/');
    const cidr = parts[1];
    if (cidr !== undefined && (Number(cidr) < 0 || Number(cidr) > 128)) return false;
    return true;
  }

  return false;
}

export default function ApiKeysPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();

  const ipWhitelistValidator = useCallback((_: unknown, value: string) => {
    if (!value || !value.trim()) return Promise.resolve();
    const entries = value.split(/[,\n；;]/).map((s) => s.trim()).filter(Boolean);
    const invalid = entries.find((entry) => !isValidIpOrCidr(entry));
    if (invalid) {
      return Promise.reject(new Error(t('apiKey.invalidIpOrCidr', { value: invalid })));
    }
    return Promise.resolve();
  }, [t]);

  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [models, setModels] = useState<{ id: number; name: string }[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [form] = Form.useForm();
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const fetchFn = useCallback((params: any) => apiKeyApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<ApiKey, any>({ fetchFn });
  const { handleDelete } = useConfirmDelete(apiKeyApi.delete, fetchData);

  const handleBatchDelete = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await apiKeyApi.batchDelete(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const handleBatchRevoke = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await apiKeyApi.batchRevoke(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const ensureOrgs = async () => {
    if (!orgs.length) {
      try { setOrgs(await orgApi.tree()); } catch {}
    }
  };

  const ensureModels = async () => {
    if (!models.length) {
      try {
        const data = await modelApi.list({ page: 1, pageSize: 1000 });
        setModels(data.items.map((m: any) => ({ id: m.id, name: `${m.provider?.name || ''}-${m.name}` })));
      } catch {}
    }
  };

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      if (values.expiresAt) values.expiresAt = values.expiresAt.toISOString();
      const res = await apiKeyApi.create(values);
      setCreatedKey(res.key);
      fetchData();
    } catch {}
  };

  const handleRevoke = async (id: number) => {
    try { await apiKeyApi.revoke(id); message.success(t('common.success')); fetchData(); }
    catch { message.error(t('common.error')); }
  };

  const statusColors: Record<string, 'success' | 'default' | 'warning' | 'error'> = { Active: 'success', Disabled: 'default', RateLimited: 'warning', Expired: 'error' };

  const columns = [
    { title: t('apiKey.name'), dataIndex: 'name', key: 'name', sorter: true },
    {
      title: t('apiKey.key'), key: 'keyPrefix',
      render: (_: any, r: ApiKey) => <span style={{ fontFamily: 'monospace' }}>{r.keyPrefix}...</span>,
    },
    { title: t('apiKey.organization'), key: 'org', render: (_: any, r: ApiKey) => r.organization?.name },
    {
      title: t('common.status'), dataIndex: 'status', key: 'status',
      render: (v: string) => <StatusDot color={statusColors[v] || 'default'} text={v} />,
    },
    { title: t('apiKey.expiresAt'), dataIndex: 'expiresAt', key: 'expiresAt', render: (v: string) => v ? dayjs(v).format('YYYY-MM-DD') : '-' },
    {
      title: t('apiKey.ipWhitelist'), dataIndex: 'ipWhitelist', key: 'ipWhitelist', ellipsis: true,
      render: (v: string) => v ? <span style={{ fontFamily: 'monospace' }}>{v}</span> : '-',
    },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 120,
      render: (_: any, r: ApiKey) => (
        <Space>
          {r.status === 'Active' && (
            <Button type="text" danger icon={<StopOutlined />} onClick={() => handleRevoke(r.id)} title={t('apiKey.revoke')} />
          )}
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const handleExport = () => {
    exportTableToCsv('api-keys', columns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('apiKey.title')}
        onCreate={async () => { await ensureOrgs(); await ensureModels(); form.resetFields(); setCreatedKey(null); setModalOpen(true); }}
        onSearch={setKeyword}
        extra={
          <Space>
            <Button icon={<ExportOutlined />} onClick={handleExport}>
              {t('common.export', 'Export')}
            </Button>
            {selectedRowKeys.length > 0 && (
              <>
                <Button onClick={handleBatchRevoke}>{t('apiKey.revoke')}</Button>
                <Button danger onClick={handleBatchDelete}>{t('common.delete')}</Button>
              </>
            )}
          </Space>
        }
      />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 700 }}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys: React.Key[]) => setSelectedRowKeys(keys),
          }}
        />
      </Card>

      <FormModal
        title={createdKey ? t('apiKey.created') : t('apiKey.title')}
        open={modalOpen}
        form={form}
        editing={false}
        onOk={createdKey ? () => { setModalOpen(false); setCreatedKey(null); } : handleCreate}
        onCancel={() => { setModalOpen(false); setCreatedKey(null); form.resetFields(); }}
      >
        {createdKey ? (
          <div>
            <Input.TextArea value={createdKey} readOnly rows={3} style={{ fontFamily: 'monospace', marginBottom: 12 }} />
            <Button icon={<CopyOutlined />} onClick={() => { navigator.clipboard.writeText(createdKey); message.success(t('common.copied')); }}>
              {t('common.copy')}
            </Button>
          </div>
        ) : (
          <>
            <Form.Item name="name" label={t('apiKey.name')} rules={[{ required: true }]}><Input /></Form.Item>
            <Form.Item name="organizationId" label={t('apiKey.organization')} rules={[{ required: true }]}>
              <Select options={orgs.map((o) => ({ value: o.id, label: o.name }))} />
            </Form.Item>
            <Form.Item name="expiresAt" label={t('apiKey.expiresAt')}><DatePicker style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="allowedModels" label={t('apiKey.allowedModels')}>
              <Select mode="multiple" options={models.map((m) => ({ value: m.id, label: m.name }))} placeholder={t('common.all')} allowClear />
            </Form.Item>
            <Form.Item name="ipWhitelist" label={t('apiKey.ipWhitelist')} rules={[{ validator: ipWhitelistValidator }]}>
              <Input.TextArea rows={2} placeholder="e.g. 192.168.1.0/24, 10.0.0.1, ::1" />
            </Form.Item>
            <Form.Item name="rateLimitRpm" label={t('quota.rpm')}><Input type="number" /></Form.Item>
          </>
        )}
      </FormModal>
    </div>
  );
}
