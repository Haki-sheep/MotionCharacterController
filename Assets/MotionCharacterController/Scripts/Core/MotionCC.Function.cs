using UnityEngine;

namespace MotionCharacterController
{
    public partial class MotionCC
    {
        #region 跳跃请求
        /// <summary>
        /// 消耗跳跃请求
        /// </summary>
        public void ConsumeJumpRequest()
        {
            jumpRequested = false;
        }
        #endregion

        #region 状态快照
        /// <summary>
        /// 获取状态
        /// </summary>
        /// <returns>状态</returns>
        public MotionCharacterMotorState GetState()
        {
            return new MotionCharacterMotorState
            {
                Position = context.TransientPosition,
                Rotation = context.TransientRotation,
                BaseVelocity = context.BaseVelocity,
                MustUnground = context.MustUnground,
                MustUngroundTime = context.MustUngroundTimeCounter,
                LastMovementIterationFoundAnyGround = context.LastMovementIterationFoundAnyGround,
                GroundingStatus = context.GroundingStatus.ToTransient(),
                AttachedRigidbody = context.AttachedRigidbody,
                AttachedRigidbodyVelocity = context.AttachedRigidbodyVelocity,
            };
        }

        /// <summary>
        /// 应用状态
        /// </summary>
        /// <param name="state">状态</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        public void ApplyState(MotionCharacterMotorState state, bool bypassInterpolation = true)
        {
            SetPositionAndRotation(state.Position, state.Rotation, bypassInterpolation);
            context.BaseVelocity = state.BaseVelocity;
            context.MustUnground = state.MustUnground;
            context.MustUngroundTimeCounter = state.MustUngroundTime;
            context.LastMovementIterationFoundAnyGround = state.LastMovementIterationFoundAnyGround;
            context.GroundingStatus.CopyFrom(state.GroundingStatus);
            context.AttachedRigidbody = state.AttachedRigidbody;
            context.AttachedRigidbodyVelocity = state.AttachedRigidbodyVelocity;
        }
        #endregion

        #region 强制离地
        /// <summary>
        /// 强制解除接地状态
        /// </summary>
        /// <param name="time">解除接地状态时间</param>
        public void ForceUnground(float time = 0.1f)
        {
            context.MustUnground = true;
            context.MustUngroundTimeCounter = time;
        }

        /// <summary>
        /// 是否需要解除接地状态
        /// </summary>
        /// <returns>是否需要解除接地状态</returns>
        public bool MustUnground()
        {
            return context.MustUnground || context.MustUngroundTimeCounter > 0f;
        }
        #endregion

        #region 位姿控制
        /// <summary>
        /// 直接移动角色到目标位置
        /// </summary>
        /// <param name="toPosition">目标位置</param>
        public void MoveCharacter(Vector3 toPosition)
        {
            context.MovePositionDirty = true;
            context.MovePositionTarget = toPosition;
        }

        /// <summary>
        /// 直接旋转角色到目标旋转
        /// </summary>
        /// <param name="toRotation">目标旋转</param>
        public void RotateCharacter(Quaternion toRotation)
        {
            context.MoveRotationDirty = true;
            context.MoveRotationTarget = toRotation;
        }

        /// <summary>
        /// 直接设置角色位置
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        public void SetPosition(Vector3 position, bool bypassInterpolation = true)
        {
            context.Transform.position = position;
            context.InitialSimulationPosition = position;
            context.TransientPosition = position;
            if (bypassInterpolation)
            {
                context.InitialTickPosition = position;
            }
        }

        /// <summary>
        /// 直接设置角色旋转
        /// </summary>
        /// <param name="rotation">目标旋转</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        public void SetRotation(Quaternion rotation, bool bypassInterpolation = true)
        {
            context.Transform.rotation = rotation;
            context.InitialSimulationRotation = rotation;
            context.TransientRotation = rotation.normalized;
            if (bypassInterpolation)
            {
                context.InitialTickRotation = rotation.normalized;
            }
        }

        /// <summary>
        /// 直接设置角色位置和旋转
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="bypassInterpolation">是否绕过插值</param>
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation, bool bypassInterpolation = true)
        {
            context.Transform.SetPositionAndRotation(position, rotation);
            context.InitialSimulationPosition = position;
            context.InitialSimulationRotation = rotation.normalized;
            context.TransientPosition = position;
            context.TransientRotation = rotation.normalized;
            if (bypassInterpolation)
            {
                context.InitialTickPosition = position;
                context.InitialTickRotation = rotation.normalized;
            }
        }
        #endregion

