using UnityEngine;

namespace ithappy
{
    public class RotationScript_Looped : MonoBehaviour
    {
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }

        [Header("Rotation")]
        public RotationAxis rotationAxis = RotationAxis.Y;
        public float rotationAngle = 120.0f;
        public float rotationDuration = 0.5f;

        [Header("Timing")]
        public float delayBeforeFirstRotation = 5.0f;
        public float delayBetweenRotations = 5.0f;

        [Header("Loop")]
        public bool loop = true;

        private Quaternion _startRotation;
        private Quaternion _targetRotation;
        private float _delayTimer;
        private float _rotationTimer;
        private bool _isRotating;
        private bool _started;

        private void OnEnable()
        {
            ResetCycle();
        }

        private void Update()
        {
            if (!_started)
            {
                _started = true;
                _delayTimer = Mathf.Max(0f, delayBeforeFirstRotation);
            }

            if (_isRotating)
            {
                UpdateRotation();
                return;
            }

            if (_delayTimer > 0f)
            {
                _delayTimer -= Time.deltaTime;
                if (_delayTimer <= 0f)
                    BeginRotation();
            }
        }

        private void BeginRotation()
        {
            _isRotating = true;
            _rotationTimer = 0f;
            _startRotation = transform.rotation;
            _targetRotation = _startRotation * Quaternion.AngleAxis(rotationAngle, GetAxisVector());
        }

        private void UpdateRotation()
        {
            float duration = Mathf.Max(0.0001f, rotationDuration);
            _rotationTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_rotationTimer / duration);

            transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);

            if (t >= 1f)
            {
                transform.rotation = _targetRotation;
                _isRotating = false;

                if (loop)
                    _delayTimer = Mathf.Max(0f, delayBetweenRotations);
            }
        }

        private Vector3 GetAxisVector()
        {
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    return Vector3.right;
                case RotationAxis.Y:
                    return Vector3.up;
                case RotationAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.up;
            }
        }

        [ContextMenu("Reset Cycle")]
        public void ResetCycle()
        {
            _delayTimer = Mathf.Max(0f, delayBeforeFirstRotation);
            _rotationTimer = 0f;
            _isRotating = false;
            _started = false;
        }

        [ContextMenu("Rotate Now")]
        public void RotateNow()
        {
            _delayTimer = 0f;
            _isRotating = false;
            BeginRotation();
        }
    }
}
