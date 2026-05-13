# 第一階段詳細計畫：方案骨架 + 領域基礎

> 父計畫：`tasks/plan.md` 第一階段
> 範圍：僅限任務 1-3
> 目標：建立一個可編譯的 .NET 方案，然後建立包含測試與架構守門的領域 (Domain) / 應用 (Application) 基礎。

---

## 執行規則

- 每個切片 (Slice) 使用一個小分支開發，然後使用 `git merge --ff-only` 合併回 `main`。
- 在開始下一個切片之前，提交 (Commit) 每個已驗證的切片。
- 未經事先詢問批准，請勿新增套件。第一階段僅允許新增 `docs/SPEC.md` 或本計畫已命名且為目前任務所需的套件；所有其他套件仍需事先詢問批准。
- 每個切片的驗證必須在執行 `--no-build` 之前先編譯目前切片。`--no-build` 僅保留給在同一任務或檢查點中明確緊隨成功編譯後的指令。
- 確保 `.claude/` 和其他僅限本地的檔案不包含在提交中。
- 對於帶有行為的程式碼，使用 BDD/TDD。僅包含骨架的專案設置可以使用編譯/引用驗證，而非行為測試。

---

## 依賴順序

```
任務 1a 儲存庫/方案配置
    -> 任務 1b 生產專案
    -> 任務 1c 測試專案
    -> 任務 1d 專案引用
    -> 任務 1e 基準編譯
        -> 任務 2a 事件契約 (Contracts)
        -> 任務 2b 具體事件
        -> 任務 2c 事件描述/測試
        -> 任務 2d 防止平台洩漏的架構規則
            -> 任務 3a 成員實體 (Entities)/值對象 (Value Objects)
            -> 任務 3b 應用層成員埠 (Ports)
            -> 任務 3c 成員領域測試
            -> 任務 3d DCI 角色隔離測試
```

---

## 任務 1a：建立方案級別的編譯配置

**描述：** 建立方案檔案和共享的 .NET 配置檔案，使所有專案繼承一致的語言版本、可為空性 (Nullable)、分析器和套件版本行為。

**驗收準則：**
- [ ] `Vulperonex.sln` 已存在。
- [ ] 共享的編譯設定已建立（C# 14 / .NET 10 並啟用 nullable）。
- [ ] 此切片中未引入任何生產或測試邏輯。

**驗證：**
- [ ] `dotnet --info` 確認 SDK 可用性。
- [ ] `dotnet sln Vulperonex.sln list` 執行成功。

**依賴：** 無

**可能涉及的檔案：**
- `Vulperonex.sln`
- `Directory.Build.props`
- `Directory.Packages.props` (如果使用中央套件管理)

**預估規模：** S (小)

---

## 任務 1b：新增生產專案骨架

**描述：** 新增核准架構中命名的所有生產專案，不包含業務邏輯。

**驗收準則：**
- [ ] 領域 (Domain)、應用 (Application)、基礎設施 (Infrastructure)、外掛程式抽象 (Plugins.Abstractions)、適配器抽象 (Adapters.Abstractions)、Twitch 適配器 (Adapters.Twitch)、模擬適配器 (Adapters.Simulation)、Web 主機、Cli 和桌面主機 (Desktop) 的 `.csproj` 檔案已存在。
- [ ] 桌面主機的目標框架為 `net10.0-windows`。
- [ ] 專案引用未違反依賴圖。

**驗證：**
- [ ] `dotnet sln Vulperonex.sln list` 顯示所有生產專案。
- [ ] `dotnet restore Vulperonex.sln /m:1 /nr:false /p:UseSharedCompilation=false` 執行成功。如果環境需要儲存庫本地的 NuGet 隔離，請在建立或確認該檔案後使用 `--configfile NuGet.Config`。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 到達編譯階段且無專案探索錯誤。

**依賴：** 任務 1a

