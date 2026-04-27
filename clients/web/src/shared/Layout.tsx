import { FolderOpenOutlined, ShareAltOutlined } from '@ant-design/icons';
import { Layout as AntLayout, Menu, Typography } from 'antd';
import { Link, Outlet, useLocation } from 'react-router-dom';
import { Header } from './Header';

const { Sider, Content } = AntLayout;

const menuItems = [
  { key: '/folders', icon: <FolderOpenOutlined />, label: <Link to="/folders">Мои папки</Link> },
  { key: '/shared', icon: <ShareAltOutlined />, label: <Link to="/shared">Доступные</Link> },
];

export function AppLayout() {
  const location = useLocation();

  return (
    <AntLayout style={{ minHeight: '100vh' }}>
      <AntLayout.Header style={{ background: '#001529' }}>
        <Typography.Text style={{ color: '#fff', fontSize: 18, fontWeight: 600 }}>
          GraNAS
        </Typography.Text>
        <Header />
      </AntLayout.Header>
      <AntLayout>
        <Sider theme="light" width={200}>
          <Menu
            mode="inline"
            selectedKeys={[location.pathname]}
            style={{ height: '100%', borderRight: 0 }}
            items={menuItems}
          />
        </Sider>
        <Content style={{ padding: 24, background: '#fff' }}>
          <Outlet />
        </Content>
      </AntLayout>
    </AntLayout>
  );
}
