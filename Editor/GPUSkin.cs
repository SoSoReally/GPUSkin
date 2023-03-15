using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Animations;
using System;
using System.Security.Principal;
using System.Runtime.Remoting.Messaging;
using UnityEngine.Assertions.Must;
using System.IO;
using UnityEngine.Windows;
using Unity.Collections.LowLevel.Unsafe;
using File = UnityEngine.Windows.File;

namespace GPUSkin
{
    public partial class GPUSkinCreatWindow 
    {


        //--------原物体-------------
        GameObject Source => SourceObjectField.value as GameObject;
        GameObject Clone;
        //--------绑定姿势-------------
        Matrix4x4[] BindPos;
        Matrix4x4[] BonePos;
        Dictionary<Transform,int> BoneIndexMap;
        Transform[] Bones;

        Dictionary<Renderer,Transform[]> MeshTransformBonesMap;

        //--------Animator-----------
        AnimatorController animator=> AnimatorControllerField.value as AnimatorController;
        AnimationClip[] Clips;
        SkinnedMeshRenderer smr;
        const int BoneRow = 3;
        public int BoneLength;
        //--------Texture -----------
        Texture3D texture3D;
        int piexlIndex = 0;
        Texture2D skintexture2D;
        //---------GPUSkinAsset----------
        GPUSkinAsset skinAsset;
        //only source informaton
        GPUSkinAssetSource sourceSkinAsset;
        //<int,int> pixelstart,framelength
        Dictionary<AnimationClip,GPUAnimationClip> AnimationClipMap;
        public void Export()
        {
            piexlIndex = 0;
            AnimationClipMap = new Dictionary<AnimationClip, GPUAnimationClip>();
            MeshTransformBonesMap = new Dictionary<Renderer, Transform[]>();
            Clone = UnityEngine.Object.Instantiate(Source, Vector3.zero, Quaternion.identity);
            Clone.transform.localScale = Vector3.one;
            Bones = FindAllBonesAndSetBindPos(Clone);
            BoneLength = Bones.Length;
            skinAsset = ScriptableObject.CreateInstance<GPUSkinAsset>();
            sourceSkinAsset = CreateInstance<GPUSkinAssetSource>();
            SetBoneIndexMap(Bones);

            Clips = animator.animationClips;
            Clips = Clips.Distinct().ToArray();
            var totalFrame = Clips.Sum((c) => (int)(c.length * GetClipFrameRate(c, frameRate)));
            var texture = CreatTexture2D(totalFrame, Bones.Length);
            var colors = texture.GetPixels();


            //---------------方式一:单一的创建网格-----------------
            //var newMesh = CreatMesh(smr);
            //---------------方法二:创建合并的网格----------------
            var newMesh = CreatCombineMesh(Clone);

            SetIdentity(colors);
            foreach (var item in Clips)
            {
                SetPixel2D(colors, item);
            }


            var floderPathAdd = floderPath + "/";


            AssetDatabase.CreateAsset(newMesh, floderPathAdd + Source.name+ "_newMesh.asset");


            texture.SetPixels(colors);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, floderPathAdd + Source.name+"_clip.asset");

            SetupGPUSkinAsset(Bones, Clips);


            SetupNewObject(skinAsset,newMesh, Shader.Find("Shader Graphs/GPUSkin"), texture);
            skinAsset.mesh = newMesh;
            skinAsset.AnimationTexture = texture;
            AssetDatabase.CreateAsset(skinAsset, floderPathAdd + Source.name + "_skinAsset.asset");

            AssetDatabase.CreateAsset(sourceSkinAsset, floderPathAdd + Source.name + "_sourceskinAsset.asset");

            Editor.DestroyImmediate(Clone);
        }

        /// <summary>
        /// 得到骨骼,并设置矫正后的BindPos (原始BindPos * skin.worldtolocal),
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private Transform[] FindAllBonesAndSetBindPos(GameObject root)
        {
            //----------------方法二: 寻找所有的骨骼----------------
            //挂件当成骨骼返回
            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            //-----获取骨骼矩阵-----
            List<Matrix4x4> BindPosList = new List<Matrix4x4>();
            foreach (var item in smrs)
            {
                MeshTransformBonesMap.Add(item, item.bones);
                Matrix4x4[] smrlocaltoworld = new Matrix4x4[item.bones.Length];
                for (int i = 0; i < item.bones.Length; i++)
                {
                    smrlocaltoworld[i] = item.sharedMesh.bindposes[i] * item.worldToLocalMatrix;
                }
                BindPosList.AddRange(smrlocaltoworld);
            }
            var bones = smrs.SelectMany<SkinnedMeshRenderer, Transform>((a) => { return (IEnumerable<Transform>)a.bones.ToList(); });
            var simpleBones = root.GetComponentsInChildren<MeshRenderer>();

            foreach (var item in simpleBones)
            {
                MeshTransformBonesMap.Add(item, new Transform[1] { item.transform });
                BindPosList.Add(item.transform.worldToLocalMatrix);
            }

            BindPos = BindPosList.ToArray();

            var result = bones.ToList();
            var simpbleBonesTransform = simpleBones.Select(a => a.transform);
            result.AddRange(simpbleBonesTransform.ToList());
            return result.ToArray();
        }

