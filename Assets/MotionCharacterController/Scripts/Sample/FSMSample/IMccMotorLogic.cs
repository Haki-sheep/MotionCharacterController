using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// 状态侧运动薄接口 由 IMcc 外壳在马达回调里转发
    /// </summary>
    public interface IMccMotorLogic
    {
        /// <summary>
        /// 更新速度
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间差</param>
        void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
    }
}
