import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Descriptions, Result, Spin, Typography } from 'antd';
import { useParams } from 'react-router-dom';
import { sharesApi } from '../api/shares.api';

export function PublicSharePage() {
  const { token } = useParams<{ token: string }>();

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-share', token],
    queryFn: () => sharesApi.getByToken(token!).then(r => r.data),
    enabled: !!token,
    retry: false,
  });

  if (isLoading) return <Spin style={{ display: 'block', marginTop: 80 }} />;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const status = (error as any)?.response?.status;

  if (status === 410) {
    return (
      <Result
        status="error"
        title="Ссылка отозвана"
        subTitle="Владелец отозвал эту ссылку доступа."
      />
    );
  }
  if (status === 404) {
    return (
      <Result
        status="404"
        title="Ссылка не найдена"
        subTitle="Ссылка не существует или истёк срок действия."
      />
    );
  }
  if (error || !data) {
    return <Result status="error" title="Ошибка" />;
  }

  return (
    <div style={{ maxWidth: 600, margin: '40px auto' }}>
      <Card>
        <Typography.Title level={3}>{data.folderName}</Typography.Title>
        <Alert
          type="info"
          message="Просмотр содержимого папки станет доступен после Phase 6 (P2P)."
          style={{ marginBottom: 16 }}
        />
        <Descriptions column={1} bordered size="small">
          <Descriptions.Item label="ID папки">{data.folderId}</Descriptions.Item>
          <Descriptions.Item label="Владелец">{data.ownerId}</Descriptions.Item>
          {data.path && <Descriptions.Item label="Путь">{data.path}</Descriptions.Item>}
          {data.expiresAt && (
            <Descriptions.Item label="Истекает">
              {new Date(data.expiresAt).toLocaleString('ru')}
            </Descriptions.Item>
          )}
        </Descriptions>
      </Card>
    </div>
  );
}
