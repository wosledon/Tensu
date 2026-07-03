import { useCallback } from 'react';
import { Table, Form, InputNumber, Select, Space, Card, Button, Tag } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { quotaApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { Quota } from '../../types';

const scopeOptions = [
  { value: 'org', label: 'quota.scopeOrg' },
  { value: 'key', label: 'quota.scopeKey' },
  { value: 'model', label: 'quota.scopeModel' },
];

export default function QuotasPage() {
  const { t } = useTranslation();

  const fetchFn = useCallback((params: any) => quotaApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<Quota, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Quota>({
    createFn: quotaApi.create,
    updateFn: quotaApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(quotaApi.delete, fetchData);

  const scopeLabelMap: Record<string, string> = {
    org: t('quota.scopeOrg'),
    key: t('quota.scopeKey'),
    model: t('quota.scopeModel'),
  };

  const columns = [
    {
      title: t('quota.scope'),
      dataIndex: 'scope',
      key: 'scope',
      render: (v: string) => <Tag>{scopeLabelMap[v] || v}</Tag>,
    },
    {
      title: t('quota.organization'),
      key: 'organization',
      render: (_: any, r: Quota) => (r.scope === 'org' ? r.organizationId ?? '-' : '-'),
    },
    {
      title: t('quota.apiKey'),
      key: 'apiKey',
      render: (_: any, r: Quota) => (r.scope === 'key' ? r.apiKeyId ?? '-' : '-'),
    },
    {
      title: t('quota.model'),
      key: 'model',
      render: (_: any, r: Quota) => (r.scope === 'model' ? r.modelId ?? '-' : '-'),
    },
    { title: t('quota.rpm'), dataIndex: 'rpm', key: 'rpm' },
    { title: t('quota.tpm'), dataIndex: 'tpm', key: 'tpm' },
    { title: t('quota.dailyTokenLimit'), dataIndex: 'dailyTokenLimit', key: 'dailyTokenLimit' },
    { title: t('quota.monthlyTokenLimit'), dataIndex: 'monthlyTokenLimit', key: 'monthlyTokenLimit' },
    { title: t('quota.concurrentRequestLimit'), dataIndex: 'concurrentRequestLimit', key: 'concurrentRequestLimit' },
    {
      title: t('common.actions'),
      key: 'actions',
      fixed: 'right' as const,
      width: 120,
      render: (_: any, r: Quota) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('quota.title')} onCreate={openCreate} onSearch={setKeyword} />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns}
          dataSource={data}
          rowKey="id"
          loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 900 }}
        />
      </Card>

      <FormModal
        title={t('quota.title')}
        open={open}
        form={form}
        editing={!!editing}
        submitting={submitting}
        onOk={submit}
        onCancel={close}
      >
        <Form.Item name="scope" label={t('quota.scope')} rules={[{ required: true }]}>
          <Select
            options={scopeOptions.map((o) => ({ value: o.value, label: t(o.label) }))}
            placeholder={t('common.all')}
          />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.scope !== cur.scope}>
          {({ getFieldValue }) => {
            const scope = getFieldValue('scope');
            if (scope === 'org') {
              return (
                <Form.Item name="organizationId" label={t('quota.organization')} rules={[{ required: true }]}>
                  <InputNumber min={1} style={{ width: '100%' }} />
                </Form.Item>
              );
            }
            if (scope === 'key') {
              return (
                <Form.Item name="apiKeyId" label={t('quota.apiKey')} rules={[{ required: true }]}>
                  <InputNumber min={1} style={{ width: '100%' }} />
                </Form.Item>
              );
            }
            if (scope === 'model') {
              return (
                <Form.Item name="modelId" label={t('quota.model')} rules={[{ required: true }]}>
                  <InputNumber min={1} style={{ width: '100%' }} />
                </Form.Item>
              );
            }
            return null;
          }}
        </Form.Item>
        <Form.Item name="rpm" label={t('quota.rpm')}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="tpm" label={t('quota.tpm')}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="dailyTokenLimit" label={t('quota.dailyTokenLimit')}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="monthlyTokenLimit" label={t('quota.monthlyTokenLimit')}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="concurrentRequestLimit" label={t('quota.concurrentRequestLimit')}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
