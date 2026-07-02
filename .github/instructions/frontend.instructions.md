---
description: "Tensu 前端开发约定（React + Vite + TS + Ant Design v5 + ECharts）。编写或修改 frontend 目录下代码时应用。"
applyTo: "frontend/**"
---

# Tensu 前端约定

完整规范见 [design/前端设计规范.md](../../design/前端设计规范.md)，以下为编码时必须遵守的硬约束。

## 主题与颜色

- **禁止硬编码颜色值**。所有颜色走 Ant Design v5 `theme` token 或 CSS 变量。
- 通过 `ConfigProvider` + `theme` 提供三套模式：浅色（`theme.defaultAlgorithm`）、深色（`theme.darkAlgorithm`）、跟随系统（监听 `window.matchMedia('(prefers-color-scheme: dark)')`）。
- 用户主题偏好持久化到 `localStorage`。
- 主色浅色 `#007AFF` / 深色 `#0A84FF`；`borderRadius: 10`，`borderRadiusLG: 18`。
- ECharts 维护浅/深两套主题，随全局主题联动切换。

## 国际化

- **禁止硬编码任何可见文案**，全部走 `react-i18next` 的 i18n key。
- key 命名遵循 `模块.子模块.含义`，如 `provider.form.apiKeyRequired`。
- 首期语言包 zh-CN / en-US，默认跟随浏览器语言，可手动切换并持久化。
- Ant Design 组件语言通过 `ConfigProvider locale` 同步；日期/数字/货币用 `Intl` 或 `dayjs` 本地化。

## 组件与交互

- 数值、token、成本、密钥、request_id 使用等宽字体；表格数值列**右对齐**。
- 表格默认服务端分页（20 条，可选 10/20/50/100）+ 后端排序，**禁止一次性加载全量**。
- 危险操作（删除、吊销、重置）统一 `Popconfirm` 或 `Modal.confirm` 二次确认，使用 danger 样式。
- 密钥类输入默认掩码，提供「显示 / 复制」按钮。
- 表单采用垂直标签布局（label 在上），必填标红星，校验即时反馈。
- 每屏 Primary 按钮 ≤ 1 个；行内操作超过 3 个收进「更多」下拉。
- 状态展示用「圆点 + 文案」组合，不单独依赖颜色（无障碍）。

## 反馈组件选型

- 轻量提示 `message`；重要通知 `notification`；页面级错误 `Result`；首屏 `Skeleton`、局部 `Spin`。

## 管理接口对接

- 统一响应格式：`{ code, message, data }`，`code === 0` 为成功；分页数据在 `data.{items,total,page,pageSize}`。
- 管理端接口前缀 `/api/admin/`，请求头带 JWT。
- 尊重 `prefers-reduced-motion` 与 `prefers-reduced-transparency`，降级动效与毛玻璃材质。
