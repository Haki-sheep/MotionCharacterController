using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 角色与动态刚体（箱子、球、可推动物体）的交互方式。可在 MccConfig 面板配置。
    /// </summary>
    public enum RigidbodyInteractionType
    {
        [LabelText("无交互")]
        None,

        [LabelText("运动学刚体")]
        Kinematic,

        [LabelText("模拟动态刚体")]
        SimulatedDynamic,
    }

    /// <summary>
    /// 遇到小台阶时是否自动抬脚走上去。可在 MccConfig 面板配置。
    /// </summary>
    public enum StepHandlingMethod
    {
        [LabelText("不处理台阶")]
        None,

        [LabelText("标准台阶处理")]
        Standard,

        [LabelText("加强台阶处理")]
        Extra,
    }

    [ReadOnly]
    public enum MovementSweepState
    {
        // 初始扫掠状态
        Initial,

        // 首次命中后
        AfterFirstHit,

        // 发现内角折线阻挡
        FoundBlockingCrease,

        // 发现外角死角阻挡
        FoundBlockingCorner,
    }


    /// <summary>
    /// 角色运动状态 该结构体用于保存角色运动状态 
    /// 后续便利于网络同步/回滚等操作
    /// </summary>
    [Serializable]
    public struct MotionCharacterMotorState
    {
        // 帧状态
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 BaseVelocity;
        // 地面信息
        public bool MustUnground;
        public float MustUngroundTime;
        public bool LastMovementIterationFoundAnyGround;
        public CharacterTransientGroundingReport GroundingStatus;
        // 刚体信息
        public Rigidbody AttachedRigidbody;
        public Vector3 AttachedRigidbodyVelocity;
    }

    /// <summary>
    /// 本帧角色接地状态 该结构体用于保存角色接地状态 
    /// </summary>
    public struct CharacterGroundingReport
    {
        // 扫略到任何地面
        public bool FoundAnyGround;
        // 是否稳定在地面上
        public bool IsStableOnGround;
        // 是否阻止地面吸附
        public bool SnappingPrevented;

        // 地面法线
        public Vector3 GroundNormal;
        // 内部地面法线 内外法线用于判断是否是边缘状态 引导后续的吸附处理
        public Vector3 InnerGroundNormal;
        // 外部地面法线 比如:大部分身子站在边缘内 则吸附到边缘内侧 大部分身子站在边缘外 则不吸附
        public Vector3 OuterGroundNormal;

        // 下面两个信息用于移动平台的处理     
        // 地面碰撞体 
        public Collider GroundCollider;
        // 地面点
        public Vector3 GroundPoint;

        /// <summary>
        /// 复制信息
        /// </summary>
        /// <param name="transientGroundingReport"></param>
        public void CopyFrom(CharacterTransientGroundingReport transientGroundingReport)
        {
            FoundAnyGround = transientGroundingReport.FoundAnyGround;
            IsStableOnGround = transientGroundingReport.IsStableOnGround;
            SnappingPrevented = transientGroundingReport.SnappingPrevented;
            GroundNormal = transientGroundingReport.GroundNormal;
            InnerGroundNormal = transientGroundingReport.InnerGroundNormal;
            OuterGroundNormal = transientGroundingReport.OuterGroundNormal;
            GroundCollider = null;
            GroundPoint = Vector3.zero;
        }

        /// <summary>
        /// 转换为瞬态接地状态
        /// </summary>
        /// <returns></returns>
        public CharacterTransientGroundingReport ToTransient()
        {
            return new CharacterTransientGroundingReport
            {
                FoundAnyGround = FoundAnyGround,
                IsStableOnGround = IsStableOnGround,
                SnappingPrevented = SnappingPrevented,
                GroundNormal = GroundNormal,
                InnerGroundNormal = InnerGroundNormal,
                OuterGroundNormal = OuterGroundNormal,
            };
        }
    }

    /// <summary>
    /// 角色瞬态接地状态 该结构体用于保存角色瞬态接地状态 
    /// 通常是上一帧的信息 是CharacterGroundingReport的精简版 没有Colllider信息
    /// </summary>
    [Serializable]
    public struct CharacterTransientGroundingReport
    {
        public bool FoundAnyGround;
        public bool IsStableOnGround;
        public bool SnappingPrevented;
        public Vector3 GroundNormal;
        public Vector3 InnerGroundNormal;
        public Vector3 OuterGroundNormal;

        public void CopyFrom(CharacterGroundingReport groundingReport)
        {
            FoundAnyGround = groundingReport.FoundAnyGround;
            IsStableOnGround = groundingReport.IsStableOnGround;
            SnappingPrevented = groundingReport.SnappingPrevented;
            GroundNormal = groundingReport.GroundNormal;
            InnerGroundNormal = groundingReport.InnerGroundNormal;
            OuterGroundNormal = groundingReport.OuterGroundNormal;
        }
    }

    /// <summary>
    /// 碰撞稳定性报告 该结构体用于保存碰撞稳定性报告 
    /// </summary>
    public struct HitStabilityReport
    {
        // 是否稳定
        public bool IsStable;
        // 是否发现内部地面法线
        public bool FoundInnerNormal;
        // 是否发现外部地面法线
        public bool FoundOuterNormal;
        // 内部地面法线
        public Vector3 InnerNormal;
        // 外部地面法线
        public Vector3 OuterNormal;

        // 是否发现有效台阶
        public bool ValidStepDetected;
        // 台阶碰撞体
        public Collider SteppedCollider;

        // 是在边缘
        public bool LedgeDetected;
        // 是否站在悬空那一侧
        public bool IsOnEmptySideOfLedge;
        // 脚底到边缘命中点的水平距离
        public float DistanceFromLedge;
        // 是否正朝悬崖方向移动
        public bool IsMovingTowardsEmptySideOfLedge;
        // 边缘可站立侧的法线
        public Vector3 LedgeGroundNormal;
        //  沿悬崖边缘横向的方向
        public Vector3 LedgeRightDirection;
        //  朝向悬崖外的方向
        public Vector3 LedgeFacingDirection;
    }

    /// <summary>
    /// 刚体投影碰撞 该结构体用于保存刚体投影碰撞信息 
    /// </summary>
    public struct RigidbodyProjectionHit
    {
        // 碰到的刚体
        public Rigidbody Rigidbody;
        // 碰撞点
        public Vector3 HitPoint;
        // 有效碰撞法线
        public Vector3 EffectiveHitNormal;
        // 碰撞时的速度
        public Vector3 HitVelocity;
    }

    /// <summary>
    /// 重叠结果 该结构体用于保存重叠结果信息 
    /// </summary>
    public struct OverlapResult
    {
        // 法线
        public Vector3 Normal;
        // 碰撞体
        public Collider Collider;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="normal">法线</param>
        /// <param name="collider">碰撞体</param>
        public OverlapResult(Vector3 normal, Collider collider)
        {
            Normal = normal;
            Collider = collider;
        }
    }

    /// <summary>
    /// 单次移动命中 统一重叠与扫掠两种来源
    /// </summary>
    public struct MovementHit
    {
        /// <summary>命中碰撞体</summary>
        public Collider Collider;
        /// <summary>命中法线</summary>
        public Vector3 Normal;
        /// <summary>命中点</summary>
        public Vector3 Point;
        /// <summary>命中距离 重叠时为 0</summary>
        public float Distance;
    }

    /// <summary>
    /// 电机只读调试快照 供编辑器采样 不暴露 RuntimeData
    /// </summary>
    public struct MccMotorDebugSample
    {
        public Vector3 TransientPosition;
        public Quaternion TransientRotation;
        public Vector3 CharacterUp;
        public Vector3 BaseVelocity;
        public Vector3 AttachedRigidbodyVelocity;
        public Vector3 Velocity;
        public Rigidbody AttachedRigidbody;
        public CharacterGroundingReport GroundingStatus;
        public float MustUngroundTimeCounter;
        public bool SolveMovementCollisions;
        public bool SolveGrounding;
        public MovementSweepState DebugLastSweepState;
        public int DebugLastMovementSweeps;
        public bool DebugLastMoveCompleted;
        public bool DebugHasMovementHit;
        public Vector3 DebugLastHitPoint;
        public Vector3 DebugLastHitNormal;
        public float DebugGroundProbeDistance;
        public float DebugPhase1Seconds;
        public float DebugPhase2Seconds;
        public bool HasCapsule;
        public float CapsuleRadius;
        public float CapsuleHeight;
        public Vector3 CapsuleCenter;
        public Vector3 CapsuleBottomHemi;
        public int OverlapsCount;
        public Vector3[] OverlapNormals;
    }
}
