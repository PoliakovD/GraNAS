import { Form, Input, Modal } from 'antd';
import { useCreateFolder } from './useFoldersQuery';

interface Props {
  open: boolean;
  parentFolderId?: string | null;
  onClose: () => void;
}

export function CreateFolderModal({ open, parentFolderId, onClose }: Props) {
  const [form] = Form.useForm<{ name: string }>();
  const create = useCreateFolder();

  const handleOk = async () => {
    const { name } = await form.validateFields();
    await create.mutateAsync({ name, parentFolderId: parentFolderId ?? null });
    form.resetFields();
    onClose();
  };

  return (
    <Modal
      title={parentFolderId ? 'Создать подпапку' : 'Создать папку'}
      open={open}
      onOk={handleOk}
      onCancel={() => { form.resetFields(); onClose(); }}
      confirmLoading={create.isPending}
    >
      <Form form={form} layout="vertical">
        <Form.Item name="name" label="Название" rules={[{ required: true, message: 'Введите название' }]}>
          <Input autoFocus />
        </Form.Item>
      </Form>
    </Modal>
  );
}
