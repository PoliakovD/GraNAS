import { DeleteOutlined, FolderAddOutlined, FolderOpenOutlined } from '@ant-design/icons';
import { App, Button, Dropdown, Empty, Skeleton, Space, Tree } from 'antd';
import type { MenuProps } from 'antd';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { buildFolderTree } from '../../lib/buildFolderTree';
import { CreateFolderModal } from './CreateFolderModal';
import { useDeleteFolder, useFoldersQuery } from './useFoldersQuery';

export function FolderTree() {
  const { user } = useAuth();
  const { data: folders = [], isLoading } = useFoldersQuery();
  const deleteFolder = useDeleteFolder();
  const navigate = useNavigate();
  const { modal } = App.useApp();
  const [modalState, setModalState] = useState<{ open: boolean; parentId?: string | null }>({ open: false });

  const treeData = buildFolderTree(folders, user?.id ?? '');

  const menuFor = (nodeId: string): MenuProps['items'] => [
    {
      key: 'sub',
      icon: <FolderAddOutlined />,
      label: 'Создать подпапку',
      onClick: () => setModalState({ open: true, parentId: nodeId }),
    },
    {
      key: 'open',
      icon: <FolderOpenOutlined />,
      label: 'Открыть',
      onClick: () => navigate(`/folders/${nodeId}`),
    },
    {
      key: 'delete',
      icon: <DeleteOutlined />,
      label: 'Удалить',
      danger: true,
      onClick: () => modal.confirm({
        title: 'Удалить папку и все вложенные?',
        content: 'Это действие нельзя отменить.',
        okText: 'Удалить',
        okType: 'danger',
        cancelText: 'Отмена',
        onOk: () => deleteFolder.mutate(nodeId),
      }),
    },
  ];

  if (isLoading) return <Skeleton active paragraph={{ rows: 4 }} />;
  if (treeData.length === 0) return <Empty description="Нет папок" />;

  return (
    <>
      <Tree
        treeData={treeData}
        titleRender={node => (
          <Dropdown menu={{ items: menuFor(node.folderId) }} trigger={['contextMenu']}>
            <Space>
              {node.title as string}
              <Button
                size="small"
                type="link"
                onClick={e => { e.stopPropagation(); navigate(`/folders/${node.folderId}`); }}
              >
                →
              </Button>
            </Space>
          </Dropdown>
        )}
        defaultExpandAll
      />
      <CreateFolderModal
        open={modalState.open}
        parentFolderId={modalState.parentId}
        onClose={() => setModalState({ open: false })}
      />
    </>
  );
}
