import { useState } from 'react';
import { Layout, Menu, Dropdown, Space, Button, Avatar } from 'antd';
import {
  DashboardOutlined, ApiOutlined, DeploymentUnitOutlined, BranchesOutlined,
  KeyOutlined, TeamOutlined, UserOutlined, BarChartOutlined, SettingOutlined, ControlOutlined, LineChartOutlined,
  SunOutlined, MoonOutlined, DesktopOutlined, GlobalOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
} from '@ant-design/icons';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../contexts/ThemeContext';
import { useAuth } from '../contexts/AuthContext';

const { Header, Sider, Content } = Layout;

export default function MainLayout() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { mode, setMode } = useThemeMode();
  const { user, logout } = useAuth();
  const [collapsed, setCollapsed] = useState(false);

  const allRoles = ['SuperAdmin', 'Admin', 'Developer', 'ReadOnly'];

  const allMenuItems = [
    { key: '/', icon: <DashboardOutlined />, label: t('nav.dashboard'), roles: allRoles },
    { key: '/providers', icon: <ApiOutlined />, label: t('nav.providers'), roles: ['SuperAdmin', 'Admin'] },
    {
      key: 'models-group',
      icon: <DeploymentUnitOutlined />,
      label: t('nav.models'),
      roles: ['SuperAdmin', 'Admin'],
      children: [
        { key: '/models', label: t('model.title') },
        { key: '/models/matrix', label: t('capability.matrixTitle') },
        { key: '/models/capabilities', label: t('capability.title') },
      ],
    },
    { key: '/route-models', icon: <BranchesOutlined />, label: t('nav.routeModels'), roles: ['SuperAdmin', 'Admin'] },
    { key: '/api-keys', icon: <KeyOutlined />, label: t('nav.apiKeys'), roles: ['SuperAdmin', 'Admin', 'Developer'] },
    { key: '/organizations', icon: <TeamOutlined />, label: t('nav.organizations'), roles: ['SuperAdmin'] },
    { key: '/users', icon: <UserOutlined />, label: t('nav.users'), roles: ['SuperAdmin'] },
    { key: '/quotas', icon: <ControlOutlined />, label: t('quota.title'), roles: ['SuperAdmin'] },
    { key: '/audit', icon: <BarChartOutlined />, label: t('nav.audit'), roles: allRoles },
    {
      key: 'analytics-group',
      icon: <LineChartOutlined />,
      label: t('nav.analytics'),
      roles: allRoles,
      children: [
        { key: '/analytics/usage', label: t('analytics.usage') },
        { key: '/analytics/cost', label: t('analytics.cost') },
        { key: '/analytics/performance', label: t('analytics.performance') },
        { key: '/analytics/cache', label: t('analytics.cache') },
        { key: '/analytics/anomalies', label: t('analytics.anomalies') },
      ],
    },
    { key: '/settings', icon: <SettingOutlined />, label: t('nav.settings'), roles: ['SuperAdmin'] },
  ];

  const filterMenuByRole = (items: typeof allMenuItems, role?: string) => {
    return items
      .filter((item) => item.roles.includes(role || ''))
      .map((item) => ({
        key: item.key,
        icon: item.icon,
        label: item.label,
        children: item.children?.map((child) => ({ key: child.key, label: child.label })),
      }));
  };

  const menuItems = filterMenuByRole(allMenuItems, user?.role);

  const themeItems = [
    { key: 'light', icon: <SunOutlined />, label: 'Light', onClick: () => setMode('light') },
    { key: 'dark', icon: <MoonOutlined />, label: 'Dark', onClick: () => setMode('dark') },
    { key: 'system', icon: <DesktopOutlined />, label: 'System', onClick: () => setMode('system') },
  ];

  const langItems = [
    { key: 'zh-CN', label: '中文', onClick: () => { i18n.changeLanguage('zh-CN'); localStorage.setItem('tensu-lang', 'zh-CN'); } },
    { key: 'en-US', label: 'English', onClick: () => { i18n.changeLanguage('en-US'); localStorage.setItem('tensu-lang', 'en-US'); } },
  ];

  const userItems = [
    { key: 'logout', icon: <LogoutOutlined />, label: t('auth.logout'), onClick: () => { logout(); navigate('/login'); } },
  ];

  const selectedKey = location.pathname;
  const openKey = selectedKey.startsWith('/models') ? 'models-group' : selectedKey.startsWith('/analytics') ? 'analytics-group' : undefined;

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={setCollapsed}
        width={240}
        collapsedWidth={68}
        style={{
          position: 'fixed',
          left: 0,
          top: 0,
          bottom: 0,
          zIndex: 100,
          backdropFilter: 'blur(20px) saturate(180%)',
          borderRight: 'none',
          overflow: 'auto',
        }}
        trigger={null}
      >
        <div style={{ height: 56, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '0 16px' }}>
          {collapsed
            ? <span style={{ fontSize: 20, fontWeight: 700, color: 'var(--ant-color-primary)' }}>T</span>
            : <span style={{ fontSize: 18, fontWeight: 700, letterSpacing: '-0.02em', color: 'var(--ant-color-primary)' }}>Tensu</span>
          }
        </div>
        <Menu
          mode="inline"
          selectedKeys={[selectedKey]}
          defaultOpenKeys={openKey ? [openKey] : []}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{ borderInlineEnd: 'none', background: 'transparent' }}
        />
      </Sider>

      <Layout style={{ marginLeft: collapsed ? 68 : 240, transition: 'margin-left 0.2s', minHeight: '100vh' }}>
        <Header style={{
          position: 'sticky',
          top: 0,
          zIndex: 99,
          height: 56,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 24px',
          backdropFilter: 'blur(20px) saturate(180%)',
          borderBottom: '1px solid var(--ant-color-border-secondary)',
        }}>
          <Button
            type="text"
            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            onClick={() => setCollapsed(!collapsed)}
            style={{ fontSize: 16 }}
          />

          <Space size={12}>
            <Dropdown menu={{ items: themeItems }} trigger={['click']}>
              <Button type="text" icon={mode === 'dark' ? <MoonOutlined /> : mode === 'light' ? <SunOutlined /> : <DesktopOutlined />} />
            </Dropdown>
            <Dropdown menu={{ items: langItems }} trigger={['click']}>
              <Button type="text" icon={<GlobalOutlined />} />
            </Dropdown>
            <Dropdown menu={{ items: userItems }} trigger={['click']}>
              <Space style={{ cursor: 'pointer' }}>
                <Avatar size={28} icon={<UserOutlined />} style={{ backgroundColor: 'var(--ant-color-primary)' }} />
                <span style={{ fontSize: 13 }}>{user?.displayName || user?.username}</span>
              </Space>
            </Dropdown>
          </Space>
        </Header>

        <Content style={{ padding: 24, minHeight: 'calc(100vh - 56px)' }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
