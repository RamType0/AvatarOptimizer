using System;
using System.Collections.Generic;
using Anatawa12.AvatarOptimizer.APIInternal;
using Anatawa12.AvatarOptimizer.Processors.SkinnedMeshes;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace Anatawa12.AvatarOptimizer.Processors.TraceAndOptimizes
{
    /// <summary>
    /// This class collects ALL dependencies of each component
    /// </summary>
    readonly struct ComponentDependencyCollector
    {
        private readonly bool _preserveEndBone;
        private readonly BuildContext _session;
        private readonly GCComponentInfoHolder _componentInfos;

        public ComponentDependencyCollector(BuildContext session, bool preserveEndBone,
            GCComponentInfoHolder componentInfos)
        {
            _preserveEndBone = preserveEndBone;
            _session = session;
            _componentInfos = componentInfos;
        }


        public void CollectAllUsages()
        {
            var collector = new Collector(this, _componentInfos);
            var unknownComponents = new Dictionary<Type, List<Object>>();
            // second iteration: process parsers
            foreach (var componentInfo in _componentInfos.AllInformation)
            {
                var component = componentInfo.Component;
                using (ErrorReport.WithContextObject(component))
                {
                    // component requires GameObject.
                    collector.Init(componentInfo);
                    if (ComponentInfoRegistry.TryGetInformation(component.GetType(), out var information))
                    {
                        information.CollectDependencyInternal(component, collector);
                    }
                    else
                    {
                        if (!unknownComponents.TryGetValue(component.GetType(), out var list))
                            unknownComponents.Add(component.GetType(), list = new List<Object>());
                        list.Add(component);

                        FallbackDependenciesParser(component, collector);
                    }

                    foreach (var requiredComponent in RequireComponentCache.GetRequiredComponents(component.GetType()))
                    foreach (var required in component.GetComponents(requiredComponent))
                            collector.AddDependency(component, required)
                                .EvenIfDependantDisabled();

                    collector.FinalizeForComponent();
                }
            }
            foreach (var (type, objects) in unknownComponents)
                BuildLog.LogWarning("TraceAndOptimize:warn:unknown-type", type, objects);
        }

        private static void FallbackDependenciesParser(Component component, API.ComponentDependencyCollector collector)
        {
            // fallback dependencies: All References are Always Dependencies.
            collector.MarkEntrypoint();
            using (var serialized = new SerializedObject(component))
            {
                foreach (var property in serialized.ObjectReferenceProperties())
                {
                    if (property.objectReferenceValue is GameObject go)
                        collector.AddDependency(go.transform).EvenIfDependantDisabled();
                    else if (property.objectReferenceValue is Component com)
                        collector.AddDependency(com).EvenIfDependantDisabled();
                }
            }
        }

        internal class Collector : API.ComponentDependencyCollector
        {
            private readonly ComponentDependencyCollector _collector;
            private GCComponentInfo _info;
            [NotNull] private readonly ComponentDependencyInfo _dependencyInfoSharedInstance;

            public Collector(ComponentDependencyCollector collector, GCComponentInfoHolder componentInfos)
            {
                _collector = collector;
                _dependencyInfoSharedInstance = new ComponentDependencyInfo(collector, componentInfos);
            }
            
            public void Init(GCComponentInfo info)
            {
                Debug.Assert(_info == null, "Init on not finished");
                _info = info;
            }

            public bool PreserveEndBone => _collector._preserveEndBone;

            public MeshInfo2 GetMeshInfoFor(SkinnedMeshRenderer renderer) =>
                _collector._session.GetMeshInfoFor(renderer);

            public override void MarkEntrypoint() => _info.EntrypointComponent = true;

            public override void MarkHeavyBehaviour() => _info.HeavyBehaviourComponent = true;
            public override void MarkBehaviour()
            {
                // currently NOP
            }

            private API.ComponentDependencyInfo AddDependencyInternal(
                [CanBeNull] GCComponentInfo info,
                [CanBeNull] Component dependency,
                GCComponentInfo.DependencyType type = GCComponentInfo.DependencyType.Normal)
            {
                _dependencyInfoSharedInstance.Finish();
                _dependencyInfoSharedInstance.Init(info, dependency, type);
                return _dependencyInfoSharedInstance;
            }

            public override API.ComponentDependencyInfo AddDependency(Component dependant, Component dependency) =>
                AddDependencyInternal(_collector._componentInfos.TryGetInfo(dependant), dependency);

            public override API.ComponentDependencyInfo AddDependency(Component dependency) =>
                AddDependencyInternal(_info, dependency);

            internal override bool? GetAnimatedFlag(Component component, string animationProperty, bool currentValue) =>
                _collector._session.GetConstantValue(component, animationProperty, currentValue);

            public void AddParentDependency(Transform component) =>
                AddDependencyInternal(_info, component.parent, GCComponentInfo.DependencyType.Parent)
                    .EvenIfDependantDisabled();

            public void AddBoneDependency(Transform bone) =>
                AddDependencyInternal(_info, bone, GCComponentInfo.DependencyType.Bone);

            public void FinalizeForComponent()
            {
                _dependencyInfoSharedInstance.Finish();
                _info = null;
            }

            private class ComponentDependencyInfo : API.ComponentDependencyInfo
            {
                private readonly ComponentDependencyCollector _collector;
                private readonly GCComponentInfoHolder _componentInfos;

                [CanBeNull] private Component _dependency;
                [CanBeNull] private GCComponentInfo _dependantInformation;
                private GCComponentInfo.DependencyType _type;

                private bool _evenIfTargetIsDisabled;
                private bool _evenIfThisIsDisabled;

                // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
                public ComponentDependencyInfo(ComponentDependencyCollector collector,
                    GCComponentInfoHolder componentInfos)
                {
                    _collector = collector;
                    _componentInfos = componentInfos;
                }

                internal void Init(
                    [CanBeNull] GCComponentInfo dependantInformation,
                    [CanBeNull] Component component,
                    GCComponentInfo.DependencyType type = GCComponentInfo.DependencyType.Normal)
                {
                    Debug.Assert(_dependency == null, "Init on not finished");
                    _dependency = component;
                    _dependantInformation = dependantInformation;
                    _evenIfTargetIsDisabled = true;
                    _evenIfThisIsDisabled = false;
                    _type = type;
                }

                internal void Finish()
                {
                    if (_dependency == null) return;
                    SetToDictionary();
                    _dependency = null;
                }

                private void SetToDictionary()
                {
                    Debug.Assert(_dependency != null, nameof(_dependency) + " != null");

                    if (_dependantInformation == null) return;
                    if (!_dependency.transform.IsChildOf(_collector._session.AvatarRootTransform))
                        return;
                    if (!_evenIfThisIsDisabled)
                    {
                        // dependant must can be able to be enable
                        if (_dependantInformation.Activeness == false) return;
                    }
                    
                    if (!_evenIfTargetIsDisabled)
                    {
                        // dependency must can be able to be enable
                        if (_componentInfos.GetInfo(_dependency).Activeness == false) return;
                    }

                    _dependantInformation.Dependencies.TryGetValue(_dependency, out var type);
                    _dependantInformation.Dependencies[_dependency] = type | _type;
                }

                public override API.ComponentDependencyInfo EvenIfDependantDisabled()
                {
                    _evenIfThisIsDisabled = true;
                    return this;
                }

                public override API.ComponentDependencyInfo OnlyIfTargetCanBeEnable()
                {
                    _evenIfTargetIsDisabled = false;
                    return this;
                }
            }
        }
    }
}

