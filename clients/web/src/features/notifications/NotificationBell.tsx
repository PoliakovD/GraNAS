import { Badge, Dropdown, List, Space, Typography } from 'antd';
import { BellOutlined } from '@ant-design/icons';
import {
  useMarkRead,
  useNotificationsList,
  useUnreadCount,
} from '../../notifications/useNotifications';
import { NotificationItem } from './NotificationItem';
import type { NotificationDto } from '../../api/notifications.api';

export function NotificationBell() {
  const { data: unreadData } = useUnreadCount();
  const { data: listData, fetchNextPage } = useNotificationsList();
  const markReadMutation = useMarkRead();

  const allItems: NotificationDto[] =
    listData?.pages.flatMap(p => p.items) ?? [];
  const preview = allItems.slice(0, 10);

  const handleItemClick = (id: string) => {
    markReadMutation.mutate(id);
  };

  const overlay = (
    <div
      style={{
        background: '#fff',
        boxShadow: '0 3px 12px rgba(0,0,0,.15)',
        borderRadius: 8,
        width: 340,
        maxHeight: 440,
        overflowY: 'auto',
      }}
    >
      <List
        size="small"
        dataSource={preview}
        locale={{ emptyText: 'Нет уведомлений' }}
        renderItem={item => (
          <List.Item
            key={item.id}
            onClick={() => handleItemClick(item.id)}
            style={{ cursor: 'pointer', padding: '8px 16px' }}
          >
            <NotificationItem notification={item} />
          </List.Item>
        )}
        footer={
          allItems.length > 10 && (
            <div style={{ textAlign: 'center', padding: 8 }}>
              <Typography.Link onClick={() => fetchNextPage()}>
                Показать ещё
              </Typography.Link>
            </div>
          )
        }
      />
    </div>
  );

  return (
    <Dropdown dropdownRender={() => overlay} trigger={['click']} placement="bottomRight">
      <Space style={{ cursor: 'pointer', lineHeight: '64px', padding: '0 8px' }}>
        <Badge count={unreadData?.unreadCount ?? 0} size="small">
          <BellOutlined style={{ fontSize: 20, color: '#fff' }} />
        </Badge>
      </Space>
    </Dropdown>
  );
}
