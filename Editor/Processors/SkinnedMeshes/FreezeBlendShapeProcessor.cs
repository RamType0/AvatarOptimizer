using System.Collections;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using UnityEngine.Profiling;

namespace Anatawa12.AvatarOptimizer.Processors.SkinnedMeshes
{
    internal class FreezeBlendShapeProcessor : EditSkinnedMeshProcessor<FreezeBlendShape>
    {
        public FreezeBlendShapeProcessor(FreezeBlendShape component) : base(component)
        {
        }

        public override EditSkinnedMeshProcessorOrder ProcessOrder => EditSkinnedMeshProcessorOrder.AfterRemoveMesh;

        public override void Process(BuildContext context, MeshInfo2 target)
        {
            FreezeBlendShapes(Target, context, target, Component.FreezingShapeKeys, true);
        }

        public static void FreezeBlendShapes(
            SkinnedMeshRenderer targetSMR,
            BuildContext context,
            MeshInfo2 target,
            HashSet<string> freezeNames,
            bool withWarning = false
        )
        {
            // Warn for blendShape animation
            if (withWarning) {
                var modified = new HashSet<string>();
                var sources = new HashSet<object>();
                var animationComponent = context.GetAnimationComponent(targetSMR);

                foreach (var blendShape in freezeNames)
                {
                    if (animationComponent.TryGetFloat($"blendShape.{blendShape}", out var p))
                    {
                        // allow constant animation
                        var weight = target.BlendShapes.Find(r => r.name == blendShape);
                        if (weight.name == null) continue; // no such blendShape 
                        var values = p.Value.PossibleValues;
                        if (values != null)
                        {
                            // animated to constant.
                            // we think the constant is the original constant value.
                            // we assume user want to override it.
                            if (values.Length == 1) continue;
                            // animated to two constant and one is current.
                            // we think the one is created during creating the new animation and
                            // the other is the original constant value.
                            // and we assume user want to override it.
                            if (values.Length == 2 && values.Contains(weight.weight)) continue;
                        } 

                        modified.Add(blendShape);
                        sources.Add(p.ContextReferences);
                    }
                }

                if (modified.Count != 0)
                {
                    // ReSharper disable once CoVariantArrayConversion
                    BuildLog.LogWarning("FreezeBlendShape:warning:animation", string.Join(", ", modified),
                            targetSMR, sources);
                }
            }

            var freezes = new BitArray(target.BlendShapes.Count);
            for (var i = 0; i < target.BlendShapes.Count; i++)
                freezes[i] = freezeNames.Contains(target.BlendShapes[i].name);

            Profiler.BeginSample("DoFreezeBlendShape");
            foreach (var vertex in target.Vertices)
            {
                for (var i = 0; i < target.BlendShapes.Count; i++)
                {
                    if (!freezes[i]) continue;
                    var (name, weight) = target.BlendShapes[i];
                    Profiler.BeginSample("TryGetBlendShape");
                    var result =
                        vertex.TryGetBlendShape(name, weight, out var position, out var normal, out var tangent);
                    Profiler.EndSample();
                    if (!result) continue;

                    Profiler.BeginSample("Apply offsets");
                    vertex.Position += position;
                    vertex.Normal += normal;
                    tangent += (Vector3)vertex.Tangent;
                    vertex.Tangent = new Vector4(tangent.x, tangent.y, tangent.z, vertex.Tangent.w);
                    vertex.BlendShapes.Remove(name);
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();

            Profiler.BeginSample("MoveProperties");
            {
                int srcI = 0, dstI = 0;
                for (; srcI < target.BlendShapes.Count; srcI++)
                {
                    if (!freezes[srcI])
                    {
                        // for keep prop: move the BlendShape index. name is not changed.
                        context.RecordMoveProperty(targetSMR, VProp.BlendShapeIndex(srcI), VProp.BlendShapeIndex(dstI));
                        target.BlendShapes[dstI++] = target.BlendShapes[srcI];
                    }
                    else
                    {
                        // for frozen prop: remove that BlendShape
                        context.RecordRemoveProperty(targetSMR, VProp.BlendShapeIndex(srcI));
                        context.RecordRemoveProperty(targetSMR, $"blendShape.{target.BlendShapes[srcI].name}");
                    }
                }

                target.BlendShapes.RemoveRange(dstI, target.BlendShapes.Count - dstI);
            }
            Profiler.EndSample();
        }

        public override IMeshInfoComputer GetComputer(IMeshInfoComputer upstream) => new MeshInfoComputer(this, upstream);

        class MeshInfoComputer : AbstractMeshInfoComputer
        {
            private readonly FreezeBlendShapeProcessor _processor;

            public MeshInfoComputer(FreezeBlendShapeProcessor processor, IMeshInfoComputer upstream) : base(upstream)
                => _processor = processor;

            public override (string, float)[] BlendShapes()
            {
                var set = _processor.Component.FreezingShapeKeys;
                return base.BlendShapes().Where(x => !set.Contains(x.name)).ToArray();
            }
        }
    }
}
