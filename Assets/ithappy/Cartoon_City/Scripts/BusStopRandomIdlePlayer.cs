using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    /// <summary>
    /// Replaces the prefab walk controller motion with one idle clip from People_Idle (set per instance).
    /// That clip plays looped on the same Animator state — no runtime switching, so no jitter between clips.
    /// Looping follows the clip import settings (Loop Time in the .anim importer).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BusStopRandomIdlePlayer : MonoBehaviour
    {
        [SerializeField] private AnimationClip loopedIdleClip;

        [Tooltip("Blend from default pose into idle (normalized transition duration, 0–1).")]
        [SerializeField] private float crossFadeNormalized = 0.18f;

        private Animator _animator;
        private AnimatorOverrideController _override;
        private readonly List<string> _originalClipNames = new List<string>(4);
        private int _baseLayerStateHash;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            RuntimeAnimatorController baseController = _animator.runtimeAnimatorController;
            if (baseController == null) return;

            foreach (AnimationClip c in baseController.animationClips)
            {
                if (c != null && !_originalClipNames.Contains(c.name))
                    _originalClipNames.Add(c.name);
            }

            if (_originalClipNames.Count == 0) return;

            _override = new AnimatorOverrideController(baseController);
            _animator.runtimeAnimatorController = _override;
        }

        private void Start()
        {
            if (_animator == null || _override == null || loopedIdleClip == null)
                return;

            foreach (string key in _originalClipNames)
                _override[key] = loopedIdleClip;

            _animator.Update(0f);
            _baseLayerStateHash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;

            float t = Random.Range(0f, 1f);
            float fade = Mathf.Clamp01(crossFadeNormalized);
            _animator.CrossFade(_baseLayerStateHash, fade, 0, t);
        }
    }
}
