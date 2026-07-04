import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, Switch, Space, Tag, Card, Button } from 'antd';
import { EditOutlined, DeleteOutlined, PlusOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { userApi, orgApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { User, Organization } from '../../types';
import { exportTableToCsv } from '../../utils/export';

export default function UsersPage() {
  const { t } = useTranslation();
  const [orgs, setOrgs] = useState<Organization[]>([]);

  const fetchFn = useCallback((params: any) => userApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<User, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<User>({
    createFn: (data) => userApi.create(data as any),
    updateFn: userApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(userApi.delete, fetchData);

  const ensureOrgs = async () => {
    if (!orgs.length) {
      try { setOrgs(await orgApi.tree()); } catch {}
    }
  };

  const roleColors: Record<string, string> = { SuperAdmin: 'red', Admin: 'orange', Developer: 'blue', ReadOnly: 'default' };

  const columns = [
    { title: t('organization.displayName'), dataIndex: 'displayName', key: 'displayName' },
    { title: t('auth.username'), dataIndex: 'username', key: 'username' },
    { title: t('organization.email'), dataIndex: 'email', key: 'email' },
    { title: t('organization.role'), dataIndex: 'role', key: 'role', render: (v: string) => <Tag color={roleColors[v]}>{v}</Tag> },
    { title: t('organization.title'), key: 'org', render: (_: any, r: User) => r.organization?.name },
    { title: t('common.enabled'), dataIndex: 'isActive', key: 'active', render: (v: boolean) => <Tag color={v ? 'success' : 'default'}>{v ? t('common.yes') : t('common.no')}</Tag> },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 160,
      render: (_: any, r: User) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEditUser(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const openEditUser = (record: User) => {
    openEdit({
      ...record,
      organizationId: record.organizationId ?? record.organization?.id,
    } as any);
  };

  const handleExport = () => {
    exportTableToCsv('users', columns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('organization.users')}
        onCreate={async () => { await ensureOrgs(); openCreate(); }}
        onSearch={setKeyword}
        extra={
          <Space>
            <Button icon={<ExportOutlined />} onClick={handleExport}>
              {t('common.export', 'Export')}
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={async () => { await ensureOrgs(); openCreate(); }}>
              {t('organization.addUser')}
            </Button>
          </Space>
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

      <FormModal title={editing ? t('common.edit') : t('organization.addUser')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="username" label={t('auth.username')} rules={[{ required: true }]}><Input disabled={!!editing} /></Form.Item>
        {!editing && <Form.Item name="password" label={t('auth.password')} rules={[{ required: true }]}><Input.Password /></Form.Item>}
        <Form.Item name="email" label={t('organization.email')}><Input /></Form.Item>
        <Form.Item name="displayName" label={t('organization.displayName')}><Input /></Form.Item>
        <Form.Item name="role" label={t('organization.role')} rules={[{ required: true }]}>
          <Select options={['SuperAdmin', 'Admin', 'Developer', 'ReadOnly'].map((r) => ({ value: r, label: t(`organization.role${r}`) }))} />
        </Form.Item>
        <Form.Item name="organizationId" label={t('organization.title')} rules={[{ required: true }]}>
          <Select options={orgs.map((o) => ({ value: o.id, label: o.name }))} />
        </Form.Item>
        {editing && <Form.Item name="isActive" label={t('common.enabled')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>}
      </FormModal>
    </div>
  );
}
