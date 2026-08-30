using MotionCharacterController;
using UnityEditor;
using UnityEngine;

namespace MotionCharacterController.Editor
{
    /// <summary>
    /// MCC 运行时调试窗口
    /// </summary>
    public class MccDebuggerWindow : EditorWindow
    {
        private enum Tab
        {
            State = 0,
            Executors = 1,
            Tuning = 2,
        }

        private Tab currentTab;
        private MotionCC targetCharacter;
        private readonly MccDebuggerRuntimeProbe probe = new MccDebuggerRuntimeProbe();
        private Vector2 scroll;
        private SerializedObject serializedConfig;
        private double lastSampleTime;

        [MenuItem("Tools/MCC/调试器")]
        public static void Open()
        {
            MccDebuggerWindow window = GetWindow<MccDebuggerWindow>("MCC 调试器");
            window.minSize = new Vector2(360f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            TryBindSelection();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnSelectionChange()
        {
            TryBindSelection();
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                probe.Reset();
                TryBindSelection();
            }
        }

        private void OnEditorUpdate()
        {
            if (!Application.isPlaying || targetCharacter == null)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - lastSampleTime < Time.fixedDeltaTime * 0.5f)
            {
                return;
            }

            lastSampleTime = EditorApplication.timeSinceStartup;
            probe.Sample(targetCharacter);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            DrawHeader();
            currentTab = (Tab)GUILayout.Toolbar((int)currentTab, new[]
            {
                "状态(State)",
                "Executor",
                "调参(Tuning)",
            });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (currentTab)
            {
                case Tab.State:
                    DrawStateTab();
                    break;
                case Tab.Executors:
                    DrawExecutorsTab();
                    break;
                case Tab.Tuning:
                    DrawTuningTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            MotionCC next = (MotionCC)EditorGUILayout.ObjectField("目标角色(MotionCC)", targetCharacter, typeof(MotionCC), true);
            if (EditorGUI.EndChangeCheck())
            {
                BindTarget(next);
            }

            using (new EditorGUI.DisabledScope(targetCharacter == null))
            {
                EditorGUILayout.BeginHorizontal();
                bool auto = MccSystem.AutoSimulation;
                bool nextAuto = EditorGUILayout.ToggleLeft("自动模拟(AutoSimulation)", auto);
                if (nextAuto != auto && targetCharacter != null)
                {
                    targetCharacter.Config.autoSimulation = nextAuto;
                    MccSystem.SyncAutoSimulationFromCharacters();
                    EditorUtility.SetDirty(targetCharacter);
                }

                using (new EditorGUI.DisabledScope(!Application.isPlaying || MccSystem.AutoSimulation))
                {
                    if (GUILayout.Button("单步模拟(Tick Step)", GUILayout.Width(130f)))
                    {
                        MccSystem.Simulate(Time.fixedDeltaTime);
                        probe.Sample(targetCharacter);
                        SceneView.RepaintAll();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            string playLabel = Application.isPlaying ? "播放中(Playing)" : "编辑模式(Edit Mode)";
            EditorGUILayout.LabelField("运行状态(Play)", playLabel);
            EditorGUILayout.Space(4f);
        }

        private void DrawStateTab()
        {
            if (targetCharacter == null)
            {
                EditorGUILayout.HelpBox("请选中带 MotionCC 的角色", MessageType.Info);
                return;
            }

            var eSample = targetCharacter.CaptureDebugSample();
            EditorGUILayout.LabelField("位姿与速度", EditorStyles.boldLabel);
            EditorGUILayout.Vector3Field("瞬时位置(TransientPosition)", eSample.TransientPosition);
            EditorGUILayout.Vector3Field("基础速度(BaseVelocity)", eSample.BaseVelocity);
            EditorGUILayout.Vector3Field("附着刚体速度(AttachedRigidbodyVelocity)", eSample.AttachedRigidbodyVelocity);
            EditorGUILayout.ObjectField("附着刚体(AttachedRigidbody)", eSample.AttachedRigidbody, typeof(Rigidbody), true);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("接地(Grounding)", EditorStyles.boldLabel);
            EditorGUILayout.Toggle("发现地面(FoundAnyGround)", eSample.GroundingStatus.FoundAnyGround);
            EditorGUILayout.Toggle("稳定站立(IsStableOnGround)", eSample.GroundingStatus.IsStableOnGround);
            EditorGUILayout.Toggle("阻止吸附(SnappingPrevented)", eSample.GroundingStatus.SnappingPrevented);
            EditorGUILayout.Vector3Field("地面法线(GroundNormal)", eSample.GroundingStatus.GroundNormal);
            EditorGUILayout.FloatField("地面探测距离(GroundProbeDistance)", eSample.DebugGroundProbeDistance);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("移动求解(Move)", EditorStyles.boldLabel);
            EditorGUILayout.EnumPopup("扫掠状态(SweepState)", eSample.DebugLastSweepState);
            EditorGUILayout.IntField("移动迭代次数(MovementSweeps)", eSample.DebugLastMovementSweeps);
            EditorGUILayout.Toggle("本帧移动完成(MoveCompleted)", eSample.DebugLastMoveCompleted);
            EditorGUILayout.Toggle("本帧有移动命中(HasMovementHit)", eSample.DebugHasMovementHit);
            EditorGUILayout.FloatField("阶段1耗时毫秒(Phase1 ms)", eSample.DebugPhase1Seconds * 1000f);
            EditorGUILayout.FloatField("阶段2耗时毫秒(Phase2 ms)", eSample.DebugPhase2Seconds * 1000f);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("最近帧曲线", EditorStyles.boldLabel);
            DrawSimpleSparkline("阶段1(P1)", probe.Phase1MsRing, probe.RingFilled, probe.RingWriteIndex);
            DrawSimpleSparkline("阶段2(P2)", probe.Phase2MsRing, probe.RingFilled, probe.RingWriteIndex);
            DrawSimpleSparkline("迭代(Sweeps)", probe.SweepCountRing, probe.RingFilled, probe.RingWriteIndex);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("事件时暂停(Pause On Event)", EditorStyles.boldLabel);
            probe.PauseOnExceedIterations = EditorGUILayout.ToggleLeft("超迭代(ExceedIterations)", probe.PauseOnExceedIterations);
            probe.PauseOnBlockingCorner = EditorGUILayout.ToggleLeft("卡角(FoundBlockingCorner)", probe.PauseOnBlockingCorner);
            probe.PauseOnForceUnground = EditorGUILayout.ToggleLeft("强制离地(ForceUnground)", probe.PauseOnForceUnground);
            probe.PauseOnPlatformChange = EditorGUILayout.ToggleLeft("换平台(PlatformChange)", probe.PauseOnPlatformChange);
        }

        private void DrawExecutorsTab()
        {
            EditorGUILayout.LabelField("场景叠加层(Scene Overlay)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在 Scene 视图查看胶囊体 速度 地面 命中 重叠", MessageType.None);
            MccDebuggerSceneOverlay.DrawOverlayToggles();

            if (targetCharacter == null)
            {
                return;
            }

            var eSample = targetCharacter.CaptureDebugSample();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Executor 快照", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("重叠数量(OverlapsCount)", eSample.OverlapsCount.ToString());
            EditorGUILayout.LabelField("强制离地计时(MustUngroundTimeCounter)", eSample.MustUngroundTimeCounter.ToString("F3"));
            EditorGUILayout.Toggle("解算移动碰撞(SolveMovementCollisions)", eSample.SolveMovementCollisions);
            EditorGUILayout.Toggle("解算接地(SolveGrounding)", eSample.SolveGrounding);
            if (eSample.DebugHasMovementHit)
            {
                EditorGUILayout.Vector3Field("最近命中点(LastHitPoint)", eSample.DebugLastHitPoint);
                EditorGUILayout.Vector3Field("最近命中法线(LastHitNormal)", eSample.DebugLastHitNormal);
            }
        }

        private void DrawTuningTab()
        {
            if (targetCharacter == null)
            {
                EditorGUILayout.HelpBox("请选中带 MotionCC 的角色", MessageType.Info);
                return;
            }

            EnsureSerializedConfig();
            serializedConfig.Update();

            EditorGUILayout.HelpBox("只显示内核真正读取的参数 右侧标注影响模块", MessageType.None);
            DrawTuningField("maxStableSlopeAngle", "最大稳定坡度角(maxStableSlopeAngle)", "地面求解(GroundingExecutor)");
            DrawTuningField("groundProbeDistance", "地面探测距离(groundProbeDistance)", "未接地探测");
            DrawTuningField("groundDetectionExtraDistance", "额外探测距离(groundDetectionExtraDistance)", "地面求解(GroundingExecutor)");
            DrawTuningField("stableGroundLayers", "稳定地面层(stableGroundLayers)", "地面求解(GroundingExecutor)");
            DrawTuningField("stepHandling", "台阶处理(stepHandling)", "台阶求解(StepExecutor)");
            DrawTuningField("maxStepHeight", "最大台阶高度(maxStepHeight)", "台阶/地面");
            DrawTuningField("allowSteppingWithoutStableGrounding", "无接地也上台阶(allowSteppingWithoutStableGrounding)", "台阶求解(StepExecutor)");
            DrawTuningField("minRequiredStepDepth", "最小台阶深度(minRequiredStepDepth)", "Extra 台阶");
            DrawTuningField("ledgeAndDenivelationHandling", "边缘与落差(ledgeAndDenivelationHandling)", "稳定性(HitStabilityExecutor)");
            DrawTuningField("maxStableDistanceFromLedge", "离边缘稳定距离(maxStableDistanceFromLedge)", "稳定性(HitStabilityExecutor)");
            DrawTuningField("maxVelocityForLedgeSnap", "边缘吸附速度上限(maxVelocityForLedgeSnap)", "稳定性(HitStabilityExecutor)");
            DrawTuningField("maxStableDenivelationAngle", "最大落差角(maxStableDenivelationAngle)", "稳定性(HitStabilityExecutor)");
            DrawTuningField("interactiveRigidbodyHandling", "刚体交互(interactiveRigidbodyHandling)", "平台/刚体");
            DrawTuningField("rigidbodyInteractionType", "刚体交互类型(rigidbodyInteractionType)", "刚体求解(RigidbodyExecutor)");
            DrawTuningField("simulatedCharacterMass", "模拟角色质量(simulatedCharacterMass)", "刚体求解(RigidbodyExecutor)");
            DrawTuningField("preserveAttachedRigidbodyMomentum", "保留平台动量(preserveAttachedRigidbodyMomentum)", "平台求解(PlatformExecutor)");
            DrawTuningField("hasPlanarConstraint", "平面约束(hasPlanarConstraint)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("planarConstraintAxis", "平面约束轴(planarConstraintAxis)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("maxMovementIterations", "最大移动迭代(maxMovementIterations)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("maxDecollisionIterations", "最大解重叠迭代(maxDecollisionIterations)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("checkMovementInitialOverlaps", "检查起步重叠(checkMovementInitialOverlaps)", "CollisionMoveExecutor.Move");
            DrawTuningField("killVelocityWhenExceedMaxMovementIterations", "超迭代清空速度(killVelocity...)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("killRemainingMovementWhenExceedMaxMovementIterations", "超迭代丢弃位移(killRemaining...)", "碰撞求解(CollisionMoveExecutor)");
            DrawTuningField("autoSimulation", "自动模拟(autoSimulation)", "系统(MccSystem)");
            DrawTuningField("interpolate", "插值(interpolate)", "系统提交");
            DrawTuningField("discreteCollisionEvents", "离散碰撞事件(discreteCollisionEvents)", "碰撞求解(CollisionMoveExecutor)");

            if (serializedConfig.ApplyModifiedProperties())
            {
                MccSystem.SyncAutoSimulationFromCharacters();
                EditorUtility.SetDirty(targetCharacter);
            }
        }

        private void DrawTuningField(string propertyName, string displayName, string solverHint)
        {
            SerializedProperty configProperty = serializedConfig.FindProperty("config");
            SerializedProperty property = configProperty != null ? configProperty.FindPropertyRelative(propertyName) : null;
            if (property == null)
            {
                EditorGUILayout.LabelField(displayName, "未找到序列化字段");
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(displayName), true);
            EditorGUILayout.LabelField("→ " + solverHint, EditorStyles.miniLabel);
        }

        private void DrawSimpleSparkline(string label, float[] values, int filled, int writeIndex)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(88f));
            Rect rect = GUILayoutUtility.GetRect(120f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            if (filled > 1)
            {
                float max = 0.001f;
                for (int i = 0; i < filled; i++)
                {
                    max = Mathf.Max(max, values[i]);
                }

                Handles.BeginGUI();
                Vector3 prev = Vector3.zero;
                bool hasPrev = false;
                int start = filled < MccDebuggerRuntimeProbe.RingCapacity ? 0 : writeIndex;
                for (int i = 0; i < filled; i++)
                {
                    int index = (start + i) % MccDebuggerRuntimeProbe.RingCapacity;
                    float normalized = values[index] / max;
                    float x = rect.x + rect.width * (i / (float)(filled - 1));
                    float y = rect.yMax - normalized * rect.height;
                    Vector3 point = new Vector3(x, y, 0f);
                    if (hasPrev)
                    {
                        Handles.color = Color.green;
                        Handles.DrawLine(prev, point);
                    }

                    prev = point;
                    hasPrev = true;
                }

                Handles.EndGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void TryBindSelection()
        {
            if (Selection.activeGameObject != null)
            {
                MotionCC selected = Selection.activeGameObject.GetComponent<MotionCC>();
                if (selected != null)
                {
                    BindTarget(selected);
                    return;
                }
            }

            if (targetCharacter == null && Application.isPlaying && MccSystem.Characters.Count > 0)
            {
                BindTarget(MccSystem.Characters[0]);
            }
            else
            {
                BindTarget(targetCharacter);
            }
        }

        private void BindTarget(MotionCC character)
        {
            targetCharacter = character;
            serializedConfig = null;
            MccDebuggerSceneOverlay.SetTarget(character);
            if (character != null)
            {
                EnsureSerializedConfig();
            }
        }

        private void EnsureSerializedConfig()
        {
            if (targetCharacter == null)
            {
                serializedConfig = null;
                return;
            }

            if (serializedConfig == null || serializedConfig.targetObject != targetCharacter)
            {
                serializedConfig = new SerializedObject(targetCharacter);
            }
        }
    }
}
