import React from 'react';
import { Outlet } from 'react-router-dom';
import { ErrorPage } from './ErrorPage';

interface State { hasError: boolean }

export class ErrorBoundary extends React.Component<{ children?: React.ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State { return { hasError: true }; }

  render() {
    if (this.state.hasError) {
      return (
        <ErrorPage
          code="error"
          title="Что-то пошло не так"
          description="Произошла непредвиденная ошибка приложения"
          action={{ label: 'Попробовать снова', onClick: () => this.setState({ hasError: false }) }}
        />
      );
    }
    return this.props.children ?? <Outlet />;
  }
}
