import { PlusOutlined } from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';
import { useState } from 'react';
import { CreateFolderModal } from '../features/folders/CreateFolderModal';
import { FolderTree } from '../features/folders/FolderTree';

export function MyFoldersPage() {
  const [createOpen, setCreateOpen] = useState(false);

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Space>
        <Typography.Title level={4} style={{ margin: 0 }}>Мои папки</Typography.Title>
        <Button icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
          Создать
        </Button>
      </Space>
      <FolderTree />
      <CreateFolderModal open={createOpen} parentFolderId={null} onClose={() => setCreateOpen(false)} />
    </Space>
  );
}
