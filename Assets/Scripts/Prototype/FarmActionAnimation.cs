using UnityEngine;

namespace WhatTheFarm.Prototype
{
    // Optional hook for Unity animation assets. No clips are synthesized or imported from Unreal.
    public sealed class FarmActionAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform grip;
        [SerializeField, Min(.1f)] private float actionDuration = .42f;
        [SerializeField] private string swingState = "Swing";
        [SerializeField] private string pickupState = "Pickup";
        [SerializeField] private string idleState = "Idle";
        private float returnTime;
        public Transform Grip => grip != null ? grip : transform;
        public void PlaySwing() => Play(swingState);
        public void PlayPickup() => Play(pickupState);
        private void Play(string state)
        {
            if (animator == null || !animator.HasState(0, Animator.StringToHash(state))) return;
            animator.CrossFadeInFixedTime(state, .05f, 0);
            returnTime = Time.time + actionDuration;
        }
        private void Update()
        {
            if (returnTime <= 0 || Time.time < returnTime) return;
            returnTime = 0;
            if (animator != null && animator.HasState(0, Animator.StringToHash(idleState)))
                animator.CrossFadeInFixedTime(idleState, .08f, 0);
        }
    }
}