**可能涉及的檔案：**
- `src/Vulperonex.Domain/Vulperonex.Domain.csproj`
- `src/Vulperonex.Application/Vulperonex.Application.csproj`
- `src/Vulperonex.Infrastructure/Vulperonex.Infrastructure.csproj`
- `src/Vulperonex.Plugins.Abstractions/Vulperonex.Plugins.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Abstractions/Vulperonex.Adapters.Abstractions.csproj`
- `src/Adapters/Vulperonex.Adapters.Twitch/Vulperonex.Adapters.Twitch.csproj`
- `src/Adapters/Vulperonex.Adapters.Simulation/Vulperonex.Adapters.Simulation.csproj`
- `src/Hosts/Vulperonex.Web/Vulperonex.Web.csproj`
- `src/Hosts/Vulperonex.Cli/Vulperonex.Cli.csproj`
- `src/Hosts/Vulperonex.Desktop/Vulperonex.Desktop.csproj`

**預估規模：** M (中)

---

## 任務 1c：新增測試專案骨架

**描述：** 新增三個測試專案，並為單元測試、整合測試和架構測試做好準備。

**驗收準則：**
- [ ] 單元測試、整合測試和架構測試專案已存在並包含在方案中。
- [ ] 測試套件選擇遵循 `docs/SPEC.md`；第一階段授權此任務所需的 SPEC 命名測試與覆蓋率套件（`xUnit 3`、`NSubstitute`、`FluentAssertions 7`、`NetArchTest`、`coverlet.msbuild 6.0.2`）。
- [ ] 每個測試專案僅在需要證明運行環境設置成功時才包含占位用的冒煙測試。

**驗證：**
- [ ] `dotnet restore Vulperonex.sln /m:1 /nr:false /p:UseSharedCompilation=false` 執行成功。如果環境需要儲存庫本地的 NuGet 隔離，請在建立或確認該檔案後使用 `--configfile NuGet.Config`。
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /m:1 /nr:false /p:UseSharedCompilation=false` 能夠發現測試專案。
- [ ] `dotnet test tests/Vulperonex.Tests.Integration /m:1 /nr:false /p:UseSharedCompilation=false` 能夠發現測試專案。
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false` 能夠發現測試專案。

**依賴：** 任務 1a

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Unit/Vulperonex.Tests.Unit.csproj`
- `tests/Vulperonex.Tests.Integration/Vulperonex.Tests.Integration.csproj`
- `tests/Vulperonex.Tests.Architecture/Vulperonex.Tests.Architecture.csproj`

**預估規模：** M (中)

---

## 任務 1d：串接專案引用與架構基準

**描述：** 新增允許的專案引用，以及首批強制執行依賴圖的架構測試。

**驗收準則：**
- [ ] 領域層 (Domain) 不引用任何 Vulperonex 專案。
- [ ] 應用層 (Application) 僅引用領域層。
- [ ] 基礎設施層 (Infrastructure) 引用應用層和領域層。
- [ ] 適配器 (Adapters) 和主機 (Hosts) 遵循 `tasks/plan.md` 中的依賴圖。
- [ ] 如果領域層引用了基礎設施層或平台適配器，架構測試應失敗。

**驗證：**
- [ ] `dotnet list src/Vulperonex.Domain reference` 返回無專案引用。
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false` 通過。

**依賴：** 任務 1b, 1c

**可能涉及的檔案：**
- 生產專案 `.csproj` 檔案
- 測試專案 `.csproj` 檔案
- `tests/Vulperonex.Tests.Architecture/Dependencies/LayerDependencyTests.cs`

**預估規模：** M (中)

---

## 任務 1e：驗證基準編譯與提交

**描述：** 在新增領域行為之前，建立首個綠燈編譯基準。

**驗收準則：**
- [ ] 全方案編譯通過。
- [ ] 全量測試指令執行目前所有測試。
- [ ] 在提交之前，Git 狀態僅包含預期的檔案。

**驗證：**
- [ ] `dotnet restore Vulperonex.sln --configfile NuGet.Config --ignore-failed-sources` (如果需要儲存庫本地 NuGet 配置)。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false`
- [ ] `git status --short --ignored`

**依賴：** 任務 1a-1d

**可能涉及的檔案：** 無（僅限驗證驅動的修復）

**預估規模：** S (小)

---

## 任務 2a：定義事件契約與 StreamUser

**描述：** 新增核心事件抽象和 `StreamUser` 值對象。

**驗收準則：**
- [ ] `IStreamEvent` 暴露 `EventId`、`EventTypeKey`、`OccurredAt`、`Platform` 和 `StreamUser? User`。
- [ ] `StreamUser` 包含 `Platform`、`UserId` 和 `DisplayName`。
- [ ] 事件契約類型是不可變的 (Immutable)。

**驗證：**
- [ ] 單元測試覆蓋基礎建構與不可變性預期。
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** 任務 1e

**可能涉及的檔案：**
- `src/Vulperonex.Domain/Events/IStreamEvent.cs`
- `src/Vulperonex.Domain/StreamUser.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**預估規模：** S (小)

