import { useCallback, useState } from 'react';
import { Table, Form, Input, Select, Switch, Space, Tag, Card, Button, InputNumber, App, Drawer, Empty, Modal } from 'antd';
import { EditOutlined, DeleteOutlined, KeyOutlined, PlusOutlined, ExportOutlined, RedoOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { providerApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, StatusDot, FormModal } from '../../components';
import type { Provider, ProviderKey } from '../../types';
import { exportTableToCsv } from '../../utils/export';

export default function ProvidersPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();

  const fetchFn = useCallback((params: any) => providerApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setSort, setKeyword } = useCrudList<Provider, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Provider>({
    createFn: providerApi.create,
    updateFn: providerApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(providerApi.delete, fetchData);

  const [drawerProvider, setDrawerProvider] = useState<Provider | null>(null);
  const [keyOpen, setKeyOpen] = useState(false);
  const [editingKey, setEditingKey] = useState<ProviderKey | null>(null);
  const [keyForm] = Form.useForm();
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const handleBatchDelete = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await providerApi.batchDelete(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const handleBatchEnable = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await providerApi.batchEnable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const handleBatchDisable = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await providerApi.batchDisable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const handleToggleEnabled = async (record: Provider) => {
    try {
      await providerApi.update(record.id, { isEnabled: !record.isEnabled });
      message.success(t('common.success'));
      fetchData();
    } catch (err: any) {
      message.error(err?.message || t('common.error'));
    }
  };

  const handleAddKey = async () => {
    if (!drawerProvider) return;
    try {
      const values = await keyForm.validateFields();
      await providerApi.addKey(drawerProvider.id, values);
      setKeyOpen(false);
      keyForm.resetFields();
      setEditingKey(null);
      message.success(t('common.success'));
      fetchData();
      setDrawerProvider({ ...drawerProvider, keys: drawerProvider.keys ?? [] });
    } catch {}
  };

  const handleUpdateKey = async () => {
    if (!drawerProvider || !editingKey) return;
    try {
      const values = await keyForm.validateFields();
      await providerApi.updateKey(drawerProvider.id, editingKey.id, values);
      setKeyOpen(false);
      setEditingKey(null);
      keyForm.resetFields();
      message.success(t('common.success'));
      fetchData();
      setDrawerProvider({ ...drawerProvider, keys: drawerProvider.keys ?? [] });
    } catch {}
  };

  const handleDeleteKey = async (keyId: number) => {
    if (!drawerProvider) return;
    try {
      await providerApi.deleteKey(drawerProvider.id, keyId);
      message.success(t('common.success'));
      fetchData();
      setDrawerProvider({ ...drawerProvider, keys: drawerProvider.keys ?? [] });
    } catch (err: any) {
      message.error(err?.message || t('common.error'));
    }
  };

  const handleRotateKey = async (key: ProviderKey) => {
    if (!drawerProvider) return;
    Modal.confirm({
      title: t('provider.rotateConfirm', 'Rotate this key?'),
      content: t('provider.rotateWarning', 'A new key will be generated. Please copy the new key immediately; it will not be shown again.'),
      okText: t('common.confirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const res = await providerApi.rotateKey(drawerProvider.id, key.id);
          message.success(t('provider.rotateSuccess', 'Key rotated successfully'));
          fetchData();
          setDrawerProvider((prev) => prev ? { ...prev, keys: prev.keys?.map((k) => k.id === res.id ? { ...k, name: res.name, status: 'Active' } : k) } : prev);
        } catch {
          message.error(t('common.error'));
        }
      },
    });
  };

  const openEditKey = (key: ProviderKey) => {
    setEditingKey(key);
    keyForm.setFieldsValue({
      name: key.name,
      status: key.status,
      weight: key.weight,
      rateLimitRpm: key.rateLimitRpm,
      rateLimitTpm: key.rateLimitTpm,
    });
    setKeyOpen(true);
  };

  const openAddKey = () => {
    setEditingKey(null);
    keyForm.resetFields();
    setKeyOpen(true);
  };

  const statusMap: Record<string, { color: 'success' | 'warning' | 'error' | 'default'; label: string }> = {
    Healthy: { color: 'success', label: t('provider.healthy') },
    Degraded: { color: 'warning', label: t('provider.degraded') },
    Unhealthy: { color: 'error', label: t('provider.unhealthy') },
    Unknown: { color: 'default', label: t('provider.unknown') },
  };

  const keyStatusMap: Record<string, { color: 'success' | 'warning' | 'error' | 'default'; label: string }> = {
    Active: { color: 'success', label: t('common.enabled') },
    Degraded: { color: 'warning', label: t('provider.degraded') },
    Inactive: { color: 'error', label: t('common.disabled') },
  };

  const providerColumns = [
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
    {
      title: t('provider.keys'), key: 'keys',
      render: (_: any, r: Provider) => {
        const count = r.keys?.length || 0;
        return <span style={{ fontFamily: 'monospace' }}>{count}</span>;
      },
    },
    {
      title: t('common.enabled'), dataIndex: 'isEnabled', key: 'isEnabled', width: 100, align: 'center' as const,
      render: (v: boolean, r: Provider) => (
        <Switch size="small" checked={v} onChange={() => handleToggleEnabled(r)} />
      ),
    },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 180,
      render: (_: any, r: Provider) => (
        <Space size="small">
          <Button type="text" icon={<KeyOutlined />} title={t('provider.manageKeys')} onClick={() => setDrawerProvider(r)} />
          <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(r)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const keyColumns = [
    { title: t('provider.keyName'), dataIndex: 'name', key: 'name' },
    {
      title: t('common.status'), dataIndex: 'status', key: 'status', width: 120,
      render: (v: string) => {
        const s = keyStatusMap[v] || keyStatusMap.Inactive;
        return <Tag color={s.color}>{s.label}</Tag>;
      },
    },
    {
      title: t('provider.weight'), dataIndex: 'weight', key: 'weight', width: 100,
      render: (v: number) => <span style={{ fontFamily: 'monospace', textAlign: 'right', display: 'block' }}>{v}</span>,
      align: 'right' as const,
    },
    {
      title: t('quota.rpm'), dataIndex: 'rateLimitRpm', key: 'rateLimitRpm', width: 100,
      render: (v?: number) => v != null ? <span style={{ fontFamily: 'monospace' }}>{v}</span> : '-',
      align: 'right' as const,
    },
    {
      title: t('quota.tpm'), dataIndex: 'rateLimitTpm', key: 'rateLimitTpm', width: 100,
      render: (v?: number) => v != null ? <span style={{ fontFamily: 'monospace' }}>{v}</span> : '-',
      align: 'right' as const,
    },
    {
      title: t('common.actions'), key: 'actions', width: 220, fixed: 'right' as const,
      render: (_: any, k: ProviderKey) => (
        <Space size="small">
          <Button type="text" icon={<RedoOutlined />} title={t('provider.rotate')} onClick={() => handleRotateKey(k)} />
          <Button type="text" icon={<EditOutlined />} onClick={() => openEditKey(k)} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDeleteKey(k.id)} />
        </Space>
      ),
    },
  ];

  const handleExport = () => {
    exportTableToCsv('providers', providerColumns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('provider.title')}
        onCreate={openCreate}
        onSearch={setKeyword}
        extra={
          <Space>
            <Button icon={<ExportOutlined />} onClick={handleExport}>
              {t('common.export', 'Export')}
            </Button>
            {selectedRowKeys.length > 0 && (
              <>
                <Button onClick={handleBatchEnable}>{t('common.enable')}</Button>
                <Button onClick={handleBatchDisable}>{t('common.disable')}</Button>
                <Button danger onClick={handleBatchDelete}>{t('common.delete')}</Button>
              </>
            )}
          </Space>
        }
      />

      <Card style={{ borderRadius: 18, marginBottom: 16 }}>
        <Table
          columns={providerColumns}
          dataSource={data}
          rowKey="id"
          loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          onChange={(_p, _f, sorter: any) => sorter.field && setSort(sorter.field, sorter.order === 'ascend' ? 'asc' : 'desc')}
          scroll={{ x: 900 }}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys: React.Key[]) => setSelectedRowKeys(keys),
          }}
        />
      </Card>

      <Drawer
        title={
          <Space>
            <span>{t('provider.keys')}</span>
            {drawerProvider && <Tag color="blue">{drawerProvider.name}</Tag>}
          </Space>
        }
        extra={
          drawerProvider ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={openAddKey}>
              {t('provider.addKey')}
            </Button>
          ) : undefined
        }
        placement="right"
        width={720}
        open={!!drawerProvider}
        onClose={() => setDrawerProvider(null)}
      >
        {drawerProvider ? (
          <Table
            size="small"
            pagination={false}
            dataSource={drawerProvider.keys || []}
            rowKey="id"
            columns={keyColumns}
            locale={{ emptyText: t('common.noData') }}
          />
        ) : (
          <Empty description={t('common.noData')} />
        )}
      </Drawer>

      <FormModal title={t('provider.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Space size={16} style={{ width: '100%' }} direction="vertical">
          <Space size={16} style={{ width: '100%' }} wrap>
            <Form.Item name="name" label={t('provider.name')} rules={[{ required: true }]} style={{ width: '100%', marginBottom: 0 }}>
              <Input />
            </Form.Item>
            <Form.Item name="protocol" label={t('provider.protocol')} rules={[{ required: true }]} style={{ width: '100%', marginBottom: 0 }}>
              <Select options={[{ value: 'OpenAI', label: t('provider.protocolOpenAI') }, { value: 'Anthropic', label: t('provider.protocolAnthropic') }]} />
            </Form.Item>
          </Space>
          <Form.Item name="baseUrl" label={t('provider.baseUrl')} rules={[{ required: true }]} style={{ marginBottom: 0 }}>
            <Input placeholder="https://api.openai.com" />
          </Form.Item>
          <Form.Item name="description" label={t('provider.description')} style={{ marginBottom: 0 }}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true} style={{ marginBottom: 0 }}>
            <Switch />
          </Form.Item>
        </Space>
      </FormModal>

      <FormModal title={editingKey ? t('provider.editKey') : t('provider.addKey')} open={keyOpen} form={keyForm} editing={!!editingKey} submitting={submitting} onOk={editingKey ? handleUpdateKey : handleAddKey} onCancel={() => { setKeyOpen(false); setEditingKey(null); }}>
        <Form.Item name="name" label={t('provider.keyName')} rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="status" label={t('common.status')} rules={[{ required: true }]}>
          <Select options={[{ value: 'Active', label: t('common.enabled') }, { value: 'Degraded', label: t('provider.degraded') }, { value: 'Inactive', label: t('common.disabled') }]} />
        </Form.Item>
        <Form.Item name="weight" label={t('provider.weight')} initialValue={1}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="rateLimitRpm" label={t('quota.rpm')}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item name="rateLimitTpm" label={t('quota.tpm')}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
      </FormModal>
    </div>
  );
}
