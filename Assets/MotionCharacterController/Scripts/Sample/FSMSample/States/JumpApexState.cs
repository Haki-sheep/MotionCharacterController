using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// 跳跃顶点状态
    /// </summary>
    public class JumpApexState : MccFsmStateBase
    {
        /// <summary>
        /// 状态更新时调用
        /// </summary>
        public override void OnUpdate()
        {
            if (Owner == null || Motion == null)
            {
                return;
            }

            if (IsStableOnGround())
            {
                ChangeToGroundState();
                return;
            }

            float verticalSpeed = MccFsmMotorUtil.GetVerticalSpeed(Motion, Motion.BaseVelocity);
            if (verticalSpeed < -Owner.JumpApexSpeedThreshold)
            {
                ChangeState<JumpFallState>();
            }
        }

        /// <summary>
        /// 更新速度
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间差</param>
        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (Motion == null || Owner == null)
            {
                return;
            }

            MccFsmMotorUtil.ApplyAirMove(
                Motion,
                ref currentVelocity,
                Owner.PlanarMoveInput,
                deltaTime,
                HasMoveInput(),
                Owner.MoveSpeedScale);
        }

        /// <summary>
        /// 落地切回地面态
        /// </summary>
        private void ChangeToGroundState()
        {
            if (Owner.IsCrouching)
            {
                if (HasMoveInput())
                {
                    ChangeState<CrouchMoveState>();
                }
                else
                {
                    ChangeState<CrouchIdleState>();
                }

                return;
            }

            if (HasMoveInput())
            {
                ChangeState<MoveState>();
            }
            else
            {
                ChangeState<IdleState>();
            }
        }
    }
}
