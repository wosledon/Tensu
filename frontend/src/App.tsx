import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Spin } from 'antd';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import MainLayout from './layouts/MainLayout';
import LoginPage from './pages/auth/LoginPage';
import './i18n';

// Lazy-loaded pages for code splitting
const DashboardPage = lazy(() => import('./pages/dashboard/DashboardPage'));
const ProvidersPage = lazy(() => import('./pages/providers/ProvidersPage'));
const ModelsPage = lazy(() => import('./pages/models/ModelsPage'));
const ModelMatrixPage = lazy(() => import('./pages/models/ModelMatrixPage'));
const RouteModelsPage = lazy(() => import('./pages/routeModels/RouteModelsPage'));
const ApiKeysPage = lazy(() => import('./pages/apiKeys/ApiKeysPage'));
const OrganizationsPage = lazy(() => import('./pages/organizations/OrganizationsPage'));
const AuditPage = lazy(() => import('./pages/audit/AuditPage'));
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage'));

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

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<RequireAuth><MainLayout /></RequireAuth>}>
              <Route index element={<Suspense fallback={<PageLoader />}><DashboardPage /></Suspense>} />
              <Route path="providers" element={<Suspense fallback={<PageLoader />}><ProvidersPage /></Suspense>} />
              <Route path="models" element={<Suspense fallback={<PageLoader />}><ModelsPage /></Suspense>} />
              <Route path="models/matrix" element={<Suspense fallback={<PageLoader />}><ModelMatrixPage /></Suspense>} />
              <Route path="route-models" element={<Suspense fallback={<PageLoader />}><RouteModelsPage /></Suspense>} />
              <Route path="api-keys" element={<Suspense fallback={<PageLoader />}><ApiKeysPage /></Suspense>} />
              <Route path="organizations" element={<Suspense fallback={<PageLoader />}><OrganizationsPage /></Suspense>} />
              <Route path="audit" element={<Suspense fallback={<PageLoader />}><AuditPage /></Suspense>} />
              <Route path="settings" element={<Suspense fallback={<PageLoader />}><SettingsPage /></Suspense>} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}
