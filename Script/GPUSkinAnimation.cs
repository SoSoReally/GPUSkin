using System.Collections;
using System.Collections.Generic;
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
        public Material material;
        public Texture2D AnimationTexture;
        public GPUSkinState[] Clips;
    }

    [System.Serializable]
    public class GPUSkinState
    {
        public GPUAnimationClip GPUClip;
        public string Name;

        [SerializeField]
        private AnimationClip sourceClip;
        public GPUSkinState(AnimationClip Clip)
        {
            sourceClip = Clip; 
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
