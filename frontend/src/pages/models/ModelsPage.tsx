import { useState, useCallback } from 'react';
import { Table, Form, Input, InputNumber, Select, Switch, Space, Tag, Card, Button, Tooltip } from 'antd';
import { EditOutlined, DeleteOutlined, DollarOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { modelApi, providerApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, CapabilityTags, FormModal } from '../../components';
import type { Model, Provider } from '../../types';

export default function ModelsPage() {
  const { t } = useTranslation();
  const [providers, setProviders] = useState<Provider[]>([]);
  const [pricingOpen, setPricingOpen] = useState(false);
  const [pricingTarget, setPricingTarget] = useState<Model | null>(null);
  const [pricingForm] = Form.useForm();

  const fetchFn = useCallback((params: any) => modelApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage } = useCrudList<Model, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Model>({
    createFn: modelApi.create,
    updateFn: modelApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(modelApi.delete, fetchData);

  const ensureProviders = async () => {
    if (!providers.length) {
      try { setProviders((await providerApi.list({ page: 1, pageSize: 100 })).items); } catch {}
    }
  };

  const handlePricing = async () => {
    if (!pricingTarget) return;
    try {
      const values = await pricingForm.validateFields();
      await modelApi.addPricing(pricingTarget.id, values);
      setPricingOpen(false);
      pricingForm.resetFields();
      fetchData();
    } catch {}
  };

  const columns = [
    {
      title: t('model.name'), key: 'name', sorter: true,
      render: (_: any, r: Model) => <span style={{ fontFamily: 'monospace' }}>{r.provider?.name}-{r.name}</span>,
    },
    { title: t('model.displayName'), dataIndex: 'displayName', key: 'displayName' },
    { title: t('model.provider'), key: 'provider', render: (_: any, r: Model) => r.provider?.name },
    {
      title: t('model.vision'), key: 'capabilities', width: 220,
      render: (_: any, r: Model) => <CapabilityTags vision={r.supportsVision} reasoning={r.supportsReasoning} toolUse={r.supportsToolUse} thinking={r.supportsThinking} />,
    },
    {
      title: t('model.inputContext'), key: 'context', width: 160,
      render: (_: any, r: Model) => (
        <span style={{ fontFamily: 'monospace', fontSize: 12 }}>
          {(r.inputContextSize / 1000).toFixed(0)}K / {(r.outputContextSize / 1000).toFixed(0)}K
        </span>
      ),
    },
    {
      title: t('model.pricing'), key: 'pricing', width: 120,
      render: (_: any, r: Model) =>
        r.pricings?.length ? (
          <Tooltip title={`$${r.pricings[0].inputPricePerMillionTokens} / $${r.pricings[0].outputPricePerMillionTokens} ${t('model.perMillion')}`}>
            <Tag color="green"><DollarOutlined /> {r.pricings[0].currency}</Tag>
          </Tooltip>
        ) : <Tag>{t('common.noData')}</Tag>,
    },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 160,
      render: (_: any, r: Model) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureProviders(); openEdit(r); }} />
          <Button type="text" icon={<DollarOutlined />} onClick={() => { setPricingTarget(r); pricingForm.resetFields(); setPricingOpen(true); }} />
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  const providerOptions = providers.map((p) => ({ value: p.id, label: `${p.name} (${p.protocol})` }));

  return (
    <div>
      <PageHeader title={t('model.title')} onCreate={async () => { await ensureProviders(); openCreate(); }} />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 1000 }}
        />
      </Card>

      <FormModal title={t('model.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="providerId" label={t('model.provider')} rules={[{ required: true }]}>
          <Select options={providerOptions} showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item name="name" label={t('model.name')} rules={[{ required: true }]}><Input placeholder="gpt-4o" /></Form.Item>
        <Form.Item name="displayName" label={t('model.displayName')}><Input /></Form.Item>
        <Space size={16} wrap>
          <Form.Item name="supportsVision" label={t('model.vision')} valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="supportsReasoning" label={t('model.reasoning')} valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="supportsToolUse" label={t('model.toolUse')} valuePropName="checked"><Switch /></Form.Item>
          <Form.Item name="supportsThinking" label={t('model.thinking')} valuePropName="checked"><Switch /></Form.Item>
        </Space>
        <Space size={16}>
          <Form.Item name="inputContextSize" label={t('model.inputContext')} rules={[{ required: true }]}>
            <InputNumber min={0} addonAfter="tokens" style={{ width: 180 }} />
          </Form.Item>
          <Form.Item name="outputContextSize" label={t('model.outputContext')} rules={[{ required: true }]}>
            <InputNumber min={0} addonAfter="tokens" style={{ width: 180 }} />
          </Form.Item>
        </Space>
        <Space size={16}>
          <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
          <Form.Item name="compressionEnabled" label={t('audit.compression')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
        </Space>
      </FormModal>

      <FormModal title={t('model.pricing')} open={pricingOpen} form={pricingForm} editing={false} onOk={handlePricing} onCancel={() => setPricingOpen(false)}>
        <Form.Item name="inputPricePerMillionTokens" label={t('model.inputPrice')} rules={[{ required: true }]}>
          <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="outputPricePerMillionTokens" label={t('model.outputPrice')} rules={[{ required: true }]}>
          <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="cachedInputPricePerMillionTokens" label={t('model.cachedPrice')}>
          <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="currency" label={t('model.currency')} initialValue="USD">
          <Select options={[{ value: 'USD', label: 'USD' }, { value: 'CNY', label: 'CNY' }, { value: 'EUR', label: 'EUR' }]} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
