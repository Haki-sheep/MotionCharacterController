using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 电机模块对外命令与只读查询
    /// </summary>
    public interface IMccMotorService
    {
        MccConfig Config { get; }
        CharacterGroundingReport GroundingStatus { get; }
        Vector3 TransientPosition { get; }
        Quaternion TransientRotation { get; }
        Vector3 CharacterUp { get; }
        Vector3 Velocity { get; }

        /// <summary>
        /// 消耗跳跃请求
        /// </summary>
        void ConsumeJumpRequest();

        /// <summary>
        /// 获取状态快照
        /// </summary>
        /// <returns>状态袋</returns>
        MotionCharacterMotorState GetState();

        /// <summary>
        /// 应用状态快照
        /// </summary>
        /// <param name="state">状态袋</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        void ApplyState(MotionCharacterMotorState state, bool bypassInterpolation = true);

        /// <summary>
        /// 强制解除接地
        /// </summary>
        /// <param name="time">离地持续时间</param>
        void ForceUnground(float time = 0.1f);

        /// <summary>
        /// 查询是否必须离地
        /// </summary>
        /// <returns>是否必须离地</returns>
        bool MustUnground();

        /// <summary>
        /// 下一模拟帧移动到目标位置
        /// </summary>
        /// <param name="toPosition">目标位置</param>
        void MoveCharacter(Vector3 toPosition);

        /// <summary>
        /// 下一模拟帧旋转到目标
        /// </summary>
        /// <param name="toRotation">目标旋转</param>
        void RotateCharacter(Quaternion toRotation);

        /// <summary>
        /// 立即设置位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        void SetPosition(Vector3 position, bool bypassInterpolation = true);

        /// <summary>
        /// 立即设置旋转
        /// </summary>
        /// <param name="rotation">目标旋转</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        void SetRotation(Quaternion rotation, bool bypassInterpolation = true);

        /// <summary>
        /// 立即设置位姿
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool bypassInterpolation = true);

        /// <summary>
        /// 设置胶囊尺寸
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="height">高度</param>
        /// <param name="yOffset">Y偏移</param>
        void SetCapsuleDimensions(float radius, float height, float yOffset);

        /// <summary>
        /// 设置胶囊碰撞激活
        /// </summary>
        /// <param name="collisionsActive">是否激活</param>
        void SetCapsuleCollisionsActivation(bool collisionsActive);

        /// <summary>
        /// 设置移动碰撞解算激活
        /// </summary>
        /// <param name="active">是否激活</param>
        void SetMovementCollisionsSolvingActivation(bool active);

        /// <summary>
        /// 设置接地解算激活
        /// </summary>
        /// <param name="active">是否激活</param>
        void SetGroundSolvingActivation(bool active);

        /// <summary>
        /// 位移换算速度
        /// </summary>
        /// <param name="movement">位移</param>
        /// <param name="deltaTime">时间差</param>
        /// <returns>速度</returns>
        Vector3 GetVelocityFromMovement(Vector3 movement, float deltaTime);

        /// <summary>
        /// 方向贴表面切线
        /// </summary>
        /// <param name="direction">原始方向</param>
        /// <param name="surfaceNormal">表面法线</param>
        /// <returns>切线方向</returns>
        Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal);

        /// <summary>
        /// 胶囊重叠查询
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="overlappedColliders">碰撞体缓冲</param>
        /// <param name="inflate">膨胀</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>有效重叠数</returns>
        int CharacterCollisionsOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, float inflate = 0f, bool acceptOnlyStableGroundLayer = false);

        /// <summary>
        /// 胶囊扫掠查询
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近命中</param>
        /// <param name="hits">命中缓冲</param>
        /// <param name="inflate">膨胀</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>有效命中数</returns>
        int CharacterCollisionsSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, float inflate = 0f, bool acceptOnlyStableGroundLayer = false);

        /// <summary>
        /// 射线查询
        /// </summary>
        /// <param name="position">起点</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近命中</param>
        /// <param name="hits">命中缓冲</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>有效命中数</returns>
        int CharacterCollisionsRaycast(Vector3 position, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, bool acceptOnlyStableGroundLayer = false);

        /// <summary>
        /// 采集只读调试快照
        /// </summary>
        /// <returns>调试快照</returns>
        MccMotorDebugSample CaptureDebugSample();
    }
}
