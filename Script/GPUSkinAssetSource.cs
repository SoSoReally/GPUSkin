using UnityEngine;

namespace GPUSkin
{
    public class GPUSkinAssetSource : ScriptableObject
    {
        public string[] BoneNames;
        public GPUSkinStateSource[] sourceClip;

        [System.Serializable]
        public class GPUSkinStateSource
        {
            public GPUSkinState GPUSkinState;
            public AnimationClip AnimationClip;
        }
    }
}
