using Sirenix.OdinInspector;
using UnityEngine;

namespace MotionCharacterController
{
    [RequireComponent(typeof(CapsuleCollider))]
    public partial class MotionCC : MonoBehaviour, IMccMotorService
    {
        [BoxGroup("基础数据")]
        [SerializeField]
        private MccConfig config = new MccConfig();

        [BoxGroup("调试")]
        [ShowInInspector, ReadOnly]
        public bool isGrounded => GroundingStatus.IsStableOnGround;

        [SerializeField]
        private bool drawGroundGizmos = true;

        #region 变量
        private readonly MccMotorContext context = new MccMotorContext();
        private CollisionMoveExecutor collisionMoveExecutor;
        private GroundingExecutor groundingExecutor;
        private StepExecutor stepExecutor;
        private PlatformExecutor platformExecutor;
        private RigidbodyExecutor rigidbodyExecutor;
        private HitStabilityExecutor hitStabilityExecutor;
        private IMcc controller;
        private Vector3 inputDirection;
        private bool jumpRequested;
        #endregion

        #region 属性
        public MccConfig Config => config;
        public IMcc Controller => controller;
        public CharacterGroundingReport GroundingStatus => context.GroundingStatus;
        public CharacterTransientGroundingReport LastGroundingStatus => context.LastGroundingStatus;
        public Vector3 InputDirection => inputDirection;
        public bool JumpRequested => jumpRequested;
        public Vector3 TransientPosition => context.TransientPosition;
        public Quaternion TransientRotation => context.TransientRotation;
        public Vector3 CharacterUp => context.CharacterUp;
        public Vector3 CharacterForward => context.CharacterForward;
        public Vector3 CharacterRight => context.CharacterRight;
        public Vector3 Velocity => context.BaseVelocity + context.AttachedRigidbodyVelocity;
        public float VerticalSpeed => Vector3.Dot(context.BaseVelocity, context.CharacterUp);
        public Rigidbody AttachedRigidbody => context.AttachedRigidbody;
        public Vector3 AttachedRigidbodyVelocity => context.AttachedRigidbodyVelocity;
        public CapsuleCollider Capsule => context.Capsule;
        public LayerMask CollidableLayers => context.CollidableLayers;
        public int OverlapsCount => context.OverlapsCount;
        public OverlapResult[] Overlaps => context.Overlaps;

        public Vector3 BaseVelocity
        {
            get => context.BaseVelocity;
            set => context.BaseVelocity = value;
        }
        #endregion

        /// <summary>
        /// 在Inspector中修改时触发 ValidateData 校验数据
        /// </summary>
        private void OnValidate()
        {
            ValidateData();
            MccSystem.SyncAutoSimulationFromCharacters();
        }

        private void Awake()
        {
            ValidateData();
            InitExecutors();
            SyncTransientFromTransform();
            controller = GetComponent<IMcc>();
        }

        private void OnEnable()
        {
            SyncTransientFromTransform();
            MccSystem.RegisterCharacter(this);
        }

        private void OnDisable()
        {
            MccSystem.UnregisterCharacter(this);
        }

        /// <summary>
        /// 用场景 Transform 初始化瞬时位姿 避免首帧 PreSimulation 用默认值把人拽走
        /// </summary>
        private void SyncTransientFromTransform()
        {
            context.TransientPosition = transform.position;
            context.TransientRotation = transform.rotation;
            context.InitialTickPosition = context.TransientPosition;
            context.InitialTickRotation = context.TransientRotation;
        }

        private void Update()
        {
            // 更新输入方向和跳跃请求
            controller?.InputVectorUpdate(ref inputDirection, ref jumpRequested);
        }

        /// <summary>
        /// 预模拟帧
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        internal void PreSimulationTick(float deltaTime)
        {
            context.InitialTickPosition = context.TransientPosition;
            context.InitialTickRotation = context.TransientRotation;
            context.Transform.SetPositionAndRotation(context.TransientPosition, context.TransientRotation);
        }

        /// <summary>
        /// 更新阶段1
        /// 此阶段主要更新
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        internal void UpdatePhase1(float deltaTime)
        {
            float phaseStart = Time.realtimeSinceStartup;
            context.BeginSimulation();
            context.SanitizeVelocity();
            rigidbodyExecutor.Clear();

            controller?.BeforeCharacterUpdate(deltaTime);

            // 如果移动位置标记为脏
            if (context.MovePositionDirty)
            {
                var moveDistance = context.MovePositionTarget - context.TransientPosition;
                var eMoveVelocity = MccGeometryCalculator.GetVelocityFromMovement(moveDistance, deltaTime);
                // 如果需要解决移动碰撞
                if (context.SolveMovementCollisions)
                    collisionMoveExecutor.Move(ref eMoveVelocity, deltaTime);
                else
                    context.TransientPosition = context.MovePositionTarget;
                
                context.MovePositionDirty = false;
            }
    
            // 如果需要解决移动碰撞
            if (context.SolveMovementCollisions)
                collisionMoveExecutor.ResolveInitialOverlaps();

            // 更新接地状态
            groundingExecutor.UpdateGrounding(deltaTime);
            // 后接地更新
            controller?.PostGroundingUpdate(deltaTime);
            // 更新平台附件
            platformExecutor.UpdateAttachment(deltaTime);
            context.DebugPhase1Seconds = Time.realtimeSinceStartup - phaseStart;
        }

