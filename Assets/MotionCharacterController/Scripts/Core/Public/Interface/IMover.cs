using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 场景移动部分的接口
    /// </summary>
    public interface IMover
    {
        void UpdateMovement(out Vector3 goalPosition, out Quaternion goalRotation, float deltaTime);
    }
}
