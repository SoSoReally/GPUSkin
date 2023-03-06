using Unity.Entities;
using UnityEngine;

namespace GPUSkin
{
    public  static partial class GPUSkinUtility
    {
        public const string GPUSkin = "Shader Graphs/GPUSkin";
        public const string GPUSkinLerp = "Shader Graphs/GPUSkinLerp";
        public const string GPUSkinTranition = "Shader Graphs/GPUSkinTransition";
        public const string GPUSkinLerpAndTranition = "Shader Graphs/GPUSkinLerpAndTransition";


        public static bool HasTransition(Shader shader)
        {
            var shaderName = shader.name;
            bool result = false;
            switch (shaderName)
            {
                case GPUSkinTranition:
                    result = true;
                    break;
                case GPUSkinLerpAndTranition:
                    result = true;
                    break;
                default: break;
            }
            return result;
        }

        public static bool HasLerp(Shader shader)
        {
            var shaderName = shader.name;
            bool result = false;
            switch (shaderName)
            {
                case GPUSkinLerp:
                    result = true;
                    break;
                case GPUSkinLerpAndTranition:
                    result = true;
                    break;
                default: break;
            }
            return result;
        }
    }


}