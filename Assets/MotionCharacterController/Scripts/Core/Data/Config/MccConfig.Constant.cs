namespace MotionCharacterController
{
    // 参数配置：常量
    public partial class MccConfig
    {

        // 皮肤厚度
        public const float SKIN_WIDTH = 0.01f;
        // 碰撞偏移量
        public const float COLLISION_OFFSET = 0.01f;
        // 最小速度大小
        public const float MIN_VELOCITY_MAGNITUDE = 0.01f;
        // 扫掠后退距离
        public const float SWEEP_BACKSTEP_DISTANCE = 0.002f;
        // 地面后退距离
        public const float GROUND_BACKSTEP_DISTANCE = 0.002f;
        // 地面探测最小距离
        public const float MIN_GROUND_PROBING_DISTANCE = 0.005f;
        // 地面反弹距离
        public const float GROUND_REBOUND_DISTANCE = 0.02f;
        // 次级探测垂直偏移量
        public const float SECONDARY_PROBES_VERTICAL = 0.02f;
        // 次级探测水平偏移量
        public const float SECONDARY_PROBES_HORIZONTAL = 0.02f;
        // 台阶攀爬前向检测距离
        public const float STEPPING_FORWARD_DISTANCE = 0.03f;

        // 认为可以移动的最小距离
        public const float MIN_CANMOVE_DISTANCE = 0.001f;

        // 认为有输入的最小距离
        public const float MIN_HAVEINPUT_VALUE = 0.01f;

        #region 地面与碰撞
        // 地面检测初始点偏移量
        public const float GROUND_START_OFFSET = 0.01f;

        // 碰撞检测最大命中数量
        public const int MAX_COLLISION_HITS = 32;

        // 碰撞检测最大迭代次数
        public const int MAX_SWEEP_ITERATIONS = 4;

        // 地面检测最大命中数量
        public const int MAX_GROUND_HITS = 16;
        // 碰撞检测最大重叠数量
        public const int MAX_COLLISION_OVERLAPS = 16;
        // 刚体检测最大命中数量
        public const int MAX_RIGIDBODY_HITS = 16;
        // 地面检测最大迭代次数
        public const int MAX_GROUNDING_SWEEP_ITERATIONS = 2;
        #endregion
    }
}