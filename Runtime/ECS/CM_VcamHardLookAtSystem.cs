using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using System;

namespace Cinemachine.ECS
{
    [Serializable]
    public struct CM_VcamHardLookAt : IComponentData
    {
    }

    [UpdateAfter(typeof(CM_VcamAimSystem))]
    [UpdateBefore(typeof(CM_VcamCorrectionSystem))]
    public class CM_VcamHardLookAtSystem : JobComponentSystem
    {
        ComponentGroup m_mainGroup;

        protected override void OnCreateManager()
        {
            m_mainGroup = GetComponentGroup(
                ComponentType.Create<CM_VcamRotation>(), 
                ComponentType.ReadOnly<CM_VcamPosition>(), 
                ComponentType.ReadOnly<CM_VcamHardLookAt>(),
                ComponentType.ReadOnly<CM_VcamLookAtTarget>());
        }

        [BurstCompile]
        struct LookAtTargetJob : IJobParallelFor
        {
            public ComponentDataArray<CM_VcamRotation> rotations;
            [ReadOnly] public ComponentDataArray<CM_VcamPosition> positions;
            [ReadOnly] public ComponentDataArray<CM_VcamLookAtTarget> targets;
            [ReadOnly] public NativeHashMap<Entity, CM_TargetSystem.TargetInfo> targetLookup;

            public void Execute(int index)
            {
                CM_TargetSystem.TargetInfo targetInfo;
                if (targetLookup.TryGetValue(targets[index].target, out targetInfo))
                {
                    var q = math.normalizesafe(rotations[index].raw, quaternion.identity);
                    float3 dir = math.normalizesafe(targetInfo.position - positions[index].raw, math.forward(q));
                    float3 up = math.normalizesafe(positions[index].up, math.up());
                    q = q.LookRotationUnit(dir, up);
                    rotations[index] = new CM_VcamRotation
                    {
                        lookAtPoint = targetInfo.position,
                        raw = q
                    };
                }
            }
        }
        
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var targetSystem = World.GetExistingManager<CM_TargetSystem>();
            var targetLookup = targetSystem.GetTargetLookupForJobs(ref inputDeps);
            if (!targetLookup.IsCreated)
                return default; // no targets yet

            var job = new LookAtTargetJob()
            {
                rotations = m_mainGroup.GetComponentDataArray<CM_VcamRotation>(),
                positions = m_mainGroup.GetComponentDataArray<CM_VcamPosition>(),
                targets = m_mainGroup.GetComponentDataArray<CM_VcamLookAtTarget>(),
                targetLookup = targetLookup
            };
            return targetSystem.RegisterTargetLookupReadJobs(
                job.Schedule(m_mainGroup.CalculateLength(), 32, inputDeps));
        }
    }
}