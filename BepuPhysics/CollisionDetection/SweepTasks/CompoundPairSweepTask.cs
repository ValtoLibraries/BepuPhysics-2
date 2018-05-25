﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using BepuPhysics.Collidables;
using BepuUtilities;
using Quaternion = BepuUtilities.Quaternion;

namespace BepuPhysics.CollisionDetection.SweepTasks
{
    public class CompoundPairSweepTask : SweepTask
    {
        public CompoundPairSweepTask()
        {
            ShapeTypeIndexA = default(Compound).TypeId;
            ShapeTypeIndexB = default(Compound).TypeId;
        }

        public override unsafe bool Sweep(
            void* shapeDataA, int shapeTypeA, in RigidPose localPoseA, in Quaternion orientationA, in BodyVelocity velocityA,
            void* shapeDataB, int shapeTypeB, in RigidPose localPoseB, in Vector3 offsetB, in Quaternion orientationB, in BodyVelocity velocityB, float maximumT,
            float minimumProgression, float convergenceThreshold, int maximumIterationCount,
            out float t0, out float t1, out Vector3 hitLocation, out Vector3 hitNormal)
        {
            throw new NotImplementedException("Compounds cannot be nested; this should never be called.");
        }

        public override unsafe bool Sweep<TSweepFilter>(
            void* shapeDataA, int shapeTypeA, in Quaternion orientationA, in BodyVelocity velocityA,
            void* shapeDataB, int shapeTypeB, in Vector3 offsetB, in Quaternion orientationB, in BodyVelocity velocityB, float maximumT,
            float minimumProgression, float convergenceThreshold, int maximumIterationCount,
            ref TSweepFilter filter, Shapes shapes, SweepTaskRegistry sweepTasks, out float t0, out float t1, out Vector3 hitLocation, out Vector3 hitNormal)
        {
            Debug.Assert((shapeTypeA == ShapeTypeIndexA && shapeTypeB == ShapeTypeIndexB),
                "Types must match expected types.");
            ref var a = ref Unsafe.AsRef<Compound>(shapeDataA);
            ref var b = ref Unsafe.AsRef<Compound>(shapeDataB);
            t0 = float.MaxValue;
            t1 = float.MaxValue;
            hitLocation = new Vector3();
            hitNormal = new Vector3();
            for (int i = 0; i < a.Children.Length; ++i)
            {
                ref var childA = ref a.Children[i];
                var childTypeA = childA.ShapeIndex.Type;
                shapes[childTypeA].GetShapeData(childA.ShapeIndex.Index, out var childShapeDataA, out _);
                for (int j = 0; j < b.Children.Length; ++j)
                {
                    if (filter.AllowTest(i, j))
                    {
                        ref var childB = ref b.Children[i];
                        var childTypeB = childA.ShapeIndex.Type;
                        shapes[childTypeB].GetShapeData(childB.ShapeIndex.Index, out var childShapeDataB, out _);
                        var task = sweepTasks.GetTask(childTypeA, childTypeB);
                        if (task != null && task.Sweep(
                            childShapeDataA, childTypeA, childA.LocalPose, orientationA, velocityA,
                            childShapeDataB, childTypeB, childB.LocalPose, offsetB, orientationB, velocityB,
                            maximumT, minimumProgression, convergenceThreshold, maximumIterationCount,
                            out var t0Candidate, out var t1Candidate, out var hitLocationCandidate, out var hitNormalCandidate))
                        {
                            //Note that we use t1 to determine whether to accept the new location. In other words, we're choosing to keep sweeps that have the earliest time of intersection.
                            //(t0 is *not* intersecting for any initially separated pair.)
                            if (t1Candidate < t1)
                            {
                                t0 = t0Candidate;
                                t1 = t1Candidate;
                                hitLocation = hitLocationCandidate;
                                hitNormal = hitNormalCandidate;
                            }
                        }
                    }
                }
            }
            return t1 < float.MaxValue;
        }
    }
}
