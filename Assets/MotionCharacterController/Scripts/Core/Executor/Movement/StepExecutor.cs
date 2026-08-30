using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 台阶检测与处理
    /// </summary>
    public class StepExecutor
    {
        private readonly MccMotorContext context;

        public StepExecutor(MccMotorContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// 检测台阶
        /// </summary>
        /// <param name="queries">碰撞求解器</param>
        /// <param name="characterPosition">角色位置</param>
        /// <param name="characterRotation">角色旋转</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="innerHitDirection">法线在水平面上朝外的投影向量</param>
        /// <param name="report">稳定性报告</param>
        public void DetectSteps(
            CollisionMoveExecutor queries,
            Vector3 characterPosition,
            Quaternion characterRotation,
            Vector3 hitPoint,
            Vector3 innerHitDirection,
            ref HitStabilityReport report)
        {
            // 如果水平方向无效 则无法检测台阶
            if (innerHitDirection.sqrMagnitude <= 0f)
                return;

            // 获取角色朝上方向
            Vector3 up = characterRotation * Vector3.up;
            // 获取碰撞点到角色位置的垂直分量
            Vector3 verticalToHit = Vector3.Project(hitPoint - characterPosition, up);
            // 获取碰撞点到角色位置的水平方向
            Vector3 horizontalDirection = Vector3.ProjectOnPlane(hitPoint - characterPosition, up).normalized;
            // 计算台阶检测起点 拉平到角色高度后上抬再略往台阶外偏
            Vector3 start = hitPoint - verticalToHit +
                            up * context.Config.maxStepHeight +
                            horizontalDirection * MccConfig.COLLISION_OFFSET * 3f;

            // 从起点向下扫掠 查找台阶顶面
            int count = queries.CharacterCollisionsSweep(start, characterRotation, -up, context.Config.maxStepHeight + MccConfig.COLLISION_OFFSET, out _, context.InternalHits, 0f, true);
            // 检查台阶是否有效
            if (CheckStepValidity(queries, count, characterPosition, characterRotation, innerHitDirection, start, out Collider hitCollider))
            {
                report.ValidStepDetected = true;
                report.SteppedCollider = hitCollider;
                return;
            }

            // 如果台阶处理方式不是加强台阶处理 则直接返回
            if (context.Config.stepHandling != StepHandlingMethod.Extra)
            {
                return;
            }

            // 加强模式 换起点再探测一次浅台阶
            start = characterPosition + up * context.Config.maxStepHeight - innerHitDirection * context.Config.minRequiredStepDepth;
            count = queries.CharacterCollisionsSweep(start, characterRotation, -up, context.Config.maxStepHeight - MccConfig.COLLISION_OFFSET, out _, context.InternalHits, 0f, true);
            if (CheckStepValidity(queries, count, characterPosition, characterRotation, innerHitDirection, start, out hitCollider))
            {
                report.ValidStepDetected = true;
                report.SteppedCollider = hitCollider;
            }
        }

        /// <summary>
        /// 尝试抬脚上台阶
        /// </summary>
        /// <param name="queries">碰撞求解器</param>
        /// <param name="movedPosition">移动后位置</param>
        /// <param name="velocity">速度</param>
        /// <param name="hitNormal">移动命中法线</param>
        /// <param name="report">稳定性报告</param>
        /// <returns>是否成功上台阶</returns>
        public bool TryStep(CollisionMoveExecutor queries, ref Vector3 movedPosition, ref Vector3 velocity, Vector3 hitNormal, HitStabilityReport report)
        {
            // 如果没有记录台阶碰撞体 则失败
            if (report.SteppedCollider == null)
            {
                return false;
            }

            // 如果阻挡面太接近垂直 则不适合当作台阶前进面
            float obstructionCorrelation = Mathf.Abs(Vector3.Dot(hitNormal, context.CharacterUp));
            if (obstructionCorrelation > 0.15f)
            {
                return false;
            }

            // 计算前进方向与抬脚检测起点
            Vector3 forward = Vector3.ProjectOnPlane(-hitNormal, context.CharacterUp).normalized;
            Vector3 start = movedPosition + forward * MccConfig.STEPPING_FORWARD_DISTANCE + context.CharacterUp * context.Config.maxStepHeight;
            // 从起点向下扫 找台阶顶面碰撞体
            int count = queries.CharacterCollisionsSweep(start, context.TransientRotation, -context.CharacterUp, context.Config.maxStepHeight, out _, context.InternalHits, 0f, true);

            for (int i = 0; i < count; i++)
            {
                // 命中之前检测到的台阶顶面 则把角色放到台面上
                if (context.InternalHits[i].collider == report.SteppedCollider)
                {
                    movedPosition = start - context.CharacterUp * Mathf.Max(0f, context.InternalHits[i].distance - MccConfig.COLLISION_OFFSET);
                    velocity = Vector3.ProjectOnPlane(velocity, context.CharacterUp);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查扫掠到的候选面是否构成有效台阶
        /// </summary>
        /// <param name="queries">碰撞求解器</param>
        /// <param name="hitCount">命中数量</param>
        /// <param name="characterPosition">角色位置</param>
        /// <param name="characterRotation">角色旋转</param>
        /// <param name="innerHitDirection">内侧水平方向</param>
        /// <param name="stepCheckStart">台阶检测起点</param>
        /// <param name="hitCollider">有效台阶碰撞体</param>
        /// <returns>是否有效</returns>
        private bool CheckStepValidity(CollisionMoveExecutor queries, int hitCount, Vector3 characterPosition, Quaternion characterRotation, Vector3 innerHitDirection, Vector3 stepCheckStart, out Collider hitCollider)
        {
            hitCollider = null;
            Vector3 up = characterRotation * Vector3.up;

            // 从最远命中开始逐个验证
            while (hitCount > 0)
            {
                // 查找当前最远命中
                int farthestIndex = 0;
                float farthestDistance = 0f;
                for (int i = 0; i < hitCount; i++)
                {
                    if (context.InternalHits[i].distance > farthestDistance)
                    {
                        farthestDistance = context.InternalHits[i].distance;
                        farthestIndex = i;
                    }
                }

                RaycastHit farthestHit = context.InternalHits[farthestIndex];
                // 计算角色站在候选台面上的位置
                Vector3 candidatePosition = stepCheckStart - up * Mathf.Max(0f, farthestHit.distance - MccConfig.COLLISION_OFFSET);

                // 候选位置无重叠
                bool clearAtStep = queries.CharacterCollisionsOverlap(candidatePosition, characterRotation, context.InternalColliders) <= 0;
                // 台阶外侧地面稳定
                bool stableOuter = queries.CharacterCollisionsRaycast(farthestHit.point + up * MccConfig.SECONDARY_PROBES_VERTICAL - innerHitDirection * MccConfig.SECONDARY_PROBES_HORIZONTAL, -up, context.Config.maxStepHeight + MccConfig.SECONDARY_PROBES_VERTICAL, out RaycastHit outerHit, context.InternalHits, true) > 0
                                   && MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, outerHit.normal, context.Config.maxStableSlopeAngle);
                // 角色头顶有足够空间
                bool clearAbove = queries.CharacterCollisionsSweep(characterPosition, characterRotation, up, context.Config.maxStepHeight - farthestHit.distance, out _, context.InternalHits) <= 0;
                // 内侧稳定 先测脚下抬高点 失败再测台阶内侧点 与 KCC 一致
                bool stableInner = IsInnerStepGroundValid(queries, characterPosition, candidatePosition, farthestHit.point, innerHitDirection, up);

                // 四项都满足 则认定为有效台阶
                if (clearAtStep && stableOuter && clearAbove && stableInner)
                {
                    hitCollider = farthestHit.collider;
                    return true;
                }

                // 当前候选无效 移除最远命中继续尝试
                hitCount--;
                if (farthestIndex < hitCount)
                {
                    context.InternalHits[farthestIndex] = context.InternalHits[hitCount];
                }
            }

            return false;
        }

        /// <summary>
        /// 判断台阶内侧是否有可站立地面
        /// </summary>
        private bool IsInnerStepGroundValid(
            CollisionMoveExecutor queries,
            Vector3 characterPosition,
            Vector3 candidatePosition,
            Vector3 stepHitPoint,
            Vector3 innerHitDirection,
            Vector3 up)
        {
            if (context.Config.allowSteppingWithoutStableGrounding)
            {
                return true;
            }

            // 第一枪 从角色位置抬到候选高度向下打
            Vector3 elevatedCharacterPosition = characterPosition + Vector3.Project(candidatePosition - characterPosition, up);
            if (queries.CharacterCollisionsRaycast(elevatedCharacterPosition, -up, context.Config.maxStepHeight, out RaycastHit innerHit, context.InternalHits, true) > 0
                && MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, innerHit.normal, context.Config.maxStableSlopeAngle))
            {
                return true;
            }

            // 第二枪 从台阶命中点再往内侧偏一点向下打 KCC 有 MCC 原先漏了
            Vector3 innerProbeOrigin = stepHitPoint + innerHitDirection * MccConfig.SECONDARY_PROBES_HORIZONTAL;
            return queries.CharacterCollisionsRaycast(innerProbeOrigin, -up, context.Config.maxStepHeight, out innerHit, context.InternalHits, true) > 0
                   && MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, innerHit.normal, context.Config.maxStableSlopeAngle);
        }
    }
}
