import { useState } from 'react';
import { Table, Button, Space, Tag, Card, App, Modal, Form, Input, Descriptions } from 'antd';
import { EyeOutlined, CheckOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useCrudList } from '../../hooks/useCrudList';
import { useTypedConfirmAction } from '../../hooks/useTypedConfirmAction';
import { dataDeletionApi } from '../../api';
import type { DataDeletionRequest, PagedRequest } from '../../types';
import { PageHeader } from '../../components';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';

const STATUS_MAP: Record<string, { color: string; labelKey: string }> = {
  pending: { color: 'orange', labelKey: 'dataDeletion.statusPending' },
  processing: { color: 'blue', labelKey: 'dataDeletion.statusProcessing' },
  completed: { color: 'green', labelKey: 'dataDeletion.statusCompleted' },
  failed: { color: 'red', labelKey: 'dataDeletion.statusFailed' },
};

export default function DataDeletionsPage() {
  const { t } = useTranslation();
  const { message } = App.useApp();
  const { confirmAction } = useTypedConfirmAction();

  const [detailOpen, setDetailOpen] = useState(false);
  const [selectedRecord, setSelectedRecord] = useState<DataDeletionRequest | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [form] = Form.useForm();

  const fetchFn = (params: PagedRequest) => dataDeletionApi.list(params);
  const { data, total, loading, params, setPage, fetchData } = useCrudList<DataDeletionRequest, PagedRequest>({ fetchFn });

  const handleCreate = async () => {
    try {
      const values = await form.validateFields();
      await dataDeletionApi.create(values);
      message.success(t('common.success'));
      setCreateOpen(false);
      form.resetFields();
      fetchData();
    } catch {}
  };

  const handleProcess = (record: DataDeletionRequest) => {
    confirmAction({
      title: t('dataDeletion.processTitle'),
      expectedText: t('dataDeletion.processConfirmText'),
      action: () => dataDeletionApi.process(record.id),
      onSuccess: fetchData,
    });
  };

  const handleViewDetail = async (record: DataDeletionRequest) => {
    try {
      const detail = await dataDeletionApi.get(record.id);
      setSelectedRecord(detail);
      setDetailOpen(true);
    } catch {
      message.error(t('common.error'));
    }
  };

  const columns: ColumnsType<DataDeletionRequest> = [
    { title: t('dataDeletion.id'), dataIndex: 'id', key: 'id', width: 80 },
    { title: t('dataDeletion.reason'), dataIndex: 'reason', key: 'reason', ellipsis: true },
    {
      title: t('common.status'), dataIndex: 'status', key: 'status', width: 120,
      render: (status: string) => {
        const mapped = STATUS_MAP[status] || { color: 'default', labelKey: status };
        return <Tag color={mapped.color}>{t(mapped.labelKey)}</Tag>;
      },
    },
    { title: t('dataDeletion.deletedRequestLogs'), dataIndex: 'deletedRequestLogs', key: 'deletedRequestLogs', width: 140 },
    { title: t('dataDeletion.deletedArchivedLogs'), dataIndex: 'deletedArchivedLogs', key: 'deletedArchivedLogs', width: 140 },
    {
      title: t('common.actions'), key: 'actions', width: 160, fixed: 'right' as const,
      render: (_, record) => (
        <Space>
          <Button type="text" icon={<EyeOutlined />} onClick={() => handleViewDetail(record)} title={t('common.show')} />
          {record.status === 'pending' && (
            <Button type="text" icon={<CheckOutlined />} onClick={() => handleProcess(record)} title={t('dataDeletion.process')} />
          )}
        </Space>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title={t('dataDeletion.title')}
        onCreate={() => setCreateOpen(true)}
        createLabel={t('dataDeletion.create')}
      />
      <Card style={{ borderRadius: 18 }}>
        <Table
          columns={columns}
          dataSource={data}
          rowKey="id"
          loading={loading}
          pagination={{ current: params.page, pageSize: params.pageSize, total, showSizeChanger: true, onChange: setPage }}
          scroll={{ x: 800 }}
        />
      </Card>

      <Modal
        title={t('dataDeletion.create')}
        open={createOpen}
        onOk={handleCreate}
        onCancel={() => { setCreateOpen(false); form.resetFields(); }}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="reason" label={t('dataDeletion.reason')} rules={[{ required: true }]}>
            <Input.TextArea rows={3} placeholder={t('dataDeletion.reasonPlaceholder')} />
          </Form.Item>
          <Form.Item name="organizationId" label={t('dataDeletion.organizationId')}>
            <Input type="number" />
          </Form.Item>
          <Form.Item name="userId" label={t('dataDeletion.userId')}>
            <Input type="number" />
          </Form.Item>
          <Form.Item name="apiKeyId" label={t('dataDeletion.apiKeyId')}>
            <Input />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={t('dataDeletion.detail')}
        open={detailOpen}
        onCancel={() => setDetailOpen(false)}
        footer={<Button onClick={() => setDetailOpen(false)}>{t('common.close')}</Button>}
        width={640}
      >
        {selectedRecord && (
          <Descriptions bordered column={1} size="small">
            <Descriptions.Item label={t('dataDeletion.id')}>{selectedRecord.id}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.status')}>
              <Tag color={STATUS_MAP[selectedRecord.status]?.color || 'default'}>
                {t(STATUS_MAP[selectedRecord.status]?.labelKey || selectedRecord.status)}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.reason')}>{selectedRecord.reason}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.organizationId')}>{selectedRecord.organizationId ?? '-'}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.userId')}>{selectedRecord.userId ?? '-'}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.apiKeyId')}>{selectedRecord.apiKeyId || '-'}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.requestId')}>{selectedRecord.requestId || '-'}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.deletedRequestLogs')}>{selectedRecord.deletedRequestLogs}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.deletedArchivedLogs')}>{selectedRecord.deletedArchivedLogs}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.errorMessage')}>{selectedRecord.errorMessage || '-'}</Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.createdAt')}>
              {selectedRecord.createdAt ? dayjs(selectedRecord.createdAt).format('YYYY-MM-DD HH:mm:ss') : '-'}
            </Descriptions.Item>
            <Descriptions.Item label={t('dataDeletion.completedAt')}>
              {selectedRecord.completedAt ? dayjs(selectedRecord.completedAt).format('YYYY-MM-DD HH:mm:ss') : '-'}
            </Descriptions.Item>
          </Descriptions>
        )}
      </Modal>
    </div>
  );
}
