import { useState, useEffect, useMemo } from 'react';
import { Card, Typography, Select, Button, Space, Tag, Alert, Tabs, Table, App, Tooltip, Input } from 'antd';
import { CopyOutlined, CodeOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiKeyApi, modelApi } from '../../api';
import { PageHeader } from '../../components';
import type { ApiKey, Model } from '../../types';

const { Text } = Typography;

export default function ApiDocsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const [keys, setKeys] = useState<ApiKey[]>([]);
  const [models, setModels] = useState<Model[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedKeyId, setSelectedKeyId] = useState<number | null>(null);
  const [selectedModelId, setSelectedModelId] = useState<number | null>(null);
  const [pastedKey, setPastedKey] = useState('');

  const [baseUrl, setBaseUrl] = useState(`${window.location.origin}/v1`);
  const selectedKey = useMemo(() => keys.find((k) => k.id === selectedKeyId), [keys, selectedKeyId]);
  const selectedModel = useMemo(() => models.find((m) => m.id === selectedModelId), [models, selectedModelId]);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      try {
        const [keyRes, modelRes] = await Promise.all([
          apiKeyApi.list({ page: 1, pageSize: 100 }),
          modelApi.allEnabled(),
        ]);
        const keyItems = keyRes.items ?? [];
        setKeys(keyItems);
        setModels(modelRes);
        if (keyItems.length > 0) setSelectedKeyId(keyItems[0].id);
        if (modelRes.length > 0) setSelectedModelId(modelRes[0].id);
      } catch {
        message.error(t('common.error'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [message, t]);

  const modelId = selectedModel ? `${selectedModel.provider?.name}-${selectedModel.name}` : 'OpenAI-gpt-4o';
  const apiKeyValue = pastedKey.trim() || selectedKey?.keyPrefix || 'YOUR_API_KEY';

  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      message.success(t('common.copied'));
    } catch {
      message.error(t('common.error'));
    }
  };

  const openaiPython = `from openai import OpenAI

client = OpenAI(
    base_url="${baseUrl}",
    api_key="${apiKeyValue}",
)

response = client.chat.completions.create(
    model="${modelId}",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "Hello!"},
    ],
)
print(response.choices[0].message.content)`;

  const openaiCurl = `curl ${baseUrl}/chat/completions \\
  -H "Authorization: Bearer ${apiKeyValue}" \\
  -H "Content-Type: application/json" \\
  -d '{
    "model": "${modelId}",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ]
  }'`;

  const anthropicPython = `from anthropic import Anthropic

client = Anthropic(
    base_url="${baseUrl}",
    api_key="${apiKeyValue}",
)

message = client.messages.create(
    model="${modelId}",
    max_tokens=1024,
    messages=[
        {"role": "user", "content": "Hello!"},
    ],
)
print(message.content[0].text)`;

  const anthropicCurl = `curl ${baseUrl}/messages \\
  -H "Authorization: Bearer ${apiKeyValue}" \\
  -H "Content-Type: application/json" \\
  -H "anthropic-version: 2023-06-01" \\
  -d '{
    "model": "${modelId}",
    "max_tokens": 1024,
    "messages": [
      {"role": "user", "content": "Hello!"}
    ]
  }'`;

  const columns = [
    { title: t('apiDocs.model'), dataIndex: 'id', key: 'id' },
    { title: t('model.provider'), dataIndex: 'provider', key: 'provider' },
  ];

  const modelTableData = models.map((m) => ({
    id: `${m.provider?.name}-${m.name}`,
    provider: m.provider?.name,
  }));

  const codeBlock = (code: string, label: string) => (
    <Card
      size="small"
      title={label}
      extra={
        <Tooltip title={t('common.copy')}>
          <Button type="text" icon={<CopyOutlined />} onClick={() => copy(code)} />
        </Tooltip>
      }
      style={{ marginTop: 16, background: 'var(--ant-color-bg-container-disabled)' }}
    >
      <pre style={{ margin: 0, overflow: 'auto', fontFamily: 'monospace', fontSize: 13 }}>
        <code>{code}</code>
      </pre>
    </Card>
  );

  return (
    <div>
      <PageHeader title={t('apiDocs.title')} icon={<CodeOutlined />} />

      <Alert
        type="info"
        showIcon
        message={t('apiDocs.subtitle')}
        description={t('apiDocs.note', { provider: '{provider}', model: '{model}' })}
        style={{ marginBottom: 16, borderRadius: 12 }}
      />

      <Card loading={loading} style={{ borderRadius: 18, marginBottom: 16 }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div>
            <Text type="secondary">{t('apiDocs.baseUrl')}</Text>
            <div>
              <Input
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                style={{ width: 320, fontFamily: 'monospace' }}
                suffix={
                  <Tooltip title={t('common.copy')}>
                    <Button type="text" icon={<CopyOutlined />} onClick={() => copy(baseUrl)} />
                  </Tooltip>
                }
              />
            </div>
          </div>

          <Space wrap>
            <div>
              <Text type="secondary">{t('apiDocs.selectKey')}</Text>
              <div>
                <Select
                  style={{ minWidth: 240 }}
                  value={selectedKeyId}
                  onChange={setSelectedKeyId}
                  options={keys.map((k) => ({ value: k.id, label: `${k.name} (${k.keyPrefix || ''}...)` }))}
                  placeholder={t('apiDocs.selectKey')}
                />
              </div>
            </div>

            <div>
              <Text type="secondary">{t('apiKey.key')}</Text>
              <div>
                <Input.Password
                  value={pastedKey}
                  onChange={(e) => setPastedKey(e.target.value)}
                  placeholder={t('apiKey.keyValuePlaceholder')}
                  style={{ minWidth: 260 }}
                />
              </div>
            </div>

            <div>
              <Text type="secondary">{t('apiDocs.model')}</Text>
              <div>
                <Select
                  style={{ minWidth: 240 }}
                  value={selectedModelId}
                  onChange={setSelectedModelId}
                  options={models.map((m) => ({ value: m.id, label: `${m.provider?.name}-${m.name}` }))}
                  placeholder={t('apiDocs.model')}
                />
              </div>
            </div>
          </Space>

          {!selectedKey && !loading && (
            <Alert type="warning" showIcon message={t('apiDocs.noKey')} />
          )}
        </Space>
      </Card>

      <Card style={{ borderRadius: 18 }}>
        <Tabs
          items={[
            {
              key: 'openai',
              label: (
                <Space>
                  <Tag color="blue">OpenAI</Tag>
                  {t('apiDocs.openaiExample')}
                </Space>
              ),
              children: (
                <>
                  {codeBlock(openaiPython, t('apiDocs.pythonExample'))}
                  {codeBlock(openaiCurl, t('apiDocs.curlExample'))}
                </>
              ),
            },
            {
              key: 'anthropic',
              label: (
                <Space>
                  <Tag color="purple">Anthropic</Tag>
                  {t('apiDocs.anthropicExample')}
                </Space>
              ),
              children: (
                <>
                  {codeBlock(anthropicPython, t('apiDocs.pythonExample'))}
                  {codeBlock(anthropicCurl, t('apiDocs.curlExample'))}
                </>
              ),
            },
          ]}
        />
      </Card>

      <Card title={t('apiDocs.availableModels')} style={{ borderRadius: 18, marginTop: 16 }}>
        <Table
          columns={columns}
          dataSource={modelTableData}
          rowKey="id"
          pagination={false}
          size="small"
          scroll={{ x: 500 }}
        />
      </Card>
    </div>
  );
}
