import { useState, useEffect, useCallback } from 'react';
import { Table, Form, Input, Select, Switch, InputNumber, Space, Tag, Card, Button, Tabs } from 'antd';
import { EditOutlined, DeleteOutlined, UserOutlined, TeamOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { orgApi, userApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { Organization, User } from '../../types';

export default function OrganizationsPage() {
  const { t } = useTranslation();
  const [orgs, setOrgs] = useState<Organization[]>([]);
  const [activeTab, setActiveTab] = useState('orgs');

  const fetchOrgs = useCallback(async () => {
    try { setOrgs(await orgApi.tree()); } catch {}
  }, []);

  useEffect(() => { fetchOrgs(); }, [fetchOrgs]);

  // --- Users ---
  const fetchUsersFn = useCallback((params: any) => userApi.list(params), []);
  const { data: users, total: userTotal, loading: userLoading, params: userParams, fetchData: fetchUsers, setPage: setUserPage } = useCrudList<User, any>({
    fetchFn: fetchUsersFn, autoFetch: false,
  });
  const { form: userForm, open: userOpen, editing: editingUser, submitting: userSubmitting, openCreate: openCreateUser, openEdit: openEditUser, close: closeUser, submit: submitUser } = useFormModal<User>({
    createFn: (data) => userApi.create(data as any),
    updateFn: userApi.update,
    onSuccess: fetchUsers,
  });
  const { handleDelete: deleteUser } = useConfirmDelete(userApi.delete, fetchUsers);
  const { handleDelete: deleteOrg } = useConfirmDelete(orgApi.delete, fetchOrgs);

  useEffect(() => { if (activeTab === 'users') fetchUsers(); }, [activeTab, fetchUsers]);

  // --- Org Modal ---
  const { form: orgForm, open: orgOpen, editing: editingOrg, submitting: orgSubmitting, openCreate: openCreateOrg, openEdit: openEditOrg, close: closeOrg, submit: submitOrg } = useFormModal<Organization>({
    createFn: (data) => orgApi.create(data),
    updateFn: orgApi.update,
    onSuccess: fetchOrgs,
  });

  const roleColors: Record<string, string> = { SuperAdmin: 'red', Admin: 'orange', Developer: 'blue', ReadOnly: 'default' };

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

  const userColumns = [
    { title: t('organization.displayName'), dataIndex: 'displayName', key: 'displayName' },
    { title: t('auth.username'), dataIndex: 'username', key: 'username' },
    { title: t('organization.email'), dataIndex: 'email', key: 'email' },
    { title: t('organization.role'), dataIndex: 'role', key: 'role', render: (v: string) => <Tag color={roleColors[v]}>{v}</Tag> },
    { title: t('organization.title'), key: 'org', render: (_: any, r: User) => r.organization?.name },
    { title: t('common.enabled'), dataIndex: 'isActive', key: 'active', render: (v: boolean) => <Tag color={v ? 'success' : 'default'}>{v ? t('common.yes') : t('common.no')}</Tag> },
    {
      title: t('common.actions'), key: 'actions', width: 120,
      render: (_: any, r: User) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={() => openEditUser(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => deleteUser(r.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('organization.title')} />
      <Card style={{ borderRadius: 18 }}>
        <Tabs activeKey={activeTab} onChange={setActiveTab} items={[
          {
            key: 'orgs', label: <span><TeamOutlined /> {t('organization.title')}</span>,
            children: (
              <>
                <div style={{ marginBottom: 16, textAlign: 'right' }}>
                  <Button type="primary" onClick={openCreateOrg}>{t('common.create')}</Button>
                </div>
                <Table columns={orgColumns} dataSource={orgs} rowKey="id" pagination={false} />
              </>
            ),
          },
          {
            key: 'users', label: <span><UserOutlined /> {t('organization.users')}</span>,
            children: (
              <>
                <div style={{ marginBottom: 16, textAlign: 'right' }}>
                  <Button type="primary" onClick={openCreateUser}>{t('organization.addUser')}</Button>
                </div>
                <Table columns={userColumns} dataSource={users} rowKey="id" loading={userLoading}
                  pagination={{ current: userParams.page, pageSize: userParams.pageSize, total: userTotal, onChange: setUserPage }} />
              </>
            ),
          },
        ]} />
      </Card>

      <FormModal title={t('organization.title')} open={orgOpen} form={orgForm} editing={!!editingOrg} submitting={orgSubmitting} onOk={submitOrg} onCancel={closeOrg}>
        <Form.Item name="name" label={t('organization.name')} rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="description" label={t('organization.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="enableContentLogging" label={t('organization.contentLogging')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
        <Form.Item name="dataRetentionDays" label={t('organization.retentionDays')} initialValue={30}><InputNumber min={7} max={180} style={{ width: '100%' }} /></Form.Item>
      </FormModal>

      <FormModal title={editingUser ? t('common.edit') : t('organization.addUser')} open={userOpen} form={userForm} editing={!!editingUser} submitting={userSubmitting} onOk={submitUser} onCancel={closeUser}>
        <Form.Item name="username" label={t('auth.username')} rules={[{ required: true }]}><Input disabled={!!editingUser} /></Form.Item>
        {!editingUser && <Form.Item name="password" label={t('auth.password')} rules={[{ required: true }]}><Input.Password /></Form.Item>}
        <Form.Item name="email" label={t('organization.email')}><Input /></Form.Item>
        <Form.Item name="displayName" label={t('organization.displayName')}><Input /></Form.Item>
        <Form.Item name="role" label={t('organization.role')} rules={[{ required: true }]}>
          <Select options={['SuperAdmin', 'Admin', 'Developer', 'ReadOnly'].map((r) => ({ value: r, label: r }))} />
        </Form.Item>
        <Form.Item name="organizationId" label={t('organization.title')} rules={[{ required: true }]}>
          <Select options={orgs.map((o) => ({ value: o.id, label: o.name }))} />
        </Form.Item>
        {editingUser && <Form.Item name="isActive" label={t('common.enabled')} valuePropName="checked"><Switch /></Form.Item>}
      </FormModal>
    </div>
  );
}
