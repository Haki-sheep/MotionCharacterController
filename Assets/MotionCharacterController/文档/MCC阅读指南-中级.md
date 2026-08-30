# MCC 阅读指南（中级）

阅读顺序：**内核（难度递增）→ 移动平台 → 多角色 → 网络同步**。

## 怎么跳转（重要）

Cursor 点 Markdown 链接有坑，本文链接已按下面规则写：

1. 路径必须是**工作区相对**：`Assets/MotionCharacterController/Scripts/...`  
   - 不要 `../`（编辑区会相对工程根解析，链到工程外 → 找不到文件）  
   - 不要前导 `/`（Windows 会当成 `C:\Assets\...`）
2. **点链接只能可靠打开文件，不能可靠跳行**  
   - `#L行号` 在 Cursor 里经常直接报「找不到文件」  
   - 行号写在链接文字里（如 `MotionCC.cs:250`），打开后按 `Ctrl+G` 输入行号
3. 最稳流程：`Ctrl+P` 搜文件名 → `Ctrl+G` 跳行

单角色本地内核够用时，可先不看后三章。

---

## 总览

| 阶段 | # | 方面 | 难度 | 一句话 |
|---|---|---|:---:|---|
| **内核** | K1 | 形状与查询 | ★1 | 用什么形状问世界 |
| | K2 | 瞬时位姿 | ★1 | 算完再提交 Transform |
| | K3 | 数值安全 | ★1 | NaN / 微抖 / 超迭代 |
| | K4 | 玩法速度 | ★2 | 手感在 IMcc 不在电机 |
| | K5 | 回调过滤 | ★2 | 玩法插手碰谁、稳不稳 |
| | K6 | 接地 | ★3 | 「碰到」≠「站得住」 |
| | K7 | 碰撞移动 | ★4 | 速度 → 不穿模位移 |
| | K8 | 台阶与边缘 | ★4 | 小坎能上 悬崖别硬吸 |
| | K9 | 墙角折线 | ★5 | 两面墙怎么滑/卡死 |
| | K10 | 动态刚体 | ★5 | 推箱子质量比 |
| **扩展** | E1 | 移动平台 | ★5 | 台与人谁先 Commit |
| | E2 | 多角色批处理 | ★4 | 同帧同一模拟顺序 |
| | E3 | 网络同步 | ★6 | 状态袋 / 谁权威 / 回滚 |

内核主序：Phase1 解重叠+接地 → Phase2 旋转+速度+Move。  
整帧入口：[MccSystem.cs:55 Simulate](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)

目录按 DCES 收口 无 Event/Save

```text
Core
├── Public/Interface     S  IMccMotorService IMcc IMover
├── Data/Config          D  MccConfig
├── Data/Runtime         D  Context Reports
├── Calculator           C  几何 折线投影
├── Executor/Movement    E  CollisionMove Step
├── Executor/Grounding   E  Grounding HitStability
├── Executor/Interaction E  Platform Rigidbody
├── Executor             E  MccSystem MccPhysicsMover
└── MotionCC.cs          Service
```

---

# 第一部分：内核（难度递增）

## K1. 形状与查询 ★1

**要解决什么**  
角色有体积。Cast 几何必须与最终站位一致。

**MCC 怎么解决**

- 定尺寸：[MccConfig.Field.cs:10](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs) → [MotionCC.cs:250 ValidateData](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs) → [MotionCC.Function.cs:149 SetCapsuleDimensions](Assets/MotionCharacterController/Scripts/Core/MotionCC.Function.cs)
- 端点缓存：[MccMotorContext.cs:155 RefreshCapsuleData](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs) / [MccGeometryCalculator.cs:75 GetCapsuleBottomHemiAt](Assets/MotionCharacterController/Scripts/Core/Calculator/MccGeometryCalculator.cs)
- 安全距：[MccConfig.Constant.cs:10 COLLISION_OFFSET](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Constant.cs) / [MccConfig.Constant.cs:16 GROUND_BACKSTEP](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Constant.cs)
- 层掩码：[MccMotorContext.cs:182 RefreshCollidableLayers](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)
- 查询：[CollisionMoveExecutor.cs:372 Overlap](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs) / [CollisionMoveExecutor.cs:411 Sweep](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)  
  地面：[GroundingExecutor.cs:148 CharacterGroundSweep](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)

---

## K2. 瞬时位姿 ★1

