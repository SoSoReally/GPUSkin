using System;
using System.Collections;
using System.Collections.Generic;
using DOTS;
using GPUSkin;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditorInternal;
using UnityEngine;
using static Unity.Collections.AllocatorManager;
namespace GPUSkin
{
    public class GPUAnimatorScript : MonoBehaviour
    {
        public GPUSkinAsset skinAsset;
        public bool HasLerp;
        public bool HasTransition;
        public CurrentFrame currentFrame;
        public LerpFrame lerpFrame;
        public TransitionFrame transitionFrame;
        public bool OnTransition = false;
        public GPUSkin.Transition transition;
        public AnimationController animationController;
        public AnimationTransition animationTransition;
        MaterialPropertyBlock block;
        public MeshRenderer meshRenderer;
        private int _CurrentFrameID;
        private int _LerpFrameID;
        private int _TransitionFrameID;
        private int _TransitionID;

        // Start is called before the first frame update
        void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            var material = meshRenderer.sharedMaterial;
            HasLerp = GPUSkinUtility.HasLerp(material.shader);
            HasTransition = GPUSkinUtility.HasTransition(material.shader);
            _CurrentFrameID = Shader.PropertyToID("_CurrentFrame");
            _LerpFrameID = Shader.PropertyToID("_LerpFrame");
            _TransitionFrameID = Shader.PropertyToID("_TransitionFrame");
            _TransitionID = Shader.PropertyToID("_Transition");
            block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            animationController = new AnimationController() { currentState = new AnimationState() { clip = GetAnimationClip(0), currentFrame = 0, travelTime = 0, index = 0 }, speed = 1f ,CanTranition = HasTransition,isLerp = HasLerp};
        }

        // Update is called once per frame
        void Update()
        {
            var deltaTime = Time.deltaTime;
            if (HasTransition && OnTransition)
            {
                animationTransition.trvael -= deltaTime * animationController.speed;
                if (animationTransition.trvael >= animationTransition.normalizeLength)
                {
                    animationController.currentState = animationTransition.nextState;
                    animationController.currentState.currentFrame = transitionFrame.value;
                    animationController.currentState.travelTime = (transitionFrame.value - animationController.currentState.clip.start) / animationTransition.nextState.clip.frameRate;
                    transitionFrame.value = 0;
                    transition.value = 0;
                    animationTransition.trvael = 0;
                    OnTransition = false;
                }
                else
                {
                    transition.value = Mathf.Clamp01(animationTransition.trvael);
                    block.SetFloat(_TransitionID, transition.value);
                }
            }

            if (HasLerp)
            {
                ref readonly var clip = ref animationController.currentState.clip;
                ref var state = ref animationController.currentState;

                float cliplength = clip.length;

                state.travelTime += (deltaTime * animationController.speed);
                var travelFrame = state.travelTime * clip.frameRate;
                if (clip.isLoop)
                {
                    lerpFrame.value = travelFrame + 1;
                    travelFrame %= cliplength;
                    lerpFrame.value %= cliplength;
                }
                else
                {
                    travelFrame = math.min(travelFrame, clip.length - 0.0001f);
                    lerpFrame.value = 0;
                }
                state.currentFrame = clip.start + travelFrame;
                currentFrame.value = state.currentFrame;
                lerpFrame.value += clip.start;
                block.SetFloat(_LerpFrameID, lerpFrame.value);
                block.SetFloat(_CurrentFrameID, currentFrame.value);
            }
            else
            {
                ref readonly var clip = ref animationController.currentState.clip;
                ref var state = ref animationController.currentState;
                currentFrame.value = state.currentFrame;

                state.travelTime += (deltaTime * animationController.speed);



                if (clip.isLoop)
                {
                    state.travelTime %= clip.normalizeLength;
                }
                else
                {
                    state.travelTime = math.min(state.travelTime, clip.normalizeLength);
                }
                var frame = state.travelTime * clip.frameRate;
                state.currentFrame = clip.start + Mathf.FloorToInt(frame);
                state.currentFrame = math.clamp(state.currentFrame, clip.start, clip.start + clip.length - 1);

                block.SetFloat(_CurrentFrameID, currentFrame.value);
            }

            meshRenderer.SetPropertyBlock(block);
        }

        public GPUAnimationClipData GetAnimationClip(int index)
        {
            ref var clip = ref skinAsset.Clips[index].GPUClip;
            return new GPUAnimationClipData()
            {
                frameRate = clip.frameRate,
                length = clip.length,
                isLoop = clip.isLoop,
                start = clip.startFrame,
                normalizeLength = clip.normalizeLength,
            };

        }

        public void SetTranitionID(int index, float transitionTime = 0.5f)
        {
            OnTransition = true;
            ref var clip = ref skinAsset.Clips[index].GPUClip;
            var next = new AnimationState()
            {
                clip = GetAnimationClip(index),
                currentFrame = 0,
                index = 1,
                travelTime = 0,

            };
            animationTransition = new AnimationTransition() { nextState = next, normalizeLength = transitionTime };
            var noramllength = (transitionTime % next.clip.normalizeLength) * next.clip.frameRate;
            transitionFrame = new TransitionFrame()
            {
                value = next.clip.start + math.ceil(noramllength)
            };
            block.SetFloat(_TransitionFrameID, transitionFrame.value);
        }
    }
}