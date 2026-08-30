using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// 跳跃下落状态
    /// </summary>
    public class JumpFallState : MccFsmStateBase
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
            else
            {
                ChangeState<IdleState>();
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

            // 落地当帧仍可能走这里 稳定后贴地刹车
            if (IsStableOnGround())
            {
                MccFsmMotorUtil.ApplyStableGroundMove(
                    Motion,
                    ref currentVelocity,
                    Owner.PlanarMoveInput,
                    deltaTime,
                    HasMoveInput(),
                    Owner.MoveSpeedScale);
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
    }
}
