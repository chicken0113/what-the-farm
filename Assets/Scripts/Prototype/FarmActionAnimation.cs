using UnityEngine;

namespace WhatTheFarm.Prototype
{
    // Procedural first-person actions, with optional Animator hooks for future clips.
    public sealed class FarmActionAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform grip;
        [SerializeField, Min(.1f)] private float actionDuration = .42f;
        [SerializeField] private string swingState = "Swing";
        [SerializeField] private string pickupState = "Pickup";
        [SerializeField] private string idleState = "Idle";
        private float returnTime;
        private float elapsed, duration;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool procedural;
        private bool punch;
        public enum Action { None, Swing, Pickup, Use, Plant }
        public Action CurrentAction { get; private set; }
        public bool IsPlaying => CurrentAction != Action.None;
        public Transform Grip => grip != null ? grip : transform;
        public static FarmActionAnimation Create(Transform camera)
        {
            var root = new GameObject("Player Action Pivot"); root.transform.SetParent(camera, false);
            var actions = root.AddComponent<FarmActionAnimation>(); actions.procedural = true;
            actions.restPosition = root.transform.localPosition; actions.restRotation = root.transform.localRotation;
            var gripObject = new GameObject("Animated Item Grip"); gripObject.transform.SetParent(root.transform, false);
            gripObject.transform.localPosition = new Vector3(.42f,-.36f,.8f); actions.grip = gripObject.transform;
            return actions;
        }
        public void PlaySwing(bool withItem = true) { punch=!withItem; Begin(Action.Swing,actionDuration); Play(swingState); }
        public void PlayPickup() { Begin(Action.Pickup,.32f); Play(pickupState); }
        public void PlayUse() { Begin(Action.Use,.42f); Play(swingState); }
        public void PlayPlant() { Begin(Action.Plant,.38f); Play(swingState); }
        private void Begin(Action action,float seconds)
        { Stop(); CurrentAction=action; elapsed=0; duration=seconds; ApplyPose(0); }
        public void Stop()
        {
            CurrentAction=Action.None;
            if(procedural) transform.SetLocalPositionAndRotation(restPosition,restRotation);
        }
        private void ApplyPose(float progress)
        {
            if(!procedural) return;
            Vector3 position=Vector3.zero, angles=Vector3.zero;
            if(CurrentAction==Action.Swing)
            {
                if(punch)
                {
                    float extend=progress<.25f ? -Mathf.SmoothStep(0,1,progress/.25f)*.12f : progress<.55f ? Mathf.Lerp(-.12f,.32f,Mathf.SmoothStep(0,1,(progress-.25f)/.3f)) : Mathf.Lerp(.32f,0,Mathf.SmoothStep(0,1,(progress-.55f)/.45f));
                    position=new Vector3(-.1f,.07f,0)*Mathf.Max(0,extend/.32f)+Vector3.forward*extend;
                    angles=new Vector3(-8,-15,-8)*Mathf.Max(0,extend/.32f);
                }
                else
                {
                if(progress<.25f)
                {
                    float t=Mathf.SmoothStep(0,1,progress/.25f);
                    position=Vector3.Lerp(Vector3.zero,new Vector3(.07f,.04f,-.12f),t);
                    angles=Vector3.Lerp(Vector3.zero,new Vector3(-22,28,18),t);
                }
                else if(progress<.55f)
                {
                    float t=Mathf.SmoothStep(0,1,(progress-.25f)/.3f);
                    position=Vector3.Lerp(new Vector3(.07f,.04f,-.12f),new Vector3(-.15f,-.04f,.2f),t);
                    angles=Vector3.Lerp(new Vector3(-22,28,18),new Vector3(38,-35,-25),t);
                }
                else
                {
                    float t=Mathf.SmoothStep(0,1,(progress-.55f)/.45f);
                    position=Vector3.Lerp(new Vector3(-.15f,-.04f,.2f),Vector3.zero,t);
                    angles=Vector3.Lerp(new Vector3(38,-35,-25),Vector3.zero,t);
                }
                }
            }
            else
            {
                float reach=Mathf.Sin(progress*Mathf.PI);
                if(CurrentAction==Action.Pickup) { position=new Vector3(-.08f,-.09f,.22f)*reach; angles=new Vector3(18,-12,-10)*reach; }
                if(CurrentAction==Action.Use) { position=new Vector3(-.04f,-.08f,.12f)*reach; angles=new Vector3(32,-12,-18)*reach; }
                if(CurrentAction==Action.Plant) { position=new Vector3(-.08f,-.17f,.2f)*reach; angles=new Vector3(42,-8,-12)*reach; }
            }
            transform.SetLocalPositionAndRotation(restPosition+position,restRotation*Quaternion.Euler(angles));
        }
        public void AdvanceAnimation(float seconds)
        {
            if(!IsPlaying || seconds<=0) return;
            elapsed+=seconds;
            if(elapsed>=duration) { Stop(); return; }
            ApplyPose(elapsed/duration);
        }
        private void Play(string state)
        {
            if (animator == null || !animator.HasState(0, Animator.StringToHash(state))) return;
            animator.CrossFadeInFixedTime(state, .05f, 0);
            returnTime = Time.time + actionDuration;
        }
        private void LateUpdate()
        {
            AdvanceAnimation(Time.deltaTime);
            if (returnTime <= 0 || Time.time < returnTime) return;
            returnTime = 0;
            if (animator != null && animator.HasState(0, Animator.StringToHash(idleState)))
                animator.CrossFadeInFixedTime(idleState, .08f, 0);
        }
    }
}
