import { screen, fireEvent, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { handlers } from './mocks/handlers';
import { renderWithProviders } from './test-utils';
import { NotificationsTab } from '../pages/settings/NotificationsTab';

const server = setupServer(...handlers);
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('NotificationsTab', () => {
  it('renders 4 event type rows and 3 channel columns', async () => {
    renderWithProviders(<NotificationsTab />);

    await waitFor(() => expect(screen.getByText('Доступ предоставлен')).toBeInTheDocument());

    expect(screen.getByText('Доступ отозван')).toBeInTheDocument();
    expect(screen.getByText('Ссылка отозвана')).toBeInTheDocument();
    expect(screen.getByText('Доступ потерян')).toBeInTheDocument();

    expect(screen.getByText('Email')).toBeInTheDocument();
    expect(screen.getByText('В приложении')).toBeInTheDocument();
    expect(screen.getByText('Push')).toBeInTheDocument();
  });

  it('calls PUT /api/auth/me/settings on save with updated prefs', async () => {
    let capturedBody: unknown;
    server.use(
      http.put('http://localhost:8080/api/auth/me/settings', async ({ request }) => {
        capturedBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      })
    );

    renderWithProviders(<NotificationsTab />);

    await waitFor(() => expect(screen.getByText('Сохранить')).toBeInTheDocument());

    const saveBtn = screen.getByText('Сохранить');
    fireEvent.click(saveBtn);

    await waitFor(() => expect(capturedBody).toBeDefined());
    expect((capturedBody as { notificationPrefs: object }).notificationPrefs).toBeDefined();
  });
});
