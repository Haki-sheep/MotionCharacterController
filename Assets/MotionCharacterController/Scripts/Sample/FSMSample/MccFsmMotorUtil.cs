using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// FSM 样例共享运动公式 对齐 NormalSample
    /// </summary>
    public static class MccFsmMotorUtil
    {
        /// <summary>
        /// 稳定地面移动
        /// </summary>
        /// <param name="motion">马达</param>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="moveInput">世界空间平面输入</param>
        /// <param name="deltaTime">时间差</param>
        /// <param name="hasMoveInput">是否有有效移动输入</param>
        /// <param name="speedScale">速度倍率</param>
        public static void ApplyStableGroundMove(
            MotionCC motion,
            ref Vector3 currentVelocity,
            Vector3 moveInput,
            float deltaTime,
            bool hasMoveInput,
            float speedScale = 1f)
        {
            MccConfig config = motion.Config;
            Vector3 up = motion.CharacterUp;

            currentVelocity = motion.GetDirectionTangentToSurface(
                                  currentVelocity,
                                  motion.GroundingStatus.GroundNormal)
                              * currentVelocity.magnitude;

            Vector3 targetVelocity = Vector3.zero;
            if (hasMoveInput)
            {
                Vector3 inputRight = Vector3.Cross(moveInput, up);
                Vector3 reorientedInput = Vector3.Cross(
                                              motion.GroundingStatus.GroundNormal,
                                              inputRight).normalized
                                          * moveInput.magnitude;
                targetVelocity = reorientedInput * (config.moveSpeed * speedScale);
            }

            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                1f - Mathf.Exp(-config.stableMovementSharpness * deltaTime));
        }

        /// <summary>
        /// 空中加速重力与阻力
        /// </summary>
        /// <param name="motion">马达</param>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="moveInput">世界空间平面输入</param>
        /// <param name="deltaTime">时间差</param>
        /// <param name="hasMoveInput">是否有有效移动输入</param>
        /// <param name="speedScale">速度倍率</param>
        public static void ApplyAirMove(
            MotionCC motion,
            ref Vector3 currentVelocity,
            Vector3 moveInput,
            float deltaTime,
            bool hasMoveInput,
            float speedScale = 1f)
        {
            MccConfig config = motion.Config;
            Vector3 up = motion.CharacterUp;

            if (hasMoveInput)
            {
                Vector3 addedVelocity = moveInput * config.AirAcceleration * deltaTime;
                Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
                float airSpeedLimit = config.maxAirMoveSpeed * speedScale;

                if (currentPlanarVelocity.magnitude < airSpeedLimit)
                {
                    Vector3 newTotal = Vector3.ClampMagnitude(
                        currentPlanarVelocity + addedVelocity,
                        airSpeedLimit);
                    addedVelocity = newTotal - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(
                        addedVelocity,
                        currentPlanarVelocity.normalized);
                }

                currentVelocity += addedVelocity;
            }

            currentVelocity += up * config.gravity * deltaTime;
            currentVelocity *= 1f / (1f + config.airDrag * deltaTime);
        }

        /// <summary>
        /// 施加跳跃冲量
        /// </summary>
        /// <param name="motion">马达</param>
        /// <param name="currentVelocity">当前速度</param>
        public static void ApplyJumpImpulse(MotionCC motion, ref Vector3 currentVelocity)
        {
            Vector3 up = motion.CharacterUp;
            motion.ForceUnground();
            currentVelocity += (up * motion.Config.jumpSpeed) - Vector3.Project(currentVelocity, up);
            motion.ConsumeJumpRequest();
        }

        /// <summary>
        /// 沿角色 Up 的竖直速度标量
        /// </summary>
        /// <param name="motion">马达</param>
        /// <param name="velocity">速度</param>
        /// <returns>竖直速度</returns>
        public static float GetVerticalSpeed(MotionCC motion, Vector3 velocity)
        {
            return Vector3.Dot(velocity, motion.CharacterUp);
        }
    }
}
