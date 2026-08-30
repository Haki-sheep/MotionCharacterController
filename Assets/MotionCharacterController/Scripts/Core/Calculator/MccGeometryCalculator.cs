using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 胶囊端点 坡度 切线 速度清理 纯计算
    /// </summary>
    public static class MccGeometryCalculator
    {
        /// <summary>
        /// 位移换算速度
        /// </summary>
        /// <param name="movement">位移</param>
        /// <param name="deltaTime">时间差</param>
        /// <returns>速度</returns>
        public static Vector3 GetVelocityFromMovement(Vector3 movement, float deltaTime)
        {
            return movement / deltaTime;
        }

        /// <summary>
        /// NaN 速度清零
        /// </summary>
        /// <param name="velocity">输入速度</param>
        /// <returns>合法速度</returns>
        public static Vector3 SanitizeVelocity(Vector3 velocity)
        {
            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
            {
                return Vector3.zero;
            }

            return velocity;
        }

        /// <summary>
        /// 法线相对角色上方向是否在稳定坡度内
        /// </summary>
        /// <param name="characterUp">角色上方向</param>
        /// <param name="normal">表面法线</param>
        /// <param name="maxStableSlopeAngle">最大稳定坡度角</param>
        /// <returns>是否稳定</returns>
        public static bool IsStableOnNormal(Vector3 characterUp, Vector3 normal, float maxStableSlopeAngle)
        {
            return Vector3.Angle(characterUp, normal) <= maxStableSlopeAngle;
        }

        /// <summary>
        /// 方向贴表面切线
        /// </summary>
        /// <param name="direction">原始方向</param>
        /// <param name="surfaceNormal">表面法线</param>
        /// <param name="characterUp">角色上方向</param>
        /// <returns>切线方向</returns>
        public static Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal, Vector3 characterUp)
        {
            if (direction.sqrMagnitude <= 0f)
            {
                return Vector3.zero;
            }

            var eDirectionRight = Vector3.Cross(direction, characterUp);
            var eTangent = Vector3.Cross(surfaceNormal, eDirectionRight);
            return eTangent.sqrMagnitude > 0f ? eTangent.normalized : Vector3.ProjectOnPlane(direction, surfaceNormal).normalized;
        }

        /// <summary>
        /// 胶囊底部半球世界坐标
        /// </summary>
        /// <param name="position">角色位置</param>
        /// <param name="rotation">角色旋转</param>
        /// <param name="transformToBottomHemi">局部底端偏移</param>
        /// <param name="inflate">膨胀</param>
        /// <returns>底部半球位置</returns>
        public static Vector3 GetCapsuleBottomHemiAt(Vector3 position, Quaternion rotation, Vector3 transformToBottomHemi, float inflate = 0f)
        {
            return position + rotation * transformToBottomHemi + rotation * Vector3.down * inflate;
        }

        /// <summary>
        /// 胶囊顶部半球世界坐标
        /// </summary>
        /// <param name="position">角色位置</param>
        /// <param name="rotation">角色旋转</param>
        /// <param name="transformToTopHemi">局部顶端偏移</param>
        /// <param name="inflate">膨胀</param>
        /// <returns>顶部半球位置</returns>
        public static Vector3 GetCapsuleTopHemiAt(Vector3 position, Quaternion rotation, Vector3 transformToTopHemi, float inflate = 0f)
        {
            return position + rotation * transformToTopHemi + rotation * Vector3.up * inflate;
        }
    }
}
