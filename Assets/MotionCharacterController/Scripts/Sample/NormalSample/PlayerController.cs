using MotionCharacterController;
using UnityEngine;

public class PlayerController : MonoBehaviour, IMcc
{
    [SerializeField] private ExampleMCharacterCamera characterCamera;
    [SerializeField, Range(0f, 1f)] private float inputDeadZone = 0.1f;

    [Header("下蹲")]
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private float crouchedCapsuleHeight = 1f;
    [SerializeField, Range(0.1f, 1f)] private float crouchMoveSpeedScale = 0.5f;
    [SerializeField] private Transform crouchMeshRoot;

    private MotionCC motion;
    private Vector3 moveInput;
    private bool jumpRequested;

    private bool shouldBeCrouching;
    private bool isCrouching;
    private float standingCapsuleHeight;
    private float standingCapsuleRadius;
    private float standingCapsuleYOffset;
    private Vector3 standingMeshLocalPosition;
    private Vector3 standingMeshLocalScale;
    private bool meshPoseCached;
    private readonly Collider[] crouchOverlapBuffer = new Collider[MccConfig.MAX_COLLISION_OVERLAPS];

    private void Awake()
    {
        motion = GetComponent<MotionCC>();

        if (characterCamera == null)
            characterCamera = FindFirstObjectByType<ExampleMCharacterCamera>();

        if (characterCamera != null)
            characterCamera.SetFollowTarget(transform);

        CacheStandingCapsule();
    }

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

    public void InputVectorUpdate(ref Vector3 inputDirection, ref bool jumpRequested)
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 rawInput = new Vector3(h, 0f, v);

        if (characterCamera != null)
            moveInput = characterCamera.ConvertInputToWorld(rawInput, Vector3.up);
        else
            moveInput = rawInput.sqrMagnitude > 1f ? rawInput.normalized : rawInput;

        inputDirection = moveInput;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            this.jumpRequested = true;
            jumpRequested = true;
        }

        shouldBeCrouching = Input.GetKey(crouchKey);
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        if (motion == null)
        {
            return;
        }

        // 按下蹲时立刻缩胶囊
        if (shouldBeCrouching && !isCrouching)
        {
            EnterCrouch();
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (motion == null)
        {
            return;
        }

        MccConfig config = motion.Config;
        Vector3 up = motion.CharacterUp;
        Vector3 input = Vector3.ProjectOnPlane(moveInput, up);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        float moveSpeedScale = isCrouching ? crouchMoveSpeedScale : 1f;

        // 如果角色稳定在地面上
        if (motion.GroundingStatus.IsStableOnGround)
        {
            // 将速度投影到地面法线上
            currentVelocity = motion.GetDirectionTangentToSurface(currentVelocity,
                                                                  motion.GroundingStatus.GroundNormal)
                                                                  * currentVelocity.magnitude;
            Vector3 targetVelocity = Vector3.zero;
            // 如果有移动输入
            if (HasMoveInput(input))
            {
                // 计算输入的右向量
                Vector3 inputRight = Vector3.Cross(input, up);
                // 将输入旋转到地面法线上
                Vector3 reorientedInput = Vector3.Cross(motion.GroundingStatus.GroundNormal,
                                                        inputRight).normalized * input.magnitude;
                // 计算目标速度
                targetVelocity = reorientedInput * (config.moveSpeed * moveSpeedScale);
            }

            // 将当前速度插值到目标速度
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                1f - Mathf.Exp(-config.stableMovementSharpness * deltaTime));

            // 如果需要跳跃
            if (jumpRequested)
            {
                // 对齐 KCC 先清竖直分量再施加跳跃 并强制离地
                motion.ForceUnground();
                // 计算跳跃速度
                currentVelocity += (up * config.jumpSpeed) - Vector3.Project(currentVelocity, up);
                // 清空跳跃请求
                jumpRequested = false;
                // 消耗跳跃请求
                motion.ConsumeJumpRequest();
            }

            return;
        }
        // 如果角色不稳定在地面上 有移动输入
        if (HasMoveInput(input))
        {
            // 计算添加速度
            Vector3 addedVelocity = input * config.AirAcceleration * deltaTime;
            // 将当前速度投影到地面法线上
            Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, up);
            float airSpeedLimit = config.maxAirMoveSpeed * moveSpeedScale;
            // 如果当前平面速度小于最大空中移动速度
            if (currentPlanarVelocity.magnitude < airSpeedLimit)
            {
                // 计算新的总速度
                Vector3 newTotal = Vector3.ClampMagnitude(currentPlanarVelocity + addedVelocity, airSpeedLimit);
                addedVelocity = newTotal - currentPlanarVelocity;
            }
            // 如果当前平面速度和添加速度的点积大于0
            else if (Vector3.Dot(currentPlanarVelocity, addedVelocity) > 0f)
            {
                // 将添加速度投影到当前平面速度的法线上
                addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentPlanarVelocity.normalized);
            }

            // 添加速度到当前速度
            currentVelocity += addedVelocity;
        }

        // 添加重力速度
        currentVelocity += up * config.gravity * deltaTime;
        // 平滑速度
        currentVelocity *= 1f / (1f + config.airDrag * deltaTime);
        // 清空跳跃请求
        jumpRequested = false;
    }

    private bool HasMoveInput(Vector3 input)
    {
        return input.sqrMagnitude > inputDeadZone * inputDeadZone;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        Vector3 lookDirection = moveInput;
        // 第一人称时角色朝向跟随相机水平朝向
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
        // 第一人称转向更跟手
        if (characterCamera != null && characterCamera.IsFirstPerson)
        {
            rotationSpeed *= 3f;
        }

        currentRotation = Quaternion.Slerp(currentRotation, target, deltaTime * rotationSpeed);
    }

    public void PostGroundingUpdate(float deltaTime)
    {
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (motion == null || !isCrouching || shouldBeCrouching)
        {
            return;
        }

        TryExitCrouch();
    }

    private void EnterCrouch()
    {
        float crouchedYOffset = standingCapsuleYOffset
                                - (standingCapsuleHeight - crouchedCapsuleHeight) * 0.5f;
        motion.SetCapsuleDimensions(standingCapsuleRadius, crouchedCapsuleHeight, crouchedYOffset);
        isCrouching = true;
        ApplyCrouchMeshScale(true);
    }

    private void TryExitCrouch()
    {
        motion.SetCapsuleDimensions(standingCapsuleRadius, standingCapsuleHeight, standingCapsuleYOffset);
        int overlapCount = motion.CharacterCollisionsOverlap(
            motion.TransientPosition,
            motion.TransientRotation,
            crouchOverlapBuffer);

        if (overlapCount > 0)
        {
            // 头顶有障碍 保持下蹲胶囊
            float crouchedYOffset = standingCapsuleYOffset
                                    - (standingCapsuleHeight - crouchedCapsuleHeight) * 0.5f;
            motion.SetCapsuleDimensions(standingCapsuleRadius, crouchedCapsuleHeight, crouchedYOffset);
            return;
        }

        isCrouching = false;
        ApplyCrouchMeshScale(false);
    }

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

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
}
