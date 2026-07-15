import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, Switch, Space, Tag, Card, Button, App, Tooltip } from 'antd';
import { EditOutlined, DeleteOutlined, ExportOutlined, BranchesOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { routeModelApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal, StatusDot } from '../../components';
import type { RouteModel, Model } from '../../types';
import { exportTableToCsv } from '../../utils/export';

export default function RouteModelsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [models, setModels] = useState<Model[]>([]);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const fetchFn = useCallback((params: any) => routeModelApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<RouteModel, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<RouteModel>({
    createFn: routeModelApi.create,
    updateFn: routeModelApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(routeModelApi.delete, fetchData);

  const handleBatchDelete = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await routeModelApi.batchDelete(selectedRowKeys.map((k) => Number(k)));
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
      await routeModelApi.batchEnable(selectedRowKeys.map((k) => Number(k)));
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
      await routeModelApi.batchDisable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const ensureModels = async () => {
    if (!models.length) {
      try { setModels(await modelApi.allEnabled()); } catch {}
    }
  };

  const modelOptions = models.map((m) => ({ value: m.id, label: `${m.provider?.name}-${m.name}` }));

  const columns = [
    { title: t('routeModel.name'), dataIndex: 'name', key: 'name', sorter: true },
    {
      title: t('routeModel.mode'), dataIndex: 'mode', key: 'mode',
      render: (v: string) => <Tag color={v === 'Shadow' ? 'blue' : 'purple'}>{v === 'Shadow' ? t('routeModel.shadow') : t('routeModel.route')}</Tag>,
    },
    { title: t('routeModel.target'), key: 'target', render: (_: any, r: RouteModel) => r.targetModel ? `${r.targetModel.provider?.name}-${r.targetModel.name}` : '-' },
    { title: t('routeModel.fallback'), key: 'fallback', render: (_: any, r: RouteModel) => r.fallbackModel ? `${r.fallbackModel.provider?.name}-${r.fallbackModel.name}` : '-' },
    { title: t('routeModel.routingModel'), key: 'routingModel', render: (_: any, r: RouteModel) => r.routingModel ? `${r.routingModel.provider?.name}-${r.routingModel.name}` : '-' },
    { title: t('common.enabled'), dataIndex: 'isEnabled', key: 'isEnabled', render: (v: boolean) => <StatusDot color={v ? 'success' : 'default'} text={v ? t('common.yes') : t('common.no')} /> },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 140,
      render: (_: any, r: RouteModel) => (
        <Space>
          <Tooltip title={t('common.edit')}>
            <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureModels(); openEdit(r); }} />
          </Tooltip>
          <Tooltip title={t('common.delete')}>
            <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
          </Tooltip>
        </Space>
      ),
    },
  ];

  const handleExport = () => {
    exportTableToCsv('route-models', columns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('routeModel.title')}
        icon={<BranchesOutlined />}
        onCreate={async () => { await ensureModels(); openCreate(); }}
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
      <Card style={{ borderRadius: 18, transition: 'box-shadow 0.2s' }} hoverable={false}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 900 }}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys: React.Key[]) => setSelectedRowKeys(keys),
          }}
        />
      </Card>

      <FormModal title={t('routeModel.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="name" label={t('routeModel.name')} rules={[{ required: true }]}><Input placeholder="smart-router" /></Form.Item>
        <Form.Item name="description" label={t('provider.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="mode" label={t('routeModel.mode')} rules={[{ required: true }]}>
          <Select options={[{ value: 'Shadow', label: t('routeModel.shadow') }, { value: 'Route', label: t('routeModel.route') }]} />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.mode !== cur.mode}>
          {({ getFieldValue }) => {
            const mode = getFieldValue('mode');
            if (mode !== 'Shadow') return null;
            return (
              <Form.Item name="targetModelId" label={t('routeModel.target')}>
                <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
              </Form.Item>
            );
          }}
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.mode !== cur.mode}>
          {({ getFieldValue }) => {
            const mode = getFieldValue('mode');
            if (mode !== 'Route') return null;
            return (
              <>
                <Form.Item name="fallbackModelId" label={t('routeModel.fallback')}>
                  <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
                </Form.Item>
                <Form.Item name="routingModelId" label={t('routeModel.routingModel')} extra={t('routeModel.routingModelHint')}>
                  <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
                </Form.Item>
              </>
            );
          }}
        </Form.Item>
        <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
      </FormModal>
    </div>
  );
}
