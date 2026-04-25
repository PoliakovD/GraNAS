import { Button, Card, Form, Input, Typography, notification } from 'antd';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form] = Form.useForm<{ email: string; password: string }>();
  const [loading, setLoading] = useState(false);

  const onFinish = async (values: { email: string; password: string }) => {
    setLoading(true);
    try {
      await login(values);
      navigate('/folders', { replace: true });
    } catch (e) {
      const description =
        (e as { response?: { data?: { title?: string } } })?.response?.data?.title ??
        'Проверьте email и пароль';
      notification.error({ message: 'Ошибка входа', description });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
      <Card style={{ width: 360 }}>
        <Typography.Title level={3} style={{ textAlign: 'center' }}>GraNAS — Вход</Typography.Title>
        <Form form={form} layout="vertical" onFinish={onFinish}>
          <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}>
            <Input autoFocus />
          </Form.Item>
          <Form.Item name="password" label="Пароль" rules={[{ required: true, min: 8 }]}>
            <Input.Password />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" block loading={loading}>Войти</Button>
          </Form.Item>
        </Form>
        <Typography.Text>
          Нет аккаунта? <Link to="/register">Зарегистрироваться</Link>
        </Typography.Text>
      </Card>
    </div>
  );
}
