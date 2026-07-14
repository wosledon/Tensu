import { useCrudList } from '../../hooks/useCrudList';
import { adminAuditApi } from '../../api';
import type { AdminAuditLog, PagedRequest } from '../../types';
import { Table, Tag, Typography, Space, Input, Select, Card } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useTranslation } from 'react-i18next';

const { Title } = Typography;
const { Search } = Input;

interface AdminAuditParams extends PagedRequest {
  action?: string;
  entityType?: string;
}

export default function AdminAuditPage() {
  const { t } = useTranslation();
  const { data, total, loading, params, setPage, setKeyword, setExtra } = useCrudList<AdminAuditLog, AdminAuditParams>({
    fetchFn: adminAuditApi.list,
  });

  const columns: ColumnsType<AdminAuditLog> = [
    {
      title: t('adminAudit.timestamp'),
      dataIndex: 'timestamp',
      key: 'timestamp',
      render: (ts: string) => new Date(ts).toLocaleString(),
      width: 180,
    },
    {
      title: t('adminAudit.username'),
      dataIndex: 'username',
      key: 'username',
      width: 120,
    },
    {
      title: t('adminAudit.action'),
      dataIndex: 'action',
      key: 'action',
      width: 100,
      render: (action: string) => {
        const color = action === 'CREATE' ? 'green' : action === 'UPDATE' ? 'blue' : action === 'DELETE' ? 'red' : 'default';
        return <Tag color={color}>{action}</Tag>;
      },
    },
    {
      title: t('adminAudit.entityType'),
      dataIndex: 'entityType',
      key: 'entityType',
      width: 120,
    },
    {
      title: t('adminAudit.entityId'),
      dataIndex: 'entityId',
      key: 'entityId',
      width: 100,
    },
    {
      title: t('adminAudit.details'),
      dataIndex: 'details',
      key: 'details',
      ellipsis: true,
    },
    {
      title: t('adminAudit.ipAddress'),
      dataIndex: 'ipAddress',
      key: 'ipAddress',
      width: 130,
    },
  ];

  return (
    <div>
      <Title level={4}>{t('nav.adminAudit')}</Title>
      <Card>
        <Space style={{ marginBottom: 16 }}>
          <Search
            placeholder={t('common.search')}
            allowClear
            onSearch={(value) => setKeyword(value)}
            style={{ width: 250 }}
          />
          <Select
            placeholder={t('adminAudit.action')}
            allowClear
            style={{ width: 120 }}
            onChange={(value) => setExtra({ action: value })}
            options={[
              { value: 'CREATE', label: 'CREATE' },
              { value: 'UPDATE', label: 'UPDATE' },
              { value: 'DELETE', label: 'DELETE' },
            ]}
          />
          <Select
            placeholder={t('adminAudit.entityType')}
            allowClear
            style={{ width: 150 }}
            onChange={(value) => setExtra({ entityType: value })}
            options={[
              { value: 'Provider', label: 'Provider' },
              { value: 'Model', label: 'Model' },
              { value: 'ApiKey', label: 'ApiKey' },
              { value: 'User', label: 'User' },
              { value: 'Organization', label: 'Organization' },
              { value: 'RouteModel', label: 'RouteModel' },
              { value: 'Quota', label: 'Quota' },
              { value: 'Webhook', label: 'Webhook' },
            ]}
          />
        </Space>
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
          scroll={{ x: 1000 }}
        />
      </Card>
    </div>
  );
}
