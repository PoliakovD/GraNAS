import { Table, Tag, Typography } from 'antd';
import type { ColumnType } from 'antd/es/table';
import { useNavigate } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import type { FolderResponse } from '../types/folder';

export function SharedWithMePage() {
  const user = useCurrentUser();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const navigate = useNavigate();

  const shared = folders.filter(f => f.ownerId !== user.id);

  const columns: ColumnType<FolderResponse>[] = [
    { title: 'Папка', dataIndex: 'name', key: 'name' },
    { title: 'Владелец', dataIndex: 'ownerId', key: 'ownerId', ellipsis: true },
    {
      title: 'Уровень',
      dataIndex: 'accessLevel',
      key: 'accessLevel',
      render: (v: string) => <Tag color={v === 'Full' ? 'green' : 'blue'}>{v}</Tag>,
    },
    { title: 'Путь', dataIndex: 'path', key: 'path', render: (v: string | null) => v ?? '—' },
  ];

  return (
    <>
      <Typography.Title level={4}>Доступные папки</Typography.Title>
      <Table
        dataSource={shared}
        columns={columns}
        rowKey="id"
        loading={isLoading}
        onRow={row => ({ onClick: () => navigate(`/folders/${row.id}`) })}
        style={{ cursor: 'pointer' }}
      />
    </>
  );
}
