using System.Collections.Generic;
using UnityEngine;

namespace MotionCharacterController
{
  /// <summary>
  /// 示例相机 第三人称跟随 按住右键切第一人称
  /// </summary>
  [DefaultExecutionOrder(100)]
  public class ExampleMCharacterCamera : MonoBehaviour
  {
    [Header("跟随目标")]
    public Transform FollowTarget;

    [Header("画面偏移")]
    public Vector2 FollowPointFraming = new Vector2(0f, 0f);
    public float FollowingSharpness = 10000f;

    [Header("相机距离")]
    public float DefaultDistance = 6f;
    public float MinDistance = 0f;
    public float MaxDistance = 10f;
    public float DistanceMovementSpeed = 5f;
    public float DistanceMovementSharpness = 10f;

    [Header("第一人称")]
    [Tooltip("按下鼠标右键切换第一人称 再按一次切回第三人称")]
    public bool ToggleRightMouseForFirstPerson = true;
    [Tooltip("第一人称挂点 拖玩家头上的空物体过来 相机直接贴在这个点上")]
    public Transform FirstPersonMount;
    [Tooltip("没有挂点时的备用眼高偏移 相对 FollowTarget")]
    public Vector3 FirstPersonLocalOffset = new Vector3(0f, 1.6f, 0f);

    [Header("旋转设置")]
    public bool InvertX = false;
    public bool InvertY = false;
    [Range(-90f, 90f)]
    public float DefaultVerticalAngle = 20f;
    [Range(-90f, 90f)]
    public float MinVerticalAngle = -90f;
    [Range(-90f, 90f)]
    public float MaxVerticalAngle = 90f;
    public float RotationSpeed = 1f;
    public float RotationSharpness = 10000f;

    [Header("遮挡检测")]
    public float ObstructionCheckRadius = 0.2f;
    public LayerMask ObstructionLayers = -1;
    public float ObstructionSharpness = 10000f;
    public List<Collider> IgnoredColliders = new List<Collider>();

    public Transform Transform { get; private set; }
    /// <summary>相机在水平面上的朝向 不含俯仰角</summary>
    public Vector3 PlanarDirection { get; set; }
    public float TargetDistance { get; set; }
    /// <summary>当前是否处于第一人称</summary>
    public bool IsFirstPerson { get; private set; }

    private bool _distanceIsObstructed;
    private float _currentDistance;
    private float _targetVerticalAngle;
    private int _obstructionCount;
    private readonly RaycastHit[] _obstructions = new RaycastHit[32];
    private Vector3 _currentFollowPosition;
    private float _thirdPersonTargetDistance;

