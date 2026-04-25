import { PlusOutlined } from '@ant-design/icons';
import { Button, Empty, Space, Typography } from 'antd';
import { useState } from 'react';
import { CreateFolderModal } from '../features/folders/CreateFolderModal';
import { FolderTree } from '../features/folders/FolderTree';
import { useFoldersQuery } from '../features/folders/useFoldersQuery';
import { useCurrentUser } from '../auth/AuthContext';

export function MyFoldersPage() {
  const [createOpen, setCreateOpen] = useState(false);
  const user = useCurrentUser();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const ownedFolders = folders.filter(f => f.ownerId === user.id);

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Space>
        <Typography.Title level={4} style={{ margin: 0 }}>Мои папки</Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
          Создать
        </Button>
      </Space>
      {!isLoading && ownedFolders.length === 0
        ? (
          <Empty description="Папок пока нет">
            <Button type="primary" onClick={() => setCreateOpen(true)}>Создать папку</Button>
          </Empty>
        )
        : <FolderTree />
      }
      <CreateFolderModal open={createOpen} parentFolderId={null} onClose={() => setCreateOpen(false)} />
    </Space>
  );
}
