export interface ApiResponse<T> {
  code: number;
  message: string;
  data: T;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PagedRequest {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: string;
  keyword?: string;
}

export interface Provider {
  id: number;
  name: string;
  protocol: 'OpenAI' | 'Anthropic';
  baseUrl: string;
  description?: string;
  healthStatus: 'Unknown' | 'Healthy' | 'Degraded' | 'Unhealthy';
  isEnabled: boolean;
  keyLoadBalanceStrategy: 'RoundRobin' | 'Weighted' | 'LowestLatency';
  keys: ProviderKey[];
  models: Model[];
  createdAt: string;
  updatedAt: string;
}

export interface ProviderKey {
  id: number;
  providerId: number;
  name: string;
  keyValue: string;
  weight: number;
  status: 'Active' | 'Degraded' | 'Inactive' | 'Expired' | 'Disabled';
  rateLimitRpm?: number;
  rateLimitTpm?: number;
  createdAt: string;
}

export interface Model {
  id: number;
  providerId: number;
  provider?: Provider;
  name: string;
  displayName?: string;
  description?: string;
  supportsVision: boolean;
  supportsReasoning: boolean;
  supportsToolUse: boolean;
  supportsThinking: boolean;
  thinkingStrengths?: string;
  inputContextSize: number;
  outputContextSize: number;
  isEnabled: boolean;
  compressionEnabled: boolean;
  cacheEnabled: boolean;
  pricings: ModelPricing[];
  createdAt: string;
  updatedAt: string;
}

export interface ModelPricing {
  id: number;
  modelId: number;
  inputPricePerMillionTokens: number;
  outputPricePerMillionTokens: number;
  cachedInputPricePerMillionTokens?: number;
  thinkingPricePerMillionTokens?: number;
  currency: string;
  exchangeRate: number;
  effectiveFrom: string;
  effectiveTo?: string;
}

export interface ModelCapability {
  id: number;
  modelId: number;
  model?: Model;
  dimension: string;
  score: number;
  source: string;
  evidence?: string;
  evaluatedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface ModelCapabilityMatrix {
  dimensions: string[];
  models: {
    modelId: number;
    modelName: string;
    providerName: string;
    scores: Record<string, number>;
    overallScore: number;
  }[];
}

export interface Organization {
  id: number;
  parentId?: number;
  name: string;
  path: string;
  description?: string;
  enableContentLogging: boolean;
  compressionEnabled: boolean;
  cacheEnabled: boolean;
  dataRetentionDays: number;
  children?: Organization[];
  createdAt: string;
}

export interface User {
  id: number;
  organizationId: number;
  organization?: Organization;
  username: string;
  email?: string;
  displayName?: string;
  pictureUrl?: string;
  role: 'SuperAdmin' | 'Admin' | 'Developer' | 'ReadOnly';
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  email?: string;
  displayName?: string;
  pictureUrl?: string;
  role: string;
  organizationId: number;
  organization?: string;
}

export interface OAuthProvider {
  id: number;
  name: string;
  displayName?: string;
  protocol: 'OAuth2' | 'OIDC';
  clientId: string;
  clientSecret: string;
  authorizationEndpoint?: string;
  tokenEndpoint?: string;
  userInfoEndpoint?: string;
  issuer?: string;
  scope: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ApiKey {
  id: number;
  organizationId: number;
  organization?: Organization;
  userId?: number;
  user?: User;
  name: string;
  keyValue: string;
  keyPrefix: string;
  expiresAt?: string;
  allowedModels?: string;
  ipWhitelist?: string;
  status: 'Active' | 'Disabled' | 'RateLimited' | 'Expired';
  rateLimitRpm?: number;
  rateLimitTpm?: number;
  createdAt: string;
}

export interface ApiKeyUsageStat {
  apiKeyId: number;
  name: string;
  keyPrefix?: string;
  user?: string;
  organization?: string;
  requests: number;
  successRate: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalCost: number;
}

export interface OrgUsageStat {
  organizationId: number;
  organizationName: string;
  requests: number;
  totalTokens: number;
  totalCost: number;
  cacheHits: number;
  cacheHitRate: number;
}

export interface RouteModel {
  id: number;
  name: string;
  description?: string;
  mode: 'Shadow' | 'Route';
  targets: RouteModelTarget[];
  fallbackModelId?: number;
  fallbackModel?: Model;
  routingModelId?: number;
  routingModel?: Model;
  isEnabled: boolean;
  rules: RouteRule[];
  createdAt: string;
}

export interface RouteModelTarget {
  id: number;
  routeModelId: number;
  modelId: number;
  model?: Model;
  isActive: boolean;
  priority: number;
  createdAt: string;
}

export interface Quota {
  id: number;
  scope: 'org' | 'key' | 'model';
  organizationId?: number;
  apiKeyId?: number;
  modelId?: number;
  organization?: Organization;
  model?: Model;
  rpm?: number;
  tpm?: number;
  dailyTokenLimit?: number;
  monthlyTokenLimit?: number;
  concurrentRequestLimit?: number;
  createdAt: string;
  updatedAt: string;
}

export interface RouteRule {
  id: number;
  routeModelId: number;
  type: 'Keyword' | 'Regex' | 'ContextSize';
  condition: string;
  targetModelId: number;
  targetModel?: Model;
  priority: number;
  isEnabled: boolean;
}

export interface AnomalyResult {
  type: string;
  severity: string;
  dimension: string;
  message: string;
  currentValue: number;
  baselineValue: number;
  detectedAt: string;
}

export interface RequestLog {
  id: number;
  requestId: string;
  timestamp: string;
  apiKeyId?: number;
  organizationId?: number;
  modelName: string;
  resolvedModelName?: string;
  providerName?: string;
  inputTokens?: number;
  inputTokensAfterCompression?: number;
  outputTokens?: number;
  cachedInputTokens?: number;
  reasoningTokens?: number;
  cacheHit: boolean;
  semanticCacheHit: boolean;
  timeToFirstTokenMs?: number;
  totalDurationMs?: number;
  outputTokensPerSecond?: number;
  inputCost?: number;
  outputCost?: number;
  status: 'Success' | 'Failed' | 'Timeout' | 'Interrupted' | 'RateLimited' | 'Forbidden';
  errorCode?: string;
  errorMessage?: string;
  retryCount?: number;
  isStream: boolean;
  compressionApplied: boolean;
  compressionStrategy?: string;
  compressionMappingKey?: string;
  currency?: string;
  requestContent?: string;
  responseContent?: string;
}

export interface ArchivedRequestLog {
  requestId: string;
  timestamp: string;
  apiKeyId?: number;
  organizationId?: number;
  userId?: number;
  modelName: string;
  resolvedModelName?: string;
  providerName?: string;
  inputTokens?: number;
  inputTokensAfterCompression?: number;
  outputTokens?: number;
  cachedInputTokens?: number;
  reasoningTokens?: number;
  cacheHit: boolean;
  semanticCacheHit?: boolean;
  timeToFirstTokenMs?: number;
  totalDurationMs?: number;
  outputTokensPerSecond?: number;
  inputCost?: number;
  outputCost?: number;
  currency?: string;
  compressionApplied: boolean;
  compressionStrategy?: string;
  status: 'Success' | 'Failed' | 'Timeout' | 'Interrupted' | 'RateLimited' | 'Forbidden';
  errorCode?: string;
  errorMessage?: string;
  retryCount?: number;
  isStream: boolean;
  requestContent?: string;
  responseContent?: string;
}

export interface AdminAuditLog {
  id: number;
  timestamp: string;
  userId: number;
  username: string;
  action: string;
  entityType: string;
  entityId: string;
  details: string;
  ipAddress?: string;
  userAgent?: string;
}

export interface WebhookNotification {
  id: number;
  name: string;
  url: string;
  secret?: string;
  isEnabled: boolean;
  events: string;
  createdAt: string;
  updatedAt: string;
}

export interface AlertRule {
  id: number;
  name: string;
  eventType: string;
  severity?: string;
  isEnabled: boolean;
  webhookIds: string;
  createdAt: string;
  updatedAt: string;
}

export interface WebhookDelivery {
  id: number;
  webhookNotificationId: number;
  eventType: string;
  payload: string;
  attemptCount: number;
  maxAttempts: number;
  lastStatusCode?: string;
  lastErrorMessage?: string;
  nextRetryAt?: string;
  createdAt: string;
  lastAttemptAt?: string;
  isSuccess: boolean;
}

export interface DataDeletionRequest {
  id: number;
  organizationId?: number;
  userId?: number;
  apiKeyId?: string;
  reason: string;
  status: string;
  errorMessage?: string;
  createdAt: string;
  completedAt?: string;
  deletedRequestLogs: number;
  deletedArchivedLogs: number;
  requestId?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}
