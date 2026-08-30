using System.Collections.Generic;
using UnityEngine;

namespace MotionCharacterController
{
    /// <summary>
    /// 运动学角色系统
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MccSystem : MonoBehaviour
    {
        private static MccSystem instance;
        /// <summary>系统级自动模拟开关 由角色 Config.autoSimulation 同步</summary>
        public static bool AutoSimulation = true;

        private static float interpolationStartTime;
        private static float interpolationDeltaTime = 0.02f;

        public static readonly List<MotionCC> Characters = new List<MotionCC>(32);
        public static readonly List<MccPhysicsMover> Movers = new List<MccPhysicsMover>(16);

        private void Awake()
        {
            instance = this;
        }

        private void FixedUpdate()
        {
            if (!AutoSimulation)
            {
                return;
            }

            Simulate(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (!ShouldInterpolate())
            {
                return;
            }

            float factor = Mathf.Clamp01((Time.time - interpolationStartTime) / interpolationDeltaTime);
            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].InterpolationUpdate(factor);
            }
        }

        /// <summary>
        /// 手动或自动驱动一整帧模拟
        /// </summary>
        /// <param name="deltaTime">时间差</param>
        public static void Simulate(float deltaTime)
        {
            CreatMccSystem();
            bool interpolate = ShouldInterpolate();

            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].PreSimulationTick(deltaTime);
            }

            for (int i = 0; i < Movers.Count; i++)
            {
                Movers[i].VelocityUpdate(deltaTime);
            }

            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].UpdatePhase1(deltaTime);
            }

            for (int i = 0; i < Movers.Count; i++)
            {
                Movers[i].CommitMovement();
            }

            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].UpdatePhase2(deltaTime);
            }

            interpolationStartTime = Time.time;
            interpolationDeltaTime = deltaTime;

            for (int i = 0; i < Characters.Count; i++)
            {
                Characters[i].CommitSimulation(interpolate);
            }
        }

        /// <summary>
        /// 创建系统单例
        /// </summary>
        public static void CreatMccSystem()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("MccSystem");
            instance = go.AddComponent<MccSystem>();
            go.hideFlags = HideFlags.NotEditable;
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 注册角色并同步系统自动模拟开关
        /// </summary>
        /// <param name="character">角色</param>
        public static void RegisterCharacter(MotionCC character)
        {
            CreatMccSystem();
            if (!Characters.Contains(character))
            {
                Characters.Add(character);
            }

            SyncAutoSimulationFromCharacters();
        }

        /// <summary>
        /// 注销角色
        /// </summary>
        /// <param name="character">角色</param>
        public static void UnregisterCharacter(MotionCC character)
        {
            Characters.Remove(character);
            SyncAutoSimulationFromCharacters();
        }

        /// <summary>
        /// 注册物理移动器
        /// </summary>
        /// <param name="mover">移动器</param>
        public static void RegisterMover(MccPhysicsMover mover)
        {
            CreatMccSystem();
            if (!Movers.Contains(mover))
            {
                Movers.Add(mover);
            }
        }

        /// <summary>
        /// 注销物理移动器
        /// </summary>
        /// <param name="mover">移动器</param>
        public static void UnregisterMover(MccPhysicsMover mover)
        {
            Movers.Remove(mover);
        }

        /// <summary>
        /// 根据已注册角色同步 AutoSimulation 任一关闭则系统关闭
        /// </summary>
        public static void SyncAutoSimulationFromCharacters()
        {
            bool auto = true;
            for (int i = 0; i < Characters.Count; i++)
            {
                MotionCC character = Characters[i];
                if (character != null && !character.Config.autoSimulation)
                {
                    auto = false;
                    break;
                }
            }

            AutoSimulation = auto;
        }

        private static bool ShouldInterpolate()
        {
            for (int i = 0; i < Characters.Count; i++)
            {
                if (Characters[i] != null && Characters[i].Config.interpolate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
