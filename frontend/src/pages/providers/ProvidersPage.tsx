import { useCallback } from 'react';
import { Table, Form, Input, Select, Switch, Space, Tag, Card, Button } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { providerApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, StatusDot, FormModal } from '../../components';
import type { Provider } from '../../types';

export default function ProvidersPage() {
  const { t } = useTranslation();

  const fetchFn = useCallback((params: any) => providerApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setSort, setKeyword } = useCrudList<Provider, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Provider>({
    createFn: providerApi.create,
    updateFn: providerApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(providerApi.delete, fetchData);

  const statusMap: Record<string, { color: 'success' | 'warning' | 'error' | 'default'; label: string }> = {
    Healthy: { color: 'success', label: t('provider.healthy') },
    Degraded: { color: 'warning', label: t('provider.degraded') },
    Unhealthy: { color: 'error', label: t('provider.unhealthy') },
    Unknown: { color: 'default', label: t('provider.unknown') },
  };

  const columns = [
    { title: t('provider.name'), dataIndex: 'name', key: 'name', sorter: true },
    { title: t('provider.protocol'), dataIndex: 'protocol', key: 'protocol', render: (v: string) => <Tag>{v}</Tag> },
    { title: t('provider.baseUrl'), dataIndex: 'baseUrl', key: 'baseUrl', ellipsis: true },
    {
      title: t('provider.healthStatus'), dataIndex: 'healthStatus', key: 'healthStatus',
      render: (v: string) => {
        const s = statusMap[v] || statusMap.Unknown;
        return <StatusDot color={s.color} text={s.label} />;
      },
    },
    { title: t('provider.keys'), key: 'keys', render: (_: any, r: Provider) => r.keys?.length || 0 },
    { title: t('common.enabled'), dataIndex: 'isEnabled', key: 'isEnabled', render: (v: boolean) => <Tag color={v ? 'success' : 'default'}>{v ? t('common.yes') : t('common.no')}</Tag> },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 120,
      render: (_: any, r: Provider) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('provider.title')} onCreate={openCreate} onSearch={setKeyword} />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          onChange={(_p, _f, sorter: any) => sorter.field && setSort(sorter.field, sorter.order === 'ascend' ? 'asc' : 'desc')}
          scroll={{ x: 800 }}
        />
      </Card>

      <FormModal title={t('provider.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="name" label={t('provider.name')} rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="protocol" label={t('provider.protocol')} rules={[{ required: true }]}>
          <Select options={[{ value: 'OpenAI', label: 'OpenAI' }, { value: 'Anthropic', label: 'Anthropic' }]} />
        </Form.Item>
        <Form.Item name="baseUrl" label={t('provider.baseUrl')} rules={[{ required: true }]}><Input placeholder="https://api.openai.com" /></Form.Item>
        <Form.Item name="description" label={t('provider.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
      </FormModal>
    </div>
  );
}
