import { useEffect, useRef, useState } from 'react';
import { Button, Space, Typography, Input, Tooltip } from 'antd';
import { PlusOutlined, SearchOutlined, ReloadOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

const { Title } = Typography;

interface PageHeaderProps {
  title: string;
  icon?: React.ReactNode;
  onCreate?: () => void;
  createLabel?: string;
  onSearch?: (keyword: string) => void;
  searchPlaceholder?: string;
  onRefresh?: () => void;
  refreshing?: boolean;
  extra?: React.ReactNode;
}

export default function PageHeader({ title, icon, onCreate, createLabel, onSearch, searchPlaceholder, onRefresh, refreshing, extra }: PageHeaderProps) {
  const { t } = useTranslation();
  const [keyword, setKeyword] = useState('');
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(undefined);

  // Debounced live search (300ms); Enter/search button triggers immediately.
  useEffect(() => {
    if (!onSearch) return;
    debounceRef.current = setTimeout(() => onSearch(keyword), 300);
    return () => clearTimeout(debounceRef.current);
  }, [keyword]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24, flexWrap: 'wrap', gap: 12 }}>
      <Title level={4} style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
        {icon && <span style={{ color: 'var(--ant-color-primary)' }}>{icon}</span>}
        {title}
      </Title>
      <Space wrap>
        {onSearch && (
          <Input.Search
            placeholder={searchPlaceholder || t('common.search')}
            allowClear
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onSearch={(v) => { clearTimeout(debounceRef.current); onSearch(v); }}
            style={{ width: 240 }}
            prefix={<SearchOutlined />}
          />
        )}
        {extra}
        {onRefresh && (
          <Tooltip title={t('common.refresh')}>
            <Button aria-label={t('common.refresh')} icon={<ReloadOutlined spin={refreshing} />} onClick={onRefresh} />
          </Tooltip>
        )}
        {onCreate && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onCreate}>
            {createLabel || t('common.create')}
          </Button>
        )}
      </Space>
    </div>
  );
}
