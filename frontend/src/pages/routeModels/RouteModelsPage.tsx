import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, Switch, Space, Tag, Card, Button, App, Tooltip, Popconfirm } from 'antd';
import { EditOutlined, DeleteOutlined, ExportOutlined, BranchesOutlined, CloseOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { routeModelApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal, StatusDot } from '../../components';
import { getApiErrorMessage } from '../../api/errorHandler';
import type { RouteModel, Model } from '../../types';
import { exportTableToCsv } from '../../utils/export';

export default function RouteModelsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [models, setModels] = useState<Model[]>([]);
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);
  const [expandedRowKeys, setExpandedRowKeys] = useState<React.Key[]>([]);

  const ensureModels = async () => {
    if (!models.length) {
      try { setModels(await modelApi.allEnabled()); } catch {}
    }
  };

  const modelOptions = models.map((m) => ({ value: m.id, label: `${m.provider?.name}-${m.name}` }));

  const fetchFn = useCallback((params: any) => routeModelApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<RouteModel, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close } = useFormModal<RouteModel>({
    createFn: routeModelApi.create,
    updateFn: routeModelApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(routeModelApi.delete, fetchData);

  // 自定义提交：创建/更新后再处理挂载模型
  const handleFormOk = async () => {
    const values = await form.validateFields();
    const { targetModelIds, ...basicValues } = values;
    const ids: number[] = targetModelIds ?? [];
    try {
      if (editing) {
        await routeModelApi.update(editing.id, basicValues);
        const currentIds = editing.targets?.map(t => t.modelId) ?? [];
        const toAdd = ids.filter((id: number) => !currentIds.includes(id));
        const toRemove = editing.targets?.filter(t => !ids.includes(t.modelId)) ?? [];
        for (const modelId of toAdd) await routeModelApi.addTarget(editing.id, modelId);
        for (const tgt of toRemove) await routeModelApi.removeTarget(editing.id, tgt.id);
        // 影子模式下设第一个目标为活跃
        if (basicValues.mode === 'Shadow' && toAdd.length > 0) {
          const created = await routeModelApi.addTarget(editing.id, toAdd[0]);
          await routeModelApi.setActiveTarget(editing.id, created.id);
        }
      } else {
        const created = await routeModelApi.create(basicValues);
        for (const modelId of ids) {
          const target = await routeModelApi.addTarget(created.id, modelId);
          if (basicValues.mode === 'Shadow' && modelId === ids[0]) {
            await routeModelApi.setActiveTarget(created.id, target.id);
          }
        }
      }
      message.success(t('common.success'));
      close();
      fetchData();
    } catch (e) {
      message.error(getApiErrorMessage(e, t('common.error')));
    }
  };

  // ── Targets 行内管理 ──

  const handleRemoveTarget = async (rmId: number, targetId: number) => {
    try {
      await routeModelApi.removeTarget(rmId, targetId);
      message.success(t('common.success'));
      fetchData();
    } catch (e) { message.error(getApiErrorMessage(e, t('common.error'))); }
  };

  const handleSetActive = async (rmId: number, targetId: number) => {
    try {
      await routeModelApi.setActiveTarget(rmId, targetId);
      message.success(t('common.success'));
      fetchData();
    } catch (e) { message.error(getApiErrorMessage(e, t('common.error'))); }
  };

  const expandedRowRender = (record: RouteModel) => {
    const targets = record.targets || [];
    if (targets.length === 0) return <div style={{ padding: '12px 24px', color: 'var(--ant-color-text-secondary)' }}>{t('routeModel.noTargets')}</div>;
    return (
      <div style={{ padding: '12px 24px' }}>
        <Space direction="vertical" style={{ width: '100%' }} size={12}>
          {targets.map((trg) => {
            const label = trg.model ? `${trg.model.provider?.name}-${trg.model.name}` : `Model #${trg.modelId}`;
            return (
              <div key={trg.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '6px 12px', borderRadius: 8, background: 'var(--ant-color-bg-elevated)' }}>
                {record.mode === 'Shadow' && (
                  <Tag icon={trg.isActive ? <CheckCircleOutlined /> : undefined} color={trg.isActive ? 'success' : 'default'} style={{ cursor: 'pointer', margin: 0 }}
                    onClick={() => !trg.isActive && handleSetActive(record.id, trg.id)}
                  >
                    {trg.isActive ? t('routeModel.target') : t('common.enable')}
                  </Tag>
                )}
                <span style={{ flex: 1, fontFamily: 'monospace' }}>{label}</span>
                {record.mode === 'Route' && (
                  <Tag>{t('routeModel.priority')}: {trg.priority}</Tag>
                )}
                <Popconfirm title={t('common.deleteConfirm')} onConfirm={() => handleRemoveTarget(record.id, trg.id)}>
                  <Button type="text" danger size="small" icon={<CloseOutlined />} />
                </Popconfirm>
              </div>
            );
          })}
        </Space>
      </div>
    );
  };

  const getTargetSummary = (record: RouteModel) => {
    const targets = record.targets || [];
    if (targets.length === 0) return <span style={{ color: 'var(--ant-color-text-secondary)' }}>0</span>;
    if (record.mode === 'Shadow') {
      const active = targets.find(t => t.isActive);
      return (
        <Select
          size="small"
          value={active?.modelId}
          style={{ minWidth: 160 }}
          options={targets.map(t => ({
            value: t.modelId,
            label: t.model ? `${t.model.provider?.name}-${t.model.name}` : `#${t.modelId}`,
          }))}
          onChange={(modelId) => {
            const t = targets.find(x => x.modelId === modelId);
            if (t) handleSetActive(record.id, t.id);
          }}
          onClick={(e) => e.stopPropagation()}
        />
      );
    }
    return <span>{targets.length} {t('routeModel.targets')}</span>;
  };

  const columns = [
    { title: t('routeModel.name'), dataIndex: 'name', key: 'name', sorter: true },
    {
      title: t('routeModel.mode'), dataIndex: 'mode', key: 'mode', width: 120,
      render: (v: string) => <Tag color={v === 'Shadow' ? 'blue' : 'purple'}>{v === 'Shadow' ? t('routeModel.shadow') : t('routeModel.route')}</Tag>,
    },
    { title: t('routeModel.targets'), key: 'targets', render: (_: any, r: RouteModel) => getTargetSummary(r) },
    { title: t('routeModel.fallback'), key: 'fallback', render: (_: any, r: RouteModel) => r.fallbackModel ? `${r.fallbackModel.provider?.name}-${r.fallbackModel.name}` : '-', responsive: ['lg' as const] },
    { title: t('routeModel.routingModel'), key: 'routingModel', render: (_: any, r: RouteModel) => r.routingModel ? `${r.routingModel.provider?.name}-${r.routingModel.name}` : '-', responsive: ['lg' as const] },
    { title: t('common.enabled'), dataIndex: 'isEnabled', key: 'isEnabled', width: 80, render: (v: boolean) => <StatusDot color={v ? 'success' : 'default'} text={v ? t('common.yes') : t('common.no')} /> },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 100,
      render: (_: any, r: RouteModel) => (
        <Space>
          <Tooltip title={t('common.edit')}>
            <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureModels(); openEdit(r); form.setFieldsValue({ targetModelIds: r.targets?.map(t => t.modelId) ?? [] }); }} />
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

  const handleBatchDelete = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await routeModelApi.batchDelete(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch { message.error(t('common.error')); }
  };

  const handleBatchEnable = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await routeModelApi.batchEnable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch { message.error(t('common.error')); }
  };

  const handleBatchDisable = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await routeModelApi.batchDisable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch { message.error(t('common.error')); }
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
          scroll={{ x: 800 }}
          expandable={{
            expandedRowRender,
            expandedRowKeys,
            onExpandedRowsChange: (keys) => setExpandedRowKeys(keys as React.Key[]),
            rowExpandable: () => true,
          }}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys: React.Key[]) => setSelectedRowKeys(keys),
          }}
        />
      </Card>

      <FormModal title={t('routeModel.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={handleFormOk} onCancel={close}>
        <Form.Item name="name" label={t('routeModel.name')} rules={[{ required: true }]}><Input placeholder="smart-router" /></Form.Item>
        <Form.Item name="description" label={t('provider.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="mode" label={t('routeModel.mode')} rules={[{ required: true }]}>
          <Select options={[{ value: 'Shadow', label: t('routeModel.shadow') }, { value: 'Route', label: t('routeModel.route') }]} />
        </Form.Item>
        <Form.Item name="targetModelIds" label={t('routeModel.targets')} extra={t('routeModel.targetsHint')}>
          <Select mode="multiple" options={modelOptions} showSearch optionFilterProp="label" placeholder={t('routeModel.selectTargets')} />
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
