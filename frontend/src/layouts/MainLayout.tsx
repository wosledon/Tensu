import { useState } from 'react';
import { Layout, Menu, Dropdown, Space, Button, Avatar } from 'antd';
import {
  DashboardOutlined, DeploymentUnitOutlined, ApiOutlined, BranchesOutlined, AppstoreOutlined, ThunderboltOutlined, CodeOutlined,
  KeyOutlined, TeamOutlined, UserOutlined, ControlOutlined, BarChartOutlined, AuditOutlined, LineChartOutlined, DollarOutlined, RocketOutlined,
  CloudOutlined, WarningOutlined, SettingOutlined, NotificationOutlined, GlobalOutlined, RobotOutlined,
  SunOutlined, MoonOutlined, DesktopOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined,
} from '@ant-design/icons';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useThemeMode } from '../hooks/useThemeMode';
import { useAuth } from '../hooks/useAuth';

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
    { key: '/api-docs', icon: <CodeOutlined />, label: t('nav.apiDocs'), roles: ['SuperAdmin', 'Admin', 'Developer'] },
    {
      key: 'gateway-group', icon: <DeploymentUnitOutlined />,
      label: t('nav.groupGateway'), roles: ['SuperAdmin', 'Admin'],
      children: [
        { key: '/providers', icon: <ApiOutlined />, label: t('nav.providers') },
        { key: '/models', icon: <BranchesOutlined />, label: t('model.title') },
        { key: '/models/matrix', icon: <AppstoreOutlined />, label: t('capability.matrixTitle') },
        { key: '/models/capabilities', icon: <ThunderboltOutlined />, label: t('capability.title') },
        { key: '/route-models', icon: <CodeOutlined />, label: t('nav.routeModels') },
        { key: '/chat', icon: <RobotOutlined />, label: t('nav.chat') },
      ],
    },
    {
      key: 'access-group', icon: <KeyOutlined />,
      label: t('nav.groupAccess'), roles: ['SuperAdmin', 'Admin'],
      children: [
        { key: '/api-keys', icon: <KeyOutlined />, label: t('nav.apiKeys') },
        { key: '/oauth-providers', icon: <GlobalOutlined />, label: t('nav.oauthProviders') },
      ],
    },
    {
      key: 'org-group', icon: <TeamOutlined />,
      label: t('nav.groupOrganization'), roles: ['SuperAdmin'],
      children: [
        { key: '/organizations', icon: <TeamOutlined />, label: t('nav.organizations') },
        { key: '/users', icon: <UserOutlined />, label: t('nav.users') },
        { key: '/quotas', icon: <ControlOutlined />, label: t('quota.title') },
      ],
    },
    {
      key: 'ops-group', icon: <BarChartOutlined />,
      label: t('nav.groupOps'), roles: allRoles,
      children: [
        { key: '/audit', icon: <AuditOutlined />, label: t('nav.audit') },
        { key: '/analytics/usage', icon: <LineChartOutlined />, label: t('analytics.usage') },
        { key: '/analytics/cost', icon: <DollarOutlined />, label: t('analytics.cost') },
        { key: '/analytics/performance', icon: <RocketOutlined />, label: t('analytics.performance') },
        { key: '/analytics/cache', icon: <CloudOutlined />, label: t('analytics.cache') },
        { key: '/analytics/anomalies', icon: <WarningOutlined />, label: t('analytics.anomalies') },
        { key: '/admin-audit', icon: <AuditOutlined />, label: t('nav.adminAudit'), roles: ['SuperAdmin'] },
        { key: '/alert-rules', icon: <WarningOutlined />, label: t('nav.alertRules'), roles: ['SuperAdmin'] },
      ],
    },
    {
      key: 'system-group', icon: <SettingOutlined />,
      label: t('nav.groupSystem'), roles: ['SuperAdmin'],
      children: [
        { key: '/settings', icon: <SettingOutlined />, label: t('nav.settings') },
        { key: '/webhooks', icon: <NotificationOutlined />, label: t('nav.webhooks') },
        { key: '/compression/restore', icon: <CodeOutlined />, label: t('nav.compressionRestore') },
      ],
    },
  ];

  const filterMenuByRole = (items: typeof allMenuItems, role?: string) => {
    return items
      .filter((item) => item.roles.includes(role || ''))
      .map((item) => ({
        key: item.key,
        icon: item.icon,
        label: item.label,
        children: item.children
          ?.filter((child) => !('roles' in child) || (child as any).roles?.includes(role || ''))
          .map((child) => ({ key: child.key, icon: child.icon, label: child.label })),
      }))
      .filter((item) => !item.children || item.children.length > 0);
  };

  const menuItems = filterMenuByRole(allMenuItems, user?.role);

  const themeItems = [
    { key: 'light', icon: <SunOutlined />, label: t('settings.themeLight'), onClick: () => setMode('light') },
    { key: 'dark', icon: <MoonOutlined />, label: t('settings.themeDark'), onClick: () => setMode('dark') },
    { key: 'system', icon: <DesktopOutlined />, label: t('settings.themeSystem'), onClick: () => setMode('system') },
  ];

  const langItems = [
    { key: 'zh-CN', label: t('settings.languageChinese'), onClick: () => { i18n.changeLanguage('zh-CN'); localStorage.setItem('tensu-lang', 'zh-CN'); } },
    { key: 'en-US', label: t('settings.languageEnglish'), onClick: () => { i18n.changeLanguage('en-US'); localStorage.setItem('tensu-lang', 'en-US'); } },
  ];

  const userItems = [
    { key: 'logout', icon: <LogoutOutlined />, label: t('auth.logout'), onClick: () => { logout(); navigate('/login'); } },
  ];

  const selectedPath = location.pathname;
  const getDefaultOpenKeys = (path: string): string[] => {
    const keys: string[] = [];
    if (path.startsWith('/providers') || path.startsWith('/models') || path.startsWith('/route-models') || path.startsWith('/chat')) keys.push('gateway-group');
    if (path.startsWith('/audit') || path.startsWith('/analytics') || path.startsWith('/admin-audit') || path.startsWith('/alert-rules')) keys.push('ops-group');
    if (path.startsWith('/api-keys') || path.startsWith('/oauth-providers')) keys.push('access-group');
    if (path.startsWith('/organizations') || path.startsWith('/users') || path.startsWith('/quotas')) keys.push('org-group');
    if (path.startsWith('/settings') || path.startsWith('/admin-audit') || path.startsWith('/webhooks') || path.startsWith('/oauth-providers') || path.startsWith('/alert-rules') || path.startsWith('/compression')) keys.push('system-group');
    return keys;
  };

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
          selectedKeys={[selectedPath]}
          defaultOpenKeys={getDefaultOpenKeys(selectedPath)}
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
