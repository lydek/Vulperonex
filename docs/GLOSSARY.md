# Ubiquitous Language & Glossary: Vulperonex

This document defines the core domain terms for Vulperonex. To maintain conceptual integrity and avoid discrepancies between English codebases, API payloads, localization (i18n) resources, and Chinese documentations, all developers and translators must strictly adhere to the mappings below.

---

## Core Domain Terms

### 1. Member / 會員
* **English Term**: `Member` / `MemberRecord`
* **Chinese Term**: `會員`
* **Definition**: A user registered in the system with a unique cross-platform identity, capable of accumulating loyalty points and check-in milestones.
* **Code Type**: `Vulperonex.Domain.Members.MemberRecord`

### 2. Platform Identity / 平台身分
* **English Term**: `PlatformIdentity`
* **Chinese Term**: `平台身分`
* **Definition**: A unique account binding of a Member on a specific streaming platform (e.g. Twitch, YouTube), consisting of `Platform` and `PlatformUserId`.
* **Code Type**: `Vulperonex.Domain.Members.PlatformIdentity`

### 3. Workflow Rule / 工作流規則
* **English Term**: `WorkflowRule` / `Rule`
* **Chinese Term**: `工作流規則` / `規則`
* **Definition**: The fundamental unit of automation, consisting of a Trigger, multiple Execution Conditions, and a sequence of Actions.
* **Code Type**: `Vulperonex.Domain.Workflows.WorkflowRule`

### 4. Trigger / 觸發器
* **English Term**: `WorkflowTrigger` / `Trigger`
* **Chinese Term**: `觸發器`
* **Definition**: The starting point of a Workflow Rule, which subscribes to a specific `EventTypeKey` (e.g., `user.message`).
* **Code Type**: `Vulperonex.Domain.Workflows.WorkflowTrigger`

### 5. Execution Condition / 執行條件
* **English Term**: `ExecutionCondition` / `Condition`
* **Chinese Term**: `執行條件` / `前置條件`
* **Definition**: Filter logic evaluated before Actions execute (e.g., Cooldown, UserRole, MessageContent).
* **Code Type**: `Vulperonex.Domain.Workflows.Conditions.ExecutionCondition`

### 6. Workflow Action / 工作流動作
* **English Term**: `WorkflowAction` / `Action`
* **Chinese Term**: `工作流動作` / `動作`
* **Definition**: A specific task executed after a rule is triggered and conditions pass (e.g., SendChatMessage, InvokeSubWorkflow).
* **Code Type**: `Vulperonex.Domain.Workflows.Actions.WorkflowAction`

### 7. Simulation / 模擬
* **English Term**: `Simulation` / `Simulate`
* **Chinese Term**: `模擬` / `模擬器`
* **Definition**: The subsystem and endpoints used to mock platform events in local development and testing environments.
* **Code Type**: `Vulperonex.Adapters.Simulation.SimulationAdapter`

### 8. Overlay / 疊加幕
* **English Term**: `Overlay`
* **Chinese Term**: `疊加幕` / `版面`
* **Definition**: The web-based rendering view loaded by OBS or other streaming software (e.g., chat room overlay, member card overlay, alerts overlay).
* **Code Type**: `Vulperonex.Application.Overlay.OverlayModule`

### 9. Preset / 預設配置
* **English Term**: `Preset` / `Template`
* **Chinese Term**: `預設配置` / `樣板`
* **Definition**: The default style layout for overlay views, categorized into built-in Vue Presets and uploaded Custom HTML Presets.
* **Code Type**: `Vulperonex.Application.Overlay.Dtos.ChatOverlayPreset`

### 10. Transient Delivery Queue (TDQ) / 瞬態遞送佇列
* **English Term**: `TransientDeliveryQueue` / `TDQ`
* **Chinese Term**: `瞬態遞送佇列` / `瞬態佇列`
* **Definition**: An SQLite-backed transient queue that handles event bus overflows to guarantee at-least-once event delivery through automatic startup replays.
* **Code Type**: `Vulperonex.Infrastructure.EventBus.TransientDeliveryQueue`

### 11. Deduplication (Dedup) / 重複抑制
* **English Term**: `Deduplication` / `Dedup`
* **Chinese Term**: `重複抑制` / `去重`
* **Definition**: A safety guard mechanism utilizing `ActionExecutionLog` to prevent duplicate action executions for the same event.
* **Code Type**: `Vulperonex.Infrastructure.EventBus.ActionExecutionLog`

### 12. Audit Log / 稽核日誌
* **English Term**: `AuditLog` / `MemberAuditLog`
* **Chinese Term**: `稽核日誌`
* **Definition**: An append-only historical database table recording manual adjustments or automated workflow changes made to Member data.
* **Code Type**: `Vulperonex.Infrastructure.Members.MemberAuditLog`

### 13. Loyalty / 忠誠點數
* **English Term**: `Loyalty` / `LoyaltyInfo`
* **Chinese Term**: `忠誠度` / `忠誠點數`
* **Definition**: Points and check-in counts accumulated by a Member through interactive activities.
* **Code Type**: `Vulperonex.Domain.Members.LoyaltyInfo`

### 14. Check-In / 打卡簽到
* **English Term**: `Check-In` / `CheckIn`
* **Chinese Term**: `打卡` / `簽到`
* **Definition**: The check-in or stamp-collecting interactive behavior performed by a Member during live streams.
* **Code Type**: `Vulperonex.Domain.Workflows.Executors.TriggerCheckInActionExecutor`

---

## Translation Alignment Matrix

| Context / Tier | English Term (Code/API) | zh-TW Translation | Avoid Using |
| --- | --- | --- | --- |
| Domain Layer | `Member` | `會員` | `成員` (inconsistent) |
| Domain Layer | `PlatformIdentity` | `平台身分` | `平台帳號` (unprofessional) |
| UI & API | `Overlay` | `疊加幕` | `覆蓋層` / `OBS畫面` (informal) |
| UI & API | `Preset` | `預設配置` | `預設樣式` / `主題` (imprecise) |
| UI & API | `Check-In` | `打卡` | `登入` / `報到` (ambiguous) |
| UI & API | `Loyalty` | `忠誠度` | `積分` / `分數` (not DDD) |
| System | `Transient Delivery Queue` | `瞬態遞送佇列` | `臨時佇列` (vague) |
| System | `Deduplication` | `重複抑制` | `去重` (slang) |
