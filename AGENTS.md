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
| **SettingsService.SetAsync 校验 key 格式** | `SettingsService.cs` | key 必须匹配 `^[A-Za-z][A-Za-z0-9._-]{0,99}$`，value ≤ 4000 字符；支持任意自定义 key |
| **流式重试仅限首字节前** | `GatewayController` | 流式请求在向客户端写入任何字节前失败会重试并故障转移；流中段失败标记 `interrupted` 不重试 |
| **语义缓存是本地特征哈希** | `CacheService.GenerateEmbedding` | 256 维 FNV 特征哈希（unigram+bigram，CJK 单字），非真实 embedding 模型；旧 100 维条目自动失效 |
| **审计内容加密开关** | `audit.encryptContent` | 开启后新写入的 RequestContent/ResponseContent 以 `enc:v1:` 前缀 AES 加密，读取自动解密；历史明文兼容 |
| **模型自动评估会真实调用上游** | `ModelCapabilityService` | Latency/Throughput/CostEfficiency 走历史日志；其余维度发送真实 LLM 探测请求（产生少量上游计费） |
| **缓存后端可配置** | `cache.backend` = `memory`(默认)/`database` | database 模式走 `exact_cache_entries` 表，多实例共享；配置 60s 热加载 |
| **缓存/压缩均有全局+组织+模型三级开关** | `IsCacheEnabledAsync`/`IsCompressionEnabledAsync` | 任一级关闭即对该请求停用；`Organization.CacheEnabled`/`Model.CacheEnabled` 默认 true |
| **压缩策略不止 minify** | `CompressionService.CompressAsync` | dedup（重复消息/段落）+ json-keys（内容内 JSON 短化 key）+ json-structure + log-template；策略名以 `+` 组合，还原靠 compression_mappings 存原文 |
| **成本多币种** | `AnalyticsService` | `currency.rate.*`=每美元兑换数；log 路径按 `RequestLog.Currency` 折算到 `currency.default`，DailyStat 聚合路径无币种字段不折算 |

## 已完成但需注意的缺口

- `OAuthProvidersController` 后端 CRUD 完整，前端管理页面已补充（路由 `/oauth-providers`）
- `POST /api/admin/compression/restore` 后端存在，前端页面已补充（路由 `/compression/restore`）
- 货币汇率管理已通过 Settings 页面集成（`currency.*` keys），`ModelPricing.ExchangeRate` 字段已存在但无自动换算逻辑
- Webhook 投递已加入重试队列（`WebhookDelivery` 实体 + `NotificationService` 自动重试失败投递）
- 告警规则配置页面已补充（路由 `/alert-rules`，CRUD 端点 `/api/admin/alert-rules`）
- 性能目标（PRD §4.2 P99 < 10ms/30ms）通过 `/metrics` 的 `tensu_latency_percentile_ms` gauge（P50/P95/P99）观测，无内置负载测试环境

## 技术栈速查

| 层次 | 版本/选型 |
|------|-----------|
| 后端 | .NET 10 / EF Core / SQLite(dev) PostgreSQL(prod) |
| 前端 | React 19 / Vite 8 / TS / AntD v6 / ECharts |
| 测试 | xunit 2.9 / Moq / WebApplicationFactory / coverlet |
| 容器 | Docker Compose，单容器 :5000，SQLite 数据卷挂载 |

## 权威文档

改动前先读：`docs/PRD.md`、`design/前端设计规范.md`
