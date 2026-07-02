---
name: admin-crud
description: "为 Tensu 新增一个管理端 CRUD 模块（后端 Controller/Service/EF 配置 + 前端列表/表单页 + i18n），遵循项目统一约定。用于新增供应商、模型、密钥、组织、用户等结构相似的管理资源。"
argument-hint: "实体名（英文单数，如 Provider）"
---

# 新增管理端 CRUD 模块

为一个新实体端到端创建管理端 CRUD 模块。适用于供应商、模型、API 密钥、组织、用户、路由模型等结构相似的资源。

## 使用前先确认输入

- 实体名（英文单数，如 `Provider`）
- 资源路径（复数 kebab-case，如 `providers`）
- i18n 前缀（如 `provider`）
- 关键字段（参考 [docs/PRD.md](../../../docs/PRD.md) §5.2）

## 先读约定

- 后端约定：[backend.instructions.md](../../instructions/backend.instructions.md)
- 前端约定：[frontend.instructions.md](../../instructions/frontend.instructions.md)

## 后端（src/）

1. EF Core 实体与 `DbContext` 配置（参考 PRD §5.2 字段），敏感字段（密钥类）加密存储。
2. Service 层封装业务逻辑与数据访问，Controller 保持轻薄。
3. Controller 路由前缀 `/api/admin/{资源路径}`，JWT 认证。
4. 提供接口：列表（服务端分页 + 排序）、详情、创建、更新、删除。
5. 响应统一格式 `{ code, message, data }`；分页放 `data.{items,total,page,pageSize}`；错误用非零 `code`。
6. 删除等危险操作做必要校验（如存在关联数据时拒绝或级联提示）。

## 前端（frontend/）

1. 列表页：表格服务端分页 + 后端排序，数值列右对齐 + 等宽字体；行操作含编辑、删除（`Popconfirm` 二次确认）。
2. 表单：垂直标签布局，必填标红星，即时校验；密钥类输入掩码 + 显示/复制。
3. 所有可见文案走 i18n key（`{i18n前缀}.xxx`），补充 zh-CN / en-US 语言包。
4. 所有颜色走 theme token，勿硬编码；适配浅/深色主题。
5. 在侧边导航挂载入口（参考设计规范 §6 信息架构）。

## 收尾

1. 补充/更新单元测试（Service 层逻辑、分页与校验）。
2. 若引入新的构建/测试命令，同步更新 [AGENTS.md](../../../AGENTS.md) 的「构建与测试」小节。
3. 自检：前后端字段命名、分页结构、响应格式是否一致。
