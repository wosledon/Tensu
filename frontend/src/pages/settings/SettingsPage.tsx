import { useState, useEffect } from 'react';
import { Card, Form, Switch, InputNumber, Button, Space, message, Typography, Spin } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../../contexts/ThemeContext';
import { settingsApi } from '../../api';
import { PageHeader } from '../../components';

const { Text } = Typography;

interface Settings {
  [key: string]: string;
}

export default function SettingsPage() {
  const { t } = useTranslation();
  const { mode, setMode } = useThemeMode();
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
          cacheTtlMinutes: parseInt(data['cache.ttlMinutes'] || '10'),
          defaultRpm: parseInt(data['rateLimit.defaultRpm'] || '60'),
          defaultTpm: parseInt(data['rateLimit.defaultTpm'] || '100000'),
          dataRetentionDays: parseInt(data['audit.dataRetentionDays'] || '30'),
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
        ['cache.ttlMinutes', String(values.cacheTtlMinutes)],
        ['rateLimit.defaultRpm', String(values.defaultRpm)],
        ['rateLimit.defaultTpm', String(values.defaultTpm)],
        ['audit.dataRetentionDays', String(values.dataRetentionDays)],
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
          <Form.Item label="Theme">
            <Button.Group>
              {(['light', 'dark', 'system'] as const).map((m) => (
                <Button key={m} type={mode === m ? 'primary' : 'default'} onClick={() => setMode(m)}>
                  {m === 'light' ? 'Light' : m === 'dark' ? 'Dark' : 'System'}
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
            CCR reversible compression. Reduces upstream token cost by compressing JSON structure and extracting log templates.
          </Text>
        </Card>

        <Card title={t('settings.cache')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="cacheEnabled" label={t('common.enabled')} valuePropName="checked">
              <Switch />
            </Form.Item>
            <Form.Item name="cacheTtlMinutes" label="TTL (minutes)">
              <InputNumber min={1} max={60} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Exact cache based on model + messages hash + parameters hash.
          </Text>
        </Card>

        <Card title={t('settings.rateLimit')} style={{ borderRadius: 18, marginBottom: 24 }}>
          <Space size={16} wrap>
            <Form.Item name="defaultRpm" label="Default RPM">
              <InputNumber min={0} style={{ width: 160 }} />
            </Form.Item>
            <Form.Item name="defaultTpm" label="Default TPM">
              <InputNumber min={0} style={{ width: 160 }} />
            </Form.Item>
          </Space>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Default rate limits applied to API keys without explicit limits.
          </Text>
        </Card>

        <Card title="Audit" style={{ borderRadius: 18 }}>
          <Form.Item name="dataRetentionDays" label={t('organization.retentionDays')}>
            <InputNumber min={7} max={180} style={{ width: 160 }} addonAfter="days" />
          </Form.Item>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Request-level audit logs older than this will be cleaned up automatically.
          </Text>
        </Card>
      </Form>
    </div>
  );
}
