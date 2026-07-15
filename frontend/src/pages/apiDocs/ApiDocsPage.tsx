import { useState } from 'react';
import { Card, Typography, Button, Space, Tag, Alert, Tabs, App, Tooltip, Input } from 'antd';
import { CopyOutlined, CodeOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { PageHeader } from '../../components';

const { Text } = Typography;

export default function ApiDocsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();

  const [baseUrl, setBaseUrl] = useState(`${window.location.origin}/v1`);

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
    api_key="YOUR_API_KEY",
)

response = client.chat.completions.create(
    model="openai-gpt-4o",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "Hello!"},
    ],
)
print(response.choices[0].message.content)`;

  const openaiCurl = `curl ${baseUrl}/chat/completions \\
  -H "Authorization: Bearer YOUR_API_KEY" \\
  -H "Authorization: Bearer YOUR_API_KEY" \

  -H "Content-Type: application/json" \\
  -d '{
    "model": "openai-gpt-4o",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ]
  }'`;

  const anthropicPython = `from anthropic import Anthropic

client = Anthropic(
    base_url="${baseUrl}",
    api_key="YOUR_API_KEY",
)

message = client.messages.create(
    model="openai-gpt-4o",
    max_tokens=1024,
    messages=[
        {"role": "user", "content": "Hello!"},
    ],
)
print(message.content[0].text)`;

  const anthropicCurl = `curl ${baseUrl}/messages \\
  -H "Authorization: Bearer YOUR_API_KEY" \\
  -H "Content-Type: application/json" \\
  -H "anthropic-version: 2023-06-01" \\
  -d '{
    "model": "openai-gpt-4o",
    "max_tokens": 1024,
    "messages": [
      {"role": "user", "content": "Hello!"}
    ]
  }'`;

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

      <Card style={{ borderRadius: 18, marginBottom: 16 }}>
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
    </div>
  );
}