---

## 任務 2b：實作 MVP 領域事件與鍵值

**描述：** 新增七個 MVP 事件記錄 (Records)，以及 `PlatformConnectionChangedEvent` 和規範的鍵值常數。

**驗收準則：**
- [ ] 所有七個 MVP 事件均實作 `IStreamEvent`。
- [ ] `PlatformConnectionChangedEvent` 實作 `IStreamEvent`。
- [ ] `StreamEventKeys` 包含 `docs/SPEC.md` 中所有規範的鍵值。
- [ ] `EventId` 預設為 ULID 字串。

**驗證：**
- [ ] 單元測試驗證每個事件的 `EventTypeKey`。
- [ ] 單元測試驗證預設 `EventId` 格式符合 ULID。

**依賴：** 任務 2a

**可能涉及的檔案：**
- `src/Vulperonex.Domain/Events/StreamEventKeys.cs`
- `src/Vulperonex.Domain/Events/UserSentMessageEvent.cs`
- `src/Vulperonex.Domain/Events/UserFollowedEvent.cs`
- `src/Vulperonex.Domain/Events/UserDonatedEvent.cs`
- `src/Vulperonex.Domain/Events/UserSubscribedEvent.cs`
- `src/Vulperonex.Domain/Events/UserGiftedSubscriptionEvent.cs`
- `src/Vulperonex.Domain/Events/ChannelRaidedEvent.cs`
- `src/Vulperonex.Domain/Events/RewardRedeemedEvent.cs`
- `src/Vulperonex.Domain/Events/PlatformConnectionChangedEvent.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/`

**預估規模：** M (中)

---

## 任務 2c：新增事件描述與領域層覆蓋率守門

**描述：** 新增供稍後 API/UI 介面使用的事件描述，並鎖定領域層覆蓋率測量。

**驗收準則：**
- [ ] `StreamEventDescriptions` 暴露所有工作流可見的 MVP 事件鍵值的中繼資料。
- [ ] `platform.connection_changed` 在需要處標記或表示為僅限系統。
- [ ] 領域層覆蓋率指令已記錄且可執行。

**驗證：**
- [ ] 單元測試驗證每個規範的工作流鍵值均有描述。
- [ ] 實作完成後，領域層覆蓋率指令通過 >90% 閾值。

**依賴：** 任務 2b

**可能涉及的檔案：**
- `src/Vulperonex.Domain/Events/StreamEventDescriptions.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Events/StreamEventDescriptionsTests.cs`

**預估規模：** S (小)

---

## 任務 2d：強制執行領域層/應用層無平台洩漏

**描述：** 新增防止 Twitch/平台特定符號進入領域層或應用層的架構測試。

**驗收準則：**
- [ ] 如果領域層或應用層包含 `Twitch*` 類型名稱，架構測試應失敗。
- [ ] 如果領域層引用了適配器程式集，架構測試應失敗。
- [ ] 測試命名採用核准的 BDD 風格。

