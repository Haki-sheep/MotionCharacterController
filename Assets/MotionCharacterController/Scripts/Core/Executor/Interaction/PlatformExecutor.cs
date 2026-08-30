using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 移动平台求解器
    /// </summary>
    public class PlatformExecutor
    {
        private readonly MccMotorContext context;
        private readonly CollisionMoveExecutor collisionMoveExecutor;

        /// <summary>
        /// 组装平台 Executor
        /// </summary>
        /// <param name="context">运行时数据</param>
        /// <param name="collisionMoveExecutor">碰撞移动</param>
        public PlatformExecutor(MccMotorContext context, CollisionMoveExecutor collisionMoveExecutor)
        {
            this.context = context;
            this.collisionMoveExecutor = collisionMoveExecutor;
        }

        /// <summary>
        /// 更新平台附着与跟随移动
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public void UpdateAttachment(float deltaTime)
        {
            // 如果未开启刚体交互 则清空平台状态
            if (!context.Config.interactiveRigidbodyHandling)
            {
                context.AttachedRigidbody = null;
                context.AttachedRigidbodyVelocity = Vector3.zero;
                return;
            }

            // 记录上一帧平台并应用外部覆盖
            context.LastAttachedRigidbody = context.AttachedRigidbody;
            context.AttachedRigidbody = context.AttachedRigidbodyOverride;

            // 如果站稳在地面上 则尝试从地面碰撞体获取平台刚体
            if (context.AttachedRigidbody == null && context.GroundingStatus.IsStableOnGround && context.GroundingStatus.GroundCollider != null)
            {
                context.AttachedRigidbody = GetInteractiveRigidbody(context.GroundingStatus.GroundCollider);
            }

            // 获取平台线速度与角速度
            Vector3 currentVelocity = Vector3.zero;
            Vector3 currentAngularVelocity = Vector3.zero;
            if (context.AttachedRigidbody != null)
            {
                GetVelocityFromRigidbody(context.AttachedRigidbody, context.TransientPosition, deltaTime, out currentVelocity, out currentAngularVelocity);
            }

            // 如果离开旧平台 则按配置保留或扣除平台惯性
            if (context.Config.preserveAttachedRigidbodyMomentum && context.LastAttachedRigidbody != null && context.AttachedRigidbody != context.LastAttachedRigidbody)
            {
                context.BaseVelocity += context.AttachedRigidbodyVelocity;
                context.BaseVelocity -= currentVelocity;
            }

            context.AttachedRigidbodyVelocity = currentVelocity;
            // 如果有平台速度 则带着角色一起移动
            if (context.AttachedRigidbodyVelocity.sqrMagnitude > 0f)
            {
                context.IsMovingFromAttachedRigidbody = true;
                Vector3 velocity = context.AttachedRigidbodyVelocity;
                if (context.SolveMovementCollisions)
                {
                    collisionMoveExecutor.Move(ref velocity, deltaTime);
                }
                else
                {
                    context.TransientPosition += velocity * deltaTime;
                }
                context.IsMovingFromAttachedRigidbody = false;
            }

            // 如果平台有旋转 则同步角色朝向
            if (currentAngularVelocity.sqrMagnitude > 0f)
            {
                Vector3 newForward = Vector3.ProjectOnPlane(Quaternion.Euler(Mathf.Rad2Deg * currentAngularVelocity * deltaTime) * context.CharacterForward, context.CharacterUp).normalized;
                if (newForward.sqrMagnitude > 0f)
                {
                    context.TransientRotation = Quaternion.LookRotation(newForward, context.CharacterUp);
                }
            }
        }

        /// <summary>
        /// 从碰撞体获取可交互的平台刚体
        /// </summary>
        /// <param name="collider">地面碰撞体</param>
        /// <returns>平台刚体</returns>
        private static Rigidbody GetInteractiveRigidbody(Collider collider)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null)
            {
                return null;
            }

            // MccPhysicsMover 或动态刚体才参与平台跟随
            return body.GetComponent<MccPhysicsMover>() != null || !body.isKinematic ? body : null;
        }

        /// <summary>
        /// 计算刚体在指定点的线速度与角速度
        /// </summary>
        /// <param name="body">刚体</param>
        /// <param name="point">采样点</param>
        /// <param name="deltaTime">时间差</param>
        /// <param name="linearVelocity">线速度</param>
        /// <param name="angularVelocity">角速度</param>
        private static void GetVelocityFromRigidbody(Rigidbody body, Vector3 point, float deltaTime, out Vector3 linearVelocity, out Vector3 angularVelocity)
        {
            linearVelocity = body.linearVelocity;
            angularVelocity = body.angularVelocity;

            // 优先使用 MccPhysicsMover 提供的速度
            MccPhysicsMover mover = body.GetComponent<MccPhysicsMover>();
            if (mover != null)
            {
                linearVelocity = mover.Velocity;
                angularVelocity = mover.AngularVelocity;
            }

            // 如果没有旋转 则无需追加切向速度
            if (deltaTime <= 0f || angularVelocity.sqrMagnitude <= 0f)
            {
                return;
            }

            // 把角速度换算成采样点处的附加线速度
            Vector3 center = body.transform.TransformPoint(body.centerOfMass);
            Vector3 offset = point - center;
            Quaternion rotation = Quaternion.Euler(Mathf.Rad2Deg * angularVelocity * deltaTime);
            Vector3 finalPoint = center + rotation * offset;
            linearVelocity += (finalPoint - point) / deltaTime;
        }
    }
}