    void OnValidate()
    {
      DefaultDistance = Mathf.Clamp(DefaultDistance, MinDistance, MaxDistance);
      DefaultVerticalAngle = Mathf.Clamp(DefaultVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
    }

    void Awake()
    {
      Transform = this.transform;
      _currentDistance = DefaultDistance;
      TargetDistance = _currentDistance;
      _thirdPersonTargetDistance = DefaultDistance;
      _targetVerticalAngle = DefaultVerticalAngle;

      if (FollowTarget != null)
      {
        PlanarDirection = FollowTarget.forward;
        _currentFollowPosition = FollowTarget.position;
      }
    }

    void LateUpdate()
    {
      if (FollowTarget == null)
      {
        return;
      }

      UpdateFirstPersonMode();

      Vector3 rotInput = new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"), 0f);
      float zoomInput = IsFirstPerson ? 0f : Input.GetAxis("Mouse ScrollWheel");
      UpdateWithInput(Time.deltaTime, zoomInput, rotInput);
    }

    /// <summary>
    /// 绑定跟随目标
    /// </summary>
    /// <param name="target">跟随目标</param>
    public void SetFollowTarget(Transform target)
    {
      FollowTarget = target;
      if (FollowTarget != null)
      {
        PlanarDirection = FollowTarget.forward;
        _currentFollowPosition = FollowTarget.position;
        TryAutoFindFirstPersonMount();
      }

      TryIgnoreTargetColliders();
    }

    /// <summary>
    /// 手动指定第一人称挂点
    /// </summary>
    /// <param name="mount">挂点</param>
    public void SetFirstPersonMount(Transform mount)
    {
      FirstPersonMount = mount;
    }

    /// <summary>
    /// 把 WASD 输入转成世界坐标下的平面移动方向
    /// </summary>
    /// <param name="input">x 左右 z 前后</param>
    /// <param name="characterUp">角色朝上</param>
    /// <returns>世界平面输入</returns>
    public Vector3 ConvertInputToWorld(Vector3 input, Vector3 characterUp)
    {
      if (input.sqrMagnitude <= 0.0001f)
      {
        return Vector3.zero;
      }

      Vector3 forward = Vector3.ProjectOnPlane(PlanarDirection, characterUp);
      if (forward.sqrMagnitude <= 0.0001f)
      {
        forward = Vector3.ProjectOnPlane(Transform.forward, characterUp);
      }

      forward.Normalize();
      Vector3 right = Vector3.Cross(characterUp, forward).normalized;
      Vector3 worldInput = forward * input.z + right * input.x;
      return worldInput.sqrMagnitude > 1f ? worldInput.normalized : worldInput;
    }

    /// <summary>
    /// 根据输入更新相机旋转与位置
    /// </summary>
    public void UpdateWithInput(float deltaTime, float zoomInput, Vector3 rotationInput)
    {
      if (FollowTarget == null)
      {
        return;
      }

      if (InvertX)
      {
        rotationInput.x *= -1f;
      }

      if (InvertY)
      {
        rotationInput.y *= -1f;
      }

      Vector3 up = FollowTarget.up;
      Quaternion rotationFromInput = Quaternion.Euler(up * (rotationInput.x * RotationSpeed));
      PlanarDirection = rotationFromInput * PlanarDirection;
      PlanarDirection = Vector3.Cross(up, Vector3.Cross(PlanarDirection, up));
      Quaternion planarRot = Quaternion.LookRotation(PlanarDirection, up);

      _targetVerticalAngle -= rotationInput.y * RotationSpeed;
      _targetVerticalAngle = Mathf.Clamp(_targetVerticalAngle, MinVerticalAngle, MaxVerticalAngle);
      Quaternion verticalRot = Quaternion.Euler(_targetVerticalAngle, 0, 0);
      Quaternion targetRotation = Quaternion.Slerp(
          Transform.rotation,
          planarRot * verticalRot,
          1f - Mathf.Exp(-RotationSharpness * deltaTime)
      );
      Transform.rotation = targetRotation;

      // 第一人称 直接贴挂点 距离瞬间归零
      if (IsFirstPerson)
      {
        Vector3 eyePosition = GetFirstPersonEyePosition();
        _currentFollowPosition = eyePosition;
        _currentDistance = 0f;
        TargetDistance = 0f;
        _distanceIsObstructed = false;
        Transform.position = eyePosition;
        return;
      }

      if (_distanceIsObstructed && Mathf.Abs(zoomInput) > 0f)
      {
        TargetDistance = _currentDistance;
      }

      TargetDistance += zoomInput * DistanceMovementSpeed;
      TargetDistance = Mathf.Clamp(TargetDistance, MinDistance, MaxDistance);
      _thirdPersonTargetDistance = TargetDistance;

      _currentFollowPosition = Vector3.Lerp(
          _currentFollowPosition,
          FollowTarget.position,
          1f - Mathf.Exp(-FollowingSharpness * deltaTime)
      );

      RaycastHit closestHit = default;
      float closestDistance = Mathf.Infinity;
      _obstructionCount = Physics.SphereCastNonAlloc(
          _currentFollowPosition,
          ObstructionCheckRadius,
          -Transform.forward,
          _obstructions,
          TargetDistance,
          ObstructionLayers,
          QueryTriggerInteraction.Ignore
      );

      for (int i = 0; i < _obstructionCount; i++)
      {
        bool isIgnored = false;
        for (int j = 0; j < IgnoredColliders.Count; j++)
        {
          if (IgnoredColliders[j] == _obstructions[i].collider)
          {
            isIgnored = true;
            break;
          }
        }

        if (!isIgnored && _obstructions[i].distance < closestDistance && _obstructions[i].distance > 0f)
        {
          closestDistance = _obstructions[i].distance;
          closestHit = _obstructions[i];
        }
      }

      float desiredDistance;
      if (closestDistance < Mathf.Infinity)
      {
        _distanceIsObstructed = true;
        desiredDistance = closestHit.distance;
      }
      else
      {
        _distanceIsObstructed = false;
        desiredDistance = TargetDistance;
      }

      _currentDistance = Mathf.Lerp(
          _currentDistance,
          desiredDistance,
          1f - Mathf.Exp(-DistanceMovementSharpness * deltaTime)
      );

      Vector3 targetPosition = _currentFollowPosition - (targetRotation * Vector3.forward * _currentDistance);
      targetPosition += Transform.right * FollowPointFraming.x;
      targetPosition += Transform.up * FollowPointFraming.y;
      Transform.position = targetPosition;
    }

    /// <summary>
    /// 取第一人称眼睛世界坐标
    /// </summary>
    private Vector3 GetFirstPersonEyePosition()
    {
      if (FirstPersonMount != null)
      {
        return FirstPersonMount.position;
      }

      return FollowTarget.TransformPoint(FirstPersonLocalOffset);
    }

    private void UpdateFirstPersonMode()
    {
      if (!ToggleRightMouseForFirstPerson)
      {
        return;
      }

      // 按下右键切换 再按切回
      if (!Input.GetMouseButtonDown(1))
      {
        return;
      }

      IsFirstPerson = !IsFirstPerson;
      if (IsFirstPerson)
      {
        _thirdPersonTargetDistance = Mathf.Max(TargetDistance, DefaultDistance * 0.5f);
        _currentDistance = 0f;
        TargetDistance = 0f;
      }
      else
      {
        TargetDistance = Mathf.Clamp(_thirdPersonTargetDistance, MinDistance, MaxDistance);
        _currentDistance = TargetDistance;
      }
    }

    /// <summary>
    /// 自动找名为 CameraMount 或 FirstPersonMount 的子物体
    /// </summary>
    private void TryAutoFindFirstPersonMount()
    {
      if (FirstPersonMount != null || FollowTarget == null)
      {
        return;
      }

      Transform[] children = FollowTarget.GetComponentsInChildren<Transform>(true);
      for (int i = 0; i < children.Length; i++)
      {
        string name = children[i].name;
        if (name == "CameraMount" || name == "FirstPersonMount" || name == "眼睛挂点")
        {
          FirstPersonMount = children[i];
          return;
        }
      }
    }

    private void TryIgnoreTargetColliders()
    {
      if (FollowTarget == null)
      {
        return;
      }

      Collider[] colliders = FollowTarget.GetComponentsInChildren<Collider>();
      for (int i = 0; i < colliders.Length; i++)
      {
        if (!IgnoredColliders.Contains(colliders[i]))
        {
          IgnoredColliders.Add(colliders[i]);
        }
      }
    }
  }
}
