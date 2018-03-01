﻿using BepuPhysics.Collidables;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BepuPhysics.CollisionDetection.CollisionTasks
{
    public struct CapsulePairTester : IPairTester<CapsuleWide, CapsuleWide, Convex2ContactManifoldWide>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Test(
            ref CapsuleWide a, ref CapsuleWide b,
            ref Vector3Wide offsetB, ref QuaternionWide orientationA, ref QuaternionWide orientationB,
            out Convex2ContactManifoldWide manifold)
        {
            //Compute the closest points between the two line segments. No clamping to begin with.
            //We want to minimize distance = ||(a + da * ta) - (b + db * tb)||.
            //Taking the derivative with respect to ta and doing some algebra (taking into account ||da|| == ||db|| == 1) to solve for ta yields:
            //ta = (da * (b - a) + (db * (a - b)) * (da * db)) / (1 - ((da * db) * (da * db))        
            QuaternionWide.TransformUnitXY(ref orientationA, out var xa, out var da);
            QuaternionWide.TransformUnitY(ref orientationB, out var db);
            Vector3Wide.Dot(ref da, ref offsetB, out var daOffsetB);
            Vector3Wide.Dot(ref db, ref offsetB, out var dbOffsetB);
            Vector3Wide.Dot(ref da, ref db, out var dadb);
            //Note potential division by zero when the axes are parallel. Arbitrarily clamp; near zero values will instead produce extreme values which get clamped to reasonable results.
            var ta = (daOffsetB - dbOffsetB * dadb) / Vector.Max(new Vector<float>(1e-15f), Vector<float>.One - dadb * dadb);
            //tb = ta * (da * db) - db * (b - a)
            var tb = ta * dadb - dbOffsetB;

            //We cannot simply clamp the ta and tb values to the capsule line segments. Instead, project each line segment onto the other line segment, clamping against the target's interval.
            //That new clamped projected interval is the valid solution space on that line segment. We can clamp the t value by that interval to get the correctly bounded solution.
            //The projected intervals are:
            //B onto A: +-BHalfLength * (da * db) + da * offsetB
            //A onto B: +-AHalfLength * (da * db) - db * offsetB
            var absdadb = Vector.Abs(dadb);
            var bOntoAOffset = b.HalfLength * absdadb;
            var aOntoBOffset = a.HalfLength * absdadb;
            var aMin = Vector.Max(-a.HalfLength, Vector.Min(a.HalfLength, daOffsetB - bOntoAOffset));
            var aMax = Vector.Min(a.HalfLength, Vector.Max(-a.HalfLength, daOffsetB + bOntoAOffset));
            var bMin = Vector.Max(-b.HalfLength, Vector.Min(b.HalfLength, -aOntoBOffset - dbOffsetB));
            var bMax = Vector.Min(b.HalfLength, Vector.Max(-b.HalfLength, aOntoBOffset - dbOffsetB));
            ta = Vector.Min(Vector.Max(ta, aMin), aMax);
            tb = Vector.Min(Vector.Max(tb, bMin), bMax);

            Vector3Wide.Scale(ref da, ref ta, out var closestPointOnA);
            Vector3Wide.Scale(ref db, ref tb, out var closestPointOnB);
            Vector3Wide.Add(ref closestPointOnB, ref offsetB, out closestPointOnB);
            //Note that normals are calibrated to point from B to A by convention.
            Vector3Wide.Subtract(ref closestPointOnA, ref closestPointOnB, out manifold.Normal);
            Vector3Wide.Length(ref manifold.Normal, out var distance);
            var inverseDistance = Vector<float>.One / distance;
            Vector3Wide.Scale(ref manifold.Normal, ref inverseDistance, out manifold.Normal);
            //In the event that the line segments are touching, the normal doesn't exist and we need an alternative. Any direction along the local horizontal (XZ) plane of either capsule
            //is valid. (Normals along the local Y axes are not guaranteed to be as quick of a path to separation due to nonzero line length.)
            var normalIsValid = Vector.GreaterThan(distance, new Vector<float>(1e-7f));
            Vector3Wide.ConditionalSelect(ref normalIsValid, ref manifold.Normal, ref xa, out manifold.Normal);

            //In the event that the two capsule axes are coplanar, we accept the whole interval as a source of contact.
            //As the axes drift away from coplanarity, the accepted interval rapidly narrows to zero length, centered on ta and tb.
            //We rate the degree of coplanarity based on the angle between the capsule axis and the plane defined by the box edge and contact normal:
            //sin(angle) = dot(da, (db x normal)/||db x normal||)
            //Finally, note that we are dealing with extremely small angles, and for small angles sin(angle) ~= angle,
            //and also that fade behavior is completely arbitrary, so we can directly use squared angle without any concern.
            //angle^2 ~= dot(da, (db x normal))^2 / ||db x normal||^2
            //Note that if ||db x normal|| is the zero, then any da should be accepted as being coplanar because there is no restriction. ConditionalSelect away the discontinuity.
            Vector3Wide.CrossWithoutOverlap(ref db, ref manifold.Normal, out var planeNormal);
            Vector3Wide.LengthSquared(ref planeNormal, out var planeNormalLengthSquared);
            Vector3Wide.Dot(ref da, ref planeNormal, out var squaredAngle);
            squaredAngle = Vector.ConditionalSelect(Vector.LessThan(planeNormalLengthSquared, new Vector<float>(1e-10f)), Vector<float>.Zero, squaredAngle / planeNormalLengthSquared);

            //Convert the squared angle to a lerp parameter. For squared angle from 0 to lowerThreshold, we should use the full interval (1). From lowerThreshold to upperThreshold, lerp to 0.
            const float lowerThresholdAngle = 0.001f;
            const float upperThresholdAngle = 0.005f;
            const float lowerThreshold = lowerThresholdAngle * lowerThresholdAngle;
            const float upperThreshold = upperThresholdAngle * upperThresholdAngle;
            var intervalWeight = Vector.Max(Vector<float>.Zero, Vector.Min(Vector<float>.One, (new Vector<float>(upperThreshold) - squaredAngle) * new Vector<float>(1f / (upperThreshold - lowerThreshold))));
            //If the line segments intersect, even if they're coplanar, we would ideally stick to using a single point. Would be easy enough,
            //but we don't bother because it's such a weird and extremely temporary corner case. Not really worth handling.
            var weightedTa = ta - ta * intervalWeight;
            aMin = intervalWeight * aMin + weightedTa;
            aMax = intervalWeight * aMax + weightedTa;

            Vector3Wide.Scale(ref da, ref aMin, out manifold.OffsetA0);
            Vector3Wide.Scale(ref da, ref aMax, out manifold.OffsetA1);
            //In the coplanar case, there are two points. We need a method of computing depth which gives a reasonable result to the second contact.
            //Note that one of the two contacts should end up with a distance equal to the previously computed segment distance, so we're doing some redundant work here.
            //It's just easier to do that extra work than it would be to track which endpoint contributed the lower distance.
            //Unproject the final interval endpoints from a back onto b.
            //dot(offsetB + db * tb0, da) = ta0
            //tb0 = (ta0 - daOffsetB) / dadb
            //distance0 = dot(a0 - (offsetB + tb0 * db), normal)
            //distance1 = dot(a1 - (offsetB + tb1 * db), normal)
            Vector3Wide.Dot(ref db, ref manifold.Normal, out var dbNormal);
            Vector3Wide.Subtract(ref manifold.OffsetA0, ref offsetB, out var offsetB0);
            Vector3Wide.Subtract(ref manifold.OffsetA1, ref offsetB, out var offsetB1);
            //Note potential division by zero. In that case, treat both projected points as the closest point. (Handled by the conditional select that chooses the previously computed distance.)
            var inverseDadb = Vector<float>.One / dadb;
            var projectedTb0 = Vector.Max(bMin, Vector.Min(bMax, (aMin - daOffsetB) * inverseDadb));
            var projectedTb1 = Vector.Max(bMin, Vector.Min(bMax, (aMax - daOffsetB) * inverseDadb));
            Vector3Wide.Dot(ref offsetB0, ref manifold.Normal, out var b0Normal);
            Vector3Wide.Dot(ref offsetB1, ref manifold.Normal, out var b1Normal);
            var capsulesArePerpendicular = Vector.LessThan(Vector.Abs(dadb), new Vector<float>(1e-7f));
            var distance0 = Vector.ConditionalSelect(capsulesArePerpendicular, distance, b0Normal - dbNormal * projectedTb0);
            var distance1 = Vector.ConditionalSelect(capsulesArePerpendicular, distance, b1Normal - dbNormal * projectedTb1);
            var combinedRadius = a.Radius + b.Radius;
            manifold.Depth0 = combinedRadius - distance0;
            manifold.Depth1 = combinedRadius - distance1;

            //Apply the normal offset to the contact positions.           
            var negativeOffsetFromA0 = manifold.Depth0 * 0.5f - a.Radius;
            var negativeOffsetFromA1 = manifold.Depth1 * 0.5f - a.Radius;
            Vector3Wide.Scale(ref manifold.Normal, ref negativeOffsetFromA0, out var normalPush0);
            Vector3Wide.Scale(ref manifold.Normal, ref negativeOffsetFromA1, out var normalPush1);
            Vector3Wide.Add(ref manifold.OffsetA0, ref normalPush0, out manifold.OffsetA0);
            Vector3Wide.Add(ref manifold.OffsetA1, ref normalPush1, out manifold.OffsetA1);
            manifold.FeatureId0 = Vector<int>.Zero;
            manifold.FeatureId1 = Vector<int>.One;
            manifold.Count = Vector.ConditionalSelect(Vector.LessThan(aMax - aMin, new Vector<float>(1e-7f) * a.HalfLength), Vector<int>.One, new Vector<int>(2));

            //TODO: Since we added in the complexity of 2 contact support, this is probably large enough to benefit from working in the local space of one of the capsules.
            //Worth looking into later.
        }

        public void Test(ref CapsuleWide a, ref CapsuleWide b, ref Vector3Wide offsetB, ref QuaternionWide orientationB, out Convex2ContactManifoldWide manifold)
        {
            throw new NotImplementedException();
        }

        public void Test(ref CapsuleWide a, ref CapsuleWide b, ref Vector3Wide offsetB, out Convex2ContactManifoldWide manifold)
        {
            throw new NotImplementedException();
        }
    }

    public class CapsulePairCollisionTask : CollisionTask
    {
        public CapsulePairCollisionTask()
        {
            BatchSize = 32;
            ShapeTypeIndexA = default(Capsule).TypeId;
            ShapeTypeIndexB = default(Capsule).TypeId;
        }


        //Every single collision task type will mirror this general layout.
        public unsafe override void ExecuteBatch<TContinuations, TFilters>(ref UntypedList batch, ref StreamingBatcher batcher, ref TContinuations continuations, ref TFilters filters)
        {
            CollisionTaskCommon.ExecuteBatch
                <TContinuations, TFilters,
                Capsule, CapsuleWide, Capsule, CapsuleWide, UnflippableTestPairWide<Capsule, CapsuleWide, Capsule, CapsuleWide>,
                Convex2ContactManifoldWide, CapsulePairTester>(ref batch, ref batcher, ref continuations, ref filters);
        }
    }
}
