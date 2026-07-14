import { Component, type ReactNode } from 'react';
import { Button, Result } from 'antd';
import i18n from '../i18n';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export default class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('ErrorBoundary caught:', error, errorInfo);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) return this.props.fallback;
      const t = i18n.t.bind(i18n);
      return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '50vh' }}>
          <Result
            status="error"
            title={t('error.title', 'Something went wrong')}
            subTitle={this.state.error?.message || t('error.subtitle', 'An unexpected error occurred')}
            extra={
              <Button type="primary" onClick={this.handleRetry}>
                {t('error.tryAgain', 'Try Again')}
              </Button>
            }
          />
        </div>
      );
    }
    return this.props.children;
  }
}
