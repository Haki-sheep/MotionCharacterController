using MiMieFSM.UpdateFsm;
using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// MCC FSM 状态基类 从黑板取宿主
    /// </summary>
    public abstract class MccFsmStateBase : StateBase, IMccMotorLogic
    {
        /// <summary>
        /// IMcc 宿主
        /// </summary>
        protected FsmPlayerController Owner =>
            GetBlackboardValue<FsmPlayerController>(EBlockBoardParme.PlayerController);

        /// <summary>
        /// 状态机
        /// </summary>
        protected StateMachine Machine => Owner != null ? Owner.Machine : null;

        /// <summary>
        /// 马达
        /// </summary>
        protected MotionCC Motion => Owner != null ? Owner.Motion : null;

        /// <summary>
        /// 更新速度
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间差</param>
        public abstract void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <typeparam name="T">目标状态</typeparam>
        protected void ChangeState<T>() where T : StateBase, new()
        {
            Machine?.ChangeState<T>();
        }

        /// <summary>
        /// 是否稳定接地
        /// </summary>
        /// <returns>稳定接地</returns>
        protected bool IsStableOnGround()
        {
            return Motion != null && Motion.GroundingStatus.IsStableOnGround;
        }

        /// <summary>
        /// 是否有移动输入
        /// </summary>
        /// <returns>有输入</returns>
        protected bool HasMoveInput()
        {
            return Owner != null && Owner.HasMoveInput;
        }
    }
}
