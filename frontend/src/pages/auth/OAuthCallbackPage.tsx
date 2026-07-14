import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Spin, App } from 'antd';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../hooks/useAuth';
import axios from 'axios';

export default function OAuthCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { login } = useAuth();
  const { t } = useTranslation();
  const { message } = App.useApp();

  useEffect(() => {
    const code = searchParams.get('code');
    const error = searchParams.get('error');

    if (error) {
      message.error(t('auth.oauthError', { error }));
      navigate('/login', { replace: true });
      return;
    }

    if (code) {
      axios.post('/api/admin/auth/oauth/exchange', { code })
        .then(res => {
          if (res.data.code === 0 && res.data.data?.token) {
            return login(res.data.data.token);
          }
          throw new Error(res.data.message || 'Exchange failed');
        })
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
  }, [searchParams, navigate, login, t, message]);

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
