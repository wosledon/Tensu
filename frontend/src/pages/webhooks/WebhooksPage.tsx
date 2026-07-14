import { useCrudList } from '../../hooks/useCrudList';
import { useFormModal } from '../../hooks/useFormModal';
import { useConfirmDelete } from '../../hooks/useConfirmDelete';
import { webhookApi } from '../../api';
import type { WebhookNotification, PagedRequest } from '../../types';
import { Table, Button, Space, Tag, Switch, Input, Card, Form, Select } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { PageHeader, FormModal } from '../../components';

export default function WebhooksPage() {
  const { t } = useTranslation();
  const { data, total, loading, params, setPage, fetchData } = useCrudList<WebhookNotification, PagedRequest>({
    fetchFn: webhookApi.list,
  });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<WebhookNotification>({
    createFn: webhookApi.create,
    updateFn: webhookApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(webhookApi.delete, fetchData);

  const columns: ColumnsType<WebhookNotification> = [
    {
      title: t('webhook.name'),
      dataIndex: 'name',
      key: 'name',
      width: 200,
    },
    {
      title: t('webhook.url'),
      dataIndex: 'url',
      key: 'url',
      ellipsis: true,
    },
    {
      title: t('webhook.events'),
      dataIndex: 'events',
      key: 'events',
      width: 250,
      render: (events: string) => {
        try {
          const parsed = JSON.parse(events);
          return Array.isArray(parsed) ? parsed.map((e: string) => <Tag key={e}>{e}</Tag>) : <Tag>{events}</Tag>;
        } catch {
          return <Tag>{events}</Tag>;
        }
      },
    },
    {
      title: t('common.status'),
      dataIndex: 'isEnabled',
      key: 'isEnabled',
      width: 100,
      render: (enabled: boolean) => (
        <Tag color={enabled ? 'green' : 'default'}>{enabled ? t('common.enabled') : t('common.disabled')}</Tag>
      ),
    },
    {
      title: t('common.actions'),
      key: 'actions',
      width: 150,
      render: (_, record) => (
        <Space>
          <Button type="link" icon={<EditOutlined />} onClick={() => openEdit(record)} />
          <Button type="link" danger icon={<DeleteOutlined />} onClick={() => handleDelete(record.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('nav.webhooks')}
        onCreate={openCreate}
      />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns}
          dataSource={data}
          rowKey="id"
          loading={loading}
          pagination={{
            current: params.page,
            pageSize: params.pageSize,
            total,
            onChange: (page, pageSize) => setPage(page, pageSize),
            showSizeChanger: true,
            showTotal: (count) => t('common.totalItems', { total: count }),
          }}
          scroll={{ x: 900 }}
        />
      </Card>

      <FormModal
        title={editing ? t('webhook.edit') : t('webhook.create')}
        open={open}
        form={form}
        editing={!!editing}
        submitting={submitting}
        onOk={submit}
        onCancel={close}
      >
        <Form.Item name="name" label={t('webhook.name')} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="url" label={t('webhook.url')} rules={[{ required: true }, { type: 'url' }]}>
          <Input placeholder="https://example.com/webhook" />
        </Form.Item>
        <Form.Item name="secret" label={t('webhook.secret')}>
          <Input.Password placeholder={t('webhook.secretPlaceholder')} />
        </Form.Item>
        <Form.Item name="events" label={t('webhook.events')} rules={[{ required: true }]}>
          <Select
            mode="tags"
            placeholder={t('webhook.eventsPlaceholder')}
            options={[
              { value: 'anomaly.detected', label: 'anomaly.detected' },
              { value: 'provider.unhealthy', label: 'provider.unhealthy' },
              { value: 'key.expired', label: 'key.expired' },
              { value: 'quota.exceeded', label: 'quota.exceeded' },
              { value: '*', label: '* (all events)' },
            ]}
          />
        </Form.Item>
        <Form.Item name="isEnabled" label={t('common.status')} valuePropName="checked" initialValue={true}>
          <Switch checkedChildren={t('common.enabled')} unCheckedChildren={t('common.disabled')} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
