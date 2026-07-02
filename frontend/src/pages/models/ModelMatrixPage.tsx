import { useState, useEffect } from 'react';
import { Card, Table, Tag, Space, Select, Button } from 'antd';
import { useTranslation } from 'react-i18next';
import { modelApi, providerApi } from '../../api';
import { PageHeader } from '../../components';
import type { Model, Provider } from '../../types';

export default function ModelMatrixPage() {
  const { t } = useTranslation();
  const [models, setModels] = useState<Model[]>([]);
  const [providers, setProviders] = useState<Provider[]>([]);
  const [loading, setLoading] = useState(true);
  const [providerFilter, setProviderFilter] = useState<number | undefined>();
  const [capabilityFilter, setCapabilityFilter] = useState<string[]>([]);

  useEffect(() => {
    Promise.all([
      modelApi.allEnabled(),
      providerApi.list({ page: 1, pageSize: 100 }),
    ]).then(([m, p]) => {
      setModels(m);
      setProviders(p.items);
    }).finally(() => setLoading(false));
  }, []);

  const filtered = models.filter((m) => {
    if (providerFilter && m.providerId !== providerFilter) return false;
    if (capabilityFilter.includes('vision') && !m.supportsVision) return false;
    if (capabilityFilter.includes('reasoning') && !m.supportsReasoning) return false;
    if (capabilityFilter.includes('toolUse') && !m.supportsToolUse) return false;
    if (capabilityFilter.includes('thinking') && !m.supportsThinking) return false;
    return true;
  });

  // Build radar chart data: group by capability
  const capabilities = [
    { key: 'vision', label: t('model.vision'), icon: '👁' },
    { key: 'reasoning', label: t('model.reasoning'), icon: '🧠' },
    { key: 'toolUse', label: t('model.toolUse'), icon: '🔧' },
    { key: 'thinking', label: t('model.thinking'), icon: '💭' },
  ];

  const columns = [
    {
      title: t('model.name'), key: 'name', sorter: (a: Model, b: Model) => a.name.localeCompare(b.name),
      render: (_: any, r: Model) => (
        <Space>
          <span style={{ fontFamily: 'monospace', fontWeight: 500 }}>{r.provider?.name}-{r.name}</span>
          {r.displayName && <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 12 }}>({r.displayName})</span>}
        </Space>
      ),
    },
    { title: t('model.provider'), key: 'provider', render: (_: any, r: Model) => <Tag>{r.provider?.name}</Tag> },
    {
      title: t('model.vision'), key: 'vision', width: 80, align: 'center' as const,
      render: (_: any, r: Model) => r.supportsVision ? <Tag color="blue">✓</Tag> : <Tag>—</Tag>,
    },
    {
      title: t('model.reasoning'), key: 'reasoning', width: 80, align: 'center' as const,
      render: (_: any, r: Model) => r.supportsReasoning ? <Tag color="purple">✓</Tag> : <Tag>—</Tag>,
    },
    {
      title: t('model.toolUse'), key: 'toolUse', width: 80, align: 'center' as const,
      render: (_: any, r: Model) => r.supportsToolUse ? <Tag color="green">✓</Tag> : <Tag>—</Tag>,
    },
    {
      title: t('model.thinking'), key: 'thinking', width: 80, align: 'center' as const,
      render: (_: any, r: Model) => r.supportsThinking ? <Tag color="orange">✓</Tag> : <Tag>—</Tag>,
    },
    {
      title: t('model.inputContext'), key: 'inputCtx', width: 120, align: 'right' as const,
      sorter: (a: Model, b: Model) => a.inputContextSize - b.inputContextSize,
      render: (_: any, r: Model) => <span style={{ fontFamily: 'monospace' }}>{(r.inputContextSize / 1000).toFixed(0)}K</span>,
    },
    {
      title: t('model.outputContext'), key: 'outputCtx', width: 120, align: 'right' as const,
      sorter: (a: Model, b: Model) => a.outputContextSize - b.outputContextSize,
      render: (_: any, r: Model) => <span style={{ fontFamily: 'monospace' }}>{(r.outputContextSize / 1000).toFixed(0)}K</span>,
    },
    {
      title: t('model.pricing'), key: 'pricing', width: 180,
      render: (_: any, r: Model) => {
        const p = r.pricings?.[0];
        if (!p) return <span style={{ color: 'var(--ant-color-text-secondary)' }}>—</span>;
        return (
          <span style={{ fontFamily: 'monospace', fontSize: 12 }}>
            ${p.inputPricePerMillionTokens} / ${p.outputPricePerMillionTokens}
          </span>
        );
      },
    },
  ];

  // Capability coverage summary
  const coverage = capabilities.map((cap) => {
    const key = `supports${cap.key.charAt(0).toUpperCase() + cap.key.slice(1)}` as keyof Model;
    const count = models.filter((m) => m[key]).length;
    return { ...cap, count, total: models.length, pct: models.length > 0 ? Math.round(count / models.length * 100) : 0 };
  });

  return (
    <div>
      <PageHeader title={t('model.title') + ' - ' + 'Capability Matrix'} />

      {/* Capability coverage cards */}
      <div style={{ display: 'flex', gap: 16, marginBottom: 24, flexWrap: 'wrap' }}>
        {coverage.map((cap) => (
          <Card
            key={cap.key}
            hoverable
            style={{
              flex: '1 1 200px', borderRadius: 18, cursor: 'pointer',
              border: capabilityFilter.includes(cap.key) ? '2px solid var(--ant-color-primary)' : undefined,
            }}
            onClick={() => {
              setCapabilityFilter((prev) =>
                prev.includes(cap.key) ? prev.filter((k) => k !== cap.key) : [...prev, cap.key]
              );
            }}
          >
            <div style={{ textAlign: 'center' }}>
              <div style={{ fontSize: 28, marginBottom: 8 }}>{cap.icon}</div>
              <div style={{ fontSize: 13, color: 'var(--ant-color-text-secondary)', marginBottom: 4 }}>{cap.label}</div>
              <div style={{ fontSize: 24, fontWeight: 600, fontFamily: 'monospace' }}>{cap.count}<span style={{ fontSize: 14, fontWeight: 400, color: 'var(--ant-color-text-secondary)' }}>/{cap.total}</span></div>
              <div style={{ fontSize: 12, color: 'var(--ant-color-text-secondary)' }}>{cap.pct}%</div>
            </div>
          </Card>
        ))}
      </div>

      {/* Filters */}
      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space wrap>
          <Select
            allowClear
            placeholder={t('model.provider')}
            style={{ width: 200 }}
            options={providers.map((p) => ({ value: p.id, label: `${p.name} (${p.protocol})` }))}
            onChange={(v) => setProviderFilter(v)}
          />
          <Select
            mode="multiple"
            allowClear
            placeholder="Capabilities"
            style={{ width: 300 }}
            options={capabilities.map((c) => ({ value: c.key, label: `${c.icon} ${c.label}` }))}
            onChange={(v) => setCapabilityFilter(v)}
          />
          {(providerFilter || capabilityFilter.length > 0) && (
            <Button onClick={() => { setProviderFilter(undefined); setCapabilityFilter([]); }}>{t('common.reset')}</Button>
          )}
          <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 13 }}>
            {filtered.length} / {models.length} models
          </span>
        </Space>
      </Card>

      {/* Matrix table */}
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns}
          dataSource={filtered}
          rowKey="id"
          loading={loading}
          pagination={false}
          scroll={{ x: 1000 }}
          size="middle"
        />
      </Card>
    </div>
  );
}
