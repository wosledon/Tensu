# Tensu — AI 编码代理指南

Tensu 是企业级大模型网关平台：统一 LLM API 接入、供应商管理、智能路由、上下文压缩、用量审计。业务方通过单一端点访问所有供应商模型。

**权威文档（改动前先读，勿在此复制其内容）：**
- 产品需求与架构：[docs/PRD.md](docs/PRD.md)
- 前端设计规范（视觉/交互/主题/组件）：[design/前端设计规范.md](design/前端设计规范.md)
- 项目概览：[README.md](README.md)

## 当前状态

项目处于初始阶段：[src/](src/)、[tests/](tests/)、[frontend/](frontend/) 均为空，[Tensu.slnx](Tensu.slnx) 尚无项目引用。搭建脚手架时按下述技术栈与约定进行，并同步更新本文件的构建/测试命令。

## 技术栈

| 层次     | 选型                                                                   |
| -------- | ---------------------------------------------------------------------- |
| 后端     | ASP.NET Core 10 Web API                                                |
| ORM      | Entity Framework Core                                                  |
| 数据库   | 开发用 SQLite，生产用 PostgreSQL                                       |
| 异步处理 | `Channel<T>`（内部消息管道，用于审计落库等）                           |
| 缓存     | 单机 `MemoryCache`；多点部署以数据库作共享计数后端（**不引入 Redis**） |
| 前端     | React + Vite + TypeScript + Ant Design v5                              |
| 图表     | ECharts（`echarts-for-react`）                                         |
| 国际化   | `react-i18next`（首期 zh-CN / en-US）                                  |

## 架构约定

- **网关透传，不做协议转换**：OpenAI 与 Anthropic 协议各自透传（`POST /v1/chat/completions`、`POST /v1/messages`），请求/响应完全兼容官方 SDK，中间层只做路由、压缩、缓存、限流、审计。
- **代理链路顺序**：认证 → 路由 → 压缩 → 缓存 → 转发 → 审计。
- **上下文压缩必须可逆（CCR）**：仅在能完整还原且不改变语义时应用，否则透传原文；每次记录压缩前/后 token 数。
- **审计异步落库**：请求级记录经 `Channel<T>` 异步写入，避免阻塞代理转发热路径（延迟目标见 PRD §4.2）。
- **无状态设计**：单实例无状态、可水平扩展；限流/配额/并发等共享计数走数据库后端。
- **敏感数据加密**：所有 API Key、供应商密钥加密存储（AES-256 等效）；审计对话内容按组织配置脱敏。

## API 约定

- 对外代理端点前缀 `/v1/`；管理端 RESTful 接口前缀 `/api/admin/`，JWT 认证。
- 平台扩展响应头：`X-Request-Id`、`X-Cache`(HIT/MISS)、`X-Route-Model`、`X-Upstream-Provider`。
- 管理接口统一响应格式（勿偏离）：
  ```json
  { "code": 0, "message": "success", "data": {} }
  ```
  分页数据放在 `data.{items,total,page,pageSize}`；错误用非零 `code` + `message`，`data: null`。
- 限流超限返回标准 `429` + `Retry-After` 头。

## 前端约定（详见设计规范）

- 视觉基调：苹果风格（毛玻璃 Vibrancy、连续圆角、柔和景深）；圆角基准 10px，卡片 18px。
- **禁止硬编码颜色**：全部走 Ant Design theme token / CSS 变量；提供浅色/深色/跟随系统三套主题。
- **禁止硬编码可见文案**：全部走 i18n key，命名 `模块.子模块.含义`（如 `provider.form.apiKeyRequired`）。
- 数值/token/成本/密钥/request_id 使用等宽字体；表格数值列右对齐。
- 长列表用服务端分页 + 后端排序，禁止一次性加载全量。
- 危险操作（删除、吊销）统一二次确认（`Popconfirm` / `Modal.confirm`）。

## 里程碑优先级

按 PRD §6：P0 供应商管理 + 基础网关代理 + JWT 认证 → P1 负载均衡/限流/重试/基础审计 → P2 路由模型/压缩/RBAC → P3 缓存/统计/组织架构 → P4 健康检查/密钥轮换/可观测性。

## 构建与测试

<!-- 脚手架搭建后补充实际命令，例如： -->
<!-- 后端：dotnet build Tensu.slnx / dotnet test -->
<!-- 前端：cd frontend && npm install && npm run dev / npm run build / npm run lint -->
尚未搭建脚手架，暂无构建/测试命令。
