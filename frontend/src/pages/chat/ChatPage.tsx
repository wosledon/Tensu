import { useState, useEffect, useRef } from 'react';
import { Input, Button, Select, Spin, Tag } from 'antd';
import { SendOutlined, RobotOutlined, UserOutlined, ClearOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { modelApi, routeModelApi } from '../../api';
import type { Model } from '../../types';

interface Message {
  role: 'user' | 'assistant';
  content: string;
}

export default function ChatPage() {
  const { t } = useTranslation();
  const [models, setModels] = useState<Model[]>([]);
  const [routeModels, setRouteModels] = useState<{ name: string; mode: string }[]>([]);
  const [selectedModel, setSelectedModel] = useState<string | undefined>();
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<Message[]>([
    { role: 'assistant', content: t('chat.welcome', 'Hello! Select a model and start testing.') },
  ]);
  const [sending, setSending] = useState(false);
  const [streamingContent, setStreamingContent] = useState('');
  const listRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const accumulatedRef = useRef('');

  useEffect(() => {
    Promise.all([
      modelApi.allEnabled(),
      routeModelApi.list({ page: 1, pageSize: 100 }),
    ]).then(([modelRes, routeRes]) => {
      setModels(modelRes);
      const rms = (routeRes.items ?? []).filter((r: any) => r.isEnabled).map((r: any) => ({ name: r.name, mode: r.mode }));
      setRouteModels(rms);
      const first = rms.length > 0 ? rms[0].name
        : modelRes.length > 0 ? `${modelRes[0].provider?.name}-${modelRes[0].name}` : undefined;
      setSelectedModel(first);
    }).catch(() => {});
  }, []);

  const modelOptions = [
    ...(routeModels.length > 0 ? [{
      label: t('nav.routeModels', 'Route Models'), options: routeModels.map((r) => ({
        value: r.name,
        label: `${r.name}  ${r.mode === 'Shadow' ? '◷' : '⇄'}`,
      })),
    }] : []),
    ...(models.length > 0 ? [{
      label: t('model.title', 'Models'), options: models.map((m) => ({
        value: `${m.provider?.name}-${m.name}`,
        label: `${m.provider?.name}-${m.name}`,
      })),
    }] : []),
  ];

  const isRouteModel = routeModels.some((r) => r.name === selectedModel);

  const sendMessage = async () => {
    const text = input.trim();
    if (!text || !selectedModel || sending) return;
    setInput('');
    setMessages((prev) => [...prev, { role: 'user', content: text }]);
    setSending(true);
    setStreamingContent('');
    accumulatedRef.current = '';

    const controller = new AbortController();
    abortRef.current = controller;

    const token = localStorage.getItem('token');
    const baseUrl = import.meta.env.DEV ? 'http://localhost:5000/api/admin' : '/api/admin';
    try {
      const res = await fetch(`${baseUrl}/chat/completions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        body: JSON.stringify({ model: selectedModel, messages: [text] }),
        signal: controller.signal,
      });

      if (!res.ok) {
        const err = await res.json();
        setMessages((prev) => [...prev, { role: 'assistant', content: `⛔ ${err.error || 'Request failed'}` }]);
        setSending(false);
        return;
      }

      // 用 text() 读取完整响应再解析 SSE，避免 ReadableStream 在浏览器中被缓冲提前关闭
      const rawText = await res.text();
      const events = rawText.split('\n').filter((l: string) => l.startsWith('data:'));
      let fullContent = '';
      for (const line of events) {
        const data = line.slice(5).trim();
        if (!data || data === '[DONE]') continue;
        try {
          const parsed = JSON.parse(data);
          const d = parsed.choices?.[0]?.delta?.content || parsed.choices?.[0]?.text || parsed.content?.[0]?.text || '';
          if (d) fullContent += d;
        } catch {}
      }
      if (!fullContent) {
        try {
          const f = JSON.parse(rawText);
          fullContent = f.choices?.[0]?.message?.content || f.choices?.[0]?.text || f.content?.[0]?.text || rawText;
        } catch { fullContent = rawText; }
      }
      setMessages((prev) => [...prev, { role: 'assistant', content: fullContent }]);
    } catch (err: any) {
      if (err.name !== 'AbortError') {
        setMessages((prev) => [...prev, { role: 'assistant', content: '⛔ Request failed' }]);
      }
    } finally {
      setSending(false);
      setStreamingContent('');
      accumulatedRef.current = '';
      abortRef.current = null;
    }
  };

  const clearChat = () => {
    if (abortRef.current) abortRef.current.abort();
    setMessages([{ role: 'assistant', content: t('chat.welcome', 'Hello! Select a model and start testing.') }]);
    setStreamingContent('');
    accumulatedRef.current = '';
    setSending(false);
  };

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, streamingContent]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 56px - 24px)' }}>

      <div style={{
        flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden',
        borderRadius: 12, border: '1px solid var(--ant-color-border)',
        background: '#fff',
      }}>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '10px 14px',
          borderBottom: '1px solid var(--ant-color-border)',
        }}>
          <span style={{ fontWeight: 600, fontSize: 14, whiteSpace: 'nowrap' }}>{t('chat.title', 'Chat')}</span>
          <Select
            value={selectedModel}
            onChange={(v) => { clearChat(); setSelectedModel(v); }}
            options={modelOptions}
            style={{ width: 280 }}
            placeholder={t('model.name')}
            showSearch optionFilterProp="label"
            size="small"
          />
          {selectedModel && (
            <Tag color={isRouteModel ? 'purple' : 'blue'} style={{ borderRadius: 4, margin: 0, lineHeight: '18px' }}>
              {isRouteModel ? (routeModels.find(r => r.name === selectedModel)?.mode === 'Shadow' ? 'Shadow' : 'Route') : 'Model'}
            </Tag>
          )}
          <div style={{ flex: 1 }} />
          <Button icon={<ClearOutlined />} onClick={clearChat} size="small" type="text" />
          {sending && <Spin size="small" />}
        </div>

        <div ref={listRef} style={{
          flex: 1, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 10,
        }}>
          {messages.map((msg, i) => (
            <div key={i} style={{
              display: 'flex', gap: 8, alignItems: 'flex-start',
              flexDirection: msg.role === 'user' ? 'row-reverse' : 'row',
            }}>
              <div style={{
                width: 26, height: 26, borderRadius: '50%', flexShrink: 0,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                background: msg.role === 'user' ? 'var(--ant-color-primary)' : 'var(--ant-color-fill-secondary)',
              }}>
                {msg.role === 'user'
                  ? <UserOutlined style={{ color: '#fff', fontSize: 12 }} />
                  : <RobotOutlined style={{ color: 'var(--ant-color-primary)', fontSize: 12 }} />
                }
              </div>
              <div style={{
                maxWidth: '75%', padding: '8px 14px',
                borderRadius: msg.role === 'user' ? '14px 14px 4px 14px' : '14px 14px 14px 4px',
                background: msg.role === 'user' ? 'var(--ant-color-primary)' : 'var(--ant-color-fill-secondary)',
                color: msg.role === 'user' ? '#fff' : 'var(--ant-color-text)',
                fontSize: 13, lineHeight: 1.6, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
              }}>
                {msg.content}
              </div>
            </div>
          ))}
          {streamingContent && (
            <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <div style={{
                width: 26, height: 26, borderRadius: '50%', flexShrink: 0,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                background: 'var(--ant-color-fill-secondary)',
              }}>
                <RobotOutlined style={{ color: 'var(--ant-color-primary)', fontSize: 12 }} />
              </div>
              <div style={{
                maxWidth: '75%', padding: '8px 14px', borderRadius: '14px 14px 14px 4px',
                background: 'var(--ant-color-fill-secondary)', color: 'var(--ant-color-text)',
                fontSize: 13, lineHeight: 1.6, whiteSpace: 'pre-wrap', wordBreak: 'break-word',
              }}>
                {streamingContent}<span style={{ opacity: 0.4 }}>▊</span>
              </div>
            </div>
          )}
        </div>

        <div style={{
          display: 'flex', gap: 8, padding: '10px 14px',
          borderTop: '1px solid var(--ant-color-border)',
        }}>
          <Input.TextArea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onPressEnter={(e) => { if (!e.shiftKey) { e.preventDefault(); sendMessage(); } }}
            placeholder={t('chat.inputPlaceholder', 'Type a message...')}
            rows={3}
            style={{ borderRadius: 8, fontSize: 13, resize: 'vertical' }}
            disabled={sending}
          />
          <Button type="primary" icon={<SendOutlined />} onClick={sendMessage}
            loading={sending} style={{ borderRadius: 8, width: 64, height: 64, flexShrink: 0 }} />
        </div>
      </div>
    </div>
  );
}
