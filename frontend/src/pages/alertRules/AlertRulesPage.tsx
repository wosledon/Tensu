import { useCrudList } from '../../hooks/useCrudList';
import { useFormModal } from '../../hooks/useFormModal';
import { useConfirmDelete } from '../../hooks/useConfirmDelete';
import { alertRuleApi } from '../../api';
import type { AlertRule, PagedRequest } from '../../types';
import { Table, Button, Space, Tag, Form, Input, Select, Switch, Card } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { PageHeader, FormModal } from '../../components';

const { Option } = Select;

const EVENT_TYPES = [
  'anomaly.detected',
  'provider.unhealthy',
  'provider.healthy',
  'key.expired',
  'key.rotated',
  'quota.exceeded',
  'quota.warning',
  'compression.mapping.created',
];

const SEVERITY_OPTIONS = ['Low', 'Medium', 'High', 'Critical'];

export default function AlertRulesPage() {
  const { t } = useTranslation();
  const { data, total, loading, params, setPage, fetchData } = useCrudList<AlertRule, PagedRequest>({
    fetchFn: alertRuleApi.list,
  });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<AlertRule>({
    createFn: alertRuleApi.create,
    updateFn: alertRuleApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(alertRuleApi.delete, fetchData);

  const columns: ColumnsType<AlertRule> = [
    {
      title: t('alertRule.name'),
      dataIndex: 'name',
      key: 'name',
      width: 200,
    },
    {
      title: t('alertRule.eventType'),
      dataIndex: 'eventType',
      key: 'eventType',
      width: 200,
      render: (eventType: string) => <Tag color="blue">{eventType}</Tag>,
    },
    {
      title: t('alertRule.severity'),
      dataIndex: 'severity',
      key: 'severity',
      width: 120,
      render: (severity?: string) => severity ? <Tag>{severity}</Tag> : '-',
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
      width: 140,
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
        title={t('alertRule.title')}
        onCreate={openCreate}
        createLabel={t('alertRule.create')}
      />
      <Card>
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
          scroll={{ x: 700 }}
        />
      </Card>

      <FormModal
        title={editing ? t('alertRule.edit') : t('alertRule.create')}
        open={open}
        form={form}
        editing={!!editing}
        submitting={submitting}
        onOk={submit}
        onCancel={close}
        width={560}
      >
        <Form.Item name="name" label={t('alertRule.name')} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="eventType" label={t('alertRule.eventType')} rules={[{ required: true }]}>
          <Select placeholder={t('alertRule.selectEventType')} showSearch optionFilterProp="children">
            {EVENT_TYPES.map((et) => (
              <Option key={et} value={et}>{et}</Option>
            ))}
          </Select>
        </Form.Item>
        <Form.Item name="severity" label={t('alertRule.severity')}>
          <Select placeholder={t('alertRule.anySeverity')} allowClear>
            {SEVERITY_OPTIONS.map((s) => (
              <Option key={s} value={s}>{s}</Option>
            ))}
          </Select>
        </Form.Item>
        <Form.Item name="isEnabled" label={t('common.status')} valuePropName="checked" initialValue={true}>
          <Switch checkedChildren={t('common.enabled')} unCheckedChildren={t('common.disabled')} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
