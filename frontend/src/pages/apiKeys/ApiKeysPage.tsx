import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, DatePicker, Space, Card, Button, message } from 'antd';
import { DeleteOutlined, StopOutlined, CopyOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiKeyApi, orgApi } from '../../api';
import { useCrudList, useConfirmDelete } from '../../hooks';
import { PageHeader, StatusDot, FormModal } from '../../components';
import type { ApiKey, Organization } from '../../types';
import dayjs from 'dayjs';

export default function ApiKeysPage() {
  const { t } = useTranslation();
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [modalOpen, setModalOpen] = useState(false);
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [form] = Form.useForm();

  const fetchFn = useCallback((params: any) => apiKeyApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<ApiKey, any>({ fetchFn });
  const { handleDelete } = useConfirmDelete(apiKeyApi.delete, fetchData);

  const ensureOrgs = async () => {
    if (!orgs.length) {
      try { setOrgs(await orgApi.tree()); } catch {}
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

  return (
    <div>
      <PageHeader
        title={t('apiKey.title')}
        onCreate={async () => { await ensureOrgs(); form.resetFields(); setCreatedKey(null); setModalOpen(true); }}
        onSearch={setKeyword}
      />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 700 }}
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
            <Form.Item name="allowedModels" label={t('apiKey.allowedModels')}><Input.TextArea rows={2} placeholder="model1,model2 (empty = all)" /></Form.Item>
            <Form.Item name="rateLimitRpm" label="RPM"><Input type="number" /></Form.Item>
          </>
        )}
      </FormModal>
    </div>
  );
}
