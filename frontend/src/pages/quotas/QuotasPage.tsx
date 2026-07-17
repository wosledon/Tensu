import { useCallback, useState } from 'react';
import { Table, Form, InputNumber, Select, Space, Card, Button, Tag } from 'antd';
import { EditOutlined, DeleteOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { quotaApi, orgApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { Quota, Organization, Model } from '../../types';
import { exportTableToCsv } from '../../utils/export';

const scopeOptions = [
  { value: 'org', label: 'quota.scopeOrg' },
  { value: 'key', label: 'quota.scopeKey' },
  { value: 'model', label: 'quota.scopeModel' },
];

function flattenOrgs(orgs: Organization[]): Organization[] {
  const result: Organization[] = [];
  const walk = (list: Organization[]) => {
    for (const org of list) {
      result.push(org);
      if (org.children?.length) walk(org.children);
    }
  };
  walk(orgs);
  return result;
}

export default function QuotasPage() {
  const { t } = useTranslation();
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [models, setModels] = useState<Model[]>([]);

  const fetchFn = useCallback((params: any) => quotaApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<Quota, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Quota>({
    createFn: quotaApi.create,
    updateFn: quotaApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(quotaApi.delete, fetchData);

  const ensureOptions = async () => {
    if (!orgs.length) {
      try { setOrgs(flattenOrgs(await orgApi.tree())); } catch {}
    }
    if (!models.length) {
      try { setModels(await modelApi.allEnabled()); } catch {}
    }
  };

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
      render: (_: any, r: Quota) => r.organization?.name ?? r.organizationId ?? '-',
    },
    {
      title: t('quota.apiKey'),
      key: 'apiKey',
      render: (_: any, r: Quota) => (r.apiKeyId != null ? <span style={{ fontFamily: 'monospace' }}>{r.apiKeyId}</span> : '-'),
    },
    {
      title: t('quota.model'),
      key: 'model',
      render: (_: any, r: Quota) =>
        r.model ? (
          <span style={{ fontFamily: 'monospace' }}>{r.model.provider?.name}-{r.model.name}</span>
        ) : (r.modelId ?? '-'),
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
          <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureOptions(); openEdit(r); }} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const handleExport = () => {
    exportTableToCsv('quotas', columns, data);
  };

  const orgOptions = orgs.map((o) => ({ value: o.id, label: o.name }));
  const modelOptions = models.map((m) => ({ value: m.id, label: `${m.provider?.name}-${m.name}` }));

  return (
    <div>
      <PageHeader
        title={t('quota.title')}
        onCreate={async () => { await ensureOptions(); openCreate(); }}
        onSearch={setKeyword}
        extra={
          <Button icon={<ExportOutlined />} onClick={handleExport}>
            {t('common.export', 'Export')}
          </Button>
        }
      />
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
        <Form.Item name="organizationId" label={t('quota.organization')} rules={[{ required: true }]}>
          <Select options={orgOptions} showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.scope !== cur.scope}>
          {({ getFieldValue }) => {
            const scope = getFieldValue('scope');
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
                  <Select options={modelOptions} showSearch optionFilterProp="label" />
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
