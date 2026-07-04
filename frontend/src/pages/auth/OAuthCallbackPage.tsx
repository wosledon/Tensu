import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Spin, App } from 'antd';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';

export default function OAuthCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();
  const { t } = useTranslation();
  const { message } = App.useApp();

  useEffect(() => {
    const token = searchParams.get('token');
    const error = searchParams.get('error');

    if (error) {
      message.error(t('auth.oauthError', { error }));
      navigate('/login', { replace: true });
      return;
    }

    if (token) {
      login(token)
        .then(() => {
          message.success(t('common.success'));
          navigate('/', { replace: true });
        })
        .catch(() => {
          message.error(t('auth.invalidCredentials'));
          navigate('/login', { replace: true });
        });
    } else {
      navigate('/login', { replace: true });
    }
  }, [searchParams, navigate, login, t]);

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 16,
      background: 'var(--ant-color-bg-layout)',
    }}>
      <Spin size="large" />
      <span style={{ color: 'var(--ant-color-text-secondary)' }}>{t('common.loading')}</span>
    </div>
  );
}
