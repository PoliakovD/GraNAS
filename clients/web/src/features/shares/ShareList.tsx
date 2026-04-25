import { PlusOutlined } from '@ant-design/icons';
import { Button, Space, Table, Tag } from 'antd';
import type { ColumnType } from 'antd/es/table';
import { useState } from 'react';
import type { ShareLinkResponse } from '../../types/share';
import { CreateShareModal } from './CreateShareModal';
import { useRevokeShare, useSharesQuery } from './useShareMutations';

interface Props { folderId: string }

export function ShareList({ folderId }: Props) {
  const { data: shares = [], isLoading } = useSharesQuery(folderId);
  const revoke = useRevokeShare(folderId);
  const [createOpen, setCreateOpen] = useState(false);

  const columns: ColumnType<ShareLinkResponse>[] = [
    { title: 'ID', dataIndex: 'id', key: 'id', ellipsis: true, width: 80 },
    { title: 'Путь', dataIndex: 'path', key: 'path', render: (v: string | null) => v ?? '—' },
    {
      title: 'Истекает',
      dataIndex: 'expiresAt',
      key: 'expiresAt',
      render: (v: string | null) => v ? new Date(v).toLocaleString('ru') : '—',
    },
    {
      title: 'Статус',
      dataIndex: 'revoked',
      key: 'revoked',
      render: (v: boolean) => <Tag color={v ? 'red' : 'green'}>{v ? 'Отозвана' : 'Активна'}</Tag>,
    },
    {
      title: '',
      key: 'action',
      render: (_: unknown, row: ShareLinkResponse) =>
        !row.revoked && (
          <Button danger size="small" onClick={() => revoke.mutate(row.id)}>
            Отозвать
          </Button>
        ),
    },
  ];

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Button icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
        Создать ссылку
      </Button>
      <Table
        dataSource={shares}
        columns={columns}
        rowKey="id"
        size="small"
        loading={isLoading}
        pagination={false}
      />
      <CreateShareModal folderId={folderId} open={createOpen} onClose={() => setCreateOpen(false)} />
    </Space>
  );
}
