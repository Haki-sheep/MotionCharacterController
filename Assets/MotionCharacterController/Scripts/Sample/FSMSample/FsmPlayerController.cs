using MiMieFSM.Unity;
using MiMieFSM.UpdateFsm;
using UnityEngine;

namespace MotionCharacterController.Samples.Fsm
{
    /// <summary>
    /// FSM 样例 IMcc 宿主 对标 NormalSample PlayerController
    /// </summary>
    [RequireComponent(typeof(MotionCC))]
    [RequireComponent(typeof(UpdateFsmHost))]
    [DefaultExecutionOrder(10)]
    public class FsmPlayerController : MonoBehaviour, IMcc
    {
        [SerializeField]
        private ExampleMCharacterCamera characterCamera;

        [SerializeField, Range(0f, 1f)]
        private float inputDeadZone = 0.1f;

        [SerializeField]
        private float jumpApexSpeedThreshold = 0.5f;

        [Header("下蹲")]
        [SerializeField]
        private KeyCode crouchKey = KeyCode.C;

        [SerializeField]
        private float crouchedCapsuleHeight = 1f;

        [SerializeField, Range(0.1f, 1f)]
        private float crouchMoveSpeedScale = 0.5f;

        [SerializeField]
        private Transform crouchMeshRoot;

        /// <summary>
        /// 马达
        /// </summary>
        private MotionCC motion;

        /// <summary>
        /// FSM 宿主
        /// </summary>
        private UpdateFsmHost fsmHost;

        /// <summary>
        /// 原始移动输入
        /// </summary>
        private Vector3 moveInput;

        /// <summary>
        /// 平面移动输入
        /// </summary>
        private Vector3 planarMoveInput;

        /// <summary>
        /// 是否希望下蹲
        /// </summary>
        private bool shouldBeCrouching;

        /// <summary>
        /// 是否正在下蹲
        /// </summary>
        private bool isCrouching;

        /// <summary>
        /// 站立胶囊高度
        /// </summary>
        private float standingCapsuleHeight;

        /// <summary>
        /// 站立胶囊半径
        /// </summary>
        private float standingCapsuleRadius;

        /// <summary>
        /// 站立胶囊 Y 偏移
        /// </summary>
        private float standingCapsuleYOffset;

        /// <summary>
        /// 站立时视觉局部位移
        /// </summary>
        private Vector3 standingMeshLocalPosition;

        /// <summary>
        /// 站立时视觉局部缩放
        /// </summary>
        private Vector3 standingMeshLocalScale;

        /// <summary>
        /// 是否已缓存视觉站立姿态
        /// </summary>
        private bool meshPoseCached;

        /// <summary>
        /// 起身重叠检测缓冲
        /// </summary>
        private readonly Collider[] crouchOverlapBuffer = new Collider[MccConfig.MAX_COLLISION_OVERLAPS];

        /// <summary>
        /// 状态机
        /// </summary>
        public StateMachine Machine => fsmHost != null ? fsmHost.Machine : null;

        /// <summary>
        /// 马达
        /// </summary>
        public MotionCC Motion => motion;

        /// <summary>
        /// 平面移动输入
        /// </summary>
        public Vector3 PlanarMoveInput => planarMoveInput;

        /// <summary>
        /// 是否有移动输入
        /// </summary>
        public bool HasMoveInput => planarMoveInput.sqrMagnitude > inputDeadZone * inputDeadZone;

        /// <summary>
        /// 跳跃请求
        /// </summary>
        public bool JumpRequested { get; set; }

        /// <summary>
        /// 待施加跳跃冲量
        /// </summary>
        public bool WantJumpImpulse { get; set; }

        /// <summary>
        /// 顶点速度阈值
        /// </summary>
        public float JumpApexSpeedThreshold => jumpApexSpeedThreshold;

        /// <summary>
        /// 是否正在下蹲
        /// </summary>
        public bool IsCrouching => isCrouching;

        /// <summary>
        /// 是否希望下蹲
        /// </summary>
        public bool ShouldBeCrouching => shouldBeCrouching;

        /// <summary>
        /// 当前地面速度倍率
        /// </summary>
        public float MoveSpeedScale => isCrouching ? crouchMoveSpeedScale : 1f;

        private void Awake()
        {
            motion = GetComponent<MotionCC>();
            fsmHost = GetComponent<UpdateFsmHost>();

            if (characterCamera == null)
            {
                characterCamera = FindAnyObjectByType<ExampleMCharacterCamera>();
            }

            if (characterCamera != null)
            {
                characterCamera.SetFollowTarget(transform);
            }

            CacheStandingCapsule();
        }

        private void Start()
        {
            if (Machine == null)
            {
                Debug.LogError("[FsmPlayerController] UpdateFsmHost.Machine 未就绪", this);
                return;
            }

            Machine.Blackboard?.SetValue(EBlockBoardParme.PlayerController, this);
            Machine.ChangeState<IdleState>();
        }

        /// <summary>
        /// 缓存站立胶囊尺寸
        /// </summary>
        private void CacheStandingCapsule()
        {
            if (motion == null)
            {
                return;
            }

            standingCapsuleHeight = motion.Config.capsuleHeight;
            standingCapsuleRadius = motion.Config.capsuleRadius;
            standingCapsuleYOffset = motion.Config.capsuleYOffset;
        }

        /// <summary>
        /// 输入更新
        /// </summary>
        /// <param name="inputDirection">输入方向</param>
        /// <param name="jumpRequested">跳跃请求</param>
        public void InputVectorUpdate(ref Vector3 inputDirection, ref bool jumpRequested)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 rawInput = new Vector3(h, 0f, v);

