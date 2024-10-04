using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;

namespace Anatawa12.AvatarOptimizer.AnimatorParsersV2
{
    class HumanoidAnimatorPropModNode : ComponentPropModNode<ValueInfo<float>, Animator>
    {
        public HumanoidAnimatorPropModNode(Animator component) : base(component)
        {
        }

        public override ValueInfo<float> Value => ValueInfo<float>.Variable;
        public override bool AppliedAlways => true;
    }

    internal readonly struct PlayableLayerNodeInfo<T> : ILayer<ValueInfo<T>>
        where T : notnull
    {
        public AnimatorWeightState Weight { get; }
        public AnimatorLayerBlendingMode BlendingMode { get; }
        public int LayerIndex { get; }
        public readonly AnimatorControllerPropModNode<T> Node;
        PropModNode<ValueInfo<T>> ILayer<ValueInfo<T>>.Node => Node;
        IPropModNode ILayer.Node => Node;

        public PlayableLayerNodeInfo(AnimatorWeightState weight, AnimatorLayerBlendingMode blendingMode,
            AnimatorControllerPropModNode<T> node, int layerIndex)
        {
            Weight = weight;
            BlendingMode = blendingMode;
            LayerIndex = layerIndex;
            Node = node;
        }

        public PlayableLayerNodeInfo(AnimatorControllerPropModNode<T> node, int layerIndex)
        {
            Weight = AnimatorWeightState.AlwaysOne;
            BlendingMode = AnimatorLayerBlendingMode.Override;
            Node = node;
            LayerIndex = layerIndex;
        }
    }

    class AnimatorPropModNode<T> : ComponentPropModNode<ValueInfo<T>, Animator>
        where T : notnull
    {
        private readonly IEnumerable<PlayableLayerNodeInfo<T>> _layersReversed;

        public AnimatorPropModNode(Animator component,IEnumerable<PlayableLayerNodeInfo<T>> layersReversed)
            : base(component)
        {
            _layersReversed = layersReversed;

            _appliedAlways = new Lazy<bool>(
                () => default(ValueInfo<T>).AlwaysAppliedForOverriding(_layersReversed),
                isThreadSafe: false);

            _constantInfo = new Lazy<ValueInfo<T>>(
                () => default(ValueInfo<T>).ConstantInfoForOverriding(_layersReversed),
                isThreadSafe: false);
        }


        private readonly Lazy<bool> _appliedAlways;
        private readonly Lazy<ValueInfo<T>> _constantInfo;

        public IEnumerable<PlayableLayerNodeInfo<T>> LayersReversed => _layersReversed;
        public override bool AppliedAlways => _appliedAlways.Value;
        public override ValueInfo<T> Value => _constantInfo.Value;
        public override IEnumerable<ObjectReference> ContextReferences => base.ContextReferences.Concat(
            _layersReversed.SelectMany(x => x.Node.ContextReferences));
    }

    internal readonly struct AnimatorLayerNodeInfo<T> : ILayer<ValueInfo<T>>
        where T : notnull
    {
        public AnimatorWeightState Weight { get; }
        public AnimatorLayerBlendingMode BlendingMode { get; }
        public int LayerIndex { get; }
        public readonly AnimatorLayerPropModNode<T> Node;
        PropModNode<ValueInfo<T>> ILayer<ValueInfo<T>>.Node => Node;
        IPropModNode ILayer.Node => Node;

        public AnimatorLayerNodeInfo(AnimatorWeightState weight, AnimatorLayerBlendingMode blendingMode,
            AnimatorLayerPropModNode<T> node, int layerIndex)
        {
            Weight = weight;
            BlendingMode = blendingMode;
            LayerIndex = layerIndex;
            Node = node;
        }
    }

    class AnimatorControllerPropModNode<T> : PropModNode<ValueInfo<T>>
        where T : notnull
    {
        private readonly IEnumerable<AnimatorLayerNodeInfo<T>> _layersReversed;

        public static AnimatorControllerPropModNode<T>? Create(List<AnimatorLayerNodeInfo<T>> value)
        {
            if (value.Count == 0) return null;
            if (value.All(x => x.BlendingMode == AnimatorLayerBlendingMode.Additive && x.Node.Value.IsConstant))
                return null; // unchanged constant

            value.Reverse();
            return new AnimatorControllerPropModNode<T>(value);
        }

        private AnimatorControllerPropModNode(IEnumerable<AnimatorLayerNodeInfo<T>> layersReversed) =>
            _layersReversed = layersReversed;

        public IEnumerable<AnimatorLayerNodeInfo<T>> LayersReversed => _layersReversed;

        public override ValueInfo<T> Value => default(ValueInfo<T>).ConstantInfoForOverriding(_layersReversed);

        // we may possible to implement complex logic which simulates state machine but not for now.
        public override bool AppliedAlways => default(ValueInfo<T>).AlwaysAppliedForOverriding(_layersReversed);

        public override IEnumerable<ObjectReference> ContextReferences =>
            _layersReversed.SelectMany(x => x.Node.ContextReferences);
    }

    internal enum AnimatorWeightState
    {
        AlwaysOne,
        EitherZeroOrOne,
        Variable
    }

    internal class AnimatorLayerPropModNode<T> : ImmutablePropModNode<ValueInfo<T>>
        where T : notnull
    {
        private readonly IEnumerable<AnimatorStatePropModNode<ValueInfo<T>>> _children;
        private readonly bool _partial;

        public AnimatorLayerPropModNode(ICollection<AnimatorStatePropModNode<ValueInfo<T>>> children, bool partial)
        {
            // expected to pass list or array
            // ReSharper disable once PossibleMultipleEnumeration
            Debug.Assert(children.Any());
            // ReSharper disable once PossibleMultipleEnumeration
            _children = children;
            _partial = partial;
        }

        public override bool AppliedAlways => !_partial && _children.All(x => x.AppliedAlways);
        public override ValueInfo<T> Value => default(ValueInfo<T>).ConstantInfoForSideBySide(_children);
        public override IEnumerable<ObjectReference> ContextReferences => _children.SelectMany(x => x.ContextReferences);
        public IEnumerable<AnimatorStatePropModNode<ValueInfo<T>>> Children => _children;
    }

    internal class AnimatorStatePropModNode<TValueInfo> : ImmutablePropModNode<TValueInfo>
        where TValueInfo : struct, IValueInfo<TValueInfo>
    {
        private readonly ImmutablePropModNode<TValueInfo> _node;
        private readonly AnimatorState _state;

        public AnimatorStatePropModNode(ImmutablePropModNode<TValueInfo> node, AnimatorState state)
        {
            _node = node;
            _state = state;
        }

        public ImmutablePropModNode<TValueInfo> Node => _node;
        public AnimatorState State => _state;
        public override bool AppliedAlways => _node.AppliedAlways;
        public override TValueInfo Value => _node.Value;
        public override IEnumerable<ObjectReference> ContextReferences => _node.ContextReferences;
    }
}
