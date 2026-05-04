import { Button, Form, Input, Select } from 'antd';
import type { AccessLevel } from '../../types/folder';
import { useGrantPermission } from './usePermissionMutations';

interface Props { folderId: string }

export function GrantPermissionForm({ folderId }: Props) {
  const [form] = Form.useForm<{ email: string; accessLevel: AccessLevel }>();
  const grant = useGrantPermission(folderId);

  const onFinish = async (values: { email: string; accessLevel: AccessLevel }) => {
    await grant.mutateAsync(values);
    form.resetFields();
  };

  return (
    <Form form={form} layout="inline" onFinish={onFinish} style={{ marginBottom: 16 }}>
      <Form.Item name="email" rules={[{ required: true, type: 'email' }]}>
        <Input placeholder="email пользователя" style={{ width: 240 }} />
      </Form.Item>
      <Form.Item name="accessLevel" initialValue="View">
        <Select style={{ width: 120 }}>
          <Select.Option value="View">View</Select.Option>
          <Select.Option value="Full">Full</Select.Option>
        </Select>
      </Form.Item>
      <Form.Item>
        <Button type="primary" htmlType="submit" loading={grant.isPending}>
          Выдать
        </Button>
      </Form.Item>
    </Form>
  );
}
