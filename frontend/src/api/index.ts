import axios from 'axios';
import type { ApiResponse, PagedResult, PagedRequest, Provider, Model, Organization, User, ApiKey, RouteModel, RequestLog, LoginResponse, CurrentUser, ProviderKey, ModelPricing, RouteRule } from '../types';

const api = axios.create({
  baseURL: '/api/admin',
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 0) throw new Error(res.data.message);
  return res.data.data;
}

// Auth
export const authApi = {
  login: (username: string, password: string) =>
    api.post<ApiResponse<LoginResponse>>('/auth/login', { username, password }).then(unwrap),
  me: () => api.get<ApiResponse<CurrentUser>>('/auth/me').then(unwrap),
  changePassword: (oldPassword: string, newPassword: string) =>
    api.post<ApiResponse<void>>('/auth/change-password', { oldPassword, newPassword }).then(unwrap),
};

// Providers
export const providerApi = {
  list: (params: PagedRequest) =>
    api.get<ApiResponse<PagedResult<Provider>>>('/providers', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<Provider>>(`/providers/${id}`).then(unwrap),
  create: (data: Partial<Provider>) =>
    api.post<ApiResponse<Provider>>('/providers', data).then(unwrap),
  update: (id: number, data: Partial<Provider>) =>
    api.put<ApiResponse<Provider>>(`/providers/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/providers/${id}`).then(unwrap),
  addKey: (providerId: number, data: Partial<ProviderKey>) =>
    api.post<ApiResponse<ProviderKey>>(`/providers/${providerId}/keys`, data).then(unwrap),
  deleteKey: (providerId: number, keyId: number) =>
    api.delete<ApiResponse<void>>(`/providers/${providerId}/keys/${keyId}`).then(unwrap),
};

// Models
export const modelApi = {
  list: (params: PagedRequest & { providerId?: number }) =>
    api.get<ApiResponse<PagedResult<Model>>>('/models', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<Model>>(`/models/${id}`).then(unwrap),
  create: (data: Partial<Model>) =>
    api.post<ApiResponse<Model>>('/models', data).then(unwrap),
  update: (id: number, data: Partial<Model>) =>
    api.put<ApiResponse<Model>>(`/models/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/models/${id}`).then(unwrap),
  addPricing: (modelId: number, data: Partial<ModelPricing>) =>
    api.post<ApiResponse<ModelPricing>>(`/models/${modelId}/pricings`, data).then(unwrap),
  allEnabled: () =>
    api.get<ApiResponse<Model[]>>('/models/all-enabled').then(unwrap),
};

// Organizations
export const orgApi = {
  tree: () =>
    api.get<ApiResponse<Organization[]>>('/organizations').then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<Organization>>(`/organizations/${id}`).then(unwrap),
  create: (data: Partial<Organization>, parentId?: number) =>
    api.post<ApiResponse<Organization>>('/organizations', data, { params: { parentId } }).then(unwrap),
  update: (id: number, data: Partial<Organization>) =>
    api.put<ApiResponse<Organization>>(`/organizations/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/organizations/${id}`).then(unwrap),
};

// Users
export const userApi = {
  list: (params: PagedRequest & { orgId?: number }) =>
    api.get<ApiResponse<PagedResult<User>>>('/users', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<User>>(`/users/${id}`).then(unwrap),
  create: (data: { username: string; password: string; email?: string; displayName?: string; role: string; organizationId: number }) =>
    api.post<ApiResponse<User>>('/users', data).then(unwrap),
  update: (id: number, data: Partial<User>) =>
    api.put<ApiResponse<User>>(`/users/${id}`, data).then(unwrap),
  resetPassword: (id: number, newPassword: string) =>
    api.post<ApiResponse<void>>(`/users/${id}/reset-password`, { newPassword }).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/users/${id}`).then(unwrap),
};

// API Keys
export const apiKeyApi = {
  list: (params: PagedRequest & { orgId?: number }) =>
    api.get<ApiResponse<PagedResult<ApiKey>>>('/api-keys', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<ApiKey>>(`/api-keys/${id}`).then(unwrap),
  create: (data: Partial<ApiKey>) =>
    api.post<ApiResponse<{ id: number; name: string; key: string; keyPrefix: string }>>('/api-keys', data).then(unwrap),
  update: (id: number, data: Partial<ApiKey>) =>
    api.put<ApiResponse<ApiKey>>(`/api-keys/${id}`, data).then(unwrap),
  revoke: (id: number) =>
    api.post<ApiResponse<void>>(`/api-keys/${id}/revoke`).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/api-keys/${id}`).then(unwrap),
};

// Route Models
export const routeModelApi = {
  list: (params: PagedRequest) =>
    api.get<ApiResponse<PagedResult<RouteModel>>>('/route-models', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<RouteModel>>(`/route-models/${id}`).then(unwrap),
  create: (data: Partial<RouteModel>) =>
    api.post<ApiResponse<RouteModel>>('/route-models', data).then(unwrap),
  update: (id: number, data: Partial<RouteModel>) =>
    api.put<ApiResponse<RouteModel>>(`/route-models/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/route-models/${id}`).then(unwrap),
  addRule: (routeModelId: number, data: Partial<RouteRule>) =>
    api.post<ApiResponse<RouteRule>>(`/route-models/${routeModelId}/rules`, data).then(unwrap),
  deleteRule: (routeModelId: number, ruleId: number) =>
    api.delete<ApiResponse<void>>(`/route-models/${routeModelId}/rules/${ruleId}`).then(unwrap),
  updateShadowTarget: (id: number, targetModelId?: number) =>
    api.post<ApiResponse<RouteModel>>(`/route-models/${id}/shadow-target`, { targetModelId }).then(unwrap),
};

// Audit
export const auditApi = {
  list: (params: PagedRequest & { modelName?: string }) =>
    api.get<ApiResponse<PagedResult<RequestLog>>>('/audit/logs', { params }).then(unwrap),
  get: (requestId: string) =>
    api.get<ApiResponse<RequestLog>>(`/audit/logs/${requestId}`).then(unwrap),
  summary: (from: string, to: string, orgId?: number) =>
    api.get<ApiResponse<Record<string, number>>>('/audit/summary', { params: { from, to, orgId } }).then(unwrap),
};

// Analytics
export const analyticsApi = {
  dashboard: (orgId?: number) =>
    api.get<ApiResponse<any>>('/analytics/dashboard', { params: { orgId } }).then(unwrap),
  usage: (from: string, to: string, granularity = 'day', orgId?: number) =>
    api.get<ApiResponse<any>>('/analytics/usage', { params: { from, to, granularity, orgId } }).then(unwrap),
  cost: (from: string, to: string, orgId?: number) =>
    api.get<ApiResponse<any>>('/analytics/cost', { params: { from, to, orgId } }).then(unwrap),
  performance: (from: string, to: string, orgId?: number) =>
    api.get<ApiResponse<any>>('/analytics/performance', { params: { from, to, orgId } }).then(unwrap),
  cache: (from: string, to: string, orgId?: number) =>
    api.get<ApiResponse<any>>('/analytics/cache', { params: { from, to, orgId } }).then(unwrap),
};

// Settings
export const settingsApi = {
  getAll: () =>
    api.get<ApiResponse<Record<string, string>>>('/settings').then(unwrap),
  get: (key: string) =>
    api.get<ApiResponse<{ key: string; value: string }>>(`/settings/${key}`).then(unwrap),
  set: (key: string, value: string) =>
    api.put<ApiResponse<void>>(`/settings/${key}`, { value }).then(unwrap),
};
