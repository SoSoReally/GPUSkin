using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUSkin
{
    [Serializable]
    public partial struct CurrentFrame
    {
        public float value;
    }
    [Serializable]
    public partial struct LerpFrame
    {
        public float value;
    }
    [Serializable]
    public partial struct TransitionFrame
    {
        public float value;
    }
    [Serializable]
    public partial struct Transition
    {
        public float value;
    }
    [Serializable]
    public struct GPUAnimationClipData
    {
        public int start;
        public int length;
        public bool isLoop;
        public float frameRate;
        public float normalizeLength;

        public GPUAnimationClipData(GPUAnimationClip gPUAnimationClip)
        {
            start = gPUAnimationClip.startFrame;
            frameRate = gPUAnimationClip.frameRate;
            isLoop = gPUAnimationClip.isLoop;
            length = gPUAnimationClip.length;
            normalizeLength = gPUAnimationClip.normalizeLength;
        }

        public static explicit operator GPUAnimationClipData(GPUAnimationClip gPUAnimationClip)
        {
            return new GPUAnimationClipData(gPUAnimationClip);
        }
    }
    [Serializable]
    public struct AnimationState
    {
        public int index;
        public float travelTime;
        public float currentFrame;
        public GPUAnimationClipData clip;
        public readonly bool IsNull => index == 0;
        public void SetNull() => index = 0;
    }


    [Serializable]
    public partial struct AnimationController
    {
        public AnimationState currentState;
        public float speed;
        public bool isLerp;
        public bool CanTranition;
    }

    [Serializable]
    public partial struct AnimationTransition
    {
        public AnimationState nextState;
        public float trvael;
        public float normalizeLength;
    }



}
