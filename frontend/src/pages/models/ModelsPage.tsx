import { useState, useCallback, useEffect } from 'react';
import { Table, Form, Input, InputNumber, Select, Space, Tag, Card, Button, Tooltip, Typography, theme, App, Row, Col } from 'antd';
import { EditOutlined, DeleteOutlined, DollarOutlined, EyeOutlined, BulbOutlined, ToolOutlined, ExperimentOutlined, ContainerOutlined, ThunderboltOutlined, CompressOutlined, SettingOutlined, ExportOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { modelApi, providerApi } from '../../api';
import { useCrudList, useFormModal, useConfirmDelete } from '../../hooks';
import { PageHeader, CapabilityTags, FormModal } from '../../components';
import type { Model, Provider } from '../../types';
import { exportTableToCsv } from '../../utils/export';

interface CapabilityItemProps {
  icon: React.ComponentType<{ style?: React.CSSProperties }>;
  label: string;
  hint: string;
  name: string;
  color?: string;
}

function CapabilityItem({ icon: Icon, label, hint, name, color }: CapabilityItemProps) {
  const { token } = theme.useToken();
  return (
    <Form.Item name={name} valuePropName="checked" style={{ marginBottom: 0 }}>
      <Form.Item noStyle shouldUpdate={(prev, cur) => prev[name] !== cur[name]}>
        {({ getFieldValue, setFieldValue }) => {
          const checked = getFieldValue(name);
          return (
            <div
              onClick={() => setFieldValue(name, !checked)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                padding: '12px 16px',
                borderRadius: token.borderRadiusLG,
                backgroundColor: checked ? `${color ?? token.colorPrimary}0F` : token.colorFillQuaternary,
                border: `1.5px solid ${checked ? color ?? token.colorPrimary : 'transparent'}`,
                cursor: 'pointer',
                transition: 'all 200ms ease',
                userSelect: 'none',
              }}
            >
              <Icon style={{ fontSize: 18, color: checked ? color ?? token.colorPrimary : token.colorTextQuaternary }} />
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 500, fontSize: 14, color: checked ? undefined : token.colorTextSecondary }}>{label}</div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>{hint}</Typography.Text>
              </div>
              <div style={{
                width: 20, height: 20, borderRadius: '50%',
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                backgroundColor: checked ? color ?? token.colorPrimary : token.colorBorderSecondary,
                transition: 'all 200ms ease', flexShrink: 0,
              }}>
                {checked && (
                  <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                    <path d="M2.5 6l2.5 2.5 4.5-5" stroke="#fff" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                )}
              </div>
            </div>
          );
        }}
      </Form.Item>
    </Form.Item>
  );
}

