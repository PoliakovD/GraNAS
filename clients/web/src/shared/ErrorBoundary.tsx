import React from 'react';

interface State { hasError: boolean }

export class ErrorBoundary extends React.Component<{ children: React.ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State { return { hasError: true }; }

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-state">
          <div className="error-state-title">Что-то пошло не так</div>
          <button className="btn primary" onClick={() => this.setState({ hasError: false })}>
            Попробовать снова
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
