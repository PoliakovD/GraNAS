import { LogoutOutlined } from '@ant-design/icons';
import { Avatar, Dropdown, Space, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

function getInitials(email: string): string {
  return email[0]?.toUpperCase() ?? '?';
}

export function Header() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  const menuItems = [
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: 'Выход',
      danger: true,
      onClick: handleLogout,
    },
  ];

  return (
    <Dropdown menu={{ items: menuItems }} trigger={['click']}>
      <Space style={{ float: 'right', lineHeight: '64px', paddingRight: 24, cursor: 'pointer' }}>
        <Avatar size="small" style={{ backgroundColor: '#722ed1' }}>
          {user ? getInitials(user.email) : '?'}
        </Avatar>
        <Typography.Text style={{ color: '#fff' }}>{user?.email}</Typography.Text>
      </Space>
    </Dropdown>
  );
}