export default function ModelsPage() {
  const { t } = useTranslation();
  const { token } = theme.useToken();
  const { message } = App.useApp();
  const [providers, setProviders] = useState<Provider[]>([]);
  const [pricingOpen, setPricingOpen] = useState(false);
  const [pricingTarget, setPricingTarget] = useState<Model | null>(null);
  const [pricingForm] = Form.useForm();
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const fetchFn = useCallback((params: any) => modelApi.list(params), []);
  const { data, total, loading, params, fetchData, setPage } = useCrudList<Model, any>({ fetchFn });
  const { form, open, editing, submitting, openCreate, openEdit, close, submit } = useFormModal<Model>({
    createFn: modelApi.create,
    updateFn: modelApi.update,
    onSuccess: fetchData,
    transformRecord: (record) => ({
      ...record,
      thinkingStrengths: record.thinkingStrengths ? JSON.parse(record.thinkingStrengths) : undefined,
    }),
    transformSubmit: (values) => ({
      ...values,
      thinkingStrengths: values.thinkingStrengths ? JSON.stringify(values.thinkingStrengths) : undefined,
    }),
  });
  const { handleDelete } = useConfirmDelete(modelApi.delete, fetchData);

  const handleBatchDelete = async () => {
    if (!selectedRowKeys.length) return;
    try {
      await modelApi.batchDelete(selectedRowKeys.map((k) => Number(k)));
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
      await modelApi.batchEnable(selectedRowKeys.map((k) => Number(k)));
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
      await modelApi.batchDisable(selectedRowKeys.map((k) => Number(k)));
      message.success(t('common.success'));
      setSelectedRowKeys([]);
      fetchData();
    } catch {
      message.error(t('common.error'));
    }
  };

  const ensureProviders = useCallback(async () => {
    if (!providers.length) {
      try { setProviders((await providerApi.list({ page: 1, pageSize: 100 })).items); } catch {}
    }
  }, [providers]);

  const [syncProviderId, setSyncProviderId] = useState<number>();
  const [syncing, setSyncing] = useState(false);

  useEffect(() => {
    ensureProviders();
  }, [ensureProviders]);

  const handleSync = async () => {
    if (!syncProviderId) return;
    setSyncing(true);
    try {
      const result = await modelApi.syncFromProvider(syncProviderId);
      message.success(t('model.syncSuccess', { added: result.added, total: result.total }));
      fetchData();
    } catch {
      message.error(t('model.syncError'));
    } finally {
      setSyncing(false);
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
      title: t('model.capabilities'), key: 'capabilities', width: 280,
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

  const handleExport = () => {
    exportTableToCsv('models', columns, data);
  };

  return (
    <div>
      <PageHeader
        title={t('model.title')}
        extra={
          <Space>
            <Select
              placeholder={t('model.selectProvider')}
              options={providerOptions}
              value={syncProviderId}
              onChange={setSyncProviderId}
              showSearch
              optionFilterProp="label"
              style={{ width: 220 }}
            />
            <Button loading={syncing} disabled={!syncProviderId} onClick={handleSync}>
              {t('model.syncFromProvider')}
            </Button>
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
        onCreate={async () => { await ensureProviders(); openCreate(); }}
      />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns} dataSource={data} rowKey="id" loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 1000 }}
          rowSelection={{
            selectedRowKeys,
            onChange: (keys: React.Key[]) => setSelectedRowKeys(keys),
          }}
        />
      </Card>

      <FormModal
        title={t('model.title')}
        header={editing ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span>{t('common.edit')}</span>
            <span style={{ color: token.colorTextTertiary }}>·</span>
            <span style={{ fontFamily: 'monospace', fontWeight: 500 }}>
              {editing.provider?.name}-{editing.name}
            </span>
          </div>
        ) : undefined}
        open={open}
        form={form}
        editing={!!editing}
        submitting={submitting}
        width={640}
        onOk={submit}
        onCancel={close}
      >
        <Card
          size="small"
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <ContainerOutlined style={{ color: token.colorPrimary }} />
              <span>{t('model.basicInfo')}</span>
            </div>
          }
          style={{ marginBottom: 16, borderRadius: token.borderRadiusLG, border: 'none' }}
          styles={{ body: { backgroundColor: token.colorFillQuaternary, borderRadius: `0 0 ${token.borderRadiusLG}px ${token.borderRadiusLG}px` } }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="providerId" label={t('model.provider')} rules={[{ required: true }]}>
                <Select options={providerOptions} showSearch optionFilterProp="label" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="name" label={t('model.name')} rules={[{ required: true }]}>
                <Input placeholder="gpt-4o" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="displayName" label={t('model.displayName')}>
                <Input />
              </Form.Item>
            </Col>
          </Row>
        </Card>

        <Card
          size="small"
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <ThunderboltOutlined style={{ color: token.colorWarning }} />
              <span>{t('model.capabilities')}</span>
            </div>
          }
          style={{ marginBottom: 16, borderRadius: token.borderRadiusLG, border: 'none' }}
          styles={{ body: { backgroundColor: token.colorFillQuaternary, borderRadius: `0 0 ${token.borderRadiusLG}px ${token.borderRadiusLG}px` } }}
        >
          <Row gutter={[16, 16]}>
            <Col span={12}>
              <CapabilityItem
                icon={EyeOutlined}
                label={t('model.vision')}
                hint={t('model.visionHint')}
                name="supportsVision"
                color={token.colorInfo}
              />
            </Col>
            <Col span={12}>
              <CapabilityItem
                icon={BulbOutlined}
                label={t('model.reasoning')}
                hint={t('model.reasoningHint')}
                name="supportsReasoning"
                color={token.colorWarning}
              />
            </Col>
            <Col span={12}>
              <CapabilityItem
                icon={ToolOutlined}
                label={t('model.toolUse')}
                hint={t('model.toolUseHint')}
                name="supportsToolUse"
                color={token.colorSuccess}
              />
            </Col>
            <Col span={12}>
              <CapabilityItem
                icon={ExperimentOutlined}
                label={t('model.thinking')}
                hint={t('model.thinkingHint')}
                name="supportsThinking"
                color={token.colorPrimary}
              />
            </Col>
            <Col span={24} style={{ marginTop: 8 }}>
              <Form.Item name="thinkingStrengths" label={t('model.thinkingStrengths')}>
                <Select mode="multiple" allowClear placeholder={t('model.reasoningIntensity')} options={[
                  { value: 'none', label: t('model.intensityNone') },
                  { value: 'low', label: t('model.intensityLow') },
                  { value: 'medium', label: t('model.intensityMedium') },
                  { value: 'high', label: t('model.intensityHigh') },
                  { value: 'max', label: t('model.intensityMax') },
                  { value: 'xhigh', label: t('model.intensityXHigh') },
                ]} />
              </Form.Item>
            </Col>
          </Row>
        </Card>

        <Card
          size="small"
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <CompressOutlined style={{ color: token.colorSuccess }} />
              <span>{t('model.contextWindow')}</span>
            </div>
          }
          style={{ marginBottom: 16, borderRadius: token.borderRadiusLG, border: 'none' }}
          styles={{ body: { backgroundColor: token.colorFillQuaternary, borderRadius: `0 0 ${token.borderRadiusLG}px ${token.borderRadiusLG}px` } }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="inputContextSize" label={t('model.inputContext')} rules={[{ required: true }]}>
                <InputNumber min={0} addonAfter="tokens" style={{ width: '100%' }} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="outputContextSize" label={t('model.outputContext')} rules={[{ required: true }]}>
                <InputNumber min={0} addonAfter="tokens" style={{ width: '100%' }} />
              </Form.Item>
            </Col>
          </Row>
        </Card>

        <Card
          size="small"
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <SettingOutlined style={{ color: token.colorTextSecondary }} />
              <span>{t('model.configuration')}</span>
            </div>
          }
          style={{ borderRadius: token.borderRadiusLG, border: 'none' }}
          styles={{ body: { backgroundColor: token.colorFillQuaternary, borderRadius: `0 0 ${token.borderRadiusLG}px ${token.borderRadiusLG}px` } }}
        >
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item name="isEnabled" label={t('common.enabled')} valuePropName="checked" initialValue={true}>
                <Form.Item noStyle shouldUpdate={(prev, cur) => prev.isEnabled !== cur.isEnabled}>
                  {({ getFieldValue, setFieldValue }) => {
                    const checked = getFieldValue('isEnabled');
                    return (
                      <div onClick={() => setFieldValue('isEnabled', !checked)}
                        style={{
                          padding: '10px 16px', borderRadius: token.borderRadiusLG, cursor: 'pointer',
                          backgroundColor: checked ? '#34C75918' : token.colorFillQuaternary,
                          border: `1.5px solid ${checked ? '#34C759' : 'transparent'}`,
                          transition: 'all 200ms ease', userSelect: 'none',
                          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                        }}
                      >
                        <span style={{ fontWeight: 500, fontSize: 14, color: checked ? '#34C759' : token.colorTextSecondary }}>
                          {checked ? t('common.yes') : t('common.no')}
                        </span>
                        <div style={{
                          width: 20, height: 20, borderRadius: '50%',
                          display: 'flex', alignItems: 'center', justifyContent: 'center',
                          backgroundColor: checked ? '#34C759' : token.colorBorderSecondary,
                          transition: 'all 200ms ease', flexShrink: 0,
                        }}>
                          {checked && (
                            <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                              <path d="M2.5 6l2.5 2.5 4.5-5" stroke="#fff" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            </svg>
                          )}
                        </div>
                      </div>
                    );
                  }}
                </Form.Item>
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="compressionEnabled" label={t('model.compression')} valuePropName="checked" initialValue={true}>
                <Form.Item noStyle shouldUpdate={(prev, cur) => prev.compressionEnabled !== cur.compressionEnabled}>
                  {({ getFieldValue, setFieldValue }) => {
                    const checked = getFieldValue('compressionEnabled');
                    return (
                      <div onClick={() => setFieldValue('compressionEnabled', !checked)}
                        style={{
                          padding: '10px 16px', borderRadius: token.borderRadiusLG, cursor: 'pointer',
                          backgroundColor: checked ? '#007AFF18' : token.colorFillQuaternary,
                          border: `1.5px solid ${checked ? '#007AFF' : 'transparent'}`,
                          transition: 'all 200ms ease', userSelect: 'none',
                          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                        }}
                      >
                        <span style={{ fontWeight: 500, fontSize: 14, color: checked ? '#007AFF' : token.colorTextSecondary }}>
                          {checked ? t('common.yes') : t('common.no')}
                        </span>
                        <div style={{
                          width: 20, height: 20, borderRadius: '50%',
                          display: 'flex', alignItems: 'center', justifyContent: 'center',
                          backgroundColor: checked ? '#007AFF' : token.colorBorderSecondary,
                          transition: 'all 200ms ease', flexShrink: 0,
                        }}>
                          {checked && (
                            <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                              <path d="M2.5 6l2.5 2.5 4.5-5" stroke="#fff" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            </svg>
                          )}
                        </div>
                      </div>
                    );
                  }}
                </Form.Item>
              </Form.Item>
            </Col>
          </Row>
        </Card>
      </FormModal>

      <FormModal title={t('model.pricing')} open={pricingOpen} form={pricingForm} editing={false} onOk={handlePricing} onCancel={() => setPricingOpen(false)}>
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item name="inputPricePerMillionTokens" label={t('model.inputPrice')} rules={[{ required: true }]}>
              <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="outputPricePerMillionTokens" label={t('model.outputPrice')} rules={[{ required: true }]}>
              <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
        </Row>
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item name="cachedInputPricePerMillionTokens" label={t('model.cachedPrice')}>
              <InputNumber min={0} step={0.01} precision={6} addonAfter={`USD${t('model.perMillion')}`} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="currency" label={t('model.currency')} initialValue="USD">
              <Select options={[{ value: 'USD', label: t('model.currencyUSD') }, { value: 'CNY', label: t('model.currencyCNY') }, { value: 'EUR', label: t('model.currencyEUR') }]} />
            </Form.Item>
          </Col>
        </Row>
      </FormModal>
    </div>
  );
}
