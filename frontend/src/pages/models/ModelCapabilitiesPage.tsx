import { useState, useCallback } from 'react';
import { Table, Form, Input, InputNumber, Select, Space, Tag, Card, Button, Tooltip } from 'antd';
import { EditOutlined, DeleteOutlined, ThunderboltOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { modelCapabilityApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { ModelCapability, Model } from '../../types';
import { exportTableToCsv } from '../../utils/export';

const dimensions = ['Reasoning', 'Code', 'Vision', 'Math', 'ToolUse', 'Multilingual', 'Safety', 'Latency', 'Throughput', 'CostEfficiency'];
const sources = ['benchmark', 'manual', 'synthetic', 'custom'];

export default function ModelCapabilitiesPage() {
  const { t } = useTranslation();
  const [models, setModels] = useState<Model[]>([]);
  const [modelFilter, setModelFilter] = useState<number | undefined>();
  const [autoEvalOpen, setAutoEvalOpen] = useState(false);
  const [autoEvalTarget, setAutoEvalTarget] = useState<ModelCapability | null>(null);
  const [autoEvalForm] = Form.useForm();

  const fetchFn = useCallback((params: any) => modelCapabilityApi.list({ ...params, modelId: modelFilter }), [modelFilter]);
  const { data, total, loading, params, fetchData, setPage } = useCrudList<ModelCapability, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<ModelCapability>({
    createFn: modelCapabilityApi.create,
    updateFn: modelCapabilityApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(modelCapabilityApi.delete, fetchData);

  const ensureModels = async () => {
    if (!models.length) {
      try { setModels(await modelApi.allEnabled()); } catch {}
    }
  };

  const modelOptions = models.map((m) => ({ value: m.id, label: `${m.provider?.name}-${m.name}` }));

  const handleAutoEvaluate = async () => {
    if (!autoEvalTarget) return;
    try {
      const values = await autoEvalForm.validateFields();
      await modelCapabilityApi.autoEvaluate(autoEvalTarget.modelId, values.dimensions || []);
      setAutoEvalOpen(false);
      autoEvalForm.resetFields();
      fetchData();
    } catch {}
  };

  const columns = [
    {
      title: t('model.name'), key: 'model',
      render: (_: any, r: ModelCapability) => <span style={{ fontFamily: 'monospace' }}>{r.model?.provider?.name}-{r.model?.name}</span>,
    },
    { title: t('capability.dimension'), dataIndex: 'dimension', key: 'dimension' },
    {
      title: t('capability.score'), key: 'score', align: 'right' as const, width: 100,
      render: (_: any, r: ModelCapability) => (
        <Tag color={r.score >= 80 ? 'green' : r.score >= 60 ? 'orange' : 'red'}>
          <span style={{ fontFamily: 'monospace' }}>{r.score.toFixed(1)}</span>
        </Tag>
      ),
    },
    { title: t('capability.source'), dataIndex: 'source', key: 'source', width: 120 },
    {
      title: t('capability.evidence'), dataIndex: 'evidence', key: 'evidence', ellipsis: true,
      render: (v?: string) => v || <span style={{ color: 'var(--ant-color-text-secondary)' }}>—</span>,
    },
    {
      title: t('capability.evaluatedAt'), dataIndex: 'evaluatedAt', key: 'evaluatedAt', width: 180,
      render: (v: string) => new Date(v).toLocaleString(),
    },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 140,
      render: (_: any, r: ModelCapability) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureModels(); openEdit(r); }} />
          <Button type="text" icon={<ThunderboltOutlined />} onClick={async () => { await ensureModels(); setAutoEvalTarget(r); autoEvalForm.setFieldsValue({ dimensions: ['Latency', 'Throughput', 'CostEfficiency'] }); setAutoEvalOpen(true); }} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const handleExport = () => {
    exportTableToCsv('model-capabilities', columns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('capability.title')}
        onCreate={async () => { await ensureModels(); openCreate(); }}
        extra={
          <Button icon={<ExportOutlined />} onClick={handleExport}>
            {t('common.export', 'Export')}
          </Button>
        }
      />
      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space wrap>
          <Select
            allowClear
            placeholder={t('model.name')}
            style={{ width: 240 }}
            options={modelOptions}
            onChange={(v) => setModelFilter(v)}
          />
          {modelFilter && <Button onClick={() => setModelFilter(undefined)}>{t('common.reset')}</Button>}
        </Space>
      </Card>
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 1000 }}
        />
      </Card>

      <FormModal title={t('capability.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="modelId" label={t('model.name')} rules={[{ required: true }]}>
          <Select options={modelOptions} showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item name="dimension" label={t('capability.dimension')} rules={[{ required: true }]}>
          <Select options={dimensions.map((d) => ({ value: d, label: d }))} showSearch />
        </Form.Item>
        <Form.Item name="score" label={t('capability.score')} initialValue={0} rules={[{ required: true, type: 'number' }]}>
          <InputNumber min={0} max={100} step={0.1} precision={2} style={{ width: '100%' }} addonAfter="0-100" />
        </Form.Item>
        <Form.Item name="source" label={t('capability.source')} rules={[{ required: true }]} initialValue="manual">
          <Select options={sources.map((s) => ({ value: s, label: s }))} />
        </Form.Item>
        <Form.Item name="evidence" label={t('capability.evidence')}>
          <Input.TextArea rows={3} />
        </Form.Item>
        <Form.Item name="evaluatedAt" label={t('capability.evaluatedAt')} rules={[{ required: true }]} initialValue={new Date().toISOString()}>
          <Input type="datetime-local" />
        </Form.Item>
      </FormModal>

      <FormModal title={t('capability.autoEvaluate')} open={autoEvalOpen} form={autoEvalForm} editing={false} onOk={handleAutoEvaluate} onCancel={() => setAutoEvalOpen(false)}>
        <Form.Item name="dimensions" label={t('capability.dimensions')} rules={[{ required: true }]}>
          <Select mode="multiple" options={[
            { value: 'Latency', label: 'Latency' },
            { value: 'Throughput', label: 'Throughput' },
            { value: 'CostEfficiency', label: 'CostEfficiency' },
          ]} />
        </Form.Item>
        <Tooltip title={t('capability.autoEvaluateHint')}>
          <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>{t('capability.autoEvaluateHint')}</span>
        </Tooltip>
      </FormModal>
    </div>
  );
}
