using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 地面检测与处理
    /// </summary>
    public class GroundingExecutor
    {
        private readonly MccMotorContext context;
        private readonly CollisionMoveExecutor collisionMoveExecutor;
        private readonly HitStabilityExecutor hitStabilityExecutor;

        /// <summary>
        /// 组装接地 Executor
        /// </summary>
        /// <param name="context">运行时数据</param>
        /// <param name="collisionMoveExecutor">碰撞查询</param>
        /// <param name="hitStabilityExecutor">稳定性</param>
        public GroundingExecutor(MccMotorContext context, CollisionMoveExecutor collisionMoveExecutor, HitStabilityExecutor hitStabilityExecutor)
        {
            this.context = context;
            this.collisionMoveExecutor = collisionMoveExecutor;
            this.hitStabilityExecutor = hitStabilityExecutor;
        }

        /// <summary>
        /// 更新地面状态
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public void UpdateGrounding(float deltaTime)
        {
            // 如果不需要解算地面 则直接返回
            if (!context.SolveGrounding)
            {
                return;
            }
        
            // 如果需要强制离地 则将角色上推
            if (context.Owner.MustUnground())
            {
                context.TransientPosition += context.CharacterUp * (MccConfig.MIN_GROUND_PROBING_DISTANCE * 1.5f);
                // 设置地面报告
                context.GroundingStatus = new CharacterGroundingReport { GroundNormal = context.CharacterUp };
            }
            else
            {
                // 计算探测距离
                float distance = GetSelectedGroundProbeDistance(context);
                context.DebugGroundProbeDistance = distance;

                // 探测地面
                Vector3 probePosition = context.TransientPosition;
                ProbeGround(ref probePosition, context.TransientRotation, distance, ref context.GroundingStatus);
                context.TransientPosition = probePosition;

                // 如果上一帧没站稳 且 这一帧站稳了 说明落地
                if (!context.LastGroundingStatus.IsStableOnGround && context.GroundingStatus.IsStableOnGround)
                {
                    // 将速度投影到地面平面
                    context.BaseVelocity = Vector3.ProjectOnPlane(context.BaseVelocity, context.CharacterUp);
                    context.BaseVelocity = MccGeometryCalculator.GetDirectionTangentToSurface(
                        context.BaseVelocity,
                        context.GroundingStatus.GroundNormal,
                        context.CharacterUp) * context.BaseVelocity.magnitude;
                }
            }
            // 重置当前帧的接地状态
            context.LastMovementIterationFoundAnyGround = false;
            if (context.MustUngroundTimeCounter > 0f)
            {
                context.MustUngroundTimeCounter -= deltaTime;
            }
            context.MustUnground = false;
        }

        /// <summary>
        /// 探测地面
        /// </summary>
        /// <param name="probingPosition">探测位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="probingDistance">探测距离</param>
        /// <param name="report">地面报告</param>
        public void ProbeGround(ref Vector3 probingPosition, Quaternion rotation, float probingDistance, ref CharacterGroundingReport report)
        {
            // 探测距离最小值
            probingDistance = Mathf.Max(probingDistance, MccConfig.MIN_GROUND_PROBING_DISTANCE);
            // 探测位置
            Vector3 sweepPosition = probingPosition;
            Vector3 sweepDirection = rotation * Vector3.down;
            // 剩余距离
            float remainingDistance = probingDistance;
            // 最大探测迭代次数

            for (int i = 0; i <= MccConfig.MAX_GROUNDING_SWEEP_ITERATIONS && remainingDistance > 0f; i++)
            {
                if (!CharacterGroundSweep(sweepPosition, rotation, sweepDirection, remainingDistance, out RaycastHit hit))
                {
                    break;
                }
                // 计算目标位置
                Vector3 targetPosition = sweepPosition + sweepDirection * hit.distance;
                // 评估稳定性
                HitStabilityReport stability = hitStabilityExecutor.Evaluate(
                    collisionMoveExecutor,
                    hit.collider,
                    hit.normal,
                    hit.point,
                    targetPosition,
                    rotation,
                    context.BaseVelocity);
                // 设置地面报告
                report.FoundAnyGround = true;
                report.GroundNormal = hit.normal;
                report.InnerGroundNormal = stability.InnerNormal;
                report.OuterGroundNormal = stability.OuterNormal;
                report.GroundCollider = hit.collider;
                report.GroundPoint = hit.point;
                report.SnappingPrevented = false;

                // 如果稳定 则设置地面报告
                if (stability.IsStable)
                {
                    report.SnappingPrevented = !hitStabilityExecutor.IsStableWithSpecialCases(ref stability, context.BaseVelocity);
                    report.IsStableOnGround = true;
                    if (!report.SnappingPrevented)
                    {
                        probingPosition = sweepPosition + sweepDirection * Mathf.Max(0f, hit.distance - MccConfig.COLLISION_OFFSET);
                    }

                    context.Owner.Controller?.OnGroundHit(hit.collider, hit.normal, hit.point, ref stability);
                    break;
                }

                // 计算扫略移动
                Vector3 sweepMovement = sweepDirection * hit.distance + context.CharacterUp * Mathf.Max(MccConfig.COLLISION_OFFSET, hit.distance);
                // 更新扫略位置
                sweepPosition += sweepMovement;
                // 更新剩余距离
                remainingDistance = Mathf.Min(MccConfig.GROUND_REBOUND_DISTANCE, Mathf.Max(remainingDistance - sweepMovement.magnitude, 0f));
                // 更新扫略方向
                sweepDirection = Vector3.ProjectOnPlane(sweepDirection, hit.normal).normalized;
            }
        }

        /// <summary>
        /// 角色胶囊体扫略检测
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近碰撞</param>
        /// <returns>是否命中</returns>
        private bool CharacterGroundSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            // 获取胶囊体上下端点并后退一点避免起点在内部
            Vector3 bottom = MccGeometryCalculator.GetCapsuleBottomHemiAt(position, rotation, context.TransformToCapsuleBottomHemi) - direction * MccConfig.GROUND_BACKSTEP_DISTANCE;
            Vector3 top = MccGeometryCalculator.GetCapsuleTopHemiAt(position, rotation, context.TransformToCapsuleTopHemi) - direction * MccConfig.GROUND_BACKSTEP_DISTANCE;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, context.Capsule.radius, direction, context.InternalHits, distance + MccConfig.GROUND_BACKSTEP_DISTANCE, context.CollidableLayers & context.Config.stableGroundLayers, QueryTriggerInteraction.Ignore);

            closestHit = default;
            float closestDistance = Mathf.Infinity;
            bool found = false;
            // 选取最近的有效命中
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = context.InternalHits[i];
                hit.distance -= MccConfig.GROUND_BACKSTEP_DISTANCE;
                if (hit.distance > 0f && context.IsColliderValidForCollisions(hit.collider) && hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 按接地状态选择本帧地面探测距离
        /// </summary>
        /// <param name="context">电机上下文</param>
        /// <returns>探测距离</returns>
        public static float GetSelectedGroundProbeDistance(MccMotorContext context)
        {
            float extra = context.Config.groundDetectionExtraDistance;
            if (!context.LastGroundingStatus.SnappingPrevented
                && (context.LastGroundingStatus.IsStableOnGround || context.LastMovementIterationFoundAnyGround))
            {
                return Mathf.Max(context.Capsule.radius, context.Config.maxStepHeight) + extra;
            }

            return Mathf.Max(MccConfig.MIN_GROUND_PROBING_DISTANCE, context.Config.groundProbeDistance) + extra;
        }
    }
}
