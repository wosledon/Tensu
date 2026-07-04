import { useState, useEffect, useCallback } from 'react';
import { Table, Form, Input, Switch, Space, Tag, Card, Button, InputNumber } from 'antd';
import { EditOutlined, DeleteOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { orgApi } from '../../api';
import { useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { Organization } from '../../types';
import { exportTableToCsv } from '../../utils/export';

export default function OrganizationsPage() {
  const { t } = useTranslation();
  const [orgs, setOrgs] = useState<Organization[]>([]);

  const fetchOrgs = useCallback(async () => {
    try { setOrgs(await orgApi.tree()); } catch {}
  }, []);

  useEffect(() => { fetchOrgs(); }, [fetchOrgs]);

  const { form: orgForm, open: orgOpen, editing: editingOrg, submitting: orgSubmitting, openCreate: openCreateOrg, openEdit: openEditOrg, close: closeOrg, submit: submitOrg } = useFormModal<Organization>({
    createFn: (data) => orgApi.create(data),
    updateFn: orgApi.update,
    onSuccess: fetchOrgs,
  });
  const { handleDelete: deleteOrg } = useConfirmDelete(orgApi.delete, fetchOrgs);

  const orgColumns = [
    { title: t('organization.name'), dataIndex: 'name', key: 'name' },
    { title: t('organization.description'), dataIndex: 'description', key: 'description', ellipsis: true },
    { title: t('organization.contentLogging'), dataIndex: 'enableContentLogging', key: 'logging', render: (v: boolean) => <Tag color={v ? 'success' : 'default'}>{v ? t('common.yes') : t('common.no')}</Tag> },
    { title: t('organization.retentionDays'), dataIndex: 'dataRetentionDays', key: 'retention', render: (v: number) => `${v}d` },
    {
      title: t('common.actions'), key: 'actions', width: 120,
      render: (_: any, r: Organization) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEditOrg(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => deleteOrg(r.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('organization.title')} />
      <Card style={{ borderRadius: 18 }}>
        <div style={{ marginBottom: 16, textAlign: 'right' }}>
          <Space>
            <Button icon={<ExportOutlined />} onClick={() => exportTableToCsv('organizations', orgColumns, orgs)}>
              {t('common.export', 'Export')}
            </Button>
            <Button type="primary" onClick={openCreateOrg}>{t('common.create')}</Button>
          </Space>
        </div>
        <Table columns={orgColumns} dataSource={orgs} rowKey="id" pagination={false} />
      </Card>

      <FormModal title={t('organization.title')} open={orgOpen} form={orgForm} editing={!!editingOrg} submitting={orgSubmitting} onOk={submitOrg} onCancel={closeOrg}>
        <Form.Item name="name" label={t('organization.name')} rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="description" label={t('organization.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="enableContentLogging" label={t('organization.contentLogging')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
        <Form.Item name="dataRetentionDays" label={t('organization.retentionDays')} initialValue={30}><InputNumber min={7} max={180} style={{ width: '100%' }} /></Form.Item>
      </FormModal>
    </div>
  );
}