**驗證：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** 任務 2b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Architecture/Domain/PlatformLeakageTests.cs`

**預估規模：** S (小)

---

## 任務 3a：實作成員領域模型

**描述：** 新增 `MemberRecord`、`PlatformIdentity` 和 `LoyaltyInfo` 作為帶有不變式的小型領域類型。

**驗收準則：**
- [ ] `MemberRecord` 包含 `MemberId` 和 `Identities`。
- [ ] `PlatformIdentity` 建模 `(Platform, PlatformUserId)`。
- [ ] 領域構造函數或工廠拒絕無效的空身分值。

**驗證：**
- [ ] 單元測試覆蓋有效建構。
- [ ] 單元測試覆蓋拒絕無效平台/用戶 ID。

**依賴：** 任務 2d

**可能涉及的檔案：**
- `src/Vulperonex.Domain/Members/MemberRecord.cs`
- `src/Vulperonex.Domain/Members/PlatformIdentity.cs`
- `src/Vulperonex.Domain/Members/LoyaltyInfo.cs`
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**預估規模：** M (中)

---

## 任務 3b：定義應用層成員埠 (Ports)

**描述：** 新增應用層成員埠，用於命令與查詢路徑，暫不包含基礎設施實作。

**驗收準則：**
- [ ] `IMemberRepository` 存在於應用層且專注於寫入。
- [ ] `IMemberQueryService` 存在於應用層並返回讀取 DTO 或查詢結果契約，而非 EF 實體。
- [ ] 領域層不引用應用層埠。

**驗證：**
- [ ] 架構測試確認領域層不引用應用層。
- [ ] 單元編譯/編譯確認應用層可以引用領域層成員類型。

**依賴：** 任務 3a

**可能涉及的檔案：**
- `src/Vulperonex.Application/Members/IMemberRepository.cs`
- `src/Vulperonex.Application/Members/IMemberQueryService.cs`
- `src/Vulperonex.Application/Members/MemberDtos.cs`

**預估規模：** S (小)

---

## 任務 3c：完成成員測試與覆蓋率

**描述：** 新增針對成員行為的測試，並運行第一階段覆蓋率守門。

**驗收準則：**
- [ ] 成員測試使用 Given/When/Then 命名或主體註釋。
- [ ] 領域層覆蓋率維持 >90%。
- [ ] 即使應用層行為仍較薄弱，也應記錄並可執行應用層覆蓋率指令。

**驗證：**
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Domain]*" /p:Exclude="[*.Tests.*]*" /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=average`
- [ ] `dotnet test tests/Vulperonex.Tests.Unit /p:CollectCoverage=true /p:Include="[Vulperonex.Application]*" /p:Exclude="[*.Tests.*]*"`

**依賴：** 任務 3b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Unit/Domain/Members/`

**預估規模：** S (小)

---

## 任務 3d：新增 DCI 角色隔離守門

**描述：** 新增 `docs/SPEC.md` 為 `*Role` 和 `*Behavior` 領域類別指定的架構測試。

**驗收準則：**
- [ ] `DciRoleIsolationTests` 掃描 `Vulperonex.Domain` 中的 `*Role` 和 `*Behavior` 類型。
- [ ] 匹配的類型不得引用基礎設施、EF Core 或 `*.Infrastructure.*`。
- [ ] 當尚不存在角色/行為類型時測試通過，且對未來新增保持意義。

**驗證：**
- [ ] `dotnet test tests/Vulperonex.Tests.Architecture /m:1 /nr:false /p:UseSharedCompilation=false`

**依賴：** 任務 3b

**可能涉及的檔案：**
- `tests/Vulperonex.Tests.Architecture/Domain/DciRoleIsolationTests.cs`

**預估規模：** S (小)

---

## 第一階段檢查點

**驗收準則：**
- [ ] 任務 1a-3d 已完成並以小切片形式提交。
- [ ] `dotnet build Vulperonex.sln --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] `dotnet test Vulperonex.sln --no-build /m:1 /nr:false /p:UseSharedCompilation=false` 通過。
- [ ] 領域層覆蓋率閾值指令通過。
- [ ] 架構測試通過。
- [ ] `git status --short --ignored` 僅顯示預期忽略的本地檔案。

**審查門檻：**
- [ ] 在開始第二階段之前，審查依賴方向、命名、任務大小和測試品質。

---

## 風險與緩解

| 風險 | 影響 | 緩解措施 |
|------|--------|------------|
| .NET 10 套件可用性或 SDK 不匹配 | 高 | 實作前驗證 `dotnet --info`；保持套件新增明確且在需要時事先詢問。 |
| 架構測試過早變得脆弱 | 中 | 從完全符合核准 SPEC 的程式集/引用規則與符號檢查開始。 |
| 任務 1 範圍過大 | 中 | 將 1a-1e 作為獨立提交；在新增領域行為前停止於基準編譯。 |
| 覆蓋率守門因生成的或瑣碎的程式碼而失敗 | 中 | 保持領域層程式碼精簡並直接測試行為；僅排除測試程式集。 |

---

## 開放問題

- 第一階段規劃無開放問題。套件安裝仍受 `docs/SPEC.md` 中事先詢問規則的約束；第一階段僅授權其活動任務所需的 SPEC 命名套件。
