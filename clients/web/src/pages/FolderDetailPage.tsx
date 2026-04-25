import { Tabs, Typography } from 'antd';
import { useParams } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { PermissionList } from '../features/permissions/PermissionList';
import { ShareList } from '../features/shares/ShareList';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';

export function FolderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const user = useCurrentUser();
  const { data: folders = [] } = useFoldersQuery();
  const folder = folders.find(f => f.id === id);
  const isOwner = folder?.ownerId === user.id;

  if (!id) return null;

  const tabs = [
    {
      key: 'permissions',
      label: 'Права доступа',
      children: isOwner
        ? <PermissionList folderId={id} />
        : <Typography.Text type="secondary">Только владелец может управлять правами</Typography.Text>,
    },
    {
      key: 'shares',
      label: 'Share-ссылки',
      children: isOwner
        ? <ShareList folderId={id} />
        : <Typography.Text type="secondary">Только владелец может управлять ссылками</Typography.Text>,
    },
  ];

  return (
    <div>
      <Typography.Title level={4}>{folder?.name ?? id}</Typography.Title>
      <Tabs items={tabs} />
    </div>
  );
}
