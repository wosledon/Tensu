import { useState, useEffect } from 'react';
import { Card, Form, Switch, InputNumber, Button, Space, Typography, Spin, App, Input, Select } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../../hooks/useThemeMode';
import { settingsApi } from '../../api';
import { PageHeader } from '../../components';

const { Text } = Typography;

interface Settings {
  [key: string]: string;
}

export default function SettingsPage() {
  const { t } = useTranslation();
  const { mode, setMode } = useThemeMode();
  const { message } = App.useApp();
  const [, setSettings] = useState<Settings>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    settingsApi.getAll()
      .then((data) => {
        setSettings(data);
        form.setFieldsValue({
          compressionEnabled: data['compression.enabled'] === 'true',
          cacheEnabled: data['cache.enabled'] === 'true',
          cacheBackend: data['cache.backend'] || 'memory',
          cacheTtlMinutes: parseInt(data['cache.ttlMinutes'] || '10'),
          auditEncryptContent: data['audit.encryptContent'] === 'true',
          defaultRpm: parseInt(data['rateLimit.defaultRpm'] || '60'),
          defaultTpm: parseInt(data['rateLimit.defaultTpm'] || '100000'),
          dataRetentionDays: parseInt(data['audit.dataRetentionDays'] || '30'),
          usageSpikeMultiplier: parseFloat(data['anomaly.usageSpikeMultiplier'] || '2.0'),
          usageDropMultiplier: parseFloat(data['anomaly.usageDropMultiplier'] || '0.5'),
          costSpikeMultiplier: parseFloat(data['anomaly.costSpikeMultiplier'] || '2.0'),
          latencySpikeMultiplier: parseFloat(data['anomaly.latencySpikeMultiplier'] || '2.0'),
          errorRateThreshold: parseFloat(data['anomaly.errorRateThreshold'] || '0.1'),
          rateLimitThreshold: parseFloat(data['anomaly.rateLimitThreshold'] || '0.05'),
          providerSuccessRateThreshold: parseFloat(data['anomaly.providerSuccessRateThreshold'] || '0.95'),
          desensitizationEnabled: data['desensitization.enabled'] !== 'false',
          desensitizationMaskApiKeys: data['desensitization.maskApiKeys'] !== 'false',
          desensitizationMaskEmails: data['desensitization.maskEmails'] !== 'false',
          desensitizationMaskIps: data['desensitization.maskIps'] !== 'false',
          desensitizationMaskTokens: data['desensitization.maskTokens'] !== 'false',
          keyRotationEnabled: data['keyRotation.enabled'] !== 'false',
          keyRotationCheckIntervalHours: parseInt(data['keyRotation.checkIntervalHours'] || '24'),
          keyRotationExpiryWarningDays: parseInt(data['keyRotation.expiryWarningDays'] || '7'),
          retryMaxRetries: parseInt(data['retry.maxRetries'] || '2'),
          retryBaseDelayMs: parseInt(data['retry.baseDelayMs'] || '500'),
          retryMaxDelayMs: parseInt(data['retry.maxDelayMs'] || '5000'),
          defaultCurrency: data['currency.default'] || 'USD',
          currencyRateUSD: parseFloat(data['currency.rate.USD'] || '1.0'),
          currencyRateCNY: parseFloat(data['currency.rate.CNY'] || '7.2'),
          currencyRateEUR: parseFloat(data['currency.rate.EUR'] || '0.92'),
          currencyRateJPY: parseFloat(data['currency.rate.JPY'] || '150.0'),
          currencyRateGBP: parseFloat(data['currency.rate.GBP'] || '0.79'),
        });
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [form]);

  const handleSave = async () => {
    setSaving(true);
    try {
      const values = form.getFieldsValue();
      const updates: [string, string][] = [
        ['compression.enabled', String(values.compressionEnabled)],
        ['cache.enabled', String(values.cacheEnabled)],
        ['cache.backend', String(values.cacheBackend)],
        ['cache.ttlMinutes', String(values.cacheTtlMinutes)],
        ['audit.encryptContent', String(values.auditEncryptContent)],
        ['rateLimit.defaultRpm', String(values.defaultRpm)],
        ['rateLimit.defaultTpm', String(values.defaultTpm)],
        ['audit.dataRetentionDays', String(values.dataRetentionDays)],
        ['anomaly.usageSpikeMultiplier', String(values.usageSpikeMultiplier)],
        ['anomaly.usageDropMultiplier', String(values.usageDropMultiplier)],
        ['anomaly.costSpikeMultiplier', String(values.costSpikeMultiplier)],
        ['anomaly.latencySpikeMultiplier', String(values.latencySpikeMultiplier)],
        ['anomaly.errorRateThreshold', String(values.errorRateThreshold)],
        ['anomaly.rateLimitThreshold', String(values.rateLimitThreshold)],
        ['anomaly.providerSuccessRateThreshold', String(values.providerSuccessRateThreshold)],
        ['desensitization.enabled', String(values.desensitizationEnabled)],
        ['desensitization.maskApiKeys', String(values.desensitizationMaskApiKeys)],
        ['desensitization.maskEmails', String(values.desensitizationMaskEmails)],
        ['desensitization.maskIps', String(values.desensitizationMaskIps)],
        ['desensitization.maskTokens', String(values.desensitizationMaskTokens)],
        ['keyRotation.enabled', String(values.keyRotationEnabled)],
        ['keyRotation.checkIntervalHours', String(values.keyRotationCheckIntervalHours)],
        ['keyRotation.expiryWarningDays', String(values.keyRotationExpiryWarningDays)],
        ['retry.maxRetries', String(values.retryMaxRetries)],
        ['retry.baseDelayMs', String(values.retryBaseDelayMs)],
        ['retry.maxDelayMs', String(values.retryMaxDelayMs)],
        ['currency.default', String(values.defaultCurrency)],
        ['currency.rate.USD', String(values.currencyRateUSD)],
        ['currency.rate.CNY', String(values.currencyRateCNY)],
        ['currency.rate.EUR', String(values.currencyRateEUR)],
        ['currency.rate.JPY', String(values.currencyRateJPY)],
        ['currency.rate.GBP', String(values.currencyRateGBP)],
      ];
      await Promise.all(updates.map(([k, v]) => settingsApi.set(k, v)));
      message.success(t('common.success'));
    } catch {
      message.error(t('common.error'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div style={{ display: 'flex', justifyContent: 'center', paddingTop: 120 }}><Spin size="large" /></div>;

  return (
    <div>
      <PageHeader
        title={t('settings.title')}
        extra={
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={handleSave}>
            {t('common.save')}
          </Button>
        }
      />

      <Form form={form} layout="vertical" style={{ maxWidth: 700 }}>
        <Card title={t('settings.general')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Form.Item label={t('settings.theme')}>
            <Button.Group>
              {(['light', 'dark', 'system'] as const).map((m) => (
                <Button key={m} type={mode === m ? 'primary' : 'default'} onClick={() => setMode(m)}>
                  {m === 'light' ? t('settings.themeLight') : m === 'dark' ? t('settings.themeDark') : t('settings.themeSystem')}
                </Button>
              ))}
            </Button.Group>
          </Form.Item>
        </Card>

        <Card title={t('settings.compression')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Form.Item name="compressionEnabled" label={t('common.enabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.compressionHint')}
          </Text>
        </Card>

        <Card title={t('settings.cache')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="cacheEnabled" label={t('common.enabled')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="cacheBackend" label={t('settings.cacheBackend')}>
              <Select
                style={{ width: 160 }}
                options={[
                  { value: 'memory', label: t('settings.cacheBackendMemory') },
                  { value: 'database', label: t('settings.cacheBackendDatabase') },
                ]}
              />
            </Form.Item>
            <Form.Item name="cacheTtlMinutes" label={t('settings.ttlMinutes')}>
              <InputNumber min={1} max={60} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.cacheHint')}
          </Text>
        </Card>

        <Card title={t('settings.audit')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="dataRetentionDays" label={t('settings.dataRetentionDays')}>
              <InputNumber min={7} max={180} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="auditEncryptContent" label={t('settings.auditEncryptContent')} valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.auditEncryptContentHint')}
          </Text>
        </Card>

        <Card title={t('settings.rateLimit')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="defaultRpm" label={t('settings.defaultRpm')}>
              <InputNumber min={0} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="defaultTpm" label={t('settings.defaultTpm')}>
              <InputNumber min={0} style={{ width: 160 }} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.rateLimitHint')}
          </Text>
        </Card>

        <Card title={t('settings.anomaly')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="usageSpikeMultiplier" label={t('settings.usageSpikeMultiplier')}>
              <InputNumber min={1} max={10} step={0.1} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="usageDropMultiplier" label={t('settings.usageDropMultiplier')}>
              <InputNumber min={0.1} max={1} step={0.1} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="costSpikeMultiplier" label={t('settings.costSpikeMultiplier')}>
              <InputNumber min={1} max={10} step={0.1} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="latencySpikeMultiplier" label={t('settings.latencySpikeMultiplier')}>
              <InputNumber min={1} max={10} step={0.1} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="errorRateThreshold" label={t('settings.errorRateThreshold')}>
              <InputNumber min={0} max={1} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="rateLimitThreshold" label={t('settings.rateLimitThreshold')}>
              <InputNumber min={0} max={1} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="providerSuccessRateThreshold" label={t('settings.providerSuccessRateThreshold')}>
              <InputNumber min={0} max={1} step={0.01} style={{ width: 160 }} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.anomalyHint')}
          </Text>
        </Card>

        <Card title={t('settings.currency')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Form.Item name="defaultCurrency" label={t('settings.defaultCurrency')}>
            <Input style={{ width: 160 }} />
          </Form.Item>
          <Space size={16} wrap>
            <Form.Item name="currencyRateUSD" label={t('settings.currencyRateUSD')}>
              <InputNumber min={0} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="currencyRateCNY" label={t('settings.currencyRateCNY')}>
              <InputNumber min={0} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="currencyRateEUR" label={t('settings.currencyRateEUR')}>
              <InputNumber min={0} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="currencyRateJPY" label={t('settings.currencyRateJPY')}>
              <InputNumber min={0} step={0.01} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="currencyRateGBP" label={t('settings.currencyRateGBP')}>
              <InputNumber min={0} step={0.01} style={{ width: 160 }} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.currencyHint')}
          </Text>
        </Card>

        <Card title={t('settings.desensitization')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Form.Item name="desensitizationEnabled" label={t('settings.desensitizationEnabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Space size={16} wrap>
            <Form.Item name="desensitizationMaskApiKeys" label={t('settings.desensitizationMaskApiKeys')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="desensitizationMaskEmails" label={t('settings.desensitizationMaskEmails')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="desensitizationMaskIps" label={t('settings.desensitizationMaskIps')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="desensitizationMaskTokens" label={t('settings.desensitizationMaskTokens')} valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>
        </Card>

        <Card title={t('settings.retry')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="retryMaxRetries" label={t('settings.retryMaxRetries')}>
              <InputNumber min={0} max={10} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="retryBaseDelayMs" label={t('settings.retryBaseDelayMs')}>
              <InputNumber min={0} max={30000} step={100} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="retryMaxDelayMs" label={t('settings.retryMaxDelayMs')}>
              <InputNumber min={0} max={120000} step={100} style={{ width: 160 }} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {t('settings.retryHint', 'Exponential backoff with jitter for non-streaming idempotent requests.')}
          </Text>
        </Card>

        <Card title={t('settings.keyRotation')} style={{ borderRadius: 18 }}>
          <Form.Item name="keyRotationEnabled" label={t('settings.keyRotationEnabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Space size={16} wrap>
            <Form.Item name="keyRotationCheckIntervalHours" label={t('settings.keyRotationCheckInterval')}>
              <InputNumber min={1} max={168} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="keyRotationExpiryWarningDays" label={t('settings.keyRotationExpiryWarning')}>
              <InputNumber min={1} max={90} style={{ width: 160 }} addonAfter={t('settings.days')} />
            </Form.Item>
          </Space>
        </Card>
      </Form>
    </div>
  );
}
