using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 刚体碰撞投影求解器
    /// </summary>
    public class RigidbodyExecutor
    {
        private readonly MccMotorContext context;
        /// <summary>本帧刚体碰撞记录数组</summary>
        private readonly RigidbodyProjectionHit[] hitArray = new RigidbodyProjectionHit[MccConfig.MAX_RIGIDBODY_HITS];
        /// <summary>本帧已记录数量</summary>
        private int hitCount;

        public RigidbodyExecutor(MccMotorContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// 清空本帧刚体碰撞记录
        /// </summary>
        public void Clear()
        {
            hitCount = 0;
        }

        /// <summary>
        /// 存储刚体碰撞信息
        /// </summary>
        /// <param name="body">刚体</param>
        /// <param name="velocity">速度</param>
        /// <param name="point">碰撞点</param>
        /// <param name="normal">碰撞法线</param>
        public void StoreHit(Rigidbody body, Vector3 velocity, Vector3 point, Vector3 normal)
        {
            if (body is null || hitCount >= hitArray.Length)
            {
                return;
            }

            hitArray[hitCount] = new RigidbodyProjectionHit
            {
                Rigidbody = body,
                HitVelocity = velocity,
                HitPoint = point,
                EffectiveHitNormal = normal,
            };
            hitCount++;
        }

        /// <summary>
        /// 根据本帧记录的碰撞 处理角色与刚体的速度交换
        /// </summary>
        /// <param name="velocity">角色速度</param>
        /// <param name="deltaTime">时间差</param>
        public void ProcessVelocityForHits(ref Vector3 velocity, float deltaTime)
        {
            if (!context.Config.interactiveRigidbodyHandling)
            {
                return;
            }

            for (int i = 0; i < hitCount; i++)
            {
                RigidbodyProjectionHit hit = hitArray[i];
                if (hit.Rigidbody == null || hit.Rigidbody == context.AttachedRigidbody)
                {
                    continue;
                }

                bool dynamicBody = !hit.Rigidbody.isKinematic;
                float characterToBodyMassRatio = ResolveMassRatio(hit.Rigidbody, dynamicBody);
                if (!dynamicBody)
                {
                    continue;
                }

                Vector3 bodyVelocity = hit.Rigidbody.linearVelocity;
                ComputeCollisionResolution(
                    hit.EffectiveHitNormal,
                    hit.HitVelocity,
                    bodyVelocity,
                    characterToBodyMassRatio,
                    out Vector3 characterVelocityChange,
                    out Vector3 bodyVelocityChange);

                velocity += characterVelocityChange;
                hit.Rigidbody.AddForceAtPosition(bodyVelocityChange, hit.HitPoint, ForceMode.VelocityChange);
            }
        }

        /// <summary>
        /// 按角色模拟质量与刚体质量计算碰撞质量占比
        /// </summary>
        /// <param name="body">命中刚体</param>
        /// <param name="dynamicBody">是否动态刚体</param>
        /// <returns>角色质量占比</returns>
        private float ResolveMassRatio(Rigidbody body, bool dynamicBody)
        {
            if (!dynamicBody)
            {
                return 0f;
            }

            if (context.Config.rigidbodyInteractionType == RigidbodyInteractionType.Kinematic)
            {
                return 1f;
            }

            float characterMass = context.Config.simulatedCharacterMass;
            float hitBodyMass = body.mass;
            if (characterMass + hitBodyMass > 0f)
            {
                return characterMass / (characterMass + hitBodyMass);
            }

            return 0.5f;
        }

        /// <summary>
        /// 计算角色与刚体在碰撞法线上的速度变化
        /// </summary>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="characterVelocity">角色速度</param>
        /// <param name="bodyVelocity">刚体速度</param>
        /// <param name="characterToBodyMassRatio">角色质量占比</param>
        /// <param name="velocityChangeOnCharacter">角色速度变化</param>
        /// <param name="velocityChangeOnBody">刚体速度变化</param>
        private static void ComputeCollisionResolution(Vector3 hitNormal, Vector3 characterVelocity, Vector3 bodyVelocity, float characterToBodyMassRatio, out Vector3 velocityChangeOnCharacter, out Vector3 velocityChangeOnBody)
        {
            velocityChangeOnCharacter = Vector3.zero;
            velocityChangeOnBody = Vector3.zero;
            float bodyToCharacterMassRatio = 1f - characterToBodyMassRatio;
            float characterVelocityOnNormal = Vector3.Dot(characterVelocity, hitNormal);
            float bodyVelocityOnNormal = Vector3.Dot(bodyVelocity, hitNormal);

            if (characterVelocityOnNormal < 0f)
            {
                velocityChangeOnCharacter += hitNormal * characterVelocityOnNormal;
            }

            if (bodyVelocityOnNormal > characterVelocityOnNormal)
            {
                Vector3 relativeImpactVelocity = hitNormal * (bodyVelocityOnNormal - characterVelocityOnNormal);
                velocityChangeOnCharacter += relativeImpactVelocity * bodyToCharacterMassRatio;
                velocityChangeOnBody += -relativeImpactVelocity * characterToBodyMassRatio;
            }
        }
    }
}
