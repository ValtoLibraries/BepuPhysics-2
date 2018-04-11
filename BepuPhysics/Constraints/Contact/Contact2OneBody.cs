﻿using BepuPhysics.CollisionDetection;
using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Quaternion = BepuUtilities.Quaternion;
using static BepuUtilities.GatherScatter;
using BepuUtilities;

namespace BepuPhysics.Constraints.Contact
{
    public struct Contact2OneBody : IConvexOneBodyContactConstraintDescription<Contact2OneBody>
    {
        public ConstraintContactData Contact0;
        public ConstraintContactData Contact1;
        public float FrictionCoefficient;
        public Vector3 Normal;
        public SpringSettings SpringSettings;
        public float MaximumRecoveryVelocity;


        public void ApplyDescription(ref TypeBatch batch, int bundleIndex, int innerIndex)
        {
            Debug.Assert(batch.TypeId == ConstraintTypeId, "The type batch passed to the description must match the description's expected type.");
            ref var target = ref GetOffsetInstance(ref Buffer<Contact2OneBodyPrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            GetFirst(ref target.OffsetA0.X) = Contact0.OffsetA.X;
            GetFirst(ref target.OffsetA0.Y) = Contact0.OffsetA.Y;
            GetFirst(ref target.OffsetA0.Z) = Contact0.OffsetA.Z;
            GetFirst(ref target.OffsetA1.X) = Contact1.OffsetA.X;
            GetFirst(ref target.OffsetA1.Y) = Contact1.OffsetA.Y;
            GetFirst(ref target.OffsetA1.Z) = Contact1.OffsetA.Z;

            GetFirst(ref target.FrictionCoefficient) = FrictionCoefficient;

            GetFirst(ref target.Normal.X) = Normal.X;
            GetFirst(ref target.Normal.Y) = Normal.Y;
            GetFirst(ref target.Normal.Z) = Normal.Z;

            GetFirst(ref target.SpringSettings.AngularFrequency) = SpringSettings.AngularFrequency;
            GetFirst(ref target.SpringSettings.TwiceDampingRatio) = SpringSettings.TwiceDampingRatio;
            GetFirst(ref target.MaximumRecoveryVelocity) = MaximumRecoveryVelocity;

            GetFirst(ref target.PenetrationDepth0) = Contact0.PenetrationDepth;
            GetFirst(ref target.PenetrationDepth1) = Contact1.PenetrationDepth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BuildDescription(ref TypeBatch batch, int bundleIndex, int innerIndex, out Contact2OneBody description)
        {
            Debug.Assert(batch.TypeId == ConstraintTypeId, "The type batch passed to the description must match the description's expected type.");
            ref var source = ref GetOffsetInstance(ref Buffer<Contact2OneBodyPrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            description.Contact0.OffsetA.X = GetFirst(ref source.OffsetA0.X);
            description.Contact0.OffsetA.Y = GetFirst(ref source.OffsetA0.Y);
            description.Contact0.OffsetA.Z = GetFirst(ref source.OffsetA0.Z);
            description.Contact1.OffsetA.X = GetFirst(ref source.OffsetA1.X);
            description.Contact1.OffsetA.Y = GetFirst(ref source.OffsetA1.Y);
            description.Contact1.OffsetA.Z = GetFirst(ref source.OffsetA1.Z);

            description.FrictionCoefficient = GetFirst(ref source.FrictionCoefficient);

            description.Normal.X = GetFirst(ref source.Normal.X);
            description.Normal.Y = GetFirst(ref source.Normal.Y);
            description.Normal.Z = GetFirst(ref source.Normal.Z);

            description.SpringSettings.AngularFrequency = GetFirst(ref source.SpringSettings.AngularFrequency);
            description.SpringSettings.TwiceDampingRatio = GetFirst(ref source.SpringSettings.TwiceDampingRatio);
            description.MaximumRecoveryVelocity = GetFirst(ref source.MaximumRecoveryVelocity);

            description.Contact0.PenetrationDepth = GetFirst(ref source.PenetrationDepth0);
            description.Contact1.PenetrationDepth = GetFirst(ref source.PenetrationDepth1);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyManifoldWideProperties(ref Vector3 normal, ref PairMaterialProperties material)
        {
            FrictionCoefficient = material.FrictionCoefficient;
            Normal = normal;
            SpringSettings = material.SpringSettings;
            MaximumRecoveryVelocity = material.MaximumRecoveryVelocity;
        }

        public int ConstraintTypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Contact2OneBodyTypeProcessor.BatchTypeId;
        }

        public Type BatchType => typeof(Contact2OneBodyTypeProcessor);
    }
    public struct Contact2OneBodyPrestepData
    {
        //NOTE: Prestep data memory layout is relied upon by the constraint description for marginally more efficient setting and getting.
        //If you modify this layout, be sure to update the associated ContactManifold4Constraint.
        //Note that this layout is defined by the execution order in the prestep. The function accesses it sequentially to ensure the prefetcher can do its job.
        public Vector3Wide OffsetA0;
        public Vector3Wide OffsetA1;
        public Vector<float> FrictionCoefficient;
        //In a convex manifold, all contacts share the same normal and tangents.
        public Vector3Wide Normal;
        //All contacts also share the spring settings.
        public SpringSettingsWide SpringSettings;
        public Vector<float> MaximumRecoveryVelocity;
        public Vector<float> PenetrationDepth0;
        public Vector<float> PenetrationDepth1;
    }

    //The key observation here is that we have 7DOFs worth of constraints that all share the exact same bodies.
    //Despite the potential premultiplication optimizations, we focus on a few big wins:
    //1) Sharing the inverse mass for the impulse->velocity projection across all constraints.
    //2) Sharing the normal as much as possible.
    //3) Resorting to iteration-side redundant calculation if it reduces memory bandwidth.
    //This is expected to slow down the single threaded performance when running on a 128 bit SIMD machine.
    //However, when using multiple threads, memory bandwidth very rapidly becomes a concern.
    //In fact, a hypothetical CLR and machine that supported AVX512 would hit memory bandwidth limits on the older implementation that used 2032 bytes per bundle for projection data...
    //on a single thread.

    public struct Contact2OneBodyProjection
    {
        public BodyInertias InertiaA;
        public Vector<float> PremultipliedFrictionCoefficient;
        public Vector3Wide Normal;
        public TangentFrictionOneBody.Projection Tangent;
        public PenetrationLimit2OneBody.Projection Penetration;
        //Lever arms aren't included in the twist projection because the number of arms required varies independently of the twist projection itself.
        public Vector<float> LeverArm0;
        public Vector<float> LeverArm1;
        public TwistFrictionProjection Twist;
    }

    //TODO: at the time of writing (May 19 2017 2.0.0-preview2-25309-07), using the 'loop body structdelegate' style introduces additional inits and overhead 
    //relative to a manually inlined version. That isn't fundamental. With any luck, future compilers will change things. 
    //Since the difference is less than 5%, we'll use the loopbodystructdelegate approach for other constraints until the incremental performance improvement 
    //of manual inlining is worth it.
    public struct Contact2OneBodyFunctions :
        IOneBodyConstraintFunctions<Contact2OneBodyPrestepData, Contact2OneBodyProjection, Contact2AccumulatedImpulses>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prestep(Bodies bodies, ref Vector<int> bodyReferences, int count,
            float dt, float inverseDt, ref Contact2OneBodyPrestepData prestep, out Contact2OneBodyProjection projection)
        {
            //Some speculative compression options not (yet) pursued:
            //1) Store the surface basis in a compressed fashion. It could be stored within 32 bits by using standard compression schemes, but we lack the necessary
            //instructions to properly SIMDify the decode operation (e.g. shift). Even with the potential savings of 3 floats (relative to uncompressed storage), it would be questionable.
            //We could drop one of the four components of the quaternion and reconstruct it relatively easily- that would just require that the encoder ensures the W component is positive.
            //It would require a square root, but it might still be a net win. On an IVB, sqrt has a 7 cycle throughput. 4 bytes saved * 4 lanes = 16 bytes, which takes 
            //about 16 / 5.5GBps = 2.9ns, where 5.5 is roughly the per-core bandwidth on a 3770K. 7 cycles is only 2ns at 3.5ghz. 
            //There are a couple of other instructions necessary to decode, but sqrt is by far the heaviest; it's likely a net win.
            //Be careful about the execution order here. It should be aligned with the prestep data layout to ensure prefetching works well.

            bodies.GatherInertia(ref bodyReferences, count, out projection.InertiaA);
            Vector3Wide.Add(ref prestep.OffsetA0, ref prestep.OffsetA1, out var offsetToManifoldCenterA);
            var scale = new Vector<float>(0.5f);
            Vector3Wide.Scale(ref offsetToManifoldCenterA, ref scale, out offsetToManifoldCenterA);
            projection.PremultipliedFrictionCoefficient = scale * prestep.FrictionCoefficient;
            projection.Normal = prestep.Normal;
            Helpers.BuildOrthnormalBasis(ref prestep.Normal, out var x, out var z);
            TangentFrictionOneBody.Prestep(ref x, ref z, ref offsetToManifoldCenterA, ref projection.InertiaA, out projection.Tangent);
            PenetrationLimit2OneBody.Prestep(ref projection.InertiaA, ref prestep.Normal, ref prestep, dt, inverseDt, out projection.Penetration);
            //Just assume the lever arms for B are the same. It's a good guess. (The only reason we computed the offset B is because we didn't want to go into world space.)
            Vector3Wide.Distance(ref prestep.OffsetA0, ref offsetToManifoldCenterA, out projection.LeverArm0);
            Vector3Wide.Distance(ref prestep.OffsetA1, ref offsetToManifoldCenterA, out projection.LeverArm1);
            TwistFrictionOneBody.Prestep(ref projection.InertiaA, ref prestep.Normal, out projection.Twist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WarmStart(ref BodyVelocities wsvA, ref Contact2OneBodyProjection projection, ref Contact2AccumulatedImpulses accumulatedImpulses)
        {
            Helpers.BuildOrthnormalBasis(ref projection.Normal, out var x, out var z);
            TangentFrictionOneBody.WarmStart(ref x, ref z, ref projection.Tangent, ref projection.InertiaA, ref accumulatedImpulses.Tangent, ref wsvA);
            PenetrationLimit2OneBody.WarmStart(ref projection.Penetration, ref projection.InertiaA,
                ref projection.Normal,
                ref accumulatedImpulses.Penetration0,
                ref accumulatedImpulses.Penetration1, ref wsvA);
            TwistFrictionOneBody.WarmStart(ref projection.Normal, ref projection.InertiaA, ref accumulatedImpulses.Twist, ref wsvA);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Solve(ref BodyVelocities wsvA, ref Contact2OneBodyProjection projection, ref Contact2AccumulatedImpulses accumulatedImpulses)
        {
            Helpers.BuildOrthnormalBasis(ref projection.Normal, out var x, out var z);
            var maximumTangentImpulse = projection.PremultipliedFrictionCoefficient *
                (accumulatedImpulses.Penetration0 + accumulatedImpulses.Penetration1);
            TangentFrictionOneBody.Solve(ref x, ref z, ref projection.Tangent, ref projection.InertiaA, ref maximumTangentImpulse, ref accumulatedImpulses.Tangent, ref wsvA);
            //Note that we solve the penetration constraints after the friction constraints. 
            //This makes the penetration constraints more authoritative at the cost of the first iteration of the first frame of an impact lacking friction influence.
            //It's a pretty minor effect either way.
            PenetrationLimit2OneBody.Solve(ref projection.Penetration, ref projection.InertiaA, ref projection.Normal,
                ref accumulatedImpulses.Penetration0,
                ref accumulatedImpulses.Penetration1, ref wsvA);
            var maximumTwistImpulse = projection.PremultipliedFrictionCoefficient * (
                accumulatedImpulses.Penetration0 * projection.LeverArm0 +
                accumulatedImpulses.Penetration1 * projection.LeverArm1);
            TwistFrictionOneBody.Solve(ref projection.Normal, ref projection.InertiaA, ref projection.Twist, ref maximumTwistImpulse, ref accumulatedImpulses.Twist, ref wsvA);
        }

    }

    /// <summary>
    /// Handles the solve iterations of a bunch of 2-contact convex manifold constraints.
    /// </summary>
    public class Contact2OneBodyTypeProcessor :
        OneBodyTypeProcessor<Contact2OneBodyPrestepData, Contact2OneBodyProjection, Contact2AccumulatedImpulses, Contact2OneBodyFunctions>
    {
        public const int BatchTypeId = 1;
    }
}
