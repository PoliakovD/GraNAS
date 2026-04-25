import { Button, Table, Tag } from 'antd';
import type { ColumnType } from 'antd/es/table';
import { useQueryClient } from '@tanstack/react-query';
import type { PermissionResponse } from '../../types/permission';
import { permissionsKey, useRevokePermission } from './usePermissionMutations';
import { GrantPermissionForm } from './GrantPermissionForm';

interface Props { folderId: string }

export function PermissionList({ folderId }: Props) {
  const qc = useQueryClient();
  const permissions = qc.getQueryData<PermissionResponse[]>(permissionsKey(folderId)) ?? [];
  const revoke = useRevokePermission(folderId);

  const columns: ColumnType<PermissionResponse>[] = [
    { title: 'Пользователь', dataIndex: 'userId', key: 'userId', ellipsis: true },
    {
      title: 'Уровень',
      dataIndex: 'accessLevel',
      key: 'accessLevel',
      render: (v: string) => <Tag color={v === 'Full' ? 'green' : 'blue'}>{v}</Tag>,
    },
    { title: 'Путь', dataIndex: 'path', key: 'path', render: (v: string | null) => v ?? '—' },
    {
      title: '',
      key: 'action',
      render: (_: unknown, row: PermissionResponse) => (
        <Button danger size="small" onClick={() => revoke.mutate(row.userId)}>
          Отозвать
        </Button>
      ),
    },
  ];

  return (
    <>
      <GrantPermissionForm folderId={folderId} />
      <Table
        dataSource={permissions}
        columns={columns}
        rowKey="userId"
        size="small"
        pagination={false}
        locale={{ emptyText: 'Нет выданных прав' }}
      />
    </>
  );
}
