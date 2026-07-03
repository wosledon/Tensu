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
  status: 'Active' | 'Degraded' | 'Inactive';
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

export interface RouteModel {
  id: number;
  name: string;
  description?: string;
  mode: 'Shadow' | 'Route';
  targetModelId?: number;
  targetModel?: Model;
  fallbackModelId?: number;
  fallbackModel?: Model;
  routingModelId?: number;
  routingModel?: Model;
  isEnabled: boolean;
  rules: RouteRule[];
  createdAt: string;
}

export interface Quota {
  id: number;
  scope: 'org' | 'key' | 'model';
  organizationId?: number;
  apiKeyId?: number;
  modelId?: number;
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
  cacheHit: boolean;
  timeToFirstTokenMs?: number;
  totalDurationMs?: number;
  outputTokensPerSecond?: number;
  inputCost?: number;
  outputCost?: number;
  status: 'Success' | 'Failed' | 'Timeout' | 'Interrupted' | 'RateLimited';
  errorCode?: string;
  errorMessage?: string;
  isStream: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  email?: string;
  displayName?: string;
  role: string;
  organizationId: number;
  organization?: string;
}
