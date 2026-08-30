using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 上下文类，保存了角色运动所需的各种数据和状态
    /// 部分信息来自于Unity引擎
    /// </summary>
    public class MccMotorContext
    {
        // 角色控制器
        public MotionCC Owner;
        // 配置
        public MccConfig Config;
        // 角色Transform
        public Transform Transform;
        // 角色胶囊体
        public CapsuleCollider Capsule;

        // 瞬时位置
        public Vector3 TransientPosition;
        // 瞬时旋转
        private Quaternion transientRotation = Quaternion.identity;
        /// <summary>
        /// 瞬时旋转 三轴始终保持与角色朝向一致 用于某些非世界空间下的旋转
        /// </summary>
        public Quaternion TransientRotation
        {
            get => transientRotation;
            set
            {
                transientRotation = value.normalized;
                RefreshCharacterAxes();
            }
        }

        // 角色朝上方向
        public Vector3 CharacterUp = Vector3.up;
        // 角色朝前方向
        public Vector3 CharacterForward = Vector3.forward;
        // 角色朝右方向
        public Vector3 CharacterRight = Vector3.right;
        // 初始模拟位置
        public Vector3 InitialSimulationPosition;
        // 初始模拟旋转
        public Quaternion InitialSimulationRotation = Quaternion.identity;
        // 初始帧位置
        public Vector3 InitialTickPosition;
        // 初始帧旋转
        public Quaternion InitialTickRotation = Quaternion.identity;

        // 胶囊体中心
        public Vector3 CapsuleCenter;
        // 胶囊体中心到Transform的转换
        public Vector3 TransformToCapsuleCenter;
        // 胶囊体底部到Transform的转换
        public Vector3 TransformToCapsuleBottom;
        // 胶囊体顶部到Transform的转换
        public Vector3 TransformToCapsuleTop;
        // 胶囊体底部半球到Transform的转换
        public Vector3 TransformToCapsuleBottomHemi;
        // 胶囊体顶部半球到Transform的转换
        public Vector3 TransformToCapsuleTopHemi;

        // 本帧接地状态 
        public CharacterGroundingReport GroundingStatus = new CharacterGroundingReport();
        // 上一帧的接地状态
        public CharacterTransientGroundingReport LastGroundingStatus = new CharacterTransientGroundingReport();
        // 可碰撞层掩码
        public LayerMask CollidableLayers = -1;
        // 基础速度
        public Vector3 BaseVelocity;
        // 附着的刚体
        public Rigidbody AttachedRigidbody;
        // 上一帧附着的刚体
        public Rigidbody LastAttachedRigidbody;
        // 附着的刚体覆盖
        public Rigidbody AttachedRigidbodyOverride;
        // 附着的刚体速度
        public Vector3 AttachedRigidbodyVelocity;
        // 是否移动
        public bool LastMovementIterationFoundAnyGround;
        // 是否解决移动碰撞
        public bool SolveMovementCollisions = true;
        // 是否解决接地
        public bool SolveGrounding = true;
        // 是否移动位置脏了
        public bool MovePositionDirty;
        // 移动位置目标
        public Vector3 MovePositionTarget;
        // 是否移动旋转脏了
        public bool MoveRotationDirty;
        // 移动旋转目标
        public Quaternion MoveRotationTarget = Quaternion.identity;
        // 是否从附着的刚体移动
        public bool IsMovingFromAttachedRigidbody;
        // 是否必须离地
        public bool MustUnground;
        // 必须离地时间计数器
        public float MustUngroundTimeCounter;
        // 重叠数量
        public int OverlapsCount;
        // 重叠结果
        public readonly OverlapResult[] Overlaps = new OverlapResult[MccConfig.MAX_COLLISION_OVERLAPS];
        // 内部命中结果
        public readonly RaycastHit[] InternalHits = new RaycastHit[MccConfig.MAX_COLLISION_HITS];
        // 内部碰撞体
        public readonly Collider[] InternalColliders = new Collider[MccConfig.MAX_COLLISION_OVERLAPS];

        #region 调试采样
        /// <summary>本帧移动扫掠状态</summary>
        public MovementSweepState DebugLastSweepState;
        /// <summary>本帧移动迭代次数</summary>
        public int DebugLastMovementSweeps;
        /// <summary>本帧移动是否在迭代上限内完成</summary>
        public bool DebugLastMoveCompleted = true;
        /// <summary>本帧最近命中点</summary>
        public Vector3 DebugLastHitPoint;
        /// <summary>本帧最近命中法线</summary>
        public Vector3 DebugLastHitNormal;
        /// <summary>本帧是否有移动命中</summary>
        public bool DebugHasMovementHit;
        /// <summary>本帧地面探测距离</summary>
        public float DebugGroundProbeDistance;
        /// <summary>本帧 Phase1 耗时秒</summary>
        public float DebugPhase1Seconds;
        /// <summary>本帧 Phase2 耗时秒</summary>
        public float DebugPhase2Seconds;
        /// <summary>上一帧附着刚体 用于换平台检测</summary>
        public Rigidbody DebugPreviousAttachedRigidbody;
        #endregion

        /// <summary>
        /// 开始模拟
        /// 重置瞬时位置和旋转 初始模拟位置和旋转 重叠数量 上一帧接地状态 当前帧接地状态
        /// </summary>
        public void BeginSimulation()
        {
            TransientPosition = Transform.position;
            TransientRotation = Transform.rotation;
            InitialSimulationPosition = TransientPosition;
            InitialSimulationRotation = TransientRotation;
            OverlapsCount = 0;
            LastGroundingStatus.CopyFrom(GroundingStatus);

            GroundingStatus = new CharacterGroundingReport
             { GroundNormal = CharacterUp, 
             InnerGroundNormal = CharacterUp, 
             OuterGroundNormal = CharacterUp };

            DebugHasMovementHit = false;
            DebugLastSweepState = MovementSweepState.Initial;
            DebugLastMovementSweeps = 0;
            DebugLastMoveCompleted = true;
        }

        /// <summary>
        /// 刷新Context中的胶囊体数据
        /// </summary>
        public void RefreshCapsuleData()
        {
            if (Capsule is null)
                return;

            CapsuleCenter = Capsule.center;
            TransformToCapsuleCenter = Capsule.center;
            TransformToCapsuleBottom = Capsule.center + Vector3.down * (Capsule.height * 0.5f);
            TransformToCapsuleTop = Capsule.center + Vector3.up * (Capsule.height * 0.5f);
            TransformToCapsuleBottomHemi = Capsule.center + Vector3.down * (Capsule.height * 0.5f - Capsule.radius);
            TransformToCapsuleTopHemi = Capsule.center + Vector3.up * (Capsule.height * 0.5f - Capsule.radius);
        }

        /// <summary>
        /// 刷新角色朝向
        /// </summary>
        public void RefreshCharacterAxes()
        {
            CharacterUp = transientRotation * Vector3.up;
            CharacterForward = transientRotation * Vector3.forward;
            CharacterRight = transientRotation * Vector3.right;
        }

        /// <summary>
        /// 刷新可碰撞层掩码
        /// 遍历所有层 如果层不忽略层碰撞 则添加到可碰撞层掩码
        /// </summary>
        public void RefreshCollidableLayers()
        {
            CollidableLayers = 0;
            // 获取角色Transform的层的Layer
            int layer = Transform is not null ?
                         Transform.gameObject.layer : LayerMask.NameToLayer("Default");
            for (int i = 0; i < 32; i++)
            {
                // GetIgnoreLayerCollision = 获取层是否忽略层碰撞 
                // false代表不忽略 取反后
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                {
                    // 或运算添加到CollidableLayers掩码里
                    // 比如CollidableLayers = 0 i=10=>00001010
                    // 00000 | 00001010 = 00001010 说明Layer10不忽略碰撞
                    CollidableLayers |= 1 << i;
                }
            }
        }

        /// <summary>
        /// 清理速度
        /// 如果速度是NaN 则清理为0
        /// </summary>
        public void SanitizeVelocity()
        {
            BaseVelocity = MccGeometryCalculator.SanitizeVelocity(BaseVelocity);
            AttachedRigidbodyVelocity = MccGeometryCalculator.SanitizeVelocity(AttachedRigidbodyVelocity);
        }

        /// <summary>
        /// 判断是否稳定站立
        /// 角色朝上方向与法线夹角小于等于最大稳定坡度角
        /// </summary>
        /// <param name="normal">法线</param>
        /// <returns>是否稳定站立</returns>
        public bool IsStableOnNormal(Vector3 normal)
        {
            return MccGeometryCalculator.IsStableOnNormal(CharacterUp, normal, Config.maxStableSlopeAngle);
        }

        /// <summary>
        /// 判断碰撞体要不要参与 MCC 的碰撞/地面检测
        /// </summary>
        /// <param name="coll">碰撞体</param>
        /// <returns>是否有效</returns>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (coll is null || coll == Capsule)
            {
                return false;
            }
            
            Rigidbody attachedRigidbody = coll.attachedRigidbody;
            if (attachedRigidbody is not null)
            {
                // 不做下面判断 不然会导致角色和移动平台打架 或者 不符合角色的刚体交互类型的表现
                // 如果角色状态是跟着附着的刚体移动 且 附着的刚体是当前附着的刚体 则不参与碰撞/地面检测
                if (IsMovingFromAttachedRigidbody && attachedRigidbody == AttachedRigidbody)
                {
                    return false;
                }

                // 如果当前角色为运动学 说明是霸体状态 和可推动物品无物理交互(仍然会产生碰撞)
                // 如果附着的刚体非运动学 则说明其是可推动的 所以MCC应该忽略其碰撞 
                // 不然就会出现角色和可推动物品打架的情况
                if (Config.rigidbodyInteractionType == RigidbodyInteractionType.Kinematic 
                && !attachedRigidbody.isKinematic)
                {
                    // 唤醒刚体是为了Unity物理引擎的碰撞检测
                    attachedRigidbody.WakeUp();
                    return false;
                }
            }

            // 如果角色控制器为空 或 外部开发者认为该碰撞体参与碰撞/地面检测 则参与碰撞/地面检测
            return Owner.Controller is null 
                || Owner.Controller.IsColliderValidForCollisions(coll);
        }

        /// <summary>
        /// 获取方向切线
        /// 通常是获取沿斜坡表面的方向向量
        /// </summary>
        /// <param name="direction">方向</param>
        /// <param name="surfaceNormal">法线</param>
        /// <returns>方向切线</returns> 
        public Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
        {
            return MccGeometryCalculator.GetDirectionTangentToSurface(direction, surfaceNormal, CharacterUp);
        }

        /// <summary>
        /// 获取胶囊体底部半球的位置
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="inflate">膨胀</param>
        /// <returns>胶囊体底部半球的位置</returns>
        public Vector3 GetCapsuleBottomHemiAt(Vector3 position, Quaternion rotation, float inflate = 0f)
        {
            return MccGeometryCalculator.GetCapsuleBottomHemiAt(position, rotation, TransformToCapsuleBottomHemi, inflate);
        }

        /// <summary>
        /// 获取胶囊体顶部半球的位置
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="inflate">膨胀</param>
        /// <returns>胶囊体顶部半球的位置</returns>
        public Vector3 GetCapsuleTopHemiAt(Vector3 position, Quaternion rotation, float inflate = 0f)
        {
            return MccGeometryCalculator.GetCapsuleTopHemiAt(position, rotation, TransformToCapsuleTopHemi, inflate);
        }
    }
}
