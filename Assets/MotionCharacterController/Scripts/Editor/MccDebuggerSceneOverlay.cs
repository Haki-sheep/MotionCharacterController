using MotionCharacterController;
using UnityEditor;
using UnityEngine;

namespace MotionCharacterController.Editor
{
    /// <summary>
    /// Scene 视图调试绘制
    /// </summary>
    public static class MccDebuggerSceneOverlay
    {
        /// <summary>绘制胶囊</summary>
        public static bool DrawCapsule = true;
        /// <summary>绘制速度</summary>
        public static bool DrawVelocity = true;
        /// <summary>绘制地面法线</summary>
        public static bool DrawGroundNormal = true;
        /// <summary>绘制地面探测</summary>
        public static bool DrawGroundProbe = true;
        /// <summary>绘制移动命中</summary>
        public static bool DrawMovementHit = true;
        /// <summary>绘制重叠法线</summary>
        public static bool DrawOverlaps = true;

        private static MotionCC targetCharacter;
        private static bool subscribed;

        /// <summary>
        /// 绑定目标角色并订阅 Scene 绘制
        /// </summary>
        /// <param name="character">目标角色</param>
        public static void SetTarget(MotionCC character)
        {
            targetCharacter = character;
            EnsureSubscribed();
        }

        /// <summary>
        /// 在窗口里绘制叠加层开关
        /// </summary>
        public static void DrawOverlayToggles()
        {
            DrawCapsule = EditorGUILayout.ToggleLeft("胶囊体(Capsule)", DrawCapsule);
            DrawVelocity = EditorGUILayout.ToggleLeft("速度箭头(Velocity)", DrawVelocity);
            DrawGroundNormal = EditorGUILayout.ToggleLeft("地面法线(GroundNormal)", DrawGroundNormal);
            DrawGroundProbe = EditorGUILayout.ToggleLeft("地面探测距离(GroundProbeDistance)", DrawGroundProbe);
            DrawMovementHit = EditorGUILayout.ToggleLeft("移动命中(MovementHit)", DrawMovementHit);
            DrawOverlaps = EditorGUILayout.ToggleLeft("重叠法线(Overlaps)", DrawOverlaps);
        }

        private static void EnsureSubscribed()
        {
            if (subscribed)
            {
                return;
            }

            SceneView.duringSceneGui += OnSceneGUI;
            subscribed = true;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (targetCharacter == null || !Application.isPlaying)
            {
                return;
            }

            var eSample = targetCharacter.CaptureDebugSample();
            Vector3 ePosition = eSample.TransientPosition;
            Quaternion eRotation = eSample.TransientRotation;

            if (DrawCapsule && eSample.HasCapsule)
            {
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                DrawWireCapsule(ePosition, eRotation, eSample.CapsuleRadius, eSample.CapsuleHeight, eSample.CapsuleCenter);
            }

            if (DrawVelocity)
            {
                Vector3 eVelocity = eSample.Velocity;
                if (eVelocity.sqrMagnitude > 0.0001f)
                {
                    Handles.color = Color.cyan;
                    Handles.ArrowHandleCap(0, ePosition, Quaternion.LookRotation(eVelocity.normalized), Mathf.Clamp(eVelocity.magnitude * 0.25f, 0.25f, 3f), EventType.Repaint);
                    Handles.Label(ePosition + eVelocity.normalized * 0.4f, $"速度(V) {eVelocity.magnitude:F2}");
                }
            }

            if (DrawGroundNormal && eSample.GroundingStatus.FoundAnyGround)
            {
                Handles.color = eSample.GroundingStatus.IsStableOnGround ? Color.green : Color.yellow;
                Handles.DrawLine(eSample.GroundingStatus.GroundPoint, eSample.GroundingStatus.GroundPoint + eSample.GroundingStatus.GroundNormal);
                Handles.SphereHandleCap(0, eSample.GroundingStatus.GroundPoint, Quaternion.identity, 0.05f, EventType.Repaint);
            }

            if (DrawGroundProbe)
            {
                float eProbeDistance = eSample.DebugGroundProbeDistance;
                Vector3 eBottom = eSample.CapsuleBottomHemi;
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Handles.DrawLine(eBottom, eBottom - eSample.CharacterUp * eProbeDistance);
                Handles.Label(eBottom, $"探测(Probe) {eProbeDistance:F3}");
            }

            if (DrawMovementHit && eSample.DebugHasMovementHit)
            {
                Handles.color = Color.magenta;
                Handles.SphereHandleCap(0, eSample.DebugLastHitPoint, Quaternion.identity, 0.06f, EventType.Repaint);
                Handles.DrawLine(eSample.DebugLastHitPoint, eSample.DebugLastHitPoint + eSample.DebugLastHitNormal);
            }

            if (DrawOverlaps && eSample.OverlapNormals != null)
            {
                Handles.color = new Color(1f, 0.4f, 0.1f, 0.9f);
                for (int i = 0; i < eSample.OverlapsCount; i++)
                {
                    Vector3 eOverlapNormal = eSample.OverlapNormals[i];
                    Handles.DrawLine(ePosition, ePosition + eOverlapNormal);
                }
            }
        }

        private static void DrawWireCapsule(Vector3 position, Quaternion rotation, float radius, float height, Vector3 center)
        {
            Vector3 worldCenter = position + rotation * center;
            float cylinderHeight = Mathf.Max(0f, height - radius * 2f);
            Vector3 up = rotation * Vector3.up;
            Vector3 top = worldCenter + up * (cylinderHeight * 0.5f);
            Vector3 bottom = worldCenter - up * (cylinderHeight * 0.5f);
            Handles.DrawWireDisc(top, up, radius);
            Handles.DrawWireDisc(bottom, up, radius);
            Vector3 right = rotation * Vector3.right * radius;
            Vector3 forward = rotation * Vector3.forward * radius;
            Handles.DrawLine(bottom + right, top + right);
            Handles.DrawLine(bottom - right, top - right);
            Handles.DrawLine(bottom + forward, top + forward);
            Handles.DrawLine(bottom - forward, top - forward);
        }
    }
}
