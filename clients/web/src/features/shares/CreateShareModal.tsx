import { DatePicker, Form, Input, Modal } from 'antd';
import { useState } from 'react';
import type { CreateShareResponse } from '../../types/share';
import { ShareCreatedModal } from './ShareCreatedModal';
import { useCreateShare } from './useShareMutations';

interface Props {
  folderId: string;
  open: boolean;
  onClose: () => void;
}

export function CreateShareModal({ folderId, open, onClose }: Props) {
  const [form] = Form.useForm<{ expiresAt: unknown; path?: string }>();
  const create = useCreateShare(folderId);
  const [created, setCreated] = useState<CreateShareResponse | null>(null);

  const handleOk = async () => {
    const values = await form.validateFields();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const expiresAt = (values.expiresAt as any)?.toISOString();
    const res = await create.mutateAsync({ expiresAt, path: values.path ?? null });
    form.resetFields();
    onClose();
    setCreated(res);
  };

  return (
    <>
      <Modal
        title="Создать share-ссылку"
        open={open}
        onOk={handleOk}
        onCancel={() => { form.resetFields(); onClose(); }}
        confirmLoading={create.isPending}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="expiresAt" label="Дата истечения" rules={[{ required: true }]}>
            <DatePicker showTime style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="path" label="Путь (необязательно)">
            <Input placeholder="subdir/filename (null = вся папка)" />
          </Form.Item>
        </Form>
      </Modal>
      {created && (
        <ShareCreatedModal
          open
          token={created.token}
          onClose={() => setCreated(null)}
        />
      )}
    </>
  );
}
