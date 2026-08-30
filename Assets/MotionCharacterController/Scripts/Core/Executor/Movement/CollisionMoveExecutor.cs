using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 碰撞检测与处理
    /// 管移动过程中的重叠推出、扫掠撞墙、滑动、上台阶衔接、墙角折线
    /// 不管向下接地探测（那是 GroundingExecutor）也不直接结算推箱子冲量（记账后交给 RigidbodyExecutor）
    /// </summary>
    public class CollisionMoveExecutor
    {
        #region 字段与构造
        private readonly MccMotorContext context;
        private readonly StepExecutor stepExecutor;
        private readonly RigidbodyExecutor rigidbodyExecutor;
        private readonly HitStabilityExecutor hitStabilityExecutor;

        /// <summary>
        /// 组装移动碰撞 Executor
        /// </summary>
        /// <param name="context">运行时数据</param>
        /// <param name="stepExecutor">台阶</param>
        /// <param name="rigidbodyExecutor">刚体</param>
        /// <param name="hitStabilityExecutor">稳定性</param>
        public CollisionMoveExecutor(MccMotorContext context, StepExecutor stepExecutor, RigidbodyExecutor rigidbodyExecutor, HitStabilityExecutor hitStabilityExecutor)
        {
            this.context = context;
            this.stepExecutor = stepExecutor;
            this.rigidbodyExecutor = rigidbodyExecutor;
            this.hitStabilityExecutor = hitStabilityExecutor;
        }
        #endregion

        #region 解重叠
        /// <summary>
        /// 解算初始重叠
        /// </summary>
        public void ResolveInitialOverlaps()
        {
            // 遍历最大重叠解算次数
            for (int iteration = 0; iteration < context.Config.maxDecollisionIterations; iteration++)
            {
                // 获取重叠数量
                int count = CharacterCollisionsOverlap(context.TransientPosition, context.TransientRotation, context.InternalColliders);
                bool solved = true;

                for (int i = 0; i < count; i++)
                {
                    Collider other = context.InternalColliders[i];
                    // 如果碰撞体无效 则跳过
                    if (!context.IsColliderValidForCollisions(other))
                        continue;

                    // 计算碰撞分离信息
                    if (Physics.ComputePenetration(
                        context.Capsule,
                        context.TransientPosition,
                        context.TransientRotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out Vector3 direction,
                        out float distance))
                    {
                        // 判断碰撞是否稳定
                        bool stable = MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, direction, context.Config.maxStableSlopeAngle);
                        // 获取障碍物法线
                        var resolutionNormal = GetObstructionNormal(direction, stable);
                        // 计算移动距离
                        var movement = resolutionNormal * (distance + MccConfig.COLLISION_OFFSET);
                        // 积分移动 解决重叠
                        context.TransientPosition += movement;

                        // 记录重叠信息
                        RememberOverlap(resolutionNormal, other);
                        solved = false;
                        break;
                    }
                }

                if (solved)
                {
                    // 没有重叠了 则结束迭代
                    break;
                }
            }
        }
        #endregion

        #region 移动主循环
        /// <summary>
        /// 移动
        /// 该方法主要解决角色移动时与碰撞体的碰撞问题
        /// </summary>
        /// <param name="velocity">速度</param>
        /// <param name="deltaTime">时间差</param>
        /// <returns>是否完成</returns>
        public bool Move(ref Vector3 velocity, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return false;
            }

            if (context.Config.hasPlanarConstraint)
            {
                velocity = Vector3.ProjectOnPlane(velocity, context.Config.planarConstraintAxis.normalized);
            }

            bool completed = true;
            Vector3 remainingDirection = velocity.normalized;
            float remainingMagnitude = velocity.magnitude * deltaTime;
            Vector3 movedPosition = context.TransientPosition;
            var sweepState = MovementSweepState.Initial;

            bool previousHitStable = false;
            Vector3 previousVelocity = Vector3.zero;
            Vector3 previousObstructionNormal = Vector3.zero;

            ProjectAgainstKnownOverlaps(ref velocity,
                                        ref remainingMagnitude,
                                        ref remainingDirection,
                                        ref sweepState,
                                        ref previousHitStable,
                                        ref previousVelocity,
                                        ref previousObstructionNormal);

            int sweeps = 0;
            while (remainingMagnitude > MccConfig.MIN_CANMOVE_DISTANCE && sweeps <= context.Config.maxMovementIterations)
            {
                // 1 先找下一步会撞到什么 没有就直接走完
                if (!TryFindNextMovementHit(movedPosition, remainingDirection, remainingMagnitude, out MovementHit hit))
                {
                    movedPosition += remainingDirection * remainingMagnitude;
                    remainingMagnitude = 0f;
                    break;
                }

                // 2 走到命中点前
                Vector3 sweepMovement = remainingDirection * Mathf.Max(0f, hit.Distance - MccConfig.COLLISION_OFFSET);
                movedPosition += sweepMovement;
                remainingMagnitude -= sweepMovement.magnitude;
                WriteDebugHit(hit);

                // 3 评估稳定性 必要时上台阶 否则投影滑动
                HitStabilityReport report = hitStabilityExecutor.Evaluate(
                    this,
                    hit.Collider,
                    hit.Normal,
                    hit.Point,
                    movedPosition,
                    context.TransientRotation,
                    velocity);

                if (!TryResolveStep(ref movedPosition, ref velocity, ref remainingDirection, ref remainingMagnitude, deltaTime, hit.Normal, report))
                {
                    ResolveSlide(hit, report, ref velocity, ref remainingMagnitude, ref remainingDirection,
                        ref sweepState, ref previousHitStable, ref previousVelocity, ref previousObstructionNormal);
                }

                // 4 迭代上限保护
                sweeps++;
                if (sweeps > context.Config.maxMovementIterations)
                {
                    completed = false;
                    if (context.Config.killRemainingMovementWhenExceedMaxMovementIterations)
                    {
                        remainingMagnitude = 0f;
                    }

                    if (context.Config.killVelocityWhenExceedMaxMovementIterations)
                    {
                        velocity = Vector3.zero;
                    }
                }
            }

            movedPosition += remainingDirection * remainingMagnitude;
            context.TransientPosition = movedPosition;
            context.DebugLastSweepState = sweepState;
            context.DebugLastMovementSweeps = sweeps;
            context.DebugLastMoveCompleted = completed;
            return completed;
        }

        /// <summary>
        /// 探测下一次移动命中 优先起步重叠 否则扫掠
        /// </summary>
        private bool TryFindNextMovementHit(Vector3 position, Vector3 direction, float remainingMagnitude, out MovementHit hit)
        {
            if (context.Config.checkMovementInitialOverlaps
                && TryGetMostObstructingOverlapHit(position, direction, out hit))
            {
                return true;
            }

            if (CharacterCollisionsSweep(position,
                                         context.TransientRotation,
                                         direction,
                                         remainingMagnitude + MccConfig.COLLISION_OFFSET,
                                         out RaycastHit sweepHit,
                                         context.InternalHits) > 0)
            {
                hit = new MovementHit
                {
                    Collider = sweepHit.collider,
                    Normal = sweepHit.normal,
                    Point = sweepHit.point,
                    Distance = sweepHit.distance,
                };
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 查找与剩余移动方向最对撞的起步重叠
        /// </summary>
        private bool TryGetMostObstructingOverlapHit(Vector3 position, Vector3 remainingDirection, out MovementHit hit)
        {
            hit = default;
            int overlapCount = CharacterCollisionsOverlap(position, context.TransientRotation, context.InternalColliders);
            if (overlapCount <= 0)
            {
                return false;
            }

            float mostObstructingDot = 2f;
            bool found = false;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider other = context.InternalColliders[i];
                if (!context.IsColliderValidForCollisions(other))
                {
                    continue;
                }

                if (!Physics.ComputePenetration(
                        context.Capsule,
                        position,
                        context.TransientRotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out Vector3 resolutionDirection,
                        out float resolutionDistance))
                {
                    continue;
                }

                float dotProduct = Vector3.Dot(remainingDirection, resolutionDirection);
                if (dotProduct >= 0f || dotProduct >= mostObstructingDot)
                {
                    continue;
                }

                mostObstructingDot = dotProduct;
                hit = new MovementHit
                {
                    Collider = other,
                    Normal = resolutionDirection,
                    Point = position + (context.TransientRotation * context.TransformToCapsuleCenter) + resolutionDirection * resolutionDistance,
                    Distance = 0f,
                };
                found = true;
            }

            return found;
        }

        /// <summary>
        /// 尝试把不稳定命中解释成上台阶
        /// </summary>
        private bool TryResolveStep(
            ref Vector3 movedPosition,
            ref Vector3 velocity,
            ref Vector3 remainingDirection,
            ref float remainingMagnitude,
            float deltaTime,
            Vector3 hitNormal,
            HitStabilityReport report)
        {
            if (!context.SolveGrounding
                || context.Config.stepHandling == StepHandlingMethod.None
                || !report.ValidStepDetected)
            {
                return false;
            }

            if (!stepExecutor.TryStep(this, ref movedPosition, ref velocity, hitNormal, report))
            {
                return false;
            }

            remainingDirection = velocity.normalized;
            remainingMagnitude = velocity.magnitude * deltaTime;
            return true;
        }

        /// <summary>
        /// 沿障碍物投影滑动
        /// </summary>
        private void ResolveSlide(
            MovementHit hit,
            HitStabilityReport report,
            ref Vector3 velocity,
            ref float remainingMagnitude,
            ref Vector3 remainingDirection,
            ref MovementSweepState sweepState,
            ref bool previousHitStable,
            ref Vector3 previousVelocity,
            ref Vector3 previousObstructionNormal)
        {
            Vector3 obstructionNormal = GetObstructionNormal(hit.Normal, report.IsStable);
            context.Owner.Controller?.OnMovementHit(hit.Collider, hit.Normal, hit.Point, ref report);

            if (hit.Collider is not null && hit.Collider.attachedRigidbody is not null)
            {
                rigidbodyExecutor.StoreHit(hit.Collider.attachedRigidbody, velocity, hit.Point, obstructionNormal);
            }

            bool stableOnHit = report.IsStable && !context.Owner.MustUnground();
            Vector3 velocityBeforeProjection = velocity;
            ApplyVelocityProjection(stableOnHit,
                                    hit.Normal,
                                    ref sweepState,
                                    previousHitStable,
                                    previousVelocity,
                                    previousObstructionNormal,
                                    ref velocity,
                                    ref remainingMagnitude,
                                    ref remainingDirection);

            previousHitStable = stableOnHit;
            previousVelocity = velocityBeforeProjection;
            previousObstructionNormal = obstructionNormal;
        }

        /// <summary>
        /// 写入本帧调试命中信息
        /// </summary>
        private void WriteDebugHit(MovementHit hit)
        {
            context.DebugHasMovementHit = true;
            context.DebugLastHitPoint = hit.Point;
            context.DebugLastHitNormal = hit.Normal;
        }
        #endregion

        #region 离散碰撞事件
        /// <summary>
        /// 处理离散碰撞事件
        /// </summary>
        public void ProcessDiscreteCollisionEvents()
        {
            // 如果未开启离散碰撞事件 则直接返回
            if (!context.Config.discreteCollisionEvents)
            {
                return;
            }

            // 检测当前位置是否与碰撞体重叠
            int count = CharacterCollisionsOverlap(context.TransientPosition, context.TransientRotation, context.InternalColliders, MccConfig.COLLISION_OFFSET * 2f);
            for (int i = 0; i < count; i++)
            {
                context.Owner.Controller?.OnDiscreteCollisionDetected(context.InternalColliders[i]);
            }
        }
        #endregion

        #region 物理查询
        /// <summary>
        /// 角色碰撞体重叠
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="colliders">碰撞体数组</param>
        /// <param name="inflate">膨胀</param>
        /// <param name="acceptOnlyStableGroundLayer">只接受稳定地面层</param>
        /// <returns>碰撞数</returns>
        public int CharacterCollisionsOverlap(Vector3 position,
                                              Quaternion rotation,
                                              Collider[] colliders,
                                              float inflate = 0f,
                                              bool acceptOnlyStableGroundLayer = false)
        {
            // 获取查询层
            int queryLayers = acceptOnlyStableGroundLayer ? context.CollidableLayers & context.Config.stableGroundLayers : context.CollidableLayers;
            // 获取胶囊体底部半球位置
            var bottom = MccGeometryCalculator.GetCapsuleBottomHemiAt(position, rotation, context.TransformToCapsuleBottomHemi, inflate);
            // 获取胶囊体顶部半球位置
            var top = MccGeometryCalculator.GetCapsuleTopHemiAt(position, rotation, context.TransformToCapsuleTopHemi, inflate);
            // 进行胶囊体重叠检测
            int rawCount = Physics.OverlapCapsuleNonAlloc(bottom,
                                                          top,
                                                          context.Capsule.radius + inflate,
                                                          colliders,
                                                          queryLayers,
                                                          QueryTriggerInteraction.Ignore);
            // 过滤碰撞体
            return FilterColliders(colliders, rawCount);
        }

        /// <summary>
        /// 角色碰撞体扫略检测
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近的碰撞</param>
        /// <param name="hits">碰撞数组</param>
        /// <param name="inflate">膨胀</param>
        /// <param name="acceptOnlyStableGroundLayer">只接受稳定地面层</param>
        /// <returns>碰撞数</returns>
        public int CharacterCollisionsSweep(Vector3 position,
                                            Quaternion rotation,
                                            Vector3 direction,
                                            float distance,
                                            out RaycastHit closestHit,
                                            RaycastHit[] hits,
                                            float inflate = 0f,
                                            bool acceptOnlyStableGroundLayer = false)
        {
            closestHit = default;
            int queryLayers = 0;
            if (direction.sqrMagnitude <= 0f || distance <= 0f)
                return 0;
            // 获取查询层
            if (acceptOnlyStableGroundLayer)
                queryLayers = context.CollidableLayers & context.Config.stableGroundLayers;
            else
                queryLayers = context.CollidableLayers;

            // 获取标准化方向
            var normalizedDirection = direction.normalized;
            // 获取胶囊体底部半球位置 稍微往里收一点 避免扫掠时卡在胶囊体内部
            var bottom = MccGeometryCalculator.GetCapsuleBottomHemiAt(position, rotation, context.TransformToCapsuleBottomHemi, inflate)
                                                        - normalizedDirection * MccConfig.SWEEP_BACKSTEP_DISTANCE;
            // 获取胶囊体顶部半球位置 稍微往里收一点 避免扫掠时卡在胶囊体内部
            var top = MccGeometryCalculator.GetCapsuleTopHemiAt(position, rotation, context.TransformToCapsuleTopHemi, inflate)
                                                        - normalizedDirection * MccConfig.SWEEP_BACKSTEP_DISTANCE;
            // 进行胶囊体扫掠
            var rawCount = Physics.CapsuleCastNonAlloc(bottom,
                                                       top,
                                                       context.Capsule.radius + inflate,
                                                       normalizedDirection,
                                                       hits,
                                                       distance + MccConfig.SWEEP_BACKSTEP_DISTANCE,
                                                       queryLayers,
                                                       QueryTriggerInteraction.Ignore);
            // 过滤碰撞
            return FilterHits(hits,
                              rawCount,
                              MccConfig.SWEEP_BACKSTEP_DISTANCE,
                              out closestHit);
        }

        /// <summary>
        /// 角色碰撞体射线检测
        /// </summary>
        /// <param name="position">起点</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近碰撞</param>
        /// <param name="hits">碰撞数组</param>
        /// <param name="acceptOnlyStableGroundLayer">只接受稳定地面层</param>
        /// <returns>碰撞数</returns>
        public int CharacterCollisionsRaycast(Vector3 position,
                                              Vector3 direction,
                                              float distance,
                                              out RaycastHit closestHit,
                                              RaycastHit[] hits,
                                              bool acceptOnlyStableGroundLayer = false)
        {
            // 获取查询层
            int queryLayers = acceptOnlyStableGroundLayer ?
                    // 如果只接受稳定地面层 则取可碰撞层掩码与稳定地面层掩码的与运算
                    context.CollidableLayers & context.Config.stableGroundLayers
                    // 如果不需要只接受稳定地面层 则取可碰撞层掩码
                    : context.CollidableLayers;
            // 进行射线检测
            int rawCount = Physics.RaycastNonAlloc(position, direction, hits, distance, queryLayers, QueryTriggerInteraction.Ignore);
            // 过滤碰撞
            return FilterHits(hits, rawCount, 0f, out closestHit);
        }
        #endregion

        #region 法线与速度投影
        /// <summary>
        /// 打包当前接地姿态给 Calculator
        /// </summary>
        /// <returns>扫掠姿态</returns>
        private MccSweepPoseState BuildSweepPose()
        {
            return new MccSweepPoseState
            {
                IsStableOnGround = context.GroundingStatus.IsStableOnGround,
                MustUnground = context.Owner.MustUnground(),
                GroundNormal = context.GroundingStatus.GroundNormal,
                CharacterUp = context.CharacterUp,
                HasPlanarConstraint = context.Config.hasPlanarConstraint,
                PlanarConstraintAxis = context.Config.planarConstraintAxis,
            };
        }

        /// <summary>
        /// 获取障碍物法线
        /// </summary>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="isStable">是否稳定</param>
        /// <returns>障碍物法线</returns>
        public Vector3 GetObstructionNormal(Vector3 hitNormal, bool isStable)
        {
            return MccSweepCalculator.GetObstructionNormal(hitNormal, isStable, BuildSweepPose());
        }

        /// <summary>
        /// 投影已知重叠信息
        /// 先根据 本帧已经记下的重叠信息，把速度/剩余位移处理一遍，避免刚被推开，又立刻往墙里钻的情况
        /// </summary>
        /// <param name="velocity">速度</param>
        /// <param name="remainingMagnitude">剩余距离</param>
        /// <param name="remainingDirection">剩余方向</param>
        /// <param name="sweepState">扫掠状态</param>
        /// <param name="previousHitStable">上一帧稳定状态</param>
        /// <param name="previousVelocity">上一帧速度</param>
        /// <param name="previousObstructionNormal">上一帧障碍物法线</param>
        private void ProjectAgainstKnownOverlaps(ref Vector3 velocity,
                                                 ref float remainingMagnitude,
                                                 ref Vector3 remainingDirection,
                                                 ref MovementSweepState sweepState,
                                                 ref bool previousHitStable,
                                                 ref Vector3 previousVelocity,
                                                 ref Vector3 previousObstructionNormal)
        {
            for (int i = 0; i < context.OverlapsCount; i++)
            {
                var overlapNormal = context.Overlaps[i].Normal;

                // 如果剩余方向与重叠法线方向相同或相反 则跳过
                if (Vector3.Dot(remainingDirection, overlapNormal) >= 0f)
                    continue;

                // 判断重叠是否稳定
                var isStable = MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, overlapNormal, context.Config.maxStableSlopeAngle)
                               && !context.Owner.MustUnground();
                // 获取障碍物法线
                var obstructionNormal = GetObstructionNormal(overlapNormal, isStable);
                // 获取速度投影前的速度
                var velocityBeforeProjection = velocity;

                ApplyVelocityProjection(isStable,
                                        obstructionNormal,
                                        ref sweepState,
                                        previousHitStable,
                                        previousVelocity,
                                        previousObstructionNormal,
                                        ref velocity,
                                        ref remainingMagnitude,
                                        ref remainingDirection);

                // 更新上一帧稳定状态、速度、障碍物法线
                previousHitStable = isStable;
                previousVelocity = velocityBeforeProjection;
                previousObstructionNormal = obstructionNormal;
            }
        }

        /// <summary>
        /// 调用 Calculator 投影速度并写回扫掠状态
        /// </summary>
        /// <param name="isStableOnHit">本次命中是否稳定</param>
        /// <param name="obstructionNormal">障碍法线</param>
        /// <param name="sweepState">扫掠状态</param>
        /// <param name="previousHitStable">上一命中是否稳定</param>
        /// <param name="previousVelocity">投影前上一速度</param>
        /// <param name="previousObstructionNormal">上一障碍法线</param>
        /// <param name="velocity">速度</param>
        /// <param name="remainingMagnitude">剩余位移</param>
        /// <param name="remainingDirection">剩余方向</param>
        private void ApplyVelocityProjection(bool isStableOnHit,
                                            Vector3 obstructionNormal,
                                            ref MovementSweepState sweepState,
                                            bool previousHitStable,
                                            Vector3 previousVelocity,
                                            Vector3 previousObstructionNormal,
                                            ref Vector3 velocity,
                                            ref float remainingMagnitude,
                                            ref Vector3 remainingDirection)
        {
            var eResult = MccSweepCalculator.Project(
                isStableOnHit,
                obstructionNormal,
                sweepState,
                previousHitStable,
                previousVelocity,
                previousObstructionNormal,
                velocity,
                remainingMagnitude,
                remainingDirection,
                BuildSweepPose());

            velocity = eResult.Velocity;
            remainingMagnitude = eResult.RemainingMagnitude;
            remainingDirection = eResult.RemainingDirection;
            sweepState = eResult.SweepState;
            if (eResult.FoundGround)
            {
                context.LastMovementIterationFoundAnyGround = true;
            }
        }
        #endregion

        #region 过滤与记录
        /// <summary>
        /// 过滤碰撞体
        /// </summary>
        /// <param name="colliders">碰撞体数组</param>
        /// <param name="count">碰撞体数量</param>
        /// <returns>有效的碰撞体数量</returns>
        private int FilterColliders(Collider[] colliders, int count)
        {
            int validCount = count;
            // 从后往前移除无效碰撞体
            for (int i = count - 1; i >= 0; i--)
            {
                if (!context.IsColliderValidForCollisions(colliders[i]))
                {
                    validCount--;
                    // 用尾部有效项填当前洞
                    if (i < validCount)
                    {
                        colliders[i] = colliders[validCount];
                    }
                }
            }
            return validCount;
        }

        /// <summary>
        /// 过滤碰撞
        /// </summary>
        /// <param name="hits">碰撞数组</param>
        /// <param name="count">碰撞数</param>
        /// <param name="backstep">后退距离</param>
        /// <param name="closestHit">最近的碰撞</param>
        /// <returns>有效的碰撞数</returns>
        private int FilterHits(RaycastHit[] hits, int count, float backstep, out RaycastHit closestHit)
        {
            closestHit = default;
            float closestDistance = Mathf.Infinity;
            int validCount = count;

            // 从后往前遍历碰撞
            for (int i = count - 1; i >= 0; i--)
            {
                // 减去后退距离
                hits[i].distance -= backstep;
                // 如果碰撞距离小于等于0 或碰撞体无效 则视为无效
                if (hits[i].distance <= 0f || !context.IsColliderValidForCollisions(hits[i].collider))
                {
                    validCount--;

                    // 将有效的移动到当前索引位置
                    if (i < validCount)
                    {
                        hits[i] = hits[validCount];
                    }
                }
                // 如果碰撞距离小于最近的碰撞距离 则更新最近的碰撞距离和信息
                else if (hits[i].distance < closestDistance)
                {
                    closestDistance = hits[i].distance;
                    closestHit = hits[i];
                }
            }

            // 返回有效的碰撞数
            return validCount;
        }

        /// <summary>
        /// 记录重叠信息
        /// </summary>
        /// <param name="normal">法线</param>
        /// <param name="collider">碰撞体</param>
        private void RememberOverlap(Vector3 normal, Collider collider)
        {
            if (context.OverlapsCount >= context.Overlaps.Length)
                return;

            // 创建重叠信息的数组 信息+1
            context.Overlaps[context.OverlapsCount] = new OverlapResult(normal, collider);
            context.OverlapsCount++;
        }
        #endregion
    }
}