**要解决什么**  
每步改 Transform 会污染查询顺序，插值也做不干净。

**MCC 怎么解决**

- 字段：[MccMotorContext.cs:21 TransientPosition](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs) / [MccMotorContext.cs:27 TransientRotation](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)
- 帧初：[MccMotorContext.cs:137 BeginSimulation](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)
- 插值起点：[MotionCC.cs:109 PreSimulationTick](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)
- Executor 只写 Transient：[CollisionMoveExecutor.cs:171 Move 末尾](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 提交：[MotionCC.cs:208 CommitSimulation](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs) → [MotionCC.cs:224 InterpolationUpdate](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)

---

## K3. 数值安全 ★1

**要解决什么**  
浮点误差、死循环迭代、NaN 会毁掉整帧。

**MCC 怎么解决**

- [MccGeometryCalculator.cs:26 SanitizeVelocity](Assets/MotionCharacterController/Scripts/Core/Calculator/MccGeometryCalculator.cs)
- 微速度归零：[MotionCC.cs:181 MIN_VELOCITY](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)
- 超迭代兜底：[CollisionMoveExecutor.cs:156](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 平面约束：[CollisionMoveExecutor.cs:96 hasPlanarConstraint](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 调试字段：[MccMotorContext.cs:110 Debug*](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)
- 对外只读快照：[MotionCC.Function.cs:279 CaptureDebugSample](Assets/MotionCharacterController/Scripts/Core/MotionCC.Function.cs) / [IMccMotorService.cs](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMccMotorService.cs)

---

## K4. 玩法速度 ★2

**要解决什么**  
走/跳/重力是手感，不该焊进碰撞内核。

**MCC 怎么解决**

- 接口：[IMcc.cs:37 UpdateVelocity](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMcc.cs) / [IMcc.cs:30 UpdateRotation](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMcc.cs)
- 调用：[MotionCC.cs:165 UpdatePhase2](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)
- 示例：[PlayerController.cs:49 UpdateVelocity](Assets/MotionCharacterController/Scripts/MccStandPlayerController/PlayerController.cs)  
  跳：[PlayerController.cs:84](Assets/MotionCharacterController/Scripts/MccStandPlayerController/PlayerController.cs) → [MotionCC.Function.cs:56 ForceUnground](Assets/MotionCharacterController/Scripts/Core/MotionCC.Function.cs)
- 输入：[MotionCC.cs:99 Update](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs) → [PlayerController.cs:24 InputVectorUpdate](Assets/MotionCharacterController/Scripts/MccStandPlayerController/PlayerController.cs)
- 参数：[MccConfig.Field.cs:16 移动跳跃](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs)

---

## K5. 回调过滤 ★2

**要解决什么**  
内核不知「碰谁」「算不算自定义地面」。

**MCC 怎么解决**

- 名单：[IMcc.cs](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMcc.cs)
- 过滤：[MccMotorContext.cs:231 IsColliderValid](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs) → [IMcc.cs:59](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMcc.cs)
- 回调：[GroundingExecutor.cs:124 OnGroundHit](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs) / [CollisionMoveExecutor.cs:310 OnMovementHit](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs) / [CollisionMoveExecutor.cs:347 Discrete](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 稳定性最后一票：[HitStabilityExecutor.cs:74](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/HitStabilityExecutor.cs)

---

## K6. 接地 ★3

**要解决什么**  
碰到 ≠ 站得住；要管坡度、探测距、吸附、跳后别立刻吸回。

**MCC 怎么解决**

- 入口：[MotionCC.cs:149](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs) → [GroundingExecutor.cs:25 UpdateGrounding](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)
- 强制离地抬高：[GroundingExecutor.cs:34](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)
- 探测距：[GroundingExecutor.cs:179 GetSelectedGroundProbeDistance](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)
- 向下扫吸附：[GroundingExecutor.cs:75 ProbeGround](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)
- 坡度：[MccGeometryCalculator.cs:43 IsStableOnNormal](Assets/MotionCharacterController/Scripts/Core/Calculator/MccGeometryCalculator.cs) / [MccConfig.Field.cs:38](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs)
- 边缘禁吸：[HitStabilityExecutor.cs:95](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/HitStabilityExecutor.cs)（细节见 K8）
- 落地刹竖直：[GroundingExecutor.cs:52](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs)
- 报告：[MccReports.cs:78 CharacterGroundingReport](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccReports.cs)

---

## K7. 碰撞移动 ★4

**要解决什么**  
`BaseVelocity` 如何变成不穿墙的位移（一次 Cast 不够）。

**MCC 怎么解决**

- 调用：[MotionCC.cs:190 → Move](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)
- 主循环：[CollisionMoveExecutor.cs:89 Move](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)  
  → [CollisionMoveExecutor.cs:182 TryFindNextMovementHit](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs) → 上台阶或滑动 → 超迭代兜底
- 解重叠：[CollisionMoveExecutor.cs:31 ResolveInitialOverlaps](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 障碍法线：[MccSweepCalculator.cs:42 GetObstructionNormal](Assets/MotionCharacterController/Scripts/Core/Calculator/MccSweepCalculator.cs)
- 投影：[MccSweepCalculator.cs:68 Project](Assets/MotionCharacterController/Scripts/Core/Calculator/MccSweepCalculator.cs)
- 配置：[MccConfig.Field.cs:85 求解安全](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs)

---

## K8. 台阶与边缘 ★4

**要解决什么**  
小坎能上；半脚悬空别硬吸；「站」与「蹭」同一套稳不稳。

**MCC 怎么解决**

- 共用入口：[HitStabilityExecutor.cs:24 Evaluate](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/HitStabilityExecutor.cs)
- 边缘：[HitStabilityExecutor.cs:21 ProcessLedgeStability](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/HitStabilityExecutor.cs) / [HitStabilityExecutor.cs:95](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/HitStabilityExecutor.cs)
- 台阶探测：[StepExecutor.cs:26 DetectSteps](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/StepExecutor.cs)（仅不稳定）
- 抬脚：[CollisionMoveExecutor.cs:269 TryResolveStep](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs) → [StepExecutor.cs:84 TryStep](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/StepExecutor.cs)
- 调用方：接地 [GroundingExecutor.cs:95](Assets/MotionCharacterController/Scripts/Core/Executor/Grounding/GroundingExecutor.cs) / 移动 [CollisionMoveExecutor.cs:137](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs)
- 配置：[MccConfig.Field.cs:48 台阶](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs) / [MccConfig.Field.cs:58 边缘](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs)

---

## K9. 墙角折线 ★5

**要解决什么**  
一面墙滑动；两面墙夹角要沿折线或卡死，否则抖/穿。

**MCC 怎么解决**

- 状态：[MccReports.cs:38 MovementSweepState](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccReports.cs)
- 状态机：[MccSweepCalculator.cs:68 Project](Assets/MotionCharacterController/Scripts/Core/Calculator/MccSweepCalculator.cs)  
  → [MccSweepCalculator.cs:160 EvaluateCrease](Assets/MotionCharacterController/Scripts/Core/Calculator/MccSweepCalculator.cs)
- 调试：[MccMotorContext.cs:112 DebugLastSweepState](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)，菜单 `Tools/MCC/调试器`

到这里，**单角色本地「走得稳」的内核主链就齐了**。

---

## K10. 动态刚体 ★5

**要解决什么**  
运动学角色推箱子不能靠默认刚体互撞。

**MCC 怎么解决**

- 配置：[MccConfig.Field.cs:72 InteractionType](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs) / [MccConfig.Field.cs:75 Mass](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs)
- 滑动记账：[CollisionMoveExecutor.cs:312](Assets/MotionCharacterController/Scripts/Core/Executor/Movement/CollisionMoveExecutor.cs) → [RigidbodyExecutor.cs:36 StoreHit](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/RigidbodyExecutor.cs)
- 帧末：[RigidbodyExecutor.cs:58 ProcessVelocityForHits](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/RigidbodyExecutor.cs) / [RigidbodyExecutor.cs:100 ResolveMassRatio](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/RigidbodyExecutor.cs)
- 清空：[MotionCC.cs:126 Clear](Assets/MotionCharacterController/Scripts/Core/MotionCC.cs)
- Kinematic 过滤：[MccMotorContext.cs:248](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)

---

# 第二部分：移动平台

## E1. 移动平台 ★5

**要解决什么**  
人跟着动台走；同帧提交顺序错会穿台/甩飞。

**MCC 怎么解决**

```text
平台 VelocityUpdate（只算速度）
  → 角色 Phase1 附着 Move
  → 平台 CommitMovement
  → 角色 Phase2 自己走
```

- 平台算速：[MccPhysicsMover.cs:72 VelocityUpdate](Assets/MotionCharacterController/Scripts/Core/Executor/MccPhysicsMover.cs) / [IMover.cs:7](Assets/MotionCharacterController/Scripts/Core/Public/Interface/IMover.cs)
- 附着：[PlatformExecutor.cs:28 UpdateAttachment](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/PlatformExecutor.cs)  
  [PlatformExecutor.cs:96](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/PlatformExecutor.cs) → [PlatformExecutor.cs:116](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/PlatformExecutor.cs) → [PlatformExecutor.cs:71 Move](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/PlatformExecutor.cs)
- 动量：[PlatformExecutor.cs:57](Assets/MotionCharacterController/Scripts/Core/Executor/Interaction/PlatformExecutor.cs)
- 平台落地：[MccPhysicsMover.cs:107 CommitMovement](Assets/MotionCharacterController/Scripts/Core/Executor/MccPhysicsMover.cs)（夹在 [MccSystem.cs:75](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)）
- 忽略自身平台：[MccMotorContext.cs:242](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccMotorContext.cs)

依赖 K6 接地 + K7 Move。

---

# 第三部分：多角色

## E2. 多角色批处理 ★4

**要解决什么**  
多个角色/平台各自 FixedUpdate 顺序不定 → 同帧结果不稳定。

**MCC 怎么解决**

- 单入口：[MccSystem.cs:55 Simulate](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)  
  全员 PreSim → 全平台速度 → 全员 Phase1 → 全平台 Commit → 全员 Phase2 → 全员 Commit
- 驱动：[MccSystem.cs:27 FixedUpdate](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs) + [MccSystem.cs:14 AutoSimulation](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)
- 注册表：[MccSystem.cs:114 RegisterCharacter](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs) / [MccSystem.cs:139 RegisterMover](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)
- 手动步进：[MccConfig.Field.cs:99 autoSimulation](Assets/MotionCharacterController/Scripts/Core/Data/Config/MccConfig.Field.cs) → [MccSystem.cs:160 Sync](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)
- 插值：[MccSystem.cs:176](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs) / [MccSystem.cs:37 LateUpdate](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)

尚未内建角色互推/避让。

---

# 第四部分：网络同步

## E3. 网络同步 ★6

**要解决什么**  
谁权威、传什么、延迟与回滚。

**MCC 已具备**

- 角色快照：[MccReports.cs:59 MotionCharacterMotorState](Assets/MotionCharacterController/Scripts/Core/Data/Runtime/MccReports.cs)
- 读写：[MotionCC.Function.cs:19 GetState](Assets/MotionCharacterController/Scripts/Core/MotionCC.Function.cs) / [MotionCC.Function.cs:40 ApplyState](Assets/MotionCharacterController/Scripts/Core/MotionCC.Function.cs)
- 平台快照：[MccPhysicsMover.cs:168](Assets/MotionCharacterController/Scripts/Core/Executor/MccPhysicsMover.cs) / [GetState:149](Assets/MotionCharacterController/Scripts/Core/Executor/MccPhysicsMover.cs) / [ApplyState:160](Assets/MotionCharacterController/Scripts/Core/Executor/MccPhysicsMover.cs)
- 手动 Simulate：[MccSystem.cs:55](Assets/MotionCharacterController/Scripts/Core/Executor/MccSystem.cs)

**需上层网络层做**

| 问题 | 常见做法 |
|---|---|
| 权威端 | 服务器/主机跑 Simulate；客户端预测 |
| 传什么 | 输入 + 定期 State；或两端同逻辑只传输入 |
| 附着刚体 | Rigidbody 引用 → 网络 ID |
| 回滚 | 历史 State → ApplyState → 重跑 Simulate |
| 远端显示 | 快照插值 |
| 平台 | 与角色同 tick 打包（E1+E2） |

---

## 推荐阅读路径

```text
内核：K1 → K2 → K3 → K4 → K6 → K7 → K5 → K8 → K9 →（可选）K10
扩展：E1 → E2 → E3
```

调试：`Tools/MCC/调试器`

---

## 代码量

| 范围 | 约行 |
|---|---:|
| 最小自制运动学原型 | 400～800 |
| MCC Core | ~3100 |
| + 示例 | ~4100 |
| Editor | ~550 |
