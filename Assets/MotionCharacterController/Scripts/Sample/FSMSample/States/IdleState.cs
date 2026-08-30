using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// 待机状态
    /// </summary>
    public class IdleState : MccFsmStateBase
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

            if (!IsStableOnGround())
            {
                ChangeState<JumpFallState>();
                return;
            }

            if (Owner.JumpRequested)
            {
                ChangeState<JumpRiseState>();
                return;
            }

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

            // 物理帧起跳 同帧转发给 JumpRise 施加冲量
            if (Owner.JumpRequested && IsStableOnGround())
            {
                ChangeState<JumpRiseState>();
                if (Machine != null && Machine.CurrentState is IMccMotorLogic nextMotor)
                {
                    nextMotor.UpdateVelocity(ref currentVelocity, deltaTime);
                }

                return;
            }

            if (!IsStableOnGround())
            {
                MccFsmMotorUtil.ApplyAirMove(
                    Motion,
                    ref currentVelocity,
                    Owner.PlanarMoveInput,
                    deltaTime,
                    HasMoveInput(),
                    Owner.MoveSpeedScale);
                return;
            }

            MccFsmMotorUtil.ApplyStableGroundMove(
                Motion,
                ref currentVelocity,
                Owner.PlanarMoveInput,
                deltaTime,
                false,
                Owner.MoveSpeedScale);
        }
    }
}
