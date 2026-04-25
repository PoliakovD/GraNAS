import { Button, Card, Form, Input, Typography, notification } from 'antd';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { authApi } from '../api/auth.api';

export function RegisterPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form] = Form.useForm<{ email: string; password: string }>();

  const onFinish = async (values: { email: string; password: string }) => {
    await authApi.register(values);
    await login(values);
    navigate('/folders', { replace: true });
    notification.success({ message: 'Регистрация прошла успешно' });
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
      <Card style={{ width: 360 }}>
        <Typography.Title level={3} style={{ textAlign: 'center' }}>Регистрация</Typography.Title>
        <Form form={form} layout="vertical" onFinish={onFinish}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}>
            <Input autoFocus />
          </Form.Item>
          <Form.Item
            name="password"
            label="Пароль"
            rules={[{ required: true, min: 8, message: 'Минимум 8 символов' }]}
          >
            <Input.Password />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" block>Зарегистрироваться</Button>
          </Form.Item>
        </Form>
        <Typography.Text>
          Уже есть аккаунт? <Link to="/login">Войти</Link>
        </Typography.Text>
      </Card>
    </div>
  );
}
