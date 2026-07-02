---
name: gateway-endpoint
description: "在 Tensu 实现或扩展网关代理端点（OpenAI 兼容 /v1/chat/completions、Anthropic 兼容 /v1/messages），透传不转换，支持非流式与流式（SSE），并落实代理链路、扩展响应头与审计。用于新增或修改对外代理转发能力。"
argument-hint: "协议（openai / anthropic）与要实现的能力"
---

# 实现/扩展网关代理端点

在 Tensu 后端实现对外代理转发，保证与官方 SDK 完全兼容。

## 先读约定

- 后端约定：[backend.instructions.md](../../instructions/backend.instructions.md)
- 需求：[docs/PRD.md](../../../docs/PRD.md)（网关 §3.2，API §5.1，性能 §4.2）

## 核心原则

- **透传不转换**：请求/响应完全兼容官方规范，中间层只做路由、压缩、缓存、限流、审计。
- **端点**：OpenAI `POST /v1/chat/completions`；Anthropic `POST /v1/messages`；认证 `Authorization: Bearer {平台API Key}`。
- **代理链路顺序**：认证 → 路由 → 压缩 → 缓存 → 转发 → 审计。

## 实现步骤

1. **认证**：校验平台 API Key，解析所属组织/用户与可访问模型范围。
2. **路由**：解析目标模型（含路由模型的影子/路由模式），确定上游供应商与 Key（负载均衡 + 故障转移）。
3. **压缩**（可选）：CCR 可逆压缩，仅在能完整还原且不改语义时应用，记录压缩前/后 token。
4. **缓存**：按 model + messages hash + parameters hash 查缓存；命中返回并标记 `X-Cache: HIT`。
5. **转发**：
   - 非流式：转发并回传响应。
   - 流式（SSE）：边转发边聚合分块；TTFT 取首个内容分块到达时间；客户端中断标记 `interrupted` 并记录已产出 token。
6. **扩展响应头**：`X-Request-Id`、`X-Cache`、`X-Route-Model`、`X-Upstream-Provider`。
7. **审计**：请求级记录经 `Channel<T>` 异步落库（字段见 PRD §3.6.1），不阻塞热路径。

## 错误与限流

- 上游错误经映射表统一为平台错误码，响应携带 `request_id`。
- 限流超限返回标准 `429` + `Retry-After`。
- 仅对幂等请求（非流式、无副作用）执行重试（指数退避 + 抖动），重试切换其他可用 Key/供应商。

## 验收

- 用官方 OpenAI / Anthropic SDK 无改动直连，非流式与流式结果与直连上游一致。
- 转发延迟满足 PRD §4.2 目标（未启用压缩 P99 < 10ms）。
- 补充集成测试（Mock 上游）覆盖完整链路。
