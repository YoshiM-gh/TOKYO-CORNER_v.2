using System;
using System.Collections.Generic;
using UnityEngine;

namespace Controller
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class CharacterMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField]
        private float m_WalkSpeed = 2.4f;
        [SerializeField]
        private float m_RunSpeed = 5.5f;
        [SerializeField, Range(0f, 360f)]
        private float m_RotateSpeed = 200f;
        [SerializeField]
        private Space m_Space = Space.Self;
        [SerializeField]
        private float m_JumpHeight = 5f;
        [SerializeField]
        private bool m_AutoFitController = true;

        [Header("Animator")]
        [SerializeField]
        private string m_HorizontalID = "Hor";
        [SerializeField]
        private string m_VerticalID = "Vert";
        [SerializeField]
        private string m_StateID = "State";
        [SerializeField]
        private string m_JumpID = "IsJump";
        [SerializeField]
        private LookWeight m_LookWeight = new(1f, 0.3f, 0.7f, 1f);
        [Header("Animator playback")]
        [Tooltip("歩行時の Animator.speed（1で既定。足捌きを早くするなら1.1〜1.3など）。")]
        [SerializeField, Min(0.01f)]
        private float m_WalkAnimPlaySpeed = 1.2f;
        [Tooltip("走行時の Animator.speed。")]
        [SerializeField, Min(0.01f)]
        private float m_RunAnimPlaySpeed = 1.35f;
        [Tooltip("止まっているときの再生速度。")]
        [SerializeField, Min(0.01f)]
        private float m_IdleAnimPlaySpeed = 1f;
        [Tooltip("空中のとき。")]
        [SerializeField, Min(0.01f)]
        private float m_AirAnimPlaySpeed = 1f;
        [Tooltip("歩行↔走行↔待機の切り替えを滑らかにする係数。大きいほど即応。")]
        [SerializeField, Min(0.01f)]
        private float m_AnimPlaySpeedLerp = 10f;

        private Transform m_Transform;
        private CharacterController m_Controller;
        private Animator m_Animator;
        private Animator[] m_Animators;
        private float m_CurrentPlaySpeed = 1f;

        private MovementHandler m_Movement;
        private AnimationHandler m_Animation;

        private Vector2 m_Axis;
        private Vector3 m_Target;
        private bool m_IsRun;
        private bool m_IsJump;

        private bool m_IsMoving;

        public Vector2 Axis => m_Axis;
        public Vector3 Target => m_Target;
        public bool IsRun => m_IsRun;

        private void OnValidate()
        {
            m_WalkSpeed = Mathf.Max(m_WalkSpeed, 0f);
            m_RunSpeed = Mathf.Max(m_RunSpeed, m_WalkSpeed);

            m_Movement?.SetStats(m_WalkSpeed / 3.6f, m_RunSpeed / 3.6f, m_RotateSpeed, m_JumpHeight, m_Space);
        }

        private void Awake()
        {
            m_Transform = transform;
            m_Controller = GetComponent<CharacterController>();
            if (m_AutoFitController)
            {
                FitControllerToCharacter();
            }
            m_Animator = GetComponent<Animator>();
            m_Animators = BuildAnimatorTargets();
            m_CurrentPlaySpeed = m_IdleAnimPlaySpeed;
            SetAllAnimatorPlaySpeeds(m_CurrentPlaySpeed);

            m_Movement = new MovementHandler(m_Controller, m_Transform, m_WalkSpeed, m_RunSpeed, m_RotateSpeed, m_JumpHeight, m_Space);
            m_Animation = new AnimationHandler(m_Animators, m_HorizontalID,  m_VerticalID, m_StateID, m_JumpID);
        }

        private void Update()
        {
            m_Movement.Move(Time.deltaTime, in m_Axis, in m_Target, m_IsRun, m_IsJump, m_IsMoving, out var animAxis, out var isAir);
            m_Animation.Animate(in animAxis, m_IsRun? 1f : 0f, isAir, Time.deltaTime);
            UpdateAnimatorPlaySpeeds(isAir, Time.deltaTime);
        }

        private void OnAnimatorIK()
        {
            m_Animation.AnimateIK(in m_Target, m_LookWeight);
        }

        public void SetInput(in Vector2 axis, in Vector3 target, in bool isRun, in bool isJump)
        {
            m_Axis = axis;
            m_Target = target;
            m_IsRun = isRun;
            m_IsJump = isJump;

            if (m_Axis.sqrMagnitude < Mathf.Epsilon)
            {
                m_Axis = Vector2.zero;
                m_IsMoving = false;
            }
            else
            {
                m_Axis = Vector3.ClampMagnitude(m_Axis, 1f);
                m_IsMoving = true;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.normal.y > m_Controller.stepOffset)
            {
                m_Movement.SetSurface(hit.normal);
            }
        }

        private Animator[] BuildAnimatorTargets()
        {
            var targets = new List<Animator> { m_Animator };
            var controller = m_Animator.runtimeAnimatorController;
            var avatar = m_Animator.avatar;

            for (int i = 0; i < m_Transform.childCount; i++)
            {
                var child = m_Transform.GetChild(i);
                if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                {
                    continue;
                }

                var childAnimator = child.GetComponent<Animator>();
                if (childAnimator == null)
                {
                    childAnimator = child.gameObject.AddComponent<Animator>();
                }

                childAnimator.runtimeAnimatorController = controller;
                childAnimator.avatar = avatar;
                childAnimator.applyRootMotion = false;
                childAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targets.Add(childAnimator);
            }

            return targets.ToArray();
        }

        private void UpdateAnimatorPlaySpeeds(bool isAir, float deltaTime)
        {
            float target;
            if (isAir)
                target = m_AirAnimPlaySpeed;
            else if (!m_IsMoving)
                target = m_IdleAnimPlaySpeed;
            else
                target = m_IsRun ? m_RunAnimPlaySpeed : m_WalkAnimPlaySpeed;

            m_CurrentPlaySpeed = Mathf.Lerp(m_CurrentPlaySpeed, target, 1f - Mathf.Exp(-m_AnimPlaySpeedLerp * deltaTime));
            SetAllAnimatorPlaySpeeds(m_CurrentPlaySpeed);
        }

        private void SetAllAnimatorPlaySpeeds(float speed)
        {
            if (m_Animators == null)
            {
                return;
            }
            for (int i = 0; i < m_Animators.Length; i++)
            {
                if (m_Animators[i] != null)
                {
                    m_Animators[i].speed = speed;
                }
            }
        }

        private void FitControllerToCharacter()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 centerLocal = m_Transform.InverseTransformPoint(worldBounds.center);
            Vector3 sizeLocal = m_Transform.InverseTransformVector(worldBounds.size);
            sizeLocal = new Vector3(Mathf.Abs(sizeLocal.x), Mathf.Abs(sizeLocal.y), Mathf.Abs(sizeLocal.z));

            float newHeight = Mathf.Max(sizeLocal.y * 0.95f, 1.2f);
            float newRadius = Mathf.Max(Mathf.Min(sizeLocal.x, sizeLocal.z) * 0.25f, 0.2f);
            float maxRadius = Mathf.Max(newHeight * 0.5f - 0.02f, 0.2f);
            newRadius = Mathf.Min(newRadius, maxRadius);

            m_Controller.height = newHeight;
            m_Controller.radius = newRadius;
            m_Controller.center = new Vector3(0f, centerLocal.y, 0f);
            m_Controller.stepOffset = Mathf.Clamp(newHeight * 0.12f, 0.1f, 0.45f);
        }

        [Serializable]
        private struct LookWeight
        {
            public float weight;
            public float body;
            public float head;
            public float eyes;

            public LookWeight(float weight, float body, float head, float eyes)
            {
                this.weight = weight;
                this.body = body;
                this.head = head;
                this.eyes = eyes;
            }
        }

        #region Handlers
        private class MovementHandler
        {
            private readonly CharacterController m_Controller;
            private readonly Transform m_Transform;

            private float m_WalkSpeed;
            private float m_RunSpeed;
            private float m_RotateSpeed;
            private float m_JumpHeight;

            private Space m_Space;

            private readonly float m_JumpReload = 1f;

            private float m_TargetAngle;
            private bool m_IsRotating = false;

            private Vector3 m_Normal;
            private Vector3 m_GravityAcelleration = Physics.gravity;

            private float m_jumpTimer;

            public MovementHandler(CharacterController controller, Transform transform, float walkSpeed, float runSpeed, float rotateSpeed, float jumpHeight, Space space)
            {
                m_Controller = controller;
                m_Transform = transform;

                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
                m_JumpHeight = jumpHeight;

                m_Space = space;
            }

            public void SetStats(float walkSpeed, float runSpeed, float rotateSpeed, float jumpHeight, Space space)
            {
                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
                m_JumpHeight = jumpHeight;

                m_Space = space;
            }

            public void SetSurface(in Vector3 normal)
            {
                m_Normal = normal;
            }

            public void Move(float deltaTime, in Vector2 axis, in Vector3 target, bool isRun, bool isJump, bool isMoving, out Vector2 animAxis, out bool isAir)
            {
                var targetForward = target - m_Transform.position;
                targetForward.y = 0f;
                if (targetForward.sqrMagnitude < 0.0001f)
                {
                    targetForward = m_Transform.forward;
                }
                else
                {
                    targetForward.Normalize();
                }

                ConvertMovement(in axis, in targetForward, out var movement);
                CaculateGravity(isJump, deltaTime, out isAir);
                Displace(deltaTime, in movement, isRun);
                Turn(in movement, isMoving);
                UpdateRotation(deltaTime);

                GenAnimationAxis(in movement, out animAxis);
            }

            private void ConvertMovement(in Vector2 axis, in Vector3 targetForward, out Vector3 movement)
            {
                Vector3 forward;
                Vector3 right;

                if (m_Space == Space.Self)
                {
                    forward = new Vector3(targetForward.x, 0f, targetForward.z);
                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        forward = m_Transform.forward;
                    }
                    else
                    {
                        forward.Normalize();
                    }
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }
                else
                {
                    forward = Vector3.forward;
                    right = Vector3.right;
                }

                movement = axis.x * right + axis.y * forward;
                movement = Vector3.ProjectOnPlane(movement, m_Normal);
            }

            private void Displace(float deltaTime, in Vector3 movement, bool isRun)
            {
                Vector3 displacement = (isRun ? m_RunSpeed : m_WalkSpeed) * movement;
                displacement += m_GravityAcelleration;
                displacement *= deltaTime;

                m_Controller.Move(displacement);
            }

            private void CaculateGravity(bool isJump, float deltaTime, out bool isAir)
            {
                m_jumpTimer = Mathf.Max(m_jumpTimer - deltaTime, 0f);

                if (m_Controller.isGrounded)
                {
                    if (isJump && m_jumpTimer <= 0)
                    {
                        var gravity = Physics.gravity;
                        var length = gravity.magnitude;
                        if (length < 0.0001f) length = 9.81f;
                        // Set initial jump velocity directly so JumpHeight maps intuitively.
                        float jumpVelocity = Mathf.Sqrt(2f * m_JumpHeight * length);
                        m_GravityAcelleration = -(gravity / length) * jumpVelocity;
                        m_jumpTimer = m_JumpReload;
                        isAir = true;

                        return;
                    }

                    m_GravityAcelleration = Physics.gravity;
                    isAir = false;

                    return;
                }

                isAir = true;

                m_GravityAcelleration += Physics.gravity * deltaTime;
                return;
            }

            private void GenAnimationAxis(in Vector3 movement, out Vector2 animAxis)
            {
                if(m_Space == Space.Self)
                {
                    animAxis = new Vector2(Vector3.Dot(movement, m_Transform.right), Vector3.Dot(movement, m_Transform.forward));
                }
                else
                {
                    animAxis = new Vector2(Vector3.Dot(movement, Vector3.right), Vector3.Dot(movement, Vector3.forward));
                }
            }

            private void Turn(in Vector3 movement, bool isMoving)
            {
                if (!isMoving)
                {
                    m_IsRotating = false;
                    return;
                }

                var flatMovement = new Vector3(movement.x, 0f, movement.z);
                if (flatMovement.sqrMagnitude < 0.0001f)
                {
                    m_IsRotating = false;
                    return;
                }

                var angle = Vector3.SignedAngle(m_Transform.forward, flatMovement.normalized, Vector3.up);
                m_IsRotating = Mathf.Abs(angle) > Mathf.Epsilon;
                m_TargetAngle = angle;
            }

            private void UpdateRotation(float deltaTime)
            {
                if(!m_IsRotating)
                {
                    return;
                }

                var rotDelta = m_RotateSpeed * deltaTime;
                if (rotDelta + Mathf.PI * 2f + Mathf.Epsilon >= Mathf.Abs(m_TargetAngle))
                {
                    rotDelta = m_TargetAngle;
                    m_IsRotating = false;
                }
                else
                {
                    rotDelta *= Mathf.Sign(m_TargetAngle);
                }

                m_Transform.Rotate(Vector3.up, rotDelta);
            }
        }

        private class AnimationHandler
        {
            private readonly Animator[] m_Animators;

            private readonly string m_HorizontalID;
            private readonly string m_VerticalID;
            private readonly string m_StateID;
            private readonly string m_JumpID;

            private readonly float k_InputFlow = 4.5f;

            private float m_FlowState;
            private Vector2 m_FlowAxis;

            public AnimationHandler(Animator[] animators, string horizontalID, string verticalID, string stateID, string jumpID)
            {
                m_Animators = animators;

                m_HorizontalID = horizontalID;
                m_VerticalID = verticalID;
                m_StateID = stateID;
                m_JumpID = jumpID;
            }

            public void Animate(in Vector2 axis, float state, bool isJump, float deltaTime)
            {
                for (int i = 0; i < m_Animators.Length; i++)
                {
                    m_Animators[i].SetFloat(m_HorizontalID, m_FlowAxis.x);
                    m_Animators[i].SetFloat(m_VerticalID, m_FlowAxis.y);
                    m_Animators[i].SetFloat(m_StateID, Mathf.Clamp01(m_FlowState));
                    m_Animators[i].SetBool(m_JumpID, isJump);
                }

                m_FlowAxis = Vector2.ClampMagnitude(m_FlowAxis + k_InputFlow * deltaTime * (axis - m_FlowAxis).normalized, 1f);
                m_FlowState = Mathf.Clamp01(m_FlowState + k_InputFlow * deltaTime * Mathf.Sign(state - m_FlowState));
            }

            public void AnimateIK(in Vector3 target, in LookWeight lookWeight)
            {
                for (int i = 0; i < m_Animators.Length; i++)
                {
                    m_Animators[i].SetLookAtPosition(target);
                    m_Animators[i].SetLookAtWeight(lookWeight.weight, lookWeight.body, lookWeight.head, lookWeight.eyes);
                }
            }
        }
        #endregion
    }
}