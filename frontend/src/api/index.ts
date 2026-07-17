import axios from 'axios';
import type { ApiResponse, PagedResult, PagedRequest, Provider, Model, ModelCapability, ModelCapabilityMatrix, Organization, User, ApiKey, ApiKeyUsageStat, RouteModel, RouteModelTarget, RequestLog, ArchivedRequestLog, LoginResponse, CurrentUser, ProviderKey, ModelPricing, RouteRule, Quota, OAuthProvider, AnomalyResult, AdminAuditLog, WebhookNotification, AlertRule, WebhookDelivery } from '../types';
import { getApiErrorMessage } from './errorHandler';

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
      return Promise.reject(new Error('Session expired'));
    }

    const message = getApiErrorMessage(error, 'Request failed');
    return Promise.reject(new Error(message));
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
  oauthProviders: () => api.get<ApiResponse<OAuthProvider[]>>('/auth/oauth/providers').then(unwrap),
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
  batchDelete: (ids: number[]) =>
    api.delete<ApiResponse<object>>('/providers/batch', { data: { ids } }).then(unwrap),
  batchEnable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/providers/batch/enable', ids).then(unwrap),
  batchDisable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/providers/batch/disable', ids).then(unwrap),
  addKey: (providerId: number, data: Partial<ProviderKey>) =>
    api.post<ApiResponse<ProviderKey>>(`/providers/${providerId}/keys`, data).then(unwrap),
  updateKey: (providerId: number, keyId: number, data: Partial<ProviderKey>) =>
    api.put<ApiResponse<ProviderKey>>(`/providers/${providerId}/keys/${keyId}`, data).then(unwrap),
  deleteKey: (providerId: number, keyId: number) =>
    api.delete<ApiResponse<void>>(`/providers/${providerId}/keys/${keyId}`).then(unwrap),
  rotateKey: (providerId: number, keyId: number) =>
    api.post<ApiResponse<{ id: number; name: string; keyValue: string }>>(`/providers/${providerId}/keys/${keyId}/rotate`).then(unwrap),
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
  batchDelete: (ids: number[]) =>
    api.delete<ApiResponse<object>>('/models/batch', { data: { ids } }).then(unwrap),
  batchEnable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/models/batch/enable', ids).then(unwrap),
  batchDisable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/models/batch/disable', ids).then(unwrap),
  addPricing: (modelId: number, data: Partial<ModelPricing>) =>
    api.post<ApiResponse<ModelPricing>>(`/models/${modelId}/pricing`, data).then(unwrap),
  syncFromProvider: (providerId: number) =>
    api.post<ApiResponse<{ added: number; existing: number; total: number }>>(`/models/sync/${providerId}`).then(unwrap),
  allEnabled: () =>
    api.get<ApiResponse<Model[]>>('/models/all-enabled').then(unwrap),
};