            if (characterCamera != null)
            {
                moveInput = characterCamera.ConvertInputToWorld(rawInput, Vector3.up);
            }
            else
            {
                moveInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;
            }

            inputDirection = moveInput;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                JumpRequested = true;
                jumpRequested = true;
            }

            shouldBeCrouching = Input.GetKey(crouchKey);
        }

        /// <summary>
        /// 角色更新前的准备阶段
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            if (motion == null)
            {
                return;
            }

            Vector3 up = motion.CharacterUp;
            planarMoveInput = Vector3.ProjectOnPlane(moveInput, up);
            if (planarMoveInput.sqrMagnitude > 1f)
            {
                planarMoveInput.Normalize();
            }

            if (shouldBeCrouching && !isCrouching)
            {
                EnterCrouch();
            }
        }

        /// <summary>
        /// 更新角色速度
        /// </summary>
        /// <param name="currentVelocity">当前速度</param>
        /// <param name="deltaTime">时间差</param>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (motion == null)
            {
                return;
            }

            if (Machine != null && Machine.CurrentState is IMccMotorLogic motorLogic)
            {
                motorLogic.UpdateVelocity(ref currentVelocity, deltaTime);
            }
        }

        /// <summary>
        /// 更新角色旋转
        /// </summary>
        /// <param name="currentRotation">当前旋转</param>
        /// <param name="deltaTime">时间差</param>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            Vector3 lookDirection = moveInput;
            if (characterCamera != null && characterCamera.IsFirstPerson)
            {
                lookDirection = Vector3.ProjectOnPlane(characterCamera.PlanarDirection, Vector3.up);
            }

            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotationSpeed = motion != null ? motion.Config.rotationSpeed : 10f;
            if (characterCamera != null && characterCamera.IsFirstPerson)
            {
                rotationSpeed *= 3f;
            }

            currentRotation = Quaternion.Slerp(currentRotation, target, deltaTime * rotationSpeed);
        }

        /// <summary>
        /// 接地检测完成后调用
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public void PostGroundingUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// 角色更新后的收尾阶段
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public void AfterCharacterUpdate(float deltaTime)
        {
            if (motion == null || !isCrouching || shouldBeCrouching)
            {
                return;
            }

            TryExitCrouch();
        }

        /// <summary>
        /// 进入下蹲
        /// </summary>
        private void EnterCrouch()
        {
            float crouchedYOffset = standingCapsuleYOffset
                                    - (standingCapsuleHeight - crouchedCapsuleHeight) * 0.5f;
            motion.SetCapsuleDimensions(standingCapsuleRadius, crouchedCapsuleHeight, crouchedYOffset);
            isCrouching = true;
            ApplyCrouchMeshScale(true);
        }

        /// <summary>
        /// 尝试起身
        /// </summary>
        private void TryExitCrouch()
        {
            motion.SetCapsuleDimensions(standingCapsuleRadius, standingCapsuleHeight, standingCapsuleYOffset);
            int overlapCount = motion.CharacterCollisionsOverlap(
                motion.TransientPosition,
                motion.TransientRotation,
                crouchOverlapBuffer);

            if (overlapCount > 0)
            {
                float crouchedYOffset = standingCapsuleYOffset
                                        - (standingCapsuleHeight - crouchedCapsuleHeight) * 0.5f;
                motion.SetCapsuleDimensions(standingCapsuleRadius, crouchedCapsuleHeight, crouchedYOffset);
                return;
            }

            isCrouching = false;
            ApplyCrouchMeshScale(false);
        }

        /// <summary>
        /// 缩放可选视觉根节点
        /// </summary>
        /// <param name="crouched">是否下蹲</param>
        private void ApplyCrouchMeshScale(bool crouched)
        {
            if (crouchMeshRoot == null)
            {
                return;
            }

            if (!meshPoseCached)
            {
                standingMeshLocalPosition = crouchMeshRoot.localPosition;
                standingMeshLocalScale = crouchMeshRoot.localScale;
                meshPoseCached = true;
            }

            float yScale = crouched
                ? Mathf.Max(0.01f, crouchedCapsuleHeight / Mathf.Max(0.01f, standingCapsuleHeight))
                : 1f;

            crouchMeshRoot.localScale = new Vector3(
                standingMeshLocalScale.x,
                standingMeshLocalScale.y * yScale,
                standingMeshLocalScale.z);

            // 视觉枢轴在中心时压 Y 会抬脚 下移补偿把脚钉回站立高度
            float footCompensate = standingCapsuleHeight * 0.5f * (1f - yScale);
            Vector3 localPos = standingMeshLocalPosition;
            localPos.y -= footCompensate;
            crouchMeshRoot.localPosition = localPos;
        }

        /// <summary>
        /// 判断某个碰撞体是否参与角色碰撞
        /// </summary>
        /// <param name="coll">碰撞体</param>
        /// <returns>是否参与</returns>
        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        /// <summary>
        /// 地面探测命中时调用
        /// </summary>
        public void OnGroundHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 移动扫掠命中时调用
        /// </summary>
        public void OnMovementHit(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 给玩法层最后一次修改稳定性报告的机会
        /// </summary>
        public void ProcessHitStabilityReport(
            Collider hitCollider,
            Vector3 hitNormal,
            Vector3 hitPoint,
            Vector3 atCharacterPosition,
            Quaternion atCharacterRotation,
            ref HitStabilityReport hitStabilityReport)
        {
        }

        /// <summary>
        /// 离散碰撞事件
        /// </summary>
        /// <param name="hitCollider">碰撞体</param>
        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }
}
