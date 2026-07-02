import { Button, Space, Typography, Input } from 'antd';
import { PlusOutlined, SearchOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';

const { Title } = Typography;

interface PageHeaderProps {
  title: string;
  onCreate?: () => void;
  createLabel?: string;
  onSearch?: (keyword: string) => void;
  searchPlaceholder?: string;
  extra?: React.ReactNode;
}

export default function PageHeader({ title, onCreate, createLabel, onSearch, searchPlaceholder, extra }: PageHeaderProps) {
  const { t } = useTranslation();

  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
      <Title level={4} style={{ margin: 0 }}>{title}</Title>
      <Space>
        {onSearch && (
          <Input.Search
            placeholder={searchPlaceholder || t('common.search')}
            allowClear
            onSearch={(v) => onSearch(v)}
            style={{ width: 240 }}
            prefix={<SearchOutlined />}
          />
        )}
        {extra}
        {onCreate && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onCreate}>
            {createLabel || t('common.create')}
          </Button>
        )}
      </Space>
    </div>
  );
}
