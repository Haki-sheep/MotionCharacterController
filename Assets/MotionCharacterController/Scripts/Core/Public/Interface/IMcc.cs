using UnityEngine;

namespace MotionCharacterController
{
    // 对外主控制接口：给上层逻辑实现
    public interface IMcc
    {
        #region 渲染帧
        /// <summary>
        /// 输入更新
        /// </summary>
        /// <param name="inputDirection"></param>
        /// <param name="jumpRequested"></param>
        void InputVectorUpdate(ref Vector3 inputDirection, ref bool jumpRequested);

        #endregion

        #region 物理帧
        /// <summary>
        /// 角色更新前的准备阶段
        /// </summary>
        /// <param name="deltaTime"></param>
        void BeforeCharacterUpdate(float deltaTime);

        /// <summary>
        /// 更新角色旋转
        /// </summary>
        /// <param name="currentRotation"></param>
        /// <param name="deltaTime"></param>
        void UpdateRotation(ref Quaternion currentRotation, float deltaTime);

        /// <summary>
        /// 更新角色速度
        /// </summary>
        /// <param name="currentVelocity"></param>
        /// <param name="deltaTime"></param>
        void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);

        /// <summary>
        /// 接地检测完成后调用，适合重置跳跃次数、切换落地状态
        /// </summary>
        /// <param name="deltaTime"></param>
        void PostGroundingUpdate(float deltaTime);

        /// <summary>
        /// 角色更新后的收尾阶段
        /// </summary>
        /// <param name="deltaTime"></param>
        void AfterCharacterUpdate(float deltaTime);

        #endregion

        #region 外部开发者调用
        /// <summary>
        /// 判断某个碰撞体是否参与角色碰撞
        /// </summary>
        /// <param name="coll"></param>
        /// <returns></returns>
        bool IsColliderValidForCollisions(Collider coll);

        /// <summary>
        /// 地面探测命中时调用
        /// </summary>
        /// <param name="hitCollider"></param>
        /// <param name="hitNormal"></param>
        /// <param name="hitPoint"></param>
        /// <param name="hitStabilityReport"></param>
        void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);

        /// <summary>
        /// 移动扫掠命中时调用
        /// </summary>
        /// <param name="hitCollider"></param>
        /// <param name="hitNormal"></param>
        /// <param name="hitPoint"></param>
        /// <param name="hitStabilityReport"></param>
        void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport);

        /// <summary>
        /// 给玩法层最后一次修改稳定性报告的机会
        /// </summary>
        /// <param name="hitCollider">碰撞体</param>
        /// <param name="hitNormal">碰撞法线</param>
        /// <param name="hitPoint">碰撞点</param>
        /// <param name="atCharacterPosition">角色位置</param>
        /// <param name="atCharacterRotation">角色旋转</param>
        /// <param name="hitStabilityReport">稳定性报告</param>
        void ProcessHitStabilityReport(Collider hitCollider,
                                       Vector3 hitNormal,
                                       Vector3 hitPoint,
                                       Vector3 atCharacterPosition,
                                       Quaternion atCharacterRotation,
                                       ref HitStabilityReport hitStabilityReport);

        /// <summary> 
        /// 离散碰撞事件
        /// </summary>
        /// <param name="hitCollider"></param>
        void OnDiscreteCollisionDetected(Collider hitCollider);
        #endregion
    }
}
