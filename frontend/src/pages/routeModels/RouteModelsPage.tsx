import { useState, useCallback } from 'react';
import { Table, Form, Input, Select, InputNumber, Switch, Space, Tag, Card, Button } from 'antd';
import { EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { routeModelApi, modelApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, FormModal } from '../../components';
import type { RouteModel, Model } from '../../types';

export default function RouteModelsPage() {
  const { t } = useTranslation();
  const [models, setModels] = useState<Model[]>([]);
  const [ruleOpen, setRuleOpen] = useState(false);
  const [ruleTarget, setRuleTarget] = useState<RouteModel | null>(null);
  const [ruleForm] = Form.useForm();

  const fetchFn = useCallback((params: any) => routeModelApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage, setKeyword } = useCrudList<RouteModel, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<RouteModel>({
    createFn: routeModelApi.create,
    updateFn: routeModelApi.update,
    onSuccess: fetchData,
  });
  const { handleDelete } = useConfirmDelete(routeModelApi.delete, fetchData);

  const ensureModels = async () => {
    if (!models.length) {
      try { setModels(await modelApi.allEnabled()); } catch {}
    }
  };

  const handleAddRule = async () => {
    if (!ruleTarget) return;
    try {
      const values = await ruleForm.validateFields();
      const condition = JSON.stringify(
        values.type === 'Keyword' ? { keywords: values.keywords.split(',').map((k: string) => k.trim()) }
        : values.type === 'Regex' ? { pattern: values.pattern }
        : { maxTokens: values.maxTokens }
      );
      await routeModelApi.addRule(ruleTarget.id, {
        type: values.type, condition, targetModelId: values.targetModelId, priority: values.priority,
      });
      setRuleOpen(false);
      ruleForm.resetFields();
      fetchData();
    } catch {}
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
    { title: t('routeModel.rules'), key: 'rules', render: (_: any, r: RouteModel) => r.rules?.length || 0 },
    { title: t('common.enabled'), dataIndex: 'isEnabled', key: 'isEnabled', render: (v: boolean) => <Tag color={v ? 'success' : 'default'}>{v ? t('common.yes') : t('common.no')}</Tag> },
    {
      title: t('common.actions'), key: 'actions', fixed: 'right' as const, width: 200,
      render: (_: any, r: RouteModel) => (
        <Space>
          <Button type="text" icon={<EditOutlined />} onClick={async () => { await ensureModels(); openEdit(r); }} />
          <Button type="text" onClick={async () => { await ensureModels(); setRuleTarget(r); ruleForm.resetFields(); setRuleOpen(true); }}>
            {t('routeModel.addRule')}
          </Button>
          <Button type="text" danger icon={<DeleteOutlined />} onClick={() => handleDelete(r.id)} />
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('routeModel.title')} onCreate={async () => { await ensureModels(); openCreate(); }} onSearch={setKeyword} />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 900 }}
        />
      </Card>

      <FormModal title={t('routeModel.title')} open={open} form={form} editing={!!editing} submitting={submitting} onOk={submit} onCancel={close}>
        <Form.Item name="name" label={t('routeModel.name')} rules={[{ required: true }]}><Input placeholder="smart-router" /></Form.Item>
        <Form.Item name="description" label={t('provider.description')}><Input.TextArea rows={2} /></Form.Item>
        <Form.Item name="mode" label={t('routeModel.mode')} rules={[{ required: true }]}>
          <Select options={[{ value: 'Shadow', label: t('routeModel.shadow') }, { value: 'Route', label: t('routeModel.route') }]} />
        </Form.Item>
        <Form.Item name="targetModelId" label={t('routeModel.target')}>
          <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item name="fallbackModelId" label={t('routeModel.fallback')}>
          <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.mode !== cur.mode}>
          {({ getFieldValue }) => {
            const mode = getFieldValue('mode');
            if (mode !== 'Route') return null;
            return (
              <Form.Item name="routingModelId" label={t('routeModel.routingModel')} extra={t('routeModel.routingModelHint')}>
                <Select options={modelOptions} allowClear showSearch optionFilterProp="label" />
              </Form.Item>
            );
          }}
        </Form.Item>
        <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true}><Switch /></Form.Item>
      </FormModal>

      <FormModal title={t('routeModel.addRule')} open={ruleOpen} form={ruleForm} editing={false} onOk={handleAddRule} onCancel={() => setRuleOpen(false)}>
        <Form.Item name="type" label={t('routeModel.ruleType')} rules={[{ required: true }]}>
          <Select options={[
            { value: 'Keyword', label: t('routeModel.keyword') },
            { value: 'Regex', label: t('routeModel.regex') },
            { value: 'ContextSize', label: t('routeModel.contextSize') },
          ]} />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(prev, cur) => prev.type !== cur.type}>
          {({ getFieldValue }) => {
            const type = getFieldValue('type');
            if (type === 'Keyword') return <Form.Item name="keywords" label={t('routeModel.keyword')} rules={[{ required: true }]}><Input placeholder="code, math, translate" /></Form.Item>;
            if (type === 'Regex') return <Form.Item name="pattern" label={t('routeModel.regex')} rules={[{ required: true }]}><Input placeholder="\\bcode\\b" /></Form.Item>;
            if (type === 'ContextSize') return <Form.Item name="maxTokens" label={t('routeModel.contextSize')} rules={[{ required: true }]}><InputNumber min={0} addonAfter="tokens" style={{ width: '100%' }} /></Form.Item>;
            return null;
          }}
        </Form.Item>
        <Form.Item name="targetModelId" label={t('routeModel.target')} rules={[{ required: true }]}>
          <Select options={modelOptions} showSearch optionFilterProp="label" />
        </Form.Item>
        <Form.Item name="priority" label={t('routeModel.priority')} initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>
      </FormModal>
    </div>
  );
}
