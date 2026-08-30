using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// 起跳上升状态
    /// </summary>
    public class JumpRiseState : MccFsmStateBase
    {
        /// <summary>
        /// 状态进入时调用
        /// </summary>
        public override void OnEnter()
        {
            if (Owner == null)
            {
                return;
            }

            // 下一帧 UpdateVelocity 施加跳跃冲量
            Owner.WantJumpImpulse = true;
            Owner.JumpRequested = false;
        }

        /// <summary>
        /// 状态更新时调用
        /// </summary>
        public override void OnUpdate()
        {
            if (Owner == null || Motion == null)
            {
                return;
            }

            // 冲量尚未施加时不切换 避免误判
            if (Owner.WantJumpImpulse)
            {
                return;
            }

            float verticalSpeed = MccFsmMotorUtil.GetVerticalSpeed(Motion, Motion.BaseVelocity);
            if (verticalSpeed <= Owner.JumpApexSpeedThreshold)
            {
                ChangeState<JumpApexState>();
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

            if (Owner.WantJumpImpulse)
            {
                MccFsmMotorUtil.ApplyJumpImpulse(Motion, ref currentVelocity);
                Owner.WantJumpImpulse = false;
            }

            MccFsmMotorUtil.ApplyAirMove(
                Motion,
                ref currentVelocity,
                Owner.PlanarMoveInput,
                deltaTime,
                HasMoveInput(),
                Owner.MoveSpeedScale);
        }
    }
}
