import { Breadcrumb, Button, Result, Tabs, Typography } from 'antd';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useCurrentUser } from '../auth/AuthContext';
import { PermissionList } from '../features/permissions/PermissionList';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { ShareList } from '../features/shares/ShareList';
import type { FolderResponse } from '../types/folder';

function buildAncestors(folders: FolderResponse[], folderId: string): FolderResponse[] {
  const map = new Map(folders.map(f => [f.id, f]));
  const chain: FolderResponse[] = [];
  let current = map.get(folderId);
  while (current) {
    chain.unshift(current);
    current = current.parentFolderId ? map.get(current.parentFolderId) : undefined;
  }
  return chain;
}

export function FolderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const user = useCurrentUser();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const navigate = useNavigate();

  if (!id) return null;

  const folder = folders.find(f => f.id === id);

  if (!isLoading && !folder) {
    return (
      <Result
        status="404"
        title="Папка не найдена"
        subTitle="Папка не существует или у вас нет доступа."
        extra={<Button type="primary" onClick={() => navigate('/folders')}>К моим папкам</Button>}
      />
    );
  }

  const isOwner = folder?.ownerId === user.id;
  const ancestors = folder ? buildAncestors(folders, folder.id) : [];
  const breadcrumbItems = [
    { title: <Link to="/folders">Мои папки</Link> },
    ...ancestors.slice(0, -1).map(f => ({ title: <Link to={`/folders/${f.id}`}>{f.name}</Link> })),
    { title: folder?.name },
  ];

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
      <Breadcrumb items={breadcrumbItems} style={{ marginBottom: 8 }} />
      <Typography.Title level={4} style={{ marginTop: 0 }}>{folder?.name ?? id}</Typography.Title>
      <Tabs items={tabs} />
    </div>
  );
}
