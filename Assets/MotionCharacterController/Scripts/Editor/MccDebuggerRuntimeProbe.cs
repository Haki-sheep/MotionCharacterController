using MotionCharacterController;
using UnityEngine;

namespace MotionCharacterController.Editor
{
    /// <summary>
    /// Play 模式采样与环缓冲
    /// </summary>
    public class MccDebuggerRuntimeProbe
    {
        public const int RingCapacity = 64;

        /// <summary>Phase1 耗时毫秒环缓冲</summary>
        public readonly float[] Phase1MsRing = new float[RingCapacity];
        /// <summary>Phase2 耗时毫秒环缓冲</summary>
        public readonly float[] Phase2MsRing = new float[RingCapacity];
        /// <summary>移动迭代次数环缓冲</summary>
        public readonly float[] SweepCountRing = new float[RingCapacity];
        /// <summary>环缓冲写入下标</summary>
        public int RingWriteIndex { get; private set; }
        /// <summary>环缓冲已写入数量</summary>
        public int RingFilled { get; private set; }

        /// <summary>是否在超迭代时暂停</summary>
        public bool PauseOnExceedIterations = true;
        /// <summary>是否在卡角时暂停</summary>
        public bool PauseOnBlockingCorner = true;
        /// <summary>是否在强制离地时暂停</summary>
        public bool PauseOnForceUnground;
        /// <summary>是否在换平台时暂停</summary>
        public bool PauseOnPlatformChange = true;

        private Rigidbody lastAttachedRigidbody;
        private float lastMustUngroundCounter;
        private bool hasSampled;

        /// <summary>
        /// 采样角色并按需触发暂停
        /// </summary>
        /// <param name="character">目标角色</param>
        public void Sample(MotionCC character)
        {
            if (character == null || !Application.isPlaying)
            {
                return;
            }

            var eSample = character.CaptureDebugSample();
            int eIndex = RingWriteIndex;
            Phase1MsRing[eIndex] = eSample.DebugPhase1Seconds * 1000f;
            Phase2MsRing[eIndex] = eSample.DebugPhase2Seconds * 1000f;
            SweepCountRing[eIndex] = eSample.DebugLastMovementSweeps;
            RingWriteIndex = (RingWriteIndex + 1) % RingCapacity;
            if (RingFilled < RingCapacity)
            {
                RingFilled++;
            }

            EvaluatePauseEvents(eSample);
            lastAttachedRigidbody = eSample.AttachedRigidbody;
            lastMustUngroundCounter = eSample.MustUngroundTimeCounter;
            hasSampled = true;
        }

        /// <summary>
        /// 重置环缓冲与暂停基准
        /// </summary>
        public void Reset()
        {
            RingWriteIndex = 0;
            RingFilled = 0;
            hasSampled = false;
            lastAttachedRigidbody = null;
            lastMustUngroundCounter = 0f;
            for (int i = 0; i < RingCapacity; i++)
            {
                Phase1MsRing[i] = 0f;
                Phase2MsRing[i] = 0f;
                SweepCountRing[i] = 0f;
            }
        }

        /// <summary>
        /// 按快照判断是否暂停编辑器
        /// </summary>
        /// <param name="sample">调试快照</param>
        private void EvaluatePauseEvents(MccMotorDebugSample sample)
        {
            if (!hasSampled)
            {
                return;
            }

            bool eShouldPause = false;
            if (PauseOnExceedIterations && !sample.DebugLastMoveCompleted)
            {
                eShouldPause = true;
            }

            if (PauseOnBlockingCorner && sample.DebugLastSweepState == MovementSweepState.FoundBlockingCorner)
            {
                eShouldPause = true;
            }

            if (PauseOnForceUnground && sample.MustUngroundTimeCounter > lastMustUngroundCounter)
            {
                eShouldPause = true;
            }

            if (PauseOnPlatformChange && sample.AttachedRigidbody != lastAttachedRigidbody)
            {
                eShouldPause = true;
            }

            if (eShouldPause)
            {
                UnityEditor.EditorApplication.isPaused = true;
            }
        }
    }
}
