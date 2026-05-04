import { Button, Result } from 'antd';
import React from 'react';

interface State { hasError: boolean }

export class ErrorBoundary extends React.Component<{ children: React.ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State { return { hasError: true }; }

  render() {
    if (this.state.hasError) {
      return (
        <Result
          status="error"
          title="Что-то пошло не так"
          extra={<Button onClick={() => this.setState({ hasError: false })}>Попробовать снова</Button>}
        />
      );
    }
    return this.props.children;
  }
}
