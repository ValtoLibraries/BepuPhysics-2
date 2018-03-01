﻿using BepuPhysics.CollisionDetection;
using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Quaternion = BepuUtilities.Quaternion;
using static BepuPhysics.GatherScatter;
namespace BepuPhysics.Constraints.Contact
{

    public struct ManifoldContactData
    {
        //TODO: Arguably storing this to match the prestep layout would be a better idea for contiguity. Consider it later.
        public Vector3 OffsetA;
        public float PenetrationDepth;
    }


    public struct Contact4 : IConstraintDescription<Contact4>
    {
        public ManifoldContactData Contact0;
        public ManifoldContactData Contact1;
        public ManifoldContactData Contact2;
        public ManifoldContactData Contact3;
        public Vector3 OffsetB;
        public float FrictionCoefficient;
        public Vector3 Normal;
        public SpringSettings SpringSettings;
        public float MaximumRecoveryVelocity;

        public void ApplyDescription(ref TypeBatch batch, int bundleIndex, int innerIndex)
        {
            Debug.Assert(batch.TypeId == ConstraintTypeId, "The type batch passed to the description must match the description's expected type.");
            ref var target = ref GetOffsetInstance(ref Buffer<Contact4PrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            GetFirst(ref target.OffsetA0.X) = Contact0.OffsetA.X;
            GetFirst(ref target.OffsetA0.Y) = Contact0.OffsetA.Y;
            GetFirst(ref target.OffsetA0.Z) = Contact0.OffsetA.Z;
            GetFirst(ref target.OffsetA1.X) = Contact1.OffsetA.X;
            GetFirst(ref target.OffsetA1.Y) = Contact1.OffsetA.Y;
            GetFirst(ref target.OffsetA1.Z) = Contact1.OffsetA.Z;
            GetFirst(ref target.OffsetA2.X) = Contact2.OffsetA.X;
            GetFirst(ref target.OffsetA2.Y) = Contact2.OffsetA.Y;
            GetFirst(ref target.OffsetA2.Z) = Contact2.OffsetA.Z;
            GetFirst(ref target.OffsetA3.X) = Contact3.OffsetA.X;
            GetFirst(ref target.OffsetA3.Y) = Contact3.OffsetA.Y;
            GetFirst(ref target.OffsetA3.Z) = Contact3.OffsetA.Z;

            GetFirst(ref target.OffsetB.X) = OffsetB.X;
            GetFirst(ref target.OffsetB.Y) = OffsetB.Y;
            GetFirst(ref target.OffsetB.Z) = OffsetB.Z;

            GetFirst(ref target.FrictionCoefficient) = FrictionCoefficient;

            GetFirst(ref target.Normal.X) = Normal.X;
            GetFirst(ref target.Normal.Y) = Normal.Y;
            GetFirst(ref target.Normal.Z) = Normal.Z;

            GetFirst(ref target.SpringSettings.NaturalFrequency) = SpringSettings.NaturalFrequency;
            GetFirst(ref target.SpringSettings.DampingRatio) = SpringSettings.DampingRatio;
            GetFirst(ref target.MaximumRecoveryVelocity) = MaximumRecoveryVelocity;

            GetFirst(ref target.PenetrationDepth0) = Contact0.PenetrationDepth;
            GetFirst(ref target.PenetrationDepth1) = Contact1.PenetrationDepth;
            GetFirst(ref target.PenetrationDepth2) = Contact2.PenetrationDepth;
            GetFirst(ref target.PenetrationDepth3) = Contact3.PenetrationDepth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BuildDescription(ref TypeBatch batch, int bundleIndex, int innerIndex, out Contact4 description)
        {
            Debug.Assert(batch.TypeId == ConstraintTypeId, "The type batch passed to the description must match the description's expected type.");
            ref var source = ref GetOffsetInstance(ref Buffer<Contact4PrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            description.Contact0.OffsetA.X = GetFirst(ref source.OffsetA0.X);
            description.Contact0.OffsetA.Y = GetFirst(ref source.OffsetA0.Y);
            description.Contact0.OffsetA.Z = GetFirst(ref source.OffsetA0.Z);
            description.Contact1.OffsetA.X = GetFirst(ref source.OffsetA1.X);
            description.Contact1.OffsetA.Y = GetFirst(ref source.OffsetA1.Y);
            description.Contact1.OffsetA.Z = GetFirst(ref source.OffsetA1.Z);
            description.Contact2.OffsetA.X = GetFirst(ref source.OffsetA2.X);
            description.Contact2.OffsetA.Y = GetFirst(ref source.OffsetA2.Y);
            description.Contact2.OffsetA.Z = GetFirst(ref source.OffsetA2.Z);
            description.Contact3.OffsetA.X = GetFirst(ref source.OffsetA3.X);
            description.Contact3.OffsetA.Y = GetFirst(ref source.OffsetA3.Y);
            description.Contact3.OffsetA.Z = GetFirst(ref source.OffsetA3.Z);

            description.OffsetB.X = GetFirst(ref source.OffsetB.X);
            description.OffsetB.Y = GetFirst(ref source.OffsetB.Y);
            description.OffsetB.Z = GetFirst(ref source.OffsetB.Z);

            description.FrictionCoefficient = GetFirst(ref source.FrictionCoefficient);

            description.Normal.X = GetFirst(ref source.Normal.X);
            description.Normal.Y = GetFirst(ref source.Normal.Y);
            description.Normal.Z = GetFirst(ref source.Normal.Z);

            description.SpringSettings.NaturalFrequency = GetFirst(ref source.SpringSettings.NaturalFrequency);
            description.SpringSettings.DampingRatio = GetFirst(ref source.SpringSettings.DampingRatio);
            description.MaximumRecoveryVelocity = GetFirst(ref source.MaximumRecoveryVelocity);

            description.Contact0.PenetrationDepth = GetFirst(ref source.PenetrationDepth0);
            description.Contact1.PenetrationDepth = GetFirst(ref source.PenetrationDepth1);
            description.Contact2.PenetrationDepth = GetFirst(ref source.PenetrationDepth2);
            description.Contact3.PenetrationDepth = GetFirst(ref source.PenetrationDepth3);

        }

        public int ConstraintTypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Contact4TypeProcessor.BatchTypeId;
        }

        public Type BatchType => typeof(Contact4TypeProcessor);
    }

    public struct Contact4PrestepData
    {
        //NOTE: Prestep data memory layout is relied upon by the constraint description for marginally more efficient setting and getting.
        //If you modify this layout, be sure to update the associated ContactManifold4Constraint.
        //Note that this layout is defined by the execution order in the prestep. The function accesses it sequentially to ensure the prefetcher can do its job.
        public Vector3Wide OffsetA0;
        public Vector3Wide OffsetA1;
        public Vector3Wide OffsetA2;
        public Vector3Wide OffsetA3;
        public Vector3Wide OffsetB;
        public Vector<float> FrictionCoefficient;
        //In a convex manifold, all contacts share the same normal and tangents.
        public Vector3Wide Normal;
        //All contacts also share the spring settings.
        public SpringSettingsWide SpringSettings;
        public Vector<float> MaximumRecoveryVelocity;
        public Vector<float> PenetrationDepth0;
        public Vector<float> PenetrationDepth1;
        public Vector<float> PenetrationDepth2;
        public Vector<float> PenetrationDepth3;
    }

    public struct Contact4AccumulatedImpulses
    {
        public Vector2Wide Tangent;
        public Vector<float> Penetration0;
        public Vector<float> Penetration1;
        public Vector<float> Penetration2;
        public Vector<float> Penetration3;
        public Vector<float> Twist;
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

    public struct Contact4Projection
    {
        public BodyInertias InertiaA;
        public BodyInertias InertiaB;
        public Vector<float> PremultipliedFrictionCoefficient;
        public Vector3Wide Normal;
        public TangentFriction.Projection Tangent;
        public PenetrationLimit4.Projection Penetration;
        //Lever arms aren't included in the twist projection because the number of arms required varies independently of the twist projection itself.
        public Vector<float> LeverArm0;
        public Vector<float> LeverArm1;
        public Vector<float> LeverArm2;
        public Vector<float> LeverArm3;
        public TwistFrictionProjection Twist;
    }

    //TODO: at the time of writing (May 19 2017 2.0.0-preview2-25309-07), using the 'loop body structdelegate' style introduces additional inits and overhead 
    //relative to a manually inlined version. That isn't fundamental. With any luck, future compilers will change things. 
    //Since the difference is less than 5%, we'll use the loopbodystructdelegate approach for other constraints until the incremental performance improvement 
    //of manual inlining is worth it.
    public struct Contact4Functions :
        IConstraintFunctions<Contact4PrestepData, Contact4Projection, Contact4AccumulatedImpulses>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prestep(Bodies bodies, ref TwoBodyReferences bodyReferences, int count,
            float dt, float inverseDt, ref Contact4PrestepData prestep, out Contact4Projection projection)
        {
            //Some speculative compression options not (yet) pursued:
            //1) Store the surface basis in a compressed fashion. It could be stored within 32 bits by using standard compression schemes, but we lack the necessary
            //instructions to properly SIMDify the decode operation (e.g. shift). Even with the potential savings of 3 floats (relative to uncompressed storage), it would be questionable.
            //We could drop one of the four components of the quaternion and reconstruct it relatively easily- that would just require that the encoder ensures the W component is positive.
            //It would require a square root, but it might still be a net win. On an IVB, sqrt has a 7 cycle throughput. 4 bytes saved * 4 lanes = 16 bytes, which takes 
            //about 16 / 5.5GBps = 2.9ns, where 5.5 is roughly the per-core bandwidth on a 3770K. 7 cycles is only 2ns at 3.5ghz. 
            //There are a couple of other instructions necessary to decode, but sqrt is by far the heaviest; it's likely a net win.
            //Be careful about the execution order here. It should be aligned with the prestep data layout to ensure prefetching works well.

            bodies.GatherInertia(ref bodyReferences, count, out projection.InertiaA, out projection.InertiaB);
            Vector3Wide.Add(ref prestep.OffsetA0, ref prestep.OffsetA1, out var a01);
            Vector3Wide.Add(ref prestep.OffsetA2, ref prestep.OffsetA3, out var a23);
            Vector3Wide.Add(ref a01, ref a23, out var offsetToManifoldCenterA);
            var scale = new Vector<float>(0.25f);
            Vector3Wide.Scale(ref offsetToManifoldCenterA, ref scale, out offsetToManifoldCenterA);
            Vector3Wide.Subtract(ref offsetToManifoldCenterA, ref prestep.OffsetB, out var offsetToManifoldCenterB);
            projection.PremultipliedFrictionCoefficient = scale * prestep.FrictionCoefficient;
            projection.Normal = prestep.Normal;
            Helpers.BuildOrthnormalBasis(ref prestep.Normal, out var x, out var z);
            TangentFriction.Prestep(ref x, ref z, ref offsetToManifoldCenterA, ref offsetToManifoldCenterB, ref projection.InertiaA, ref projection.InertiaB, out projection.Tangent);
            PenetrationLimit4.Prestep(ref projection.InertiaA, ref projection.InertiaB, ref prestep.Normal, ref prestep, dt, inverseDt, out projection.Penetration);
            //Just assume the lever arms for B are the same. It's a good guess. (The only reason we computed the offset B is because we didn't want to go into world space.)
            Vector3Wide.Distance(ref prestep.OffsetA0, ref offsetToManifoldCenterA, out projection.LeverArm0);
            Vector3Wide.Distance(ref prestep.OffsetA1, ref offsetToManifoldCenterA, out projection.LeverArm1);
            Vector3Wide.Distance(ref prestep.OffsetA2, ref offsetToManifoldCenterA, out projection.LeverArm2);
            Vector3Wide.Distance(ref prestep.OffsetA3, ref offsetToManifoldCenterA, out projection.LeverArm3);
            TwistFriction.Prestep(ref projection.InertiaA, ref projection.InertiaB, ref prestep.Normal, out projection.Twist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WarmStart(ref BodyVelocities wsvA, ref BodyVelocities wsvB, ref Contact4Projection projection, ref Contact4AccumulatedImpulses accumulatedImpulses)
        {
            Helpers.BuildOrthnormalBasis(ref projection.Normal, out var x, out var z);
            TangentFriction.WarmStart(ref x, ref z, ref projection.Tangent, ref projection.InertiaA, ref projection.InertiaB, ref accumulatedImpulses.Tangent, ref wsvA, ref wsvB);
            PenetrationLimit4.WarmStart(ref projection.Penetration, ref projection.InertiaA, ref projection.InertiaB,
                ref projection.Normal,
                ref accumulatedImpulses.Penetration0,
                ref accumulatedImpulses.Penetration1,
                ref accumulatedImpulses.Penetration2,
                ref accumulatedImpulses.Penetration3, ref wsvA, ref wsvB);
            TwistFriction.WarmStart(ref projection.Normal, ref projection.InertiaA, ref projection.InertiaB, ref accumulatedImpulses.Twist, ref wsvA, ref wsvB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Solve(ref BodyVelocities wsvA, ref BodyVelocities wsvB, ref Contact4Projection projection, ref Contact4AccumulatedImpulses accumulatedImpulses)
        {
            Helpers.BuildOrthnormalBasis(ref projection.Normal, out var x, out var z);
            var maximumTangentImpulse = projection.PremultipliedFrictionCoefficient *
                (accumulatedImpulses.Penetration0 + accumulatedImpulses.Penetration1 + accumulatedImpulses.Penetration2 + accumulatedImpulses.Penetration3);
            TangentFriction.Solve(ref x, ref z, ref projection.Tangent, ref projection.InertiaA, ref projection.InertiaB, ref maximumTangentImpulse, ref accumulatedImpulses.Tangent, ref wsvA, ref wsvB);
            //Note that we solve the penetration constraints after the friction constraints. 
            //This makes the penetration constraints more authoritative at the cost of the first iteration of the first frame of an impact lacking friction influence.
            //It's a pretty minor effect either way.
            PenetrationLimit4.Solve(ref projection.Penetration, ref projection.InertiaA, ref projection.InertiaB, ref projection.Normal,
                ref accumulatedImpulses.Penetration0,
                ref accumulatedImpulses.Penetration1,
                ref accumulatedImpulses.Penetration2,
                ref accumulatedImpulses.Penetration3, ref wsvA, ref wsvB);
            var maximumTwistImpulse = projection.PremultipliedFrictionCoefficient * (
                accumulatedImpulses.Penetration0 * projection.LeverArm0 +
                accumulatedImpulses.Penetration1 * projection.LeverArm1 +
                accumulatedImpulses.Penetration2 * projection.LeverArm2 +
                accumulatedImpulses.Penetration3 * projection.LeverArm3);
            TwistFriction.Solve(ref projection.Normal, ref projection.InertiaA, ref projection.InertiaB, ref projection.Twist, ref maximumTwistImpulse, ref accumulatedImpulses.Twist, ref wsvA, ref wsvB);
        }

    }

    /// <summary>
    /// Handles the solve iterations of a bunch of 4-contact convex manifold constraints.
    /// </summary>
    public class Contact4TypeProcessor :
        //UnposedTwoBodyTypeBatch<ContactManifold4PrestepData, ContactManifold4Projection, ContactManifold4AccumulatedImpulses, ContactManifold4>
        TwoBodyTypeProcessor<Contact4PrestepData, Contact4Projection, Contact4AccumulatedImpulses, Contact4Functions>
    {
        public const int BatchTypeId = 11;
    }
}
