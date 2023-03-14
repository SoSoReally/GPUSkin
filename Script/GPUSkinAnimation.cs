﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor.Purchasing;
using UnityEngine;

namespace GPUSkin
{
    public class GPUSkinAsset : ScriptableObject
    {
        public const int BoneRow=3;
        public string[] BoneNames;
        public int EveryFramePixelLength;
        public int BoneCount;
        public int ClipCount;
        public Mesh mesh;
        public Material materialDefault;
        public Texture2D AnimationTexture;
        public GPUSkinState[] Clips;
    }

    [System.Serializable]
    public class GPUSkinState
    {
        public GPUAnimationClip GPUClip;
        public string Name;
#if UNITY_EDITOR
        public AnimationClip sourceClip;
#endif
        public GPUSkinState()
        {

        }
    }
    [System.Serializable]
    public struct GPUAnimationClip
    {
        public int startPixel;
        public int startFrame;
        public int length;
        public int frameRate;
        public float normalizeLength;
        public bool isLoop;
    }
}