        /// <summary>
        /// 更新阶段2
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        internal void UpdatePhase2(float deltaTime)
        {
            float phaseStart = Time.realtimeSinceStartup;
            Quaternion rotation = context.TransientRotation;
            controller?.UpdateRotation(ref rotation, deltaTime);
            context.TransientRotation = rotation.normalized;

            // 如果移动旋转标记为脏
            if (context.MoveRotationDirty)
            {
                context.TransientRotation = context.MoveRotationTarget.normalized;
                context.MoveRotationDirty = false;
            }

            // 如果需要解决移动碰撞
            if (context.SolveMovementCollisions)
            {
                collisionMoveExecutor.ResolveInitialOverlaps();
            }

            // 更新速度
            controller?.UpdateVelocity(ref context.BaseVelocity, deltaTime);

            // 如果速度小于最小速度
            if (context.BaseVelocity.magnitude < MccConfig.MIN_VELOCITY_MAGNITUDE)
            {
                context.BaseVelocity = Vector3.zero;
            }

            // 如果速度大于0
            if (context.BaseVelocity.sqrMagnitude > 0f)
            {
                if (context.SolveMovementCollisions)
                {
                    collisionMoveExecutor.Move(ref context.BaseVelocity, deltaTime);
                }
                else
                {
                    context.TransientPosition += context.BaseVelocity * deltaTime;
                }
            }

            // 处理速度碰撞
            rigidbodyExecutor.ProcessVelocityForHits(ref context.BaseVelocity, deltaTime);
            // 处理离散碰撞事件
            collisionMoveExecutor.ProcessDiscreteCollisionEvents();
            // 角色更新后的收尾阶段
            controller?.AfterCharacterUpdate(deltaTime);
            // 记录阶段2时间
            context.DebugPhase2Seconds = Time.realtimeSinceStartup - phaseStart;
        }

        /// <summary>
        /// 提交模拟
        /// </summary>
        /// <param name="interpolate">是否插值</param>
        internal void CommitSimulation(bool interpolate)
        {
            // 如果需要插值 则设置为初始位置和旋转让后续LateUpdate插值使用
            if (interpolate)
            {
                context.Transform.SetPositionAndRotation(context.InitialTickPosition,
                                                         context.InitialTickRotation);
            }
            // 否则设置为当前位置和旋转
            else
            {
                context.Transform.SetPositionAndRotation(context.TransientPosition,
                                                         context.TransientRotation);
            }
        }

        /// <summary>
        /// 插值更新
        /// </summary>
        /// <param name="factor">插值系数</param>
        internal void InterpolationUpdate(float factor)
        {
            // 插值位置和旋转
            context.Transform.SetPositionAndRotation(
                Vector3.Lerp(context.InitialTickPosition, context.TransientPosition, factor),
                Quaternion.Slerp(context.InitialTickRotation, context.TransientRotation, factor));
        }


        /// <summary>
        /// 校验数据
        /// </summary>
        private void ValidateData()
        {
            if (context.Transform is null)
                context.Transform = transform;

            // 注意这里 从Unity获取胶囊体组件 后续给到Context 相当于程序的入口
            context.Capsule = GetComponent<CapsuleCollider>();
            context.Config = config;
            context.Owner = this;
            context.Transform.localScale = Vector3.one;

            // 设置Context中的胶囊体尺寸
            if (context.Capsule is not null)
            {
                SetCapsuleDimensions(config.capsuleRadius, config.capsuleHeight, config.capsuleYOffset);
                context.Capsule.direction = 1;
                context.Capsule.sharedMaterial = config.capsulePhysicsMaterial;
            }

            // 刷新Context中的可碰撞层和角色朝向
            context.RefreshCollidableLayers();
            context.RefreshCharacterAxes();
        }

        /// <summary>
        /// 组装全部 Executor
        /// </summary>
        private void InitExecutors()
        {
            if (collisionMoveExecutor is not null)
                return;

            stepExecutor = new StepExecutor(context);
            rigidbodyExecutor = new RigidbodyExecutor(context);
            hitStabilityExecutor = new HitStabilityExecutor(context, stepExecutor);
            collisionMoveExecutor = new CollisionMoveExecutor(context, stepExecutor, rigidbodyExecutor, hitStabilityExecutor);
            groundingExecutor = new GroundingExecutor(context, collisionMoveExecutor, hitStabilityExecutor);
            platformExecutor = new PlatformExecutor(context, collisionMoveExecutor);
        }
    }
}
