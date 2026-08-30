using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 命中稳定性与边缘 由 MotionCC 组装后注入接地与移动
    /// </summary>
    public class HitStabilityExecutor
    {
        private readonly MccMotorContext context;
        private readonly StepExecutor stepExecutor;

        /// <summary>
        /// 注入运行时数据与台阶 Executor
        /// </summary>
        /// <param name="context">运行时数据</param>
        /// <param name="stepExecutor">台阶 Executor</param>
        public HitStabilityExecutor(MccMotorContext context, StepExecutor stepExecutor)
        {
            this.context = context;
            this.stepExecutor = stepExecutor;
        }

        /// <summary>
        /// 评估碰撞面稳定性
        /// </summary>
        /// <param name="queries">碰撞查询</param>
        /// <param name="hitCollider">碰撞体</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="atCharacterPosition">角色位置</param>
        /// <param name="atCharacterRotation">角色旋转</param>
        /// <param name="velocity">速度</param>
        /// <returns>稳定性报告</returns>
        public HitStabilityReport Evaluate(
            CollisionMoveExecutor queries,
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            Vector3 velocity)
        {
            HitStabilityReport report = new HitStabilityReport
            {
                IsStable = context.SolveGrounding && MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, hitNormal, context.Config.maxStableSlopeAngle),
                InnerNormal = hitNormal,
                OuterNormal = hitNormal,
            };

            if (!context.SolveGrounding)
            {
                return report;
            }

            ProcessLedgeStability(queries, hitNormal, hitPoint, atCharacterPosition, atCharacterRotation, velocity, ref report);

            if (context.Config.stepHandling is not StepHandlingMethod.None && !report.IsStable)
            {
                Rigidbody body = hitCollider is not null ? hitCollider.attachedRigidbody : null;
                if (!(body is not null && !body.isKinematic))
                {
                    stepExecutor.DetectSteps(
                        queries,
                        atCharacterPosition,
                        atCharacterRotation,
                        hitPoint,
                        Vector3.ProjectOnPlane(hitNormal, atCharacterRotation * Vector3.up).normalized,
                        ref report);
                    if (report.ValidStepDetected)
                        report.IsStable = true;
                }
            }

            context.Owner.Controller?.ProcessHitStabilityReport(
                hitCollider,
                hitNormal,
                hitPoint,
                atCharacterPosition,
                atCharacterRotation,
                ref report);

            return report;
        }

        /// <summary>
        /// 边缘与落差特殊稳定性判定
        /// </summary>
        /// <param name="report">稳定性报告</param>
        /// <param name="velocity">速度</param>
        /// <returns>是否仍可视为稳定</returns>
        public bool IsStableWithSpecialCases(ref HitStabilityReport report, Vector3 velocity)
        {
            if (!context.Config.ledgeAndDenivelationHandling)
            {
                return true;
            }

            if (report.LedgeDetected)
            {
                if (report.IsMovingTowardsEmptySideOfLedge && Vector3.Project(velocity, report.LedgeFacingDirection).magnitude >= context.Config.maxVelocityForLedgeSnap)
                {
                    return false;
                }

                if (report.IsOnEmptySideOfLedge && report.DistanceFromLedge > context.Config.maxStableDistanceFromLedge)
                {
                    return false;
                }
            }

            if (context.LastGroundingStatus.FoundAnyGround && report.InnerNormal.sqrMagnitude > 0f && report.OuterNormal.sqrMagnitude > 0f)
            {
                if (Vector3.Angle(report.InnerNormal, report.OuterNormal) > context.Config.maxStableDenivelationAngle)
                {
                    return false;
                }

                if (Vector3.Angle(context.LastGroundingStatus.InnerGroundNormal, report.OuterNormal) > context.Config.maxStableDenivelationAngle)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 处理边缘稳定性
        /// </summary>
        /// <param name="queries">碰撞查询</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="atCharacterPosition">角色位置</param>
        /// <param name="atCharacterRotation">角色旋转</param>
        /// <param name="velocity">速度</param>
        /// <param name="report">稳定性报告</param>
        private void ProcessLedgeStability(
            CollisionMoveExecutor queries,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            Vector3 velocity,
            ref HitStabilityReport report)
        {
            if (!context.Config.ledgeAndDenivelationHandling)
            {
                return;
            }

            Vector3 up = atCharacterRotation * Vector3.up;
            Vector3 innerDirection = Vector3.ProjectOnPlane(hitNormal, up).normalized;
            if (innerDirection.sqrMagnitude <= 0f)
            {
                return;
            }

            float checkHeight = context.Config.stepHandling != StepHandlingMethod.None ? context.Config.maxStepHeight : MccConfig.MIN_GROUND_PROBING_DISTANCE;
            bool innerStable = false;
            bool outerStable = false;

            if (queries.CharacterCollisionsRaycast(hitPoint + up * MccConfig.SECONDARY_PROBES_VERTICAL + innerDirection * MccConfig.SECONDARY_PROBES_HORIZONTAL, -up, checkHeight + MccConfig.SECONDARY_PROBES_VERTICAL, out RaycastHit innerHit, context.InternalHits) > 0)
            {
                report.InnerNormal = innerHit.normal;
                report.FoundInnerNormal = true;
                innerStable = MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, innerHit.normal, context.Config.maxStableSlopeAngle);
            }

            if (queries.CharacterCollisionsRaycast(hitPoint + up * MccConfig.SECONDARY_PROBES_VERTICAL - innerDirection * MccConfig.SECONDARY_PROBES_HORIZONTAL, -up, checkHeight + MccConfig.SECONDARY_PROBES_VERTICAL, out RaycastHit outerHit, context.InternalHits) > 0)
            {
                report.OuterNormal = outerHit.normal;
                report.FoundOuterNormal = true;
                outerStable = MccGeometryCalculator.IsStableOnNormal(context.CharacterUp, outerHit.normal, context.Config.maxStableSlopeAngle);
            }

            report.LedgeDetected = innerStable != outerStable;
            if (report.LedgeDetected)
            {
                report.IsOnEmptySideOfLedge = outerStable && !innerStable;
                report.LedgeGroundNormal = outerStable ? report.OuterNormal : report.InnerNormal;
                report.LedgeRightDirection = Vector3.Cross(hitNormal, report.LedgeGroundNormal).normalized;
                report.LedgeFacingDirection = Vector3.ProjectOnPlane(Vector3.Cross(report.LedgeGroundNormal, report.LedgeRightDirection), context.CharacterUp).normalized;
                report.DistanceFromLedge = Vector3.ProjectOnPlane(hitPoint - (atCharacterPosition + atCharacterRotation * context.TransformToCapsuleBottom), up).magnitude;
                report.IsMovingTowardsEmptySideOfLedge = Vector3.Dot(velocity.normalized, report.LedgeFacingDirection) > 0f;
            }

            if (report.IsStable)
            {
                report.IsStable = IsStableWithSpecialCases(ref report, velocity);
            }
        }
    }
}
