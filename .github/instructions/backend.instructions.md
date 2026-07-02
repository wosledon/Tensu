---
description: "Tensu 后端开发约定（ASP.NET Core 10 + EF Core + Channel + MemoryCache）。编写或修改 src 目录下代码时应用。"
applyTo: "src/**"
---

# Tensu 后端约定

完整需求与架构见 [docs/PRD.md](../../docs/PRD.md)，以下为编码时必须遵守的硬约束。

## 网关代理

- **透传不转换**：OpenAI（`POST /v1/chat/completions`）与 Anthropic（`POST /v1/messages`）协议各自透传，请求/响应完全兼容官方 SDK。
- 支持非流式与流式（SSE）透传；流式响应在转发的同时聚合内容，请求结束后统一落库，TTFT 取首个内容分块到达时间，中途断开标记为 `interrupted`。
- 代理链路顺序固定：**认证 → 路由 → 压缩 → 缓存 → 转发 → 审计**。
- 平台扩展响应头：`X-Request-Id`、`X-Cache`(HIT/MISS)、`X-Route-Model`、`X-Upstream-Provider`。
- 转发热路径延迟敏感（P99 目标见 PRD §4.2），避免在热路径做阻塞 I/O。

## 上下文压缩（CCR）

- 压缩在转发到上游**之前**执行，且**必须可逆**：仅在能完整还原且不改变语义时应用，否则透传原文。
- 对格式敏感场景（few-shot、代码块、结构化 Prompt）保守处理或跳过。
- 每次请求记录压缩前/后 token 数与压缩策略。

## 审计

- 请求级记录经 `Channel<T>` **异步写入**，不阻塞代理转发。
- 记录字段见 PRD §3.6.1（含原始/压缩后 token、TTFT、总延迟、成本、缓存命中、状态）。
- 审计日志追加写入、不可修改/删除（防篡改）。

## 数据与缓存

- EF Core 建模参考 PRD §5.2 的实体关系；开发用 SQLite，生产用 PostgreSQL，注意两者兼容性（避免数据库特定 SQL）。
- 缓存后端可配置：单机 `MemoryCache`；多点部署以数据库作共享计数/状态后端。**不引入 Redis**。
- 限流（RPM/TPM）、配额、并发上限等共享计数走数据库后端，保证跨实例一致；服务保持无状态、可水平扩展。

## API 与响应格式

- 管理端 RESTful 接口前缀 `/api/admin/`，JWT 认证。
- 管理接口统一响应格式（**勿偏离**）：
  ```json
  { "code": 0, "message": "success", "data": {} }
  ```
  分页数据放 `data.{items,total,page,pageSize}`；错误用非零 `code` + `message`，`data: null`。
- 限流超限返回标准 `429` + `Retry-After` 头；上游错误经映射表统一为平台错误码，响应携带 `request_id`。

## 安全

- 所有 API Key、供应商密钥加密存储（AES-256 等效），禁止明文落库或记日志。
- 审计对话内容按组织配置脱敏；敏感操作（删除供应商、吊销密钥）需二次确认。
