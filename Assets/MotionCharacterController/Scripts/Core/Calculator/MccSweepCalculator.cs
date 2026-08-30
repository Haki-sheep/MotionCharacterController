using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 扫掠投影所需的只读姿态
    /// </summary>
    public struct MccSweepPoseState
    {
        public bool IsStableOnGround;
        public bool MustUnground;
        public Vector3 GroundNormal;
        public Vector3 CharacterUp;
        public bool HasPlanarConstraint;
        public Vector3 PlanarConstraintAxis;
    }

    /// <summary>
    /// 速度投影计算结果
    /// </summary>
    public struct MccSweepProjectionResult
    {
        public Vector3 Velocity;
        public float RemainingMagnitude;
        public Vector3 RemainingDirection;
        public MovementSweepState SweepState;
        public bool FoundGround;
    }

    /// <summary>
    /// 障碍法线 速度投影 折线判定 纯计算
    /// </summary>
    public static class MccSweepCalculator
    {
        /// <summary>
        /// 稳定地面上把不稳定命中法线折成沿地面障碍法线
        /// </summary>
        /// <param name="hitNormal">命中法线</param>
        /// <param name="isStable">命中是否稳定</param>
        /// <param name="pose">扫掠姿态</param>
        /// <returns>障碍法线</returns>
        public static Vector3 GetObstructionNormal(Vector3 hitNormal, bool isStable, MccSweepPoseState pose)
        {
            var eObstructionNormal = hitNormal;
            if (pose.IsStableOnGround && !pose.MustUnground && !isStable)
            {
                var eObstructionLeftAlongGround = Vector3.Cross(pose.GroundNormal, eObstructionNormal).normalized;
                eObstructionNormal = Vector3.Cross(eObstructionLeftAlongGround, pose.CharacterUp).normalized;
            }

            return eObstructionNormal.sqrMagnitude > 0f ? eObstructionNormal : hitNormal;
        }

        /// <summary>
        /// 去掉穿入速度并更新扫掠状态
        /// </summary>
        /// <param name="isStableOnHit">本次命中是否稳定</param>
        /// <param name="obstructionNormal">障碍法线</param>
        /// <param name="sweepState">当前扫掠状态</param>
        /// <param name="previousHitStable">上一命中是否稳定</param>
        /// <param name="previousVelocity">投影前上一速度</param>
        /// <param name="previousObstructionNormal">上一障碍法线</param>
        /// <param name="velocity">当前速度</param>
        /// <param name="remainingMagnitude">剩余位移</param>
        /// <param name="remainingDirection">剩余方向</param>
        /// <param name="pose">扫掠姿态</param>
        /// <returns>投影结果</returns>
        public static MccSweepProjectionResult Project(
            bool isStableOnHit,
            Vector3 obstructionNormal,
            MovementSweepState sweepState,
            bool previousHitStable,
            Vector3 previousVelocity,
            Vector3 previousObstructionNormal,
            Vector3 velocity,
            float remainingMagnitude,
            Vector3 remainingDirection,
            MccSweepPoseState pose)
        {
            var eResult = new MccSweepProjectionResult
            {
                Velocity = velocity,
                RemainingMagnitude = remainingMagnitude,
                RemainingDirection = remainingDirection,
                SweepState = sweepState,
                FoundGround = false,
            };

            if (velocity.sqrMagnitude <= 0f)
            {
                return eResult;
            }

            var eVelocityBeforeProjection = velocity;
            var eVelocity = velocity;
            var eSweepState = sweepState;
            if (isStableOnHit)
            {
                eResult.FoundGround = true;
                ProjectVelocity(ref eVelocity, obstructionNormal, true, pose);
            }
            else if (eSweepState == MovementSweepState.Initial)
            {
                ProjectVelocity(ref eVelocity, obstructionNormal, false, pose);
                eSweepState = MovementSweepState.AfterFirstHit;
            }
            else if (eSweepState == MovementSweepState.AfterFirstHit)
            {
                EvaluateCrease(
                    velocity,
                    previousVelocity,
                    obstructionNormal,
                    previousObstructionNormal,
                    isStableOnHit,
                    previousHitStable,
                    pose.IsStableOnGround && !pose.MustUnground,
                    out bool eFoundCrease,
                    out Vector3 eCreaseDirection);
                if (eFoundCrease)
                {
                    eVelocity = pose.IsStableOnGround ? Vector3.zero : Vector3.Project(eVelocity, eCreaseDirection);
                    eSweepState = pose.IsStableOnGround ? MovementSweepState.FoundBlockingCorner : MovementSweepState.FoundBlockingCrease;
                }
                else
                {
                    ProjectVelocity(ref eVelocity, obstructionNormal, false, pose);
                }
            }
            else if (eSweepState == MovementSweepState.FoundBlockingCrease)
            {
                eVelocity = Vector3.zero;
                eSweepState = MovementSweepState.FoundBlockingCorner;
            }

            if (pose.HasPlanarConstraint)
            {
                eVelocity = Vector3.ProjectOnPlane(eVelocity, pose.PlanarConstraintAxis.normalized);
            }

            float eVelocityFactor = eVelocityBeforeProjection.magnitude > 0f ? eVelocity.magnitude / eVelocityBeforeProjection.magnitude : 0f;
            eResult.Velocity = eVelocity;
            eResult.RemainingMagnitude = remainingMagnitude * eVelocityFactor;
            eResult.RemainingDirection = eVelocity.sqrMagnitude > 0f ? eVelocity.normalized : Vector3.zero;
            eResult.SweepState = eSweepState;
            return eResult;
        }

        /// <summary>
        /// 评估内角折线阻挡
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="previousVelocity">上一速度</param>
        /// <param name="currentNormal">当前法线</param>
        /// <param name="previousNormal">上一法线</param>
        /// <param name="currentStable">当前是否稳定</param>
        /// <param name="previousStable">上一是否稳定</param>
        /// <param name="characterStable">角色是否稳定接地</param>
        /// <param name="validCrease">是否有效折线</param>
        /// <param name="creaseDirection">折线方向</param>
        public static void EvaluateCrease(
            Vector3 currentVelocity,
            Vector3 previousVelocity,
            Vector3 currentNormal,
            Vector3 previousNormal,
            bool currentStable,
            bool previousStable,
            bool characterStable,
            out bool validCrease,
            out Vector3 creaseDirection)
        {
            validCrease = false;
            creaseDirection = Vector3.zero;
            if (characterStable && currentStable && previousStable)
            {
                return;
            }

            Vector3 eTmpCreaseDirection = Vector3.Cross(currentNormal, previousNormal).normalized;
            if (eTmpCreaseDirection.sqrMagnitude <= 0f || Vector3.Dot(currentNormal, previousNormal) >= 0.999f)
            {
                return;
            }

            Vector3 eNormalA = Vector3.ProjectOnPlane(currentNormal, eTmpCreaseDirection).normalized;
            Vector3 eNormalB = Vector3.ProjectOnPlane(previousNormal, eTmpCreaseDirection).normalized;
            Vector3 eEnteringVelocity = Vector3.ProjectOnPlane(previousVelocity, eTmpCreaseDirection).normalized;
            float eDotPlanes = Vector3.Dot(eNormalA, eNormalB);
            if (eDotPlanes <= Vector3.Dot(-eEnteringVelocity, eNormalA) + 0.001f && eDotPlanes <= Vector3.Dot(-eEnteringVelocity, eNormalB) + 0.001f)
            {
                validCrease = true;
                creaseDirection = Vector3.Dot(eTmpCreaseDirection, currentVelocity) < 0f ? -eTmpCreaseDirection : eTmpCreaseDirection;
            }
        }

        /// <summary>
        /// 速度沿障碍投影
        /// </summary>
        /// <param name="velocity">速度</param>
        /// <param name="obstructionNormal">障碍法线</param>
        /// <param name="stableOnHit">命中是否稳定</param>
        /// <param name="pose">扫掠姿态</param>
        private static void ProjectVelocity(ref Vector3 velocity, Vector3 obstructionNormal, bool stableOnHit, MccSweepPoseState pose)
        {
            if (pose.IsStableOnGround && !pose.MustUnground)
            {
                if (stableOnHit)
                {
                    velocity = MccGeometryCalculator.GetDirectionTangentToSurface(velocity, obstructionNormal, pose.CharacterUp) * velocity.magnitude;
                }
                else
                {
                    Vector3 eObstructionRightAlongGround = Vector3.Cross(obstructionNormal, pose.GroundNormal).normalized;
                    Vector3 eObstructionUpAlongGround = Vector3.Cross(eObstructionRightAlongGround, obstructionNormal).normalized;
                    velocity = MccGeometryCalculator.GetDirectionTangentToSurface(velocity, eObstructionUpAlongGround, pose.CharacterUp) * velocity.magnitude;
                    velocity = Vector3.ProjectOnPlane(velocity, obstructionNormal);
                }
            }
            else if (stableOnHit)
            {
                velocity = Vector3.ProjectOnPlane(velocity, pose.CharacterUp);
                velocity = MccGeometryCalculator.GetDirectionTangentToSurface(velocity, obstructionNormal, pose.CharacterUp) * velocity.magnitude;
            }
            else
            {
                velocity = Vector3.ProjectOnPlane(velocity, obstructionNormal);
            }
        }
    }
}
