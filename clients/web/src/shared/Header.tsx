import { LogoutOutlined, UserOutlined } from '@ant-design/icons';
import { Button, Space, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function Header() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/login', { replace: true });
  };

  return (
    <Space style={{ float: 'right', lineHeight: '64px', paddingRight: 24 }}>
      <UserOutlined />
      <Typography.Text>{user?.email}</Typography.Text>
      <Button icon={<LogoutOutlined />} type="text" onClick={handleLogout}>
        Выход
      </Button>
    </Space>
  );
}
