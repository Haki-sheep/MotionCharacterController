using UnityEngine;

namespace MotionCharacterController
{
    [RequireComponent(typeof(Rigidbody))]
    public class MccPhysicsMover : MonoBehaviour
    {
        [SerializeField]
        private bool moveWithPhysics = true;

        private IMover moverController;
        private Vector3 transientPosition;
        private Quaternion transientRotation;
        private Vector3 initialSimulationPosition;
        private Quaternion initialSimulationRotation;

        public Rigidbody Rigidbody { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public Vector3 TransientPosition => transientPosition;
        public Quaternion TransientRotation => transientRotation;

        private void Reset()
        {
            ValidateData();
        }

        private void OnValidate()
        {
            ValidateData();
        }

        private void Awake()
        {
            InitComponents();
            transientPosition = Rigidbody.position;
            transientRotation = Rigidbody.rotation;
            initialSimulationPosition = transientPosition;
            initialSimulationRotation = transientRotation;
        }

        private void OnEnable()
        {
            MccSystem.RegisterMover(this);
        }

        private void OnDisable()
        {
            MccSystem.UnregisterMover(this);
        }

        private void ValidateData()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Rigidbody.isKinematic = true;
            Rigidbody.interpolation = RigidbodyInterpolation.None;
            Rigidbody.maxAngularVelocity = Mathf.Infinity;
            Rigidbody.maxDepenetrationVelocity = Mathf.Infinity;
        }

        /// <summary>
        /// 初始化组件引用
        /// </summary>
        private void InitComponents()
        {
            ValidateData();
            moverController = GetComponent<IMover>();
        }

        internal void VelocityUpdate(float deltaTime)
        {
            initialSimulationPosition = transientPosition;
            initialSimulationRotation = transientRotation;

            if (moverController != null)
            {
                moverController.UpdateMovement(out transientPosition, out transientRotation, deltaTime);
                transientRotation = transientRotation.normalized;
            }
            else
            {
                transientPosition = transform.position;
                transientRotation = transform.rotation;
            }

            if (deltaTime <= 0f)
            {
                Velocity = Vector3.zero;
                AngularVelocity = Vector3.zero;
                return;
            }

            Velocity = (transientPosition - initialSimulationPosition) / deltaTime;
            Quaternion deltaRotation = transientRotation * Quaternion.Inverse(initialSimulationRotation);
            deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            AngularVelocity = axis.sqrMagnitude > 0f ? axis.normalized * (angle * Mathf.Deg2Rad / deltaTime) : Vector3.zero;
        }

        internal void CommitMovement()
        {
            transform.SetPositionAndRotation(transientPosition, transientRotation);
            if (moveWithPhysics)
            {
                Rigidbody.MovePosition(transientPosition);
                Rigidbody.MoveRotation(transientRotation);
            }
            else
            {
                Rigidbody.position = transientPosition;
                Rigidbody.rotation = transientRotation;
            }
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            Rigidbody.position = position;
            initialSimulationPosition = position;
            transientPosition = position;
        }

        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
            Rigidbody.rotation = rotation;
            initialSimulationRotation = rotation.normalized;
            transientRotation = rotation.normalized;
        }

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Rigidbody.position = position;
            Rigidbody.rotation = rotation.normalized;
            initialSimulationPosition = position;
            initialSimulationRotation = rotation.normalized;
            transientPosition = position;
            transientRotation = rotation.normalized;
        }

        public MccPhysicsMoverState GetState()
        {
            return new MccPhysicsMoverState
            {
                Position = transientPosition,
                Rotation = transientRotation,
                Velocity = Velocity,
                AngularVelocity = AngularVelocity,
            };
        }

        public void ApplyState(MccPhysicsMoverState state)
        {
            SetPositionAndRotation(state.Position, state.Rotation);
            Velocity = state.Velocity;
            AngularVelocity = state.AngularVelocity;
        }
    }

    public struct MccPhysicsMoverState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }
}