        private void SetBoneIndexMap(Transform[] bones)
        {
            BoneIndexMap = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
            {
                BoneIndexMap.Add(bones[i], i);
            }
        }
        private Texture2D CreatTexture2D(int frame, int bonesLength)
        {
            int size = 1;
            while (size < frame * bonesLength * BoneRow)
            {
                size <<= 1;
            }
            int width = (int)Mathf.Sqrt(size);
            width = Mathf.NextPowerOfTwo(width);
            var d = new Texture2D((int)width,(int)width,TextureFormat.RGBAHalf,false,true);
            d.filterMode = FilterMode.Point;
            return d;
        }
        private ColorDepth3[] CreatPixel(AnimationClip clip, GameObject skinRoot)
        {
            var finalFrameRate= Mathf.RoundToInt( GetClipFrameRate(clip, frameRate));
            int frame = Mathf.FloorToInt(clip.length* finalFrameRate);
            float dt = clip.length/frame;
            //---skinAsset--
            if (!AnimationClipMap.ContainsKey(clip))
            {
                AnimationClipMap.Add(clip, new GPUAnimationClip()
                {
                    startPixel = piexlIndex,
                    length = frame,
                    startFrame = piexlIndex / (BoneLength * BoneRow),
                    frameRate = (int)finalFrameRate,
                    normalizeLength = ((float)frame) / finalFrameRate,
                    isLoop = clip.isLooping
                }
                ) ;
            }

            ColorDepth3[] pixel = new ColorDepth3[frame*Bones.Length];

            for (int f = 0; f < frame; f++)
            {

                clip.SampleAnimation(skinRoot, f * dt);

                for (int b = 0; b < Bones.Length; b++)
                {
                    var index= b + f * Bones.Length;
                    Matrix4x4 ma =   Bones[b].localToWorldMatrix* skinRoot.transform.worldToLocalMatrix * BindPos[b];
                    //ma = new Matrix4x4(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 0), new Vector4(0, 0, 0, 1));
                    pixel[index].Frist = ma.GetRow(0);
                    pixel[index].Scenod = ma.GetRow(1);
                    pixel[index].Third = ma.GetRow(2);
                }
            }
            return pixel;
        }

        private void SetIdentity(Color[] Colors)
        {
            for (int i = 0; i < Bones.Length; i++)
            {
                Colors[piexlIndex++] = new Vector4(1, 0, 0, 0);
                Colors[piexlIndex++] = new Vector4(0, 1, 0, 0);
                Colors[piexlIndex++] = new Vector4(0, 0, 1, 0);
            }
        }