        #region 胶囊与求解开关
        /// <summary>
        /// 设置Context中的胶囊体尺寸
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="height">高度</param>
        /// <param name="yOffset">Y偏移</param>
        public void SetCapsuleDimensions(float radius, float height, float yOffset)
        {
            if (context.Capsule is null)
                return;

            height = Mathf.Max(height, radius * 2f + MccConfig.COLLISION_OFFSET);
            config.capsuleRadius = Mathf.Clamp(radius, 0f, height * 0.5f);
            config.capsuleHeight = height;
            config.capsuleYOffset = yOffset;
            // 设置Context中的胶囊体尺寸
            context.Capsule.radius = config.capsuleRadius;
            context.Capsule.height = config.capsuleHeight;
            context.Capsule.center = new(0f, config.capsuleYOffset, 0f);
            // 刷新Context中的胶囊体数据
            context.RefreshCapsuleData();
        }

        /// <summary>
        /// 设置胶囊体碰撞激活状态
        /// </summary>
        /// <param name="collisionsActive">是否激活</param>
        public void SetCapsuleCollisionsActivation(bool collisionsActive)
        {
            if (context.Capsule != null)
            {
                context.Capsule.isTrigger = !collisionsActive;
            }
        }

        /// <summary>
        /// 设置移动碰撞解算激活状态
        /// </summary>
        /// <param name="active">是否激活</param>
        public void SetMovementCollisionsSolvingActivation(bool active)
        {
            context.SolveMovementCollisions = active;
        }

        /// <summary>
        /// 设置地面检测解算激活状态
        /// </summary>
        /// <param name="active">是否激活</param>
        public void SetGroundSolvingActivation(bool active)
        {
            context.SolveGrounding = active;
        }
        #endregion

        #region 工具与查询转发
        /// <summary>
        /// 获取移动速度
        /// </summary>
        /// <param name="movement">移动距离</param>
        /// <param name="deltaTime">时间差</param>
        /// <returns>移动速度</returns>
        public Vector3 GetVelocityFromMovement(Vector3 movement, float deltaTime)
        {
            return MccGeometryCalculator.GetVelocityFromMovement(movement, deltaTime);
        }

        /// <summary>
        /// 获取方向向量在某个表面切线方向上的重定向结果
        /// </summary>
        /// <param name="direction">原始方向</param>
        /// <param name="surfaceNormal">表面法线</param>
        /// <returns>沿表面切线的方向</returns>
        public Vector3 GetDirectionTangentToSurface(Vector3 direction, Vector3 surfaceNormal)
        {
            return MccGeometryCalculator.GetDirectionTangentToSurface(direction, surfaceNormal, context.CharacterUp);
        }

        /// <summary>
        /// 获取角色碰撞体与某个位置的碰撞信息
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="overlappedColliders">碰撞体数组</param>
        /// <param name="inflate">膨胀量</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>碰撞体数量</returns>
        public int CharacterCollisionsOverlap(Vector3 position, Quaternion rotation, Collider[] overlappedColliders, float inflate = 0f, bool acceptOnlyStableGroundLayer = false)
        {
            return collisionMoveExecutor.CharacterCollisionsOverlap(position, rotation, overlappedColliders, inflate, acceptOnlyStableGroundLayer);
        }

        /// <summary>
        /// 获取角色碰撞体与某个方向的碰撞信息
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">旋转</param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近碰撞信息</param>
        /// <param name="hits">碰撞信息数组</param>
        /// <param name="inflate">膨胀量</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>碰撞体数量</returns>
        public int CharacterCollisionsSweep(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, float inflate = 0f, bool acceptOnlyStableGroundLayer = false)
        {
            return collisionMoveExecutor.CharacterCollisionsSweep(position, rotation, direction, distance, out closestHit, hits, inflate, acceptOnlyStableGroundLayer);
        }

        /// <summary>
        /// 获取角色碰撞体与某个方向的碰撞信息 射线检测
        /// </summary>
        /// <param name="position"></param>
        /// <param name="direction">方向</param>
        /// <param name="distance">距离</param>
        /// <param name="closestHit">最近碰撞信息</param>
        /// <param name="hits">碰撞信息数组</param>
        /// <param name="acceptOnlyStableGroundLayer">是否只接受稳定地面层</param>
        /// <returns>碰撞体数量</returns>
        public int CharacterCollisionsRaycast(Vector3 position, Vector3 direction, float distance, out RaycastHit closestHit, RaycastHit[] hits, bool acceptOnlyStableGroundLayer = false)
        {
            return collisionMoveExecutor.CharacterCollisionsRaycast(position, direction, distance, out closestHit, hits, acceptOnlyStableGroundLayer);
        }

        /// <summary>
        /// 采集只读调试快照
        /// </summary>
        /// <returns>调试快照</returns>
        public MccMotorDebugSample CaptureDebugSample()
        {
            var eSample = new MccMotorDebugSample
            {
                TransientPosition = context.TransientPosition,
                TransientRotation = context.TransientRotation,
                CharacterUp = context.CharacterUp,
                BaseVelocity = context.BaseVelocity,
                AttachedRigidbodyVelocity = context.AttachedRigidbodyVelocity,
                Velocity = Velocity,
                AttachedRigidbody = context.AttachedRigidbody,
                GroundingStatus = context.GroundingStatus,
                MustUngroundTimeCounter = context.MustUngroundTimeCounter,
                SolveMovementCollisions = context.SolveMovementCollisions,
                SolveGrounding = context.SolveGrounding,
                DebugLastSweepState = context.DebugLastSweepState,
                DebugLastMovementSweeps = context.DebugLastMovementSweeps,
                DebugLastMoveCompleted = context.DebugLastMoveCompleted,
                DebugHasMovementHit = context.DebugHasMovementHit,
                DebugLastHitPoint = context.DebugLastHitPoint,
                DebugLastHitNormal = context.DebugLastHitNormal,
                DebugGroundProbeDistance = context.DebugGroundProbeDistance,
                DebugPhase1Seconds = context.DebugPhase1Seconds,
                DebugPhase2Seconds = context.DebugPhase2Seconds,
                HasCapsule = context.Capsule is not null,
                CapsuleRadius = context.Capsule is not null ? context.Capsule.radius : 0f,
                CapsuleHeight = context.Capsule is not null ? context.Capsule.height : 0f,
                CapsuleCenter = context.Capsule is not null ? context.Capsule.center : Vector3.zero,
                CapsuleBottomHemi = MccGeometryCalculator.GetCapsuleBottomHemiAt(
                    context.TransientPosition,
                    context.TransientRotation,
                    context.TransformToCapsuleBottomHemi),
                OverlapsCount = context.OverlapsCount,
                OverlapNormals = new Vector3[context.OverlapsCount],
            };

            for (int i = 0; i < context.OverlapsCount; i++)
            {
                eSample.OverlapNormals[i] = context.Overlaps[i].Normal;
            }

            return eSample;
        }
        #endregion

        #region 调试Gizmos
        /// <summary>
        /// 绘制地面检测Gizmos
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawGroundGizmos)
            {
                return;
            }

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return;
            }

            Vector3 up = transform.up;
            Vector3 bottomHemi = transform.position + transform.rotation * (capsule.center + Vector3.down * (capsule.height * 0.5f - capsule.radius));
            Vector3 probeOrigin = bottomHemi + up * MccConfig.GROUND_START_OFFSET;
            float probeRadius = Mathf.Max(MccConfig.SKIN_WIDTH, capsule.radius - MccConfig.SKIN_WIDTH);
            float probeDistance = Mathf.Max(MccConfig.MIN_GROUND_PROBING_DISTANCE, config.groundProbeDistance) + config.groundDetectionExtraDistance + MccConfig.GROUND_START_OFFSET;
            Vector3 probeEnd = probeOrigin - up * probeDistance;

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(probeOrigin, 0.025f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(probeOrigin, probeRadius);
            Gizmos.DrawLine(probeOrigin, probeEnd);

            bool grounded = Physics.SphereCast(probeOrigin, probeRadius, -up, out RaycastHit hit, probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            && Vector3.Angle(up, hit.normal) <= config.maxStableSlopeAngle;
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawSphere(grounded ? hit.point : probeEnd, 0.04f);
        }
        #endregion
    }
}
