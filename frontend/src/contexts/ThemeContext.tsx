import React, { createContext, useEffect, useState } from 'react';
import { ConfigProvider, theme } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import enUS from 'antd/locale/en_US';
import { useTranslation } from 'react-i18next';

type ThemeMode = 'light' | 'dark' | 'system';

interface ThemeContextType {
  mode: ThemeMode;
  isDark: boolean;
  setMode: (mode: ThemeMode) => void;
}

const ThemeContext = createContext<ThemeContextType>({ mode: 'system', isDark: false, setMode: () => {} });

export { ThemeContext };

const lightTokens = {
  colorPrimary: '#007AFF',
  colorSuccess: '#34C759',
  colorWarning: '#FF9500',
  colorError: '#FF3B30',
  colorInfo: '#5AC8FA',
  colorBgLayout: '#F2F2F7',
  colorBgContainer: '#FFFFFF',
  colorText: '#1C1C1E',
  colorTextSecondary: 'rgba(60,60,67,0.6)',
  colorBorderSecondary: 'rgba(60,60,67,0.12)',
  borderRadius: 10,
  borderRadiusLG: 18,
  fontSize: 14,
  wireframe: false,
};

const darkTokens = {
  colorPrimary: '#0A84FF',
  colorSuccess: '#30D158',
  colorWarning: '#FF9F0A',
  colorError: '#FF453A',
  colorInfo: '#64D2FF',
  colorBgLayout: '#000000',
  colorBgContainer: '#1C1C1E',
  colorText: '#FFFFFF',
  colorTextSecondary: 'rgba(235,235,245,0.6)',
  colorBorderSecondary: 'rgba(84,84,88,0.36)',
  borderRadius: 10,
  borderRadiusLG: 18,
  fontSize: 14,
  wireframe: false,
};

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(() => (localStorage.getItem('tensu-theme') as ThemeMode) || 'system');
  const [systemDark, setSystemDark] = useState(() => window.matchMedia('(prefers-color-scheme: dark)').matches);
  const { i18n } = useTranslation();

  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const handler = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  }, []);

  useEffect(() => {
    localStorage.setItem('tensu-theme', mode);
  }, [mode]);

  const isDark = mode === 'dark' || (mode === 'system' && systemDark);
  const antdLocale = i18n.language?.startsWith('zh') ? zhCN : enUS;

  return (
    <ThemeContext.Provider value={{ mode, isDark, setMode }}>
      <ConfigProvider
        locale={antdLocale}
        theme={{
          algorithm: isDark ? theme.darkAlgorithm : theme.defaultAlgorithm,
          token: isDark ? darkTokens : lightTokens,
          components: {
            Layout: {
              headerBg: isDark ? 'rgba(30,30,32,0.72)' : 'rgba(255,255,255,0.72)',
              siderBg: isDark ? 'rgba(28,28,30,0.8)' : 'rgba(246,246,248,0.8)',
              bodyBg: isDark ? '#000000' : '#F2F2F7',
            },
            Menu: {
              itemBg: 'transparent',
              subMenuItemBg: 'transparent',
            },
            Card: {
              borderRadiusLG: 18,
            },
            Table: {
              borderRadius: 16,
            },
          },
        }}
      >
        {children}
      </ConfigProvider>
    </ThemeContext.Provider>
  );
}