        private Mesh CreatCombineMesh(GameObject root)
        {


            CombineInstance[] combineInstances = new CombineInstance[MeshTransformBonesMap.Count];
            var renders = MeshTransformBonesMap.Keys.ToArray();
            //------------------Test------------------

            //---------------Test End-----------------
            for (int i = 0; i < MeshTransformBonesMap.Count; i++)
            {
                var render= renders[i];
                switch (render)
                {
                    case SkinnedMeshRenderer smr:
                        SMR(smr, i);
                        break;
                    case MeshRenderer mr:
                        MR(mr, i);
                        break;
                    default:
                        break;
                }
            }
            Mesh final = new Mesh();
            final.CombineMeshes(combineInstances, true);

            return final;

            void SMR(SkinnedMeshRenderer smr, int index)
            {
                List<Vector4> BoneIndex = new List<Vector4>();
                List<Vector4> BoneWeight = new List<Vector4>();
                var mesh  = new Mesh();
                mesh.vertices = smr.sharedMesh.vertices;
                mesh.normals = smr.sharedMesh.normals;
                mesh.triangles = smr.sharedMesh.triangles;
                mesh.uv = smr.sharedMesh.uv;
                mesh.tangents = smr.sharedMesh.tangents;

                var boneweights = smr.sharedMesh.boneWeights;
                var bones = MeshTransformBonesMap[smr];

                foreach (var item in boneweights)
                {
                    var boneIndex1 = item.boneIndex0;
                    var boneIndex2 = item.boneIndex1;
                    var boneIndex3 = item.boneIndex2;
                    var boneIndex4 = item.boneIndex3;

                    var finalBoneIndex1 = BoneIndexMap[bones[boneIndex1]];
                    var finalBoneIndex2 = BoneIndexMap[bones[boneIndex2]];
                    var finalBoneIndex3 = BoneIndexMap[bones[boneIndex3]];
                    var finalBoneIndex4 = BoneIndexMap[bones[boneIndex4]];
                    BoneIndex.Add(new Vector4(finalBoneIndex1, finalBoneIndex2, finalBoneIndex3, finalBoneIndex4));


                    var weight0 = item.weight0;
                    var weight1 = item.weight1;
                    var weight2 = item.weight2;
                    var weight3 = item.weight3;

                    BoneWeight.Add(new Vector4(weight0, weight1, weight2, weight3));
                }
                mesh.SetUVs(1, BoneIndex);
                mesh.SetUVs(2, BoneWeight);


                combineInstances[index] = new CombineInstance()
                {
                    mesh = mesh,
                    transform = smr.transform.root.worldToLocalMatrix* smr.localToWorldMatrix
                };
            }

            void MR(MeshRenderer mr, int index)
            {
                var mesh = new Mesh();

                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null)
                {
                    Debug.LogError("无法合并");
                    return;
                }

                mesh.vertices = mf.sharedMesh.vertices;
                mesh.normals = mf.sharedMesh.normals;
                mesh.triangles = mf.sharedMesh.triangles;
                mesh.uv = mf.sharedMesh.uv;
                mesh.tangents = mf.sharedMesh.tangents;
                List<Vector4> BoneIndex = new List<Vector4>();
                List<Vector4> BoneWeight = new List<Vector4>();
                for (int i = 0; i < mesh.vertices.Length; i++)
                {
                    var boneIndex = BoneIndexMap[MeshTransformBonesMap[mr][0]];
                    Debug.Log(boneIndex);
                    BoneIndex.Add(new Vector4(boneIndex, boneIndex, boneIndex, boneIndex));

                    BoneWeight.Add(new Vector4(1, 0, 0, 0));
                }

                mesh.SetUVs(1, BoneIndex);
                mesh.SetUVs(2, BoneWeight);

                combineInstances[index] = new CombineInstance()
                {
                    mesh = mesh,
                    transform =  mr.transform.root.worldToLocalMatrix * mr.transform.localToWorldMatrix

                };

            }
        }

        private void SetupGPUSkinAsset(Transform[] bones, AnimationClip[] clips)
        {
            skinAsset.BoneCount = bones.Length;

            sourceSkinAsset.BoneNames = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                sourceSkinAsset.BoneNames[i] = bones[i].name;
            }
            skinAsset.ClipCount = clips.Length;

            skinAsset.Clips = new GPUSkinState[clips.Length];
            sourceSkinAsset.sourceClip = new GPUSkinAssetSource.GPUSkinStateSource[clips.Length];
            for (int i = 0; i < skinAsset.Clips.Length; i++)
            {
                skinAsset.Clips[i] = new GPUSkinState()
                { 
                    Name = clips[i].name,
                    GPUClip = AnimationClipMap[clips[i]],
                };

                sourceSkinAsset.sourceClip[i] = new GPUSkinAssetSource.GPUSkinStateSource()
                {
                    GPUSkinState = skinAsset.Clips[i],
                    AnimationClip = clips[i]
                };
            }
            skinAsset.EveryFramePixelLength = skinAsset.BoneCount * BoneRow;

        }
        private void SetPixel2D(Color[] Colors, AnimationClip clip)
        {
            Debug.Log(Colors.Length);

            //var skm = Clone.GetComponentInChildren<SkinnedMeshRenderer>();

            var pixel = CreatPixel(clip, Clone.gameObject);



            for (int i = 0; i < pixel.Length; i++)
            {
                Colors[piexlIndex++] = pixel[i].Frist;
                Colors[piexlIndex++] = pixel[i].Scenod;
                Colors[piexlIndex++] = pixel[i].Third;
            }
        }
        private void SetupNewObject(GPUSkinAsset skinAsset, Mesh mesh, Shader shader, Texture2D texture2D)
        {
            var floderPathAdd = floderPath + "/";
            GameObject prefab = new GameObject(Source.name+"_GPUSkin");
            prefab.AddComponent<MeshFilter>().mesh = mesh;
            prefab.transform.position = Vector3.zero;
            var material = new Material(shader);
            material.SetTexture("_GPUSkinAllAnimationClip", texture2D);
            material.SetFloat("_EveryFramePixelLength", skinAsset.EveryFramePixelLength);
            material.SetFloat("_BoneRow", BoneRow);
            material.SetFloat("_CurrentFrame", 1);
            material.SetFloat("_NextFrame", 0);
            material.SetFloat("_Transition", 0);
            prefab.AddComponent<MeshRenderer>().material = material;


            skinAsset.materialDefault = material;
            AssetDatabase.CreateAsset(skinAsset.materialDefault, floderPathAdd + Source.name + ".mat");


            var prefabPath = floderPath + "/" + prefab.name + ".prefab";
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
            var result = PrefabUtility.SaveAsPrefabAsset(prefab, floderPathAdd + prefab.name + ".prefab");
          
            PrefabUtility.InstantiatePrefab(result);
            // GameObject.Instantiate(result);
            DestroyImmediate(prefab);
        }

        private void ConverSkinTexture2DTo3D(Texture2D texture2D)
        {
            var length = texture2D.width*texture2D.height;
            length /= 3;
            var cps = Mathf.Sqrt(length);
            var size = Mathf.NextPowerOfTwo((int)cps);

            var td3 = new Texture3D(size,size,3,TextureFormat.RGBAHalf,false);
            var colors = texture2D.GetPixels();
            var colors3D = td3.GetPixels();
            List<Matrix4x4> frame = new List<Matrix4x4>();
            for (int i = 0; i < length; i += 3)
            {
                var ma4 = new Matrix4x4(
                    new Vector4(colors[i].r,colors[i].g,colors[i].b,colors[i].a),
                    new Vector4(colors[i+1].r, colors[i+1].g,colors[i+1].b,colors[i+1].a),
                    new Vector4(colors[i+2].r,colors[i+2].g,colors[i+2].b,colors[i+2].a),
                    new Vector4(0f,0f,0f,1f));
                frame.Add(ma4);
            }
            var p = size*size;
            for (int i = 0; i < frame.Count; i++)
            {
                colors3D[i] = frame[i].GetColumn(0);
                colors3D[i + p] = frame[i].GetColumn(1);
                colors3D[i + 2 * p] = frame[i].GetColumn(2);
            }
            td3.SetPixels(colors3D);
            td3.Apply();
            AssetDatabase.CreateAsset(td3, floderPath + "_skin3d.asset");
            AssetDatabase.Refresh();
        }

        private Dictionary<int, Dictionary<string, AnimationClip>> GetAllMition(AnimatorController controller, out Dictionary<string, AnimationClip> allClips)
        {
            Dictionary<int,Dictionary<string,AnimationClip> > final = new Dictionary<int, Dictionary<string, AnimationClip>>();
            allClips = new Dictionary<string, AnimationClip>();

            for (int i = 0; i < controller.layers.Length; i++)
            {
                Dictionary<string,AnimationClip> da = new Dictionary<string, AnimationClip>();
                foreach (var m in controller.layers[i].stateMachine.states)
                {
                    if (m.state.motion == null)
                    {
                        continue;
                    }
                    if (m.state.motion is BlendTree blendtree)
                    {
                        for (int j  = 0; j < blendtree.children.Length; j++)
                        {
                            var child = blendtree.children[j];
                            if (child.motion==null)
                            {
                                continue;
                            }
                            if (!allClips.ContainsKey(child.motion.name))
                            {
                                allClips.Add(child.motion.name, child.motion as AnimationClip);
                            }
                            da.Add(blendtree.name+"", child.motion as AnimationClip);
                        }
                    }
                    else
                    {
                        if (!allClips.ContainsKey(m.state.motion.name))
                        {
                            allClips.Add(m.state.motion.name, m.state.motion as AnimationClip);
                        }
                        da.Add(m.state.name, m.state.motion as AnimationClip);
                    }
                }
                final.Add(i, da);
            }

            return final;
        }


        private float GetClipFrameRate(AnimationClip clip,FrameRate rate)
        {
            float final =0f;
            switch (rate)
            {
                case FrameRate.ClipFrame:
                     final = clip.frameRate;
                    break;
                case FrameRate.f60:
                    final = 60f;
                    break;
                case FrameRate.f30:
                    final = 30f;
                    break;
                case FrameRate.f25:
                    final = 25f;
                    break;
                default:
                    final = clip.frameRate;
                    break;
            }
            return final;
        }

    }
    public struct ColorDepth3
    {
        public Color Frist;
        public Color Scenod;
        public Color Third;
    }

    public enum FrameRate : int
    {
        f60 = 60,
        f30 = 30,
        f25 = 25,
        ClipFrame,
    }
}
