# Tensu — Agent Guide

## Commands

```bash
dotnet build Tensu.slnx          # 注意不是 .sln
dotnet test Tensu.slnx
dotnet run --project src/Tensu.Api

cd frontend
npm run dev        # :3000，代理到后端 :5000
npm run build      # 同时执行 tsc -b && vite build
npm run lint       # oxlint（非 eslint）
```

## Repo structure

```
src/Tensu.Core/     — 实体、枚举、通用类型
src/Tensu.Api/      — Web API（Program.cs 单文件启动，控制器 + 服务两层）
frontend/src/       — React 19 + Vite 8 + Ant Design v6
tests/Tensu.Tests/  — xunit + Moq + WebApplicationFactory + coverlet
```

## Backend 关键约定

- **启动即自动迁移 + 种子数据**：`Program.cs:124-159`，开发环境第一次启动自动建库并创建 `admin/admin123`
- **JWT / Encryption key 未配置时使用不安全默认值**：生产必须设置 `Jwt:Key`（≥32 chars）与 `Encryption:Key`（32 bytes）
- **管理接口统一响应**：`{"code": 0, "message": "success", "data": {}}`；分页在 `data.items/total/page/pageSize`，非零 `code` 表示错误
- **平台响应头**：`X-Request-Id`, `X-Cache`(HIT/MISS), `X-Route-Model`, `X-Upstream-Provider`
- **Admin Controller 基类**：继承 `AdminBaseController` 获得 `CurrentUserId/CurrentOrgId/IsSuperAdmin` 等快捷属性
- **审计异步落库**：请求级审计走 `AuditChannel` + `AuditBackgroundService`，不得在代理热路径同步写库
- **无 Redis**：限流/配额/并发计数直接走数据库（EF Core 单条 UPDATE/COUNT 原子操作）
- **敏感字段加密**：`EncryptionService`（AES-256 等效）用于 ProviderKey.ApiKeyValue 等字段

## 前端关键约定

- **页面全部懒加载**：`App.tsx` 用 `React.lazy` + `Suspense`，新增页面必须在此注册路由
- **路由守卫**：`RequireAuth` + `RequireRole`，SuperAdmin/Admin 才能访问管理页
- **Token 存储**：`localStorage.token`，拦截器自动附带 Bearer；401 自动跳 `/login`
- **i18n 命名**：`模块.子模块.含义`（如 `provider.form.apiKeyRequired`），禁止硬编码可见文案
- **数值列右对齐、request_id/token 等用等宽字体**
- **禁止硬编码颜色**：全部走 Ant Design theme token / CSS 变量

## 已知陷阱（务必注意）

| 问题 | 位置 | 说明 |
|------|------|------|
| **SettingsService.SetAsync 限制 key** | `SettingsService.cs:85` | 只允许写入 `Defaults` 字典中已有的 key，前端 SettingsPage 尝试写入未知 key 时会抛异常 |
| **QuotasPage 的 `model` scope 后端不支持** | `QuotaService` 只按 ApiKeyId 过滤 | 前端下拉选了也不会生效 |
| **ModelCapabilitiesPage dimension 筛选参数丢失** | 前端 filter state 只跟踪 modelId，不跟踪 dimension | 实际传参未生效 |
| **语义缓存是词袋模型** | `CacheService.GenerateEmbedding` | 100 维词频向量，非真实 embedding |
| **流式请求不重试** | `GatewayController` | 仅非流式走 RetryPolicy，流式直接透传 |
| **模型自动评估未调用 LLM** | `ModelCapabilityService.RunSyntheticEvaluationAsync` | 只基于历史日志计算指标，未调用 LLM 做真实评估 |

## 已完成但需注意的缺口

- `OAuthProvidersController` 后端 CRUD 完整，前端管理页面已补充（路由 `/oauth-providers`）
- `POST /api/admin/compression/restore` 后端存在，前端页面已补充（路由 `/compression/restore`）
- 货币汇率管理已通过 Settings 页面集成（`currency.*` keys），`ModelPricing.ExchangeRate` 字段已存在但无自动换算逻辑
- Webhook 投递已加入重试队列（`WebhookDelivery` 实体 + `NotificationService` 自动重试失败投递）
- 告警规则配置页面已补充（路由 `/alert-rules`，CRUD 端点 `/api/admin/alert-rules`）

## 技术栈速查

| 层次 | 版本/选型 |
|------|-----------|
| 后端 | .NET 10 / EF Core / SQLite(dev) PostgreSQL(prod) |
| 前端 | React 19 / Vite 8 / TS / AntD v6 / ECharts |
| 测试 | xunit 2.9 / Moq / WebApplicationFactory / coverlet |
| 容器 | Docker Compose，单容器 :5000，SQLite 数据卷挂载 |

## 权威文档

改动前先读：`docs/PRD.md`、`design/前端设计规范.md`
