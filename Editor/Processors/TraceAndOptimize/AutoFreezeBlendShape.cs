using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;

#if AAO_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace Anatawa12.AvatarOptimizer.Processors.TraceAndOptimizes
{
    internal class AutoFreezeBlendShape : TraceAndOptimizePass<AutoFreezeBlendShape>
    {
        public override string DisplayName => "T&O: AutoFreezeBlendShape";

        protected override void Execute(BuildContext context, TraceAndOptimizeState state)
        {
            if (!state.FreezeBlendShape) return;

            if (!state.SkipFreezingNonAnimatedBlendShape)
                FreezeNonAnimatedBlendShapes(context, state);
            if (!state.SkipFreezingMeaninglessBlendShape)
                FreezeMeaninglessBlendShapes(context, state);
        }

        void FreezeNonAnimatedBlendShapes(BuildContext context, TraceAndOptimizeState state)
        {
            // first optimization: unused BlendShapes
            foreach (var skinnedMeshRenderer in context.GetComponents<SkinnedMeshRenderer>())
            {
                if (state.Exclusions.Contains(skinnedMeshRenderer.gameObject)) continue; // manual exclusiton

                var meshInfo = context.GetMeshInfoFor(skinnedMeshRenderer);

                var modifies = context.GetAnimationComponent(skinnedMeshRenderer);

                var unchanged = new HashSet<string>();

                for (var i = 0; i < meshInfo.BlendShapes.Count; i++)
                {
                    var (name, weight) = meshInfo.BlendShapes[i];
                    if (IsUnchangedBlendShape(name, weight, out var newWeight))
                    {
                        unchanged.Add(name);
                        meshInfo.BlendShapes[i] = (name, newWeight);
                    }
                }
                
                bool IsUnchangedBlendShape(string name, float weight, out float newWeight)
                {
                    newWeight = weight;
                    if (!modifies.TryGetFloat($"blendShape.{name}", out var prop)) return true;

                    if (prop.Value.TryGetConstantValue(out var constWeight))
                    {
                        if (prop.AppliedAlways)
                        {
                            newWeight = constWeight;
                            return true;
                        }
                        else
                        {
                            return constWeight.Equals(weight);
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (unchanged.Count == 0) continue;

                var freeze = skinnedMeshRenderer.gameObject.GetOrAddComponent<FreezeBlendShape>();
                var shapeKeys = freeze.shapeKeysSet.GetAsSet();
                shapeKeys.UnionWith(unchanged);
                freeze.shapeKeysSet.SetValueNonPrefab(shapeKeys);
            }
        }

        void FreezeMeaninglessBlendShapes(BuildContext context, TraceAndOptimizeState state) {
            ComputePreserveBlendShapes(context, state.PreserveBlendShapes);

            // second optimization: remove meaningless blendShapes
            foreach (var skinnedMeshRenderer in context.GetComponents<SkinnedMeshRenderer>())
            {
                if (state.Exclusions.Contains(skinnedMeshRenderer.gameObject)) continue; // manual exclusion
                skinnedMeshRenderer.gameObject.GetOrAddComponent<FreezeBlendShape>();
                skinnedMeshRenderer.gameObject.GetOrAddComponent<InternalAutoFreezeMeaninglessBlendShape>();
            }
        }

        private void ComputePreserveBlendShapes(BuildContext context, Dictionary<SkinnedMeshRenderer, HashSet<string>> preserveBlendShapes)
        {
#if AAO_VRCSDK3_AVATARS
            // some BlendShapes manipulated by VRC Avatar Descriptor must exists
            var descriptor = context.AvatarDescriptor;
            if (descriptor)
            {
                switch (descriptor.lipSync)
                {
                    case VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape when descriptor.VisemeSkinnedMesh != null:
                    {
                        var skinnedMeshRenderer = descriptor.VisemeSkinnedMesh;
                        if (!preserveBlendShapes.TryGetValue(skinnedMeshRenderer, out var set))
                            preserveBlendShapes.Add(skinnedMeshRenderer, set = new HashSet<string>());
                        set.UnionWith(descriptor.VisemeBlendShapes);
                        break;
                    }
                    case VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape when descriptor.VisemeSkinnedMesh != null:
                    {
                        var skinnedMeshRenderer = descriptor.VisemeSkinnedMesh;
                        if (!preserveBlendShapes.TryGetValue(skinnedMeshRenderer, out var set))
                            preserveBlendShapes.Add(skinnedMeshRenderer, set = new HashSet<string>());
                        set.Add(descriptor.MouthOpenBlendShapeName);
                        break;
                    }
                }

                if (descriptor.enableEyeLook)
                {
                    switch (descriptor.customEyeLookSettings.eyelidType)
                    {
                        case VRCAvatarDescriptor.EyelidType.None:
                            break;
                        case VRCAvatarDescriptor.EyelidType.Bones:
                            break;
                        case VRCAvatarDescriptor.EyelidType.Blendshapes
                            when descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null:
                        {
                            var skinnedMeshRenderer = descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
                            if (!preserveBlendShapes.TryGetValue(skinnedMeshRenderer, out var set))
                                preserveBlendShapes.Add(skinnedMeshRenderer, set = new HashSet<string>());

                            var mesh = skinnedMeshRenderer.sharedMesh;
                            set.UnionWith(
                                from index in descriptor.customEyeLookSettings.eyelidsBlendshapes
                                where 0 <= index && index < mesh.blendShapeCount
                                select mesh.GetBlendShapeName(index)
                            );
                        }
                            break;
                    }
                }
            }
#endif
        }
    }
}
