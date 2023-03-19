// generated by generate.ts
// running generate.ts via deno will output this file.
using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using VRC.Dynamics;
using Object = UnityEngine.Object;

namespace Anatawa12.AvatarOptimizer.PrefabSafeSet
{
    [Serializable]
    public class SkinnedMeshRendererSet : PrefabSafeSet<SkinnedMeshRenderer, SkinnedMeshRendererSet.Layer>
    {
        public SkinnedMeshRendererSet(Object outerObject) : base(outerObject)
        {
        }
        [Serializable]
        public class Layer : PrefabLayer<SkinnedMeshRenderer>{}
    }
    [Serializable]
    public class MeshRendererSet : PrefabSafeSet<MeshRenderer, MeshRendererSet.Layer>
    {
        public MeshRendererSet(Object outerObject) : base(outerObject)
        {
        }
        [Serializable]
        public class Layer : PrefabLayer<MeshRenderer>{}
    }
    [Serializable]
    public class MaterialSet : PrefabSafeSet<Material, MaterialSet.Layer>
    {
        public MaterialSet(Object outerObject) : base(outerObject)
        {
        }
        [Serializable]
        public class Layer : PrefabLayer<Material>{}
    }
    [Serializable]
    public class StringSet : PrefabSafeSet<String, StringSet.Layer>
    {
        public StringSet(Object outerObject) : base(outerObject)
        {
        }
        [Serializable]
        public class Layer : PrefabLayer<String>{}
    }
    [Serializable]
    public class VRCPhysBoneBaseSet : PrefabSafeSet<VRCPhysBoneBase, VRCPhysBoneBaseSet.Layer>
    {
        public VRCPhysBoneBaseSet(Object outerObject) : base(outerObject)
        {
        }
        [Serializable]
        public class Layer : PrefabLayer<VRCPhysBoneBase>{}
    }
}
