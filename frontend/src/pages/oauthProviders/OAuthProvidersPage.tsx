import { useCrudList } from '../../hooks/useCrudList';
import { useFormModal } from '../../hooks/useFormModal';
import { useConfirmDelete } from '../../hooks/useConfirmDelete';
import { oauthProviderApi } from '../../api';
import type { OAuthProvider, PagedRequest } from '../../types';
import { Table, Button, Space, Tag, Form, Input, Select, Switch, Card } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { PageHeader, FormModal } from '../../components';

const { Option } = Select;

export default function OAuthProvidersPage() {
  const { t } = useTranslation();
  const { data, total, loading, params, setPage, fetchData } = useCrudList<OAuthProvider, PagedRequest>({
    fetchFn: oauthProviderApi.list,
  });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<OAuthProvider>({
    createFn: oauthProviderApi.create,
    updateFn: oauthProviderApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(oauthProviderApi.delete, fetchData);

  const columns: ColumnsType<OAuthProvider> = [
    {
      title: t('oauthProvider.name'),
      dataIndex: 'name',
      key: 'name',
      width: 180,
    },
    {
      title: t('oauthProvider.displayName'),
      dataIndex: 'displayName',
      key: 'displayName',
      width: 180,
    },
    {
      title: t('oauthProvider.protocol'),
      dataIndex: 'protocol',
      key: 'protocol',
      width: 120,
      render: (protocol: string) => (
        <Tag color={protocol === 'OIDC' ? 'blue' : 'green'}>
          {protocol === 'OIDC' ? t('oauthProvider.protocolOIDC') : t('oauthProvider.protocolOAuth2')}
        </Tag>
      ),
    },
    {
      title: t('oauthProvider.clientId'),
      dataIndex: 'clientId',
      key: 'clientId',
      ellipsis: true,
      width: 220,
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
        title={t('nav.oauthProviders')}
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
        title={editing ? t('oauthProvider.edit') : t('oauthProvider.create')}
        open={open}
        form={form}
        editing={!!editing}
        submitting={submitting}
        onOk={submit}
        onCancel={close}
        width={640}
      >
        <Form.Item name="name" label={t('oauthProvider.name')} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="displayName" label={t('oauthProvider.displayName')}>
          <Input />
        </Form.Item>
        <Form.Item name="protocol" label={t('oauthProvider.protocol')} rules={[{ required: true }]} initialValue="OIDC">
          <Select>
            <Option value="OIDC">{t('oauthProvider.protocolOIDC')}</Option>
            <Option value="OAuth2">{t('oauthProvider.protocolOAuth2')}</Option>
          </Select>
        </Form.Item>
        <Form.Item name="clientId" label={t('oauthProvider.clientId')} rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="clientSecret" label={t('oauthProvider.clientSecret')} rules={[{ required: true }]}>
          <Input.Password />
        </Form.Item>
        <Form.Item name="authorizationEndpoint" label={t('oauthProvider.authorizationEndpoint')}>
          <Input placeholder={t('oauthProvider.authorizationPlaceholder')} />
        </Form.Item>
        <Form.Item name="tokenEndpoint" label={t('oauthProvider.tokenEndpoint')}>
          <Input placeholder={t('oauthProvider.tokenPlaceholder')} />
        </Form.Item>
        <Form.Item name="userInfoEndpoint" label={t('oauthProvider.userInfoEndpoint')}>
          <Input placeholder={t('oauthProvider.userInfoPlaceholder')} />
        </Form.Item>
        <Form.Item name="issuer" label={t('oauthProvider.issuer')}>
          <Input placeholder={t('oauthProvider.issuerPlaceholder')} />
        </Form.Item>
        <Form.Item name="scope" label={t('oauthProvider.scope')} initialValue="openid profile email">
          <Input />
        </Form.Item>
        <Form.Item name="isEnabled" label={t('common.status')} valuePropName="checked" initialValue={true}>
          <Switch checkedChildren={t('common.enabled')} unCheckedChildren={t('common.disabled')} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