// Model Capabilities
export const modelCapabilityApi = {
  list: (params: PagedRequest & { modelId?: number; dimension?: string }) =>
    api.get<ApiResponse<PagedResult<ModelCapability>>>('/model-capabilities', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<ModelCapability>>(`/model-capabilities/${id}`).then(unwrap),
  create: (data: Partial<ModelCapability>) =>
    api.post<ApiResponse<ModelCapability>>('/model-capabilities', data).then(unwrap),
  update: (id: number, data: Partial<ModelCapability>) =>
    api.put<ApiResponse<ModelCapability>>(`/model-capabilities/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/model-capabilities/${id}`).then(unwrap),
  import: (data: Partial<ModelCapability>[]) =>
    api.post<ApiResponse<ModelCapability[]>>('/model-capabilities/import', data).then(unwrap),
  matrix: (providerId?: number) =>
    api.get<ApiResponse<ModelCapabilityMatrix>>('/model-capabilities/matrix', { params: { providerId } }).then(unwrap),
  autoEvaluate: (modelId: number, dimensions: string[]) =>
    api.post<ApiResponse<{ modelId: number; modelName: string; results: { dimension: string; score?: number; message: string }[] }>>(`/model-capabilities/${modelId}/auto-evaluate`, dimensions).then(unwrap),
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
  enable: (id: number) =>
    api.post<ApiResponse<void>>(`/api-keys/${id}/enable`).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/api-keys/${id}`).then(unwrap),
  batchDelete: (ids: number[]) =>
    api.delete<ApiResponse<object>>('/api-keys/batch', { data: { ids } }).then(unwrap),
  batchRevoke: (ids: number[]) =>
    api.post<ApiResponse<object>>('/api-keys/batch/revoke', ids).then(unwrap),
  reveal: (id: number) =>
    api.post<ApiResponse<{ key: string }>>(`/api-keys/${id}/reveal`).then(unwrap),
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
  batchDelete: (ids: number[]) =>
    api.delete<ApiResponse<object>>('/route-models/batch', { data: { ids } }).then(unwrap),
  batchEnable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/route-models/batch/enable', ids).then(unwrap),
  batchDisable: (ids: number[]) =>
    api.post<ApiResponse<object>>('/route-models/batch/disable', ids).then(unwrap),
  // Targets
  addTarget: (routeModelId: number, modelId: number) =>
    api.post<ApiResponse<RouteModelTarget>>(`/route-models/${routeModelId}/targets`, { modelId }).then(unwrap),
  removeTarget: (routeModelId: number, targetId: number) =>
    api.delete<ApiResponse<void>>(`/route-models/${routeModelId}/targets/${targetId}`).then(unwrap),
  setActiveTarget: (routeModelId: number, targetId: number) =>
    api.post<ApiResponse<RouteModelTarget[]>>(`/route-models/${routeModelId}/targets/active`, { targetId }).then(unwrap),
  updateTargetPriority: (routeModelId: number, targetId: number, priority: number) =>
    api.put<ApiResponse<void>>(`/route-models/${routeModelId}/targets/priority`, { targetId, priority }).then(unwrap),
  // Rules
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
  listArchived: (params: PagedRequest & { modelName?: string; orgId?: number }) =>
    api.get<ApiResponse<PagedResult<ArchivedRequestLog>>>('/audit/archived', { params }).then(unwrap),
  getArchived: (requestId: string) =>
    api.get<ApiResponse<ArchivedRequestLog>>(`/audit/archived/${requestId}`).then(unwrap),
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
  byApiKey: (from: string, to: string, orgId?: number) =>
    api.get<ApiResponse<{ items: ApiKeyUsageStat[] }>>('/analytics/by-api-key', { params: { from, to, orgId } }).then(unwrap),
  anomalies: (from: string, to: string, orgId?: number, severity?: string, type?: string, page = 1, pageSize = 20) =>
    api.get<ApiResponse<{ items: AnomalyResult[]; total: number; page: number; pageSize: number }>>('/analytics/anomalies', { params: { from, to, orgId, severity, type, page, pageSize } }).then(unwrap),
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

// Quotas
export const quotaApi = {
  list: (params: PagedRequest & { scope?: string; orgId?: number }) =>
    api.get<ApiResponse<PagedResult<Quota>>>('/quotas', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<Quota>>(`/quotas/${id}`).then(unwrap),
  create: (data: Partial<Quota>) =>
    api.post<ApiResponse<Quota>>('/quotas', data).then(unwrap),
  update: (id: number, data: Partial<Quota>) =>
    api.put<ApiResponse<Quota>>(`/quotas/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/quotas/${id}`).then(unwrap),
};

// Admin Audit
export const adminAuditApi = {
  list: (params: PagedRequest & { action?: string; entityType?: string }) =>
    api.get<ApiResponse<PagedResult<AdminAuditLog>>>('/admin-audit', { params }).then(unwrap),
};

// Webhooks
export const webhookApi = {
  list: (params: PagedRequest) =>
    api.get<ApiResponse<PagedResult<WebhookNotification>>>('/webhooks', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<WebhookNotification>>(`/webhooks/${id}`).then(unwrap),
  create: (data: Partial<WebhookNotification>) =>
    api.post<ApiResponse<WebhookNotification>>('/webhooks', data).then(unwrap),
  update: (id: number, data: Partial<WebhookNotification>) =>
    api.put<ApiResponse<WebhookNotification>>(`/webhooks/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/webhooks/${id}`).then(unwrap),
};

// OAuth Providers
export const oauthProviderApi = {
  list: (params: PagedRequest) =>
    api.get<ApiResponse<PagedResult<OAuthProvider>>>('/oauth-providers', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<OAuthProvider>>(`/oauth-providers/${id}`).then(unwrap),
  create: (data: Partial<OAuthProvider>) =>
    api.post<ApiResponse<OAuthProvider>>('/oauth-providers', data).then(unwrap),
  update: (id: number, data: Partial<OAuthProvider>) =>
    api.put<ApiResponse<OAuthProvider>>(`/oauth-providers/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/oauth-providers/${id}`).then(unwrap),
};

// Compression
export const compressionApi = {
  mappings: (params: { requestId?: string; page?: number; pageSize?: number }) =>
    api.get<ApiResponse<{ items: any[]; total: number; page: number; pageSize: number }>>('/compression/mappings', { params }).then(unwrap),
  getMapping: (decompressionKey: string) =>
    api.get<ApiResponse<any>>(`/compression/mappings/${decompressionKey}`).then(unwrap),
  restore: (decompressionKey: string) =>
    api.post<ApiResponse<{ originalBody: string }>>('/compression/restore', { decompressionKey }).then(unwrap),
  deleteMapping: (decompressionKey: string) =>
    api.delete<ApiResponse<void>>(`/compression/mappings/${decompressionKey}`).then(unwrap),
};

// Webhook Deliveries
export const webhookDeliveryApi = {
  list: (params: PagedRequest & { webhookId?: number; success?: boolean }) =>
    api.get<ApiResponse<PagedResult<WebhookDelivery>>>('/webhook-deliveries', { params }).then(unwrap),
};

// Alert Rules
export const alertRuleApi = {
  list: (params: PagedRequest) =>
    api.get<ApiResponse<PagedResult<AlertRule>>>('/alert-rules', { params }).then(unwrap),
  get: (id: number) =>
    api.get<ApiResponse<AlertRule>>(`/alert-rules/${id}`).then(unwrap),
  create: (data: Partial<AlertRule>) =>
    api.post<ApiResponse<AlertRule>>('/alert-rules', data).then(unwrap),
  update: (id: number, data: Partial<AlertRule>) =>
    api.put<ApiResponse<AlertRule>>(`/alert-rules/${id}`, data).then(unwrap),
  delete: (id: number) =>
    api.delete<ApiResponse<void>>(`/alert-rules/${id}`).then(unwrap),
};
