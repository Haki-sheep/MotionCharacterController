using System;
using UnityEngine;

namespace MotionCharacterController
{
    // 参数配置：MCC 面板上能改的核心数值
    [Serializable]
    public partial class MccConfig
    {
        #region 胶囊体
        [Header("胶囊体")]
        public float capsuleHeight = 2f;
        public float capsuleRadius = 0.5f;
        public float capsuleYOffset = 0f;
        public PhysicsMaterial capsulePhysicsMaterial;
        #endregion

        #region 示例移动
        [Header("示例移动")]
        [Tooltip("示例 PlayerController 地面目标速度")]
        public float moveSpeed = 10f;
        [Tooltip("示例 PlayerController 转向速度")]
        public float rotationSpeed = 10f;
        [Tooltip("示例 PlayerController 地面速度插值锐度")]
        public float stableMovementSharpness = 15f;
        [Tooltip("示例 PlayerController 空中加速")]
        public float AirAcceleration = 30f;
        [Tooltip("示例 PlayerController 空中水平速度上限")]
        public float maxAirMoveSpeed = 10f;
        #endregion

        #region 跳跃和重力
        [Header("跳跃和重力")]
        [Tooltip("起跳向上速度 对齐 KCC JumpUpSpeed")]
        public float jumpSpeed = 10f;
        [Tooltip("重力加速度 对齐 KCC Gravity.y=-30")]
        public float gravity = -30f;
        [Tooltip("空中阻力 0  Drag=0")]
        public float airDrag = 0f;
        #endregion

        #region 地面
        [Header("地面")]
        [Tooltip("角色能稳定站住的最大坡度角比如 60 表示小于等于 60 度的斜坡都算地面")]
        public float maxStableSlopeAngle = 60f;
        [Tooltip("未稳定接地时的基础探测距离 太小下坡易短暂离地 太大吸地感过强")]
        public float groundProbeDistance = 0.2f;
        [Tooltip("额外增加的地面探测距离高速移动、下坡、台阶场景可以适当加大")]
        public float groundDetectionExtraDistance = 0f;
        [Tooltip("哪些层可以被当作稳定地面检测通常保持 Everything，特殊场景可只勾选 Ground")]
        public LayerMask stableGroundLayers = -1;
        [Tooltip("是否开启离散碰撞事件开启后，角色最终位置与碰撞体重叠时会回调 OnDiscreteCollisionDetected")]
        public bool discreteCollisionEvents = false;
        #endregion

        #region 台阶
        [Header("台阶")]
        [Tooltip("台阶处理方式None 不爬台阶；Standard 普通台阶；Extra 会额外检测更浅的小台阶")]
        public StepHandlingMethod stepHandling = StepHandlingMethod.Standard;
        [Tooltip("角色能自动走上去的最大台阶高度比如 0.5 表示半米以内的小台阶可以走上去")]
        public float maxStepHeight = 0.5f;
        [Tooltip("没有稳定接地时是否仍允许上台阶一般关闭，避免空中蹭墙时被台阶吸上去")]
        public bool allowSteppingWithoutStableGrounding = false;
        [Tooltip("Extra 台阶模式下，台阶至少要有多深才允许踩上去值越大越不容易爬很薄的边")]
        public float minRequiredStepDepth = 0.1f;
        #endregion

        #region 边缘
        [Header("边缘")]
        [Tooltip("是否开启边缘和落差检测开启后，角色站在平台边缘时会更稳定，不容易半个身子悬空还被当作站稳")]
        public bool ledgeAndDenivelationHandling = true;
        [Tooltip("角色离边缘多远以内还算能稳定站住通常不要大于胶囊半径")]
        public float maxStableDistanceFromLedge = 0.5f;
        [Tooltip("朝平台外侧移动速度超过这个值时，阻止继续吸附地面0 表示只要朝外移动就更容易离开边缘")]
        public float maxVelocityForLedgeSnap = 0f;
        [Range(1f, 180f)]
        [Tooltip("地面内外法线落差超过这个角度时，不再认为角色能稳定贴地用于防止陡坎处强行吸地")]
        public float maxStableDenivelationAngle = 180f;
        #endregion

        #region 刚体交互
        [Header("刚体交互")]
        [Tooltip("是否处理移动平台、动态刚体推挤、站在刚体上跟随移动等逻辑")]
        public bool interactiveRigidbodyHandling = true;
        [Tooltip("角色和动态刚体的交互方式Kinematic 更像霸体角色；SimulatedDynamic 会按模拟质量推刚体")]
        public RigidbodyInteractionType rigidbodyInteractionType = RigidbodyInteractionType.Kinematic;
        [Tooltip("模拟动态交互时角色的质量质量越大，推箱子时越有力量")]
        public float simulatedCharacterMass = 1f;
        [Tooltip("离开移动平台时是否保留平台速度开启后从移动平台跳下会带一点惯性")]
        public bool preserveAttachedRigidbodyMomentum = true;
        #endregion

        #region 约束
        [Header("约束")]
        [Tooltip("是否把角色移动限制在某个平面上普通 3D 角色一般关闭")]
        public bool hasPlanarConstraint = false;
        [Tooltip("开启平面约束时使用的法线方向比如 forward 表示限制在与 Z 轴垂直的平面上")]
        public Vector3 planarConstraintAxis = Vector3.forward;
        #endregion

        #region 求解安全
        [Header("求解安全")]
        [Tooltip("一次移动最多做多少次碰撞扫掠越大越不容易卡墙角，但性能消耗更高")]
        public int maxMovementIterations = 5;
        [Tooltip("角色一开始卡进墙里时，最多尝试推出几次一般 1 到 3 就够")]
        public int maxDecollisionIterations = 1;
        [Tooltip("移动迭代内是否先检查起步重叠 开启后更不容易穿模")]
        public bool checkMovementInitialOverlaps = true;
        [Tooltip("超过最大移动迭代次数时是否清空速度,开启能避免角色在复杂墙角里疯狂抖动")]
        public bool killVelocityWhenExceedMaxMovementIterations = true;
        [Tooltip("超过最大移动迭代次数时是否丢弃剩余位移开启能避免角色被挤进墙里")]
        public bool killRemainingMovementWhenExceedMaxMovementIterations = true;
        #endregion

        #region 系统
        [Header("系统")]
        [Tooltip("系统级开关 写入后同步到 MccSystem.AutoSimulation 关闭后需自行调用 MccSystem.Simulate")]
        public bool autoSimulation = true;
        [Tooltip("是否开启角色显示位置插值开启后画面更顺；如果相机或角色抖动，可临时关闭对比测试")]
        public bool interpolate = true;
        #endregion
    }
}
