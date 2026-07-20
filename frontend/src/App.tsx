import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Spin, App as AntApp } from 'antd';
import { AuthProvider } from './contexts/AuthContext';
import { useAuth } from './hooks/useAuth';
import { ThemeProvider } from './contexts/ThemeContext';
import MainLayout from './layouts/MainLayout';
import LoginPage from './pages/auth/LoginPage';
import OAuthCallbackPage from './pages/auth/OAuthCallbackPage';
import ErrorBoundary from './components/ErrorBoundary';
import './i18n';

// Lazy-loaded pages for code splitting
const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage'));
const ProvidersPage = lazy(() => import('./pages/providers/ProvidersPage'));
const ModelsPage = lazy(() => import('./pages/models/ModelsPage'));
const ModelMatrixPage = lazy(() => import('./pages/models/ModelMatrixPage'));
const ModelCapabilitiesPage = lazy(() => import('./pages/models/ModelCapabilitiesPage'));
const RouteModelsPage = lazy(() => import('./pages/routeModels/RouteModelsPage'));
const ApiKeysPage = lazy(() => import('./pages/apiKeys/ApiKeysPage'));
const ApiDocsPage = lazy(() => import('./pages/apiDocs/ApiDocsPage'));
const OrganizationsPage = lazy(() => import('./pages/organizations/OrganizationsPage'));
const UsersPage = lazy(() => import('./pages/users/UsersPage'));
const QuotasPage = lazy(() => import('./pages/quotas/QuotasPage'));
const AuditPage = lazy(() => import('./pages/audit/AuditPage'));
const UsagePage = lazy(() => import('./pages/analytics/UsagePage'));
const CostPage = lazy(() => import('./pages/analytics/CostPage'));
const PerformancePage = lazy(() => import('./pages/analytics/PerformancePage'));
const CachePage = lazy(() => import('./pages/analytics/CachePage'));
const AnomalyPage = lazy(() => import('./pages/analytics/AnomalyPage'));
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage'));
const AdminAuditPage = lazy(() => import('./pages/adminAudit/AdminAuditPage'));
const WebhooksPage = lazy(() => import('./pages/webhooks/WebhooksPage'));
const OAuthProvidersPage = lazy(() => import('./pages/oauthProviders/OAuthProvidersPage'));
const CompressionRestorePage = lazy(() => import('./pages/compression/CompressionRestorePage'));
const ChatPage = lazy(() => import('./pages/chat/ChatPage'));
const AlertRulesPage = lazy(() => import('./pages/alertRules/AlertRulesPage'));
const DataDeletionsPage = lazy(() => import('./pages/dataDeletions/DataDeletionsPage'));

function PageLoader() {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '50vh' }}>
      <Spin size="large" />
    </div>
  );
}

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { token, loading } = useAuth();
  if (loading) return <PageLoader />;
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

const adminRoles = ['SuperAdmin', 'Admin'];

function RequireRole({ roles, children }: { roles: string[]; children: React.ReactNode }) {
  const { user } = useAuth();
  if (!user || !roles.includes(user.role)) return <Navigate to="/" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <AntApp>
            <ErrorBoundary>
              <Routes>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/oauth/callback" element={<OAuthCallbackPage />} />
                <Route path="/" element={<RequireAuth><MainLayout /></RequireAuth>}>
                  <Route index element={<Suspense fallback={<PageLoader />}><DashboardPage /></Suspense>} />
                  <Route path="providers" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><ProvidersPage /></Suspense></RequireRole>} />
                  <Route path="models" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><ModelsPage /></Suspense></RequireRole>} />
                  <Route path="models/matrix" element={<Suspense fallback={<PageLoader />}><ModelMatrixPage /></Suspense>} />
                  <Route path="models/capabilities" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><ModelCapabilitiesPage /></Suspense></RequireRole>} />
                  <Route path="route-models" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><RouteModelsPage /></Suspense></RequireRole>} />
                  <Route path="api-keys" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><ApiKeysPage /></Suspense></RequireRole>} />
                  <Route path="api-docs" element={<RequireRole roles={['SuperAdmin', 'Admin', 'Developer']}><Suspense fallback={<PageLoader />}><ApiDocsPage /></Suspense></RequireRole>} />
                  <Route path="organizations" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><OrganizationsPage /></Suspense></RequireRole>} />
                  <Route path="users" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><UsersPage /></Suspense></RequireRole>} />
                  <Route path="quotas" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><QuotasPage /></Suspense></RequireRole>} />
                  <Route path="audit" element={<Suspense fallback={<PageLoader />}><AuditPage /></Suspense>} />
                  <Route path="analytics/usage" element={<Suspense fallback={<PageLoader />}><UsagePage /></Suspense>} />
                  <Route path="analytics/cost" element={<Suspense fallback={<PageLoader />}><CostPage /></Suspense>} />
                  <Route path="analytics/performance" element={<Suspense fallback={<PageLoader />}><PerformancePage /></Suspense>} />
                  <Route path="analytics/cache" element={<Suspense fallback={<PageLoader />}><CachePage /></Suspense>} />
                  <Route path="analytics/anomalies" element={<Suspense fallback={<PageLoader />}><AnomalyPage /></Suspense>} />
                  <Route path="settings" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><SettingsPage /></Suspense></RequireRole>} />
                  <Route path="admin-audit" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><AdminAuditPage /></Suspense></RequireRole>} />
                  <Route path="webhooks" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><WebhooksPage /></Suspense></RequireRole>} />
                  <Route path="oauth-providers" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><OAuthProvidersPage /></Suspense></RequireRole>} />
                  <Route path="compression/restore" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><CompressionRestorePage /></Suspense></RequireRole>} />
                  <Route path="chat" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><ChatPage /></Suspense></RequireRole>} />
                  <Route path="alert-rules" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><AlertRulesPage /></Suspense></RequireRole>} />
                  <Route path="data-deletions" element={<RequireRole roles={adminRoles}><Suspense fallback={<PageLoader />}><DataDeletionsPage /></Suspense></RequireRole>} />
                  <Route path="*" element={<Navigate to="/" replace />} />
                </Route>
              </Routes>
            </ErrorBoundary>
          </AntApp>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}
