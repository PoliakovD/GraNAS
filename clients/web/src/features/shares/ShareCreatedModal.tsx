import { CopyOutlined } from '@ant-design/icons';
import { Alert, Button, Modal, Space, Typography } from 'antd';

interface Props {
  open: boolean;
  token: string;
  onClose: () => void;
}

export function ShareCreatedModal({ open, token, onClose }: Props) {
  const shareUrl = `${window.location.origin}/s/${token}`;

  const copy = () => navigator.clipboard.writeText(shareUrl);

  return (
    <Modal
      title="Ссылка создана"
      open={open}
      onOk={onClose}
      onCancel={onClose}
      cancelButtonProps={{ style: { display: 'none' } }}
    >
      <Alert
        type="warning"
        message="Сохраните ссылку — она показывается только один раз."
        style={{ marginBottom: 12 }}
      />
      <Space.Compact style={{ width: '100%' }}>
        <Typography.Text code copyable style={{ flex: 1, wordBreak: 'break-all' }}>
          {shareUrl}
        </Typography.Text>
        <Button icon={<CopyOutlined />} onClick={copy}>Копировать</Button>
      </Space.Compact>
    </Modal>
  );
}
