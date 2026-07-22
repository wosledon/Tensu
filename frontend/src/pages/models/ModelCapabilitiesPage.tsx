import { useState, useCallback } from 'react';
import { Table, Form, Input, InputNumber, Select, Space, Tag, Card, Button, Tooltip, DatePicker, App } from 'antd';
import { EditOutlined, DeleteOutlined, ThunderboltOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import dayjs from 'dayjs';
import { modelCapabilityApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import { getApiErrorMessage } from '../../api/errorHandler';
import type { ModelCapability, Model } from '../../types';
import { exportTableToCsv } from '../../utils/export';

const dimensions = ['Reasoning', 'Code', 'Vision', 'Math', 'ToolUse', 'Multilingual', 'Safety', 'Latency', 'Throughput', 'CostEfficiency'];
const sources = ['benchmark', 'manual', 'synthetic', 'custom'];

export default function ModelCapabilitiesPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [models, setModels] = useState<Model[]>([]);
  const [modelFilter, setModelFilter] = useState<number | undefined>();
  const [dimensionFilter, setDimensionFilter] = useState<string | undefined>();
  const [autoEvalOpen, setAutoEvalOpen] = useState(false);
  const [autoEvalTarget, setAutoEvalTarget] = useState<ModelCapability | null>(null);
  const [autoEvalForm] = Form.useForm();

  const fetchFn = useCallback(
    (params: any) => modelCapabilityApi.list({ ...params, modelId: modelFilter, dimension: dimensionFilter }),
    [modelFilter, dimensionFilter],
  );
  const { data, total, loading, params, fetchData, setPage } = useCrudList<ModelCapability, any>({ fetchFn, persistKey: 'modelCapabilities' });
  const { form, open, editing, submitting, openCreate, openEdit, close } = useFormModal<ModelCapability>({
    createFn: modelCapabilityApi.create,
    updateFn: modelCapabilityApi.update,
    onSuccess: fetchData,
    transformRecord: (record) => ({
      ...record,
      evaluatedAt: record.evaluatedAt ? dayjs(record.evaluatedAt) : dayjs(),
    } as any),
    transformSubmit: (values) => ({
      ...values,
      evaluatedAt: (values as any).evaluatedAt ? dayjs((values as any).evaluatedAt).toISOString() : values.evaluatedAt,
    }),
  });

  // 自定义提交：每条记录 = 一个模型 + 一个维度 + 对应分数
  const handleFormOk = async () => {
    const values = await form.validateFields();
    const evaluatedAt = values.evaluatedAt ? dayjs(values.evaluatedAt).toISOString() : new Date().toISOString();
    try {
      if (editing) {
        await modelCapabilityApi.update(editing.id, {
          modelId: values.modelId,
          dimension: values.dimension,
          score: values.score,
          source: values.source,
          evidence: values.evidence,
          evaluatedAt,
        });
      } else {
        // 批量创建：从 scores 对象生成每条记录
        const scores: Record<string, number> = values.scores ?? {};
        const records = Object.entries(scores)
          .filter(([_, score]) => score != null)
          .map(([dim, score]) => ({
            modelId: values.modelId,
            dimension: dim,
            score,
            source: values.source,
            evidence: values.evidence,
            evaluatedAt,
          }));
        if (records.length === 0) {
          message.warning(t('capability.noScores'));
          return;
        }
        await modelCapabilityApi.import(records);
      }
      message.success(t('common.success'));
      close();
      fetchData();
    } catch (e) {
      message.error(getApiErrorMessage(e, t('common.error')));
    }
  };

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
    { title: t('capability.dimension'), dataIndex: 'dimension', key: 'dimension', render: (v: string) => <span style={{ fontFamily: 'monospace' }}>{v}</span> },
    {
      title: t('capability.score'), key: 'score', align: 'right' as const, width: 100,
      render: (_: any, r: ModelCapability) => (
        <Tag color={r.score >= 80 ? 'green' : r.score >= 60 ? 'orange' : 'red'}>
          <span style={{ fontFamily: 'monospace' }}>{r.score.toFixed(1)}</span>
        </Tag>
      ),
    },
    { title: t('capability.source'), dataIndex: 'source', key: 'source', width: 120, render: (v: string) => <span style={{ fontFamily: 'monospace' }}>{t(`capability.src${v.charAt(0).toUpperCase() + v.slice(1)}`)}</span> },
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
        onRefresh={fetchData}
        refreshing={loading}
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
          <Select
            allowClear
            placeholder={t('capability.dimension')}
            style={{ width: 200 }}
            options={dimensions.map((d) => ({ value: d, label: t(`capability.dim${d}`) }))}
            value={dimensionFilter}
            onChange={(v) => setDimensionFilter(v)}
          />
          {(modelFilter || dimensionFilter) && (
            <Button onClick={() => { setModelFilter(undefined); setDimensionFilter(undefined); }}>{t('common.reset')}</Button>
          )}
        </Space>
      </Card>
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 1000 }}
        />
      </Card>

      <FormModal title={t('capability.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={handleFormOk} onCancel={close} width={640}>
        <Form.Item name="modelId" label={t('model.name')} rules={[{ required: true }]}>
          <Select options={modelOptions} showSearch optionFilterProp="label" />
        </Form.Item>

        {editing ? (
          // 编辑模式：单维度单分数
          <>
            <Form.Item name="dimension" label={t('capability.dimension')} rules={[{ required: true }]}>
              <Select options={dimensions.map((d) => ({ value: d, label: t(`capability.dim${d}`) }))} showSearch />
            </Form.Item>
            <Form.Item name="score" label={t('capability.score')} rules={[{ required: true, type: 'number' }]}>
              <InputNumber min={0} max={100} step={0.1} precision={2} style={{ width: '100%' }} addonAfter="0-100" />
            </Form.Item>
          </>
        ) : (
          // 创建模式：选择维度后逐维评分
          <>
            <Form.Item name="dimensions" label={t('capability.dimensions')} rules={[{ required: true, type: 'array', min: 1 }]}
              tooltip={t('capability.selectMultipleHint')}
            >
              <Select mode="multiple" options={dimensions.map((d) => ({ value: d, label: t(`capability.dim${d}`) }))} showSearch placeholder={t('capability.selectMultipleHint')} />
            </Form.Item>
            <Form.Item noStyle shouldUpdate={(prev, cur) => prev.dimensions !== cur.dimensions}>
              {({ getFieldValue }) => {
                const selected: string[] = getFieldValue('dimensions') ?? [];
                if (selected.length === 0) return null;
                return (
                  <div style={{ marginBottom: 16 }}>
                    <div style={{ fontWeight: 500, marginBottom: 8 }}>{t('capability.scores')}</div>
                    <Space direction="vertical" style={{ width: '100%' }} size={8}>
                      {selected.map((dim) => (
                        <div key={dim} style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                          <Tag style={{ width: 120, textAlign: 'center', margin: 0 }}>{t(`capability.dim${dim}`)}</Tag>
                          <Form.Item name={['scores', dim]} rules={[{ required: true, type: 'number', min: 0, max: 100 }]} noStyle initialValue={85}>
                            <InputNumber min={0} max={100} step={0.1} precision={2} style={{ width: 160 }} addonAfter="0-100" />
                          </Form.Item>
                        </div>
                      ))}
                    </Space>
                  </div>
                );
              }}
            </Form.Item>
          </>
        )}

        <Form.Item name="source" label={t('capability.source')} rules={[{ required: true }]} initialValue="manual">
          <Select options={sources.map((s) => ({ value: s, label: t(`capability.src${s.charAt(0).toUpperCase() + s.slice(1)}`) }))} />
        </Form.Item>
        <Form.Item name="evidence" label={t('capability.evidence')}>
          <Input.TextArea rows={3} />
        </Form.Item>
        <Form.Item name="evaluatedAt" label={t('capability.evaluatedAt')} rules={[{ required: true }]} initialValue={dayjs()}>
          <DatePicker showTime style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>

      <FormModal title={t('capability.autoEvaluate')} open={autoEvalOpen} form={autoEvalForm} editing={false} onOk={handleAutoEvaluate} onCancel={() => setAutoEvalOpen(false)}>
        <Form.Item name="dimensions" label={t('capability.dimensions')} rules={[{ required: true }]}>
          <Select mode="multiple" options={dimensions.map((d) => ({ value: d, label: t(`capability.dim${d}`) }))} />
        </Form.Item>
        <Tooltip title={t('capability.autoEvaluateHint')}>
          <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>{t('capability.autoEvaluateHint')}</span>
        </Tooltip>
      </FormModal>
    </div>
  );
}
