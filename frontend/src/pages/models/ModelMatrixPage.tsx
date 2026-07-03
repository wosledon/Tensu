import { useState, useEffect } from 'react';
import { Card, Table, Tag, Space, Select, Button, Row, Col } from 'antd';
import { useTranslation } from 'react-i18next';
import ReactECharts from 'echarts-for-react';
import { modelCapabilityApi, providerApi } from '../../api';
import { PageHeader } from '../../components';
import type { Provider, ModelCapabilityMatrix } from '../../types';

export default function ModelMatrixPage() {
  const { t } = useTranslation();
  const [matrix, setMatrix] = useState<ModelCapabilityMatrix | null>(null);
  const [providers, setProviders] = useState<Provider[]>([]);
  const [loading, setLoading] = useState(true);
  const [providerFilter, setProviderFilter] = useState<number | undefined>();
  const [selectedModel, setSelectedModel] = useState<number | undefined>();

  const fetchMatrix = async (providerId?: number) => {
    setLoading(true);
    try {
      const data = await modelCapabilityApi.matrix(providerId);
      setMatrix(data);
      if (data.models.length > 0) setSelectedModel(data.models[0].modelId);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    providerApi.list({ page: 1, pageSize: 100 }).then((p) => setProviders(p.items));
    fetchMatrix();
  }, []);

  useEffect(() => {
    fetchMatrix(providerFilter);
  }, [providerFilter]);

  const dimensions = matrix?.dimensions ?? [];

  const radarOption = {
    tooltip: { trigger: 'item' },
    legend: { data: matrix?.models.map((m) => `${m.providerName}-${m.modelName}`) ?? [] },
    radar: {
      indicator: dimensions.map((d) => ({ name: d, max: 100 })),
      radius: '65%',
    },
    series: [
      {
        type: 'radar',
        data: matrix?.models.map((m) => ({
          value: dimensions.map((d) => m.scores[d] ?? 0),
          name: `${m.providerName}-${m.modelName}`,
        })) ?? [],
      },
    ],
  };

  const selected = matrix?.models.find((m) => m.modelId === selectedModel);

  const columns = [
    {
      title: t('model.name'),
      key: 'name',
      sorter: (a: ModelCapabilityMatrix['models'][0], b: ModelCapabilityMatrix['models'][0]) => a.modelName.localeCompare(b.modelName),
      render: (_: any, r: ModelCapabilityMatrix['models'][0]) => (
        <Space>
          <span style={{ fontFamily: 'monospace', fontWeight: 500 }}>{r.providerName}-{r.modelName}</span>
        </Space>
      ),
    },
    ...dimensions.map((d) => ({
      title: d,
      key: d,
      width: 100,
      align: 'center' as const,
      render: (_: any, r: ModelCapabilityMatrix['models'][0]) => {
        const score = r.scores[d];
        return score !== undefined ? (
          <span style={{ fontFamily: 'monospace', fontWeight: 500 }}>{score.toFixed(1)}</span>
        ) : (
          <span style={{ color: 'var(--ant-color-text-secondary)' }}>—</span>
        );
      },
    })),
    {
      title: t('capability.overallScore'),
      key: 'overall',
      width: 120,
      align: 'right' as const,
      sorter: (a: ModelCapabilityMatrix['models'][0], b: ModelCapabilityMatrix['models'][0]) => a.overallScore - b.overallScore,
      render: (_: any, r: ModelCapabilityMatrix['models'][0]) => (
        <Tag color={r.overallScore >= 80 ? 'green' : r.overallScore >= 60 ? 'orange' : 'red'}>
          <span style={{ fontFamily: 'monospace' }}>{r.overallScore.toFixed(1)}</span>
        </Tag>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title={t('model.title') + ' - ' + t('capability.matrixTitle')} />

      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col xs={24} lg={16}>
          <Card style={{ borderRadius: 18 }} loading={loading}>
            <ReactECharts option={radarOption} style={{ height: 400 }} />
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card style={{ borderRadius: 18 }} title={t('capability.modelDetail')} loading={loading}>
            {selected ? (
              <div>
                <div style={{ fontSize: 16, fontWeight: 600, marginBottom: 16 }}>
                  {selected.providerName}-{selected.modelName}
                </div>
                <div style={{ marginBottom: 8 }}>
                  <span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('capability.overallScore')}:</span>{' '}
                  <span style={{ fontFamily: 'monospace', fontWeight: 600, fontSize: 18 }}>{selected.overallScore.toFixed(1)}</span>
                </div>
                {dimensions.map((d) => (
                  <div key={d} style={{ marginBottom: 8, display: 'flex', justifyContent: 'space-between' }}>
                    <span>{d}</span>
                    <span style={{ fontFamily: 'monospace' }}>
                      {selected.scores[d] !== undefined ? selected.scores[d].toFixed(1) : '—'}
                    </span>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ color: 'var(--ant-color-text-secondary)' }}>{t('common.noData')}</div>
            )}
          </Card>
        </Col>
      </Row>

      <Card style={{ borderRadius: 18, marginBottom: 24 }}>
        <Space wrap>
          <Select
            allowClear
            placeholder={t('model.provider')}
            style={{ width: 200 }}
            options={providers.map((p) => ({ value: p.id, label: `${p.name} (${p.protocol})` }))}
            onChange={(v) => setProviderFilter(v)}
          />
          {providerFilter && (
            <Button onClick={() => setProviderFilter(undefined)}>{t('common.reset')}</Button>
          )}
          <span style={{ color: 'var(--ant-color-text-secondary)', fontSize: 13 }}>
            {matrix?.models.length ?? 0} models
          </span>
        </Space>
      </Card>

      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns}
          dataSource={matrix?.models ?? []}
          rowKey="modelId"
          loading={loading}
          pagination={false}
          scroll={{ x: 1000 }}
          size="middle"
          rowSelection={{
            type: 'radio',
            selectedRowKeys: selectedModel ? [selectedModel] : [],
            onChange: (keys) => setSelectedModel(keys[0] as number),
          }}
        />
      </Card>
    </div>
  );
}
