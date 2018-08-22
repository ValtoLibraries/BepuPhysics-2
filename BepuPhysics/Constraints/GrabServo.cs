﻿using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using static BepuUtilities.GatherScatter;
namespace BepuPhysics.Constraints
{
    public struct GrabServo : IConstraintDescription<GrabServo>
    {
        public Vector3 LocalOffset;
        public Vector3 Target;
        public SpringSettings SpringSettings;
        public ServoSettings ServoSettings;

        public int ConstraintTypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return GrabServoTypeProcessor.BatchTypeId;
            }
        }

        public Type BatchType => typeof(GrabServoTypeProcessor);
        
        public void ApplyDescription(ref TypeBatch batch, int bundleIndex, int innerIndex)
        {
            Debug.Assert(ConstraintTypeId == batch.TypeId, "The type batch passed to the description must match the description's expected type.");
            ref var target = ref GetOffsetInstance(ref Buffer<GrabServoPrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            Vector3Wide.WriteFirst(LocalOffset, ref target.LocalOffset);
            Vector3Wide.WriteFirst(Target, ref target.Target);
            SpringSettingsWide.WriteFirst(SpringSettings, ref target.SpringSettings);
            ServoSettingsWide.WriteFirst(ServoSettings, ref target.ServoSettings);
        }
        
        public void BuildDescription(ref TypeBatch batch, int bundleIndex, int innerIndex, out GrabServo description)
        {
            Debug.Assert(ConstraintTypeId == batch.TypeId, "The type batch passed to the description must match the description's expected type.");
            ref var source = ref GetOffsetInstance(ref Buffer<GrabServoPrestepData>.Get(ref batch.PrestepData, bundleIndex), innerIndex);
            Vector3Wide.ReadFirst(source.LocalOffset, out description.LocalOffset);
            Vector3Wide.ReadFirst(source.Target, out description.Target);
            SpringSettingsWide.ReadFirst(source.SpringSettings, out description.SpringSettings);
            ServoSettingsWide.ReadFirst(source.ServoSettings, out description.ServoSettings);
        }
    }

    public struct GrabServoPrestepData
    {
        public Vector3Wide LocalOffset;
        //TODO: This depends upon position being represented as a 32 bit floating point number.
        //That's a little problematic in the long run. You could avoid this dependency by representing the target as a relative target, but that is quite a bit more difficult to use-
        //you would have to update the target location every single time step, or else it would just continually accelerate.
        public Vector3Wide Target;
        public SpringSettingsWide SpringSettings;
        public ServoSettingsWide ServoSettings;
    }

    public struct GrabServoProjection
    {
        public Vector3Wide Offset;
        public Vector3Wide BiasVelocity;
        public Symmetric3x3Wide EffectiveMass;
        public Vector<float> SoftnessImpulseScale;
        public Vector<float> MaximumImpulse;
        public BodyInertias Inertia;
    }

    public struct GrabServoFunctions : IOneBodyConstraintFunctions<GrabServoPrestepData, GrabServoProjection, Vector3Wide>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prestep(Bodies bodies, ref Vector<int> bodyReferences, int count, float dt, float inverseDt, ref GrabServoPrestepData prestep,
            out GrabServoProjection projection)
        {
            //TODO: Note that this grabs a world position. That poses a problem for different position representations.
            bodies.GatherInertiaAndPose(ref bodyReferences, count, out var position, out var orientation, out projection.Inertia);

            //The grabber is roughly equivalent to a ball socket joint with a nonzero goal (and only one body).

            QuaternionWide.TransformWithoutOverlap(prestep.LocalOffset, orientation, out projection.Offset);
            Symmetric3x3Wide.SkewSandwichWithoutOverlap(projection.Offset, projection.Inertia.InverseInertiaTensor, out var inverseEffectiveMass);

            //Linear contributions are simply I * inverseMass * I, which is just boosting the diagonal.
            inverseEffectiveMass.XX += projection.Inertia.InverseMass;
            inverseEffectiveMass.YY += projection.Inertia.InverseMass;
            inverseEffectiveMass.ZZ += projection.Inertia.InverseMass;
            Symmetric3x3Wide.Invert(inverseEffectiveMass, out projection.EffectiveMass);
            SpringSettingsWide.ComputeSpringiness(prestep.SpringSettings, dt, out var positionErrorToVelocity, out var effectiveMassCFMScale, out projection.SoftnessImpulseScale);
            Symmetric3x3Wide.Scale(projection.EffectiveMass, effectiveMassCFMScale, out projection.EffectiveMass);

            //Compute the position error and bias velocities. Note the order of subtraction when calculating error- we want the bias velocity to counteract the separation.
            Vector3Wide.Add(projection.Offset, position, out var worldGrabPoint);
            Vector3Wide.Subtract(prestep.Target, worldGrabPoint, out var error);
            Vector3Wide.Scale(error, positionErrorToVelocity, out projection.BiasVelocity);
            Vector3Wide.Length(projection.BiasVelocity, out var speed);
            var newSpeed = Vector.Min(prestep.ServoSettings.MaximumSpeed, Vector.Max(prestep.ServoSettings.BaseSpeed, speed));
            var scale = newSpeed / speed;
            Vector3Wide.Scale(projection.BiasVelocity, scale, out var scaledVelocity);
            Vector3Wide.ConditionalSelect(Vector.GreaterThan(speed, Vector<float>.Zero), scaledVelocity, projection.BiasVelocity, out projection.BiasVelocity);

            projection.MaximumImpulse = prestep.ServoSettings.MaximumForce * dt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyImpulse(ref BodyVelocities velocityA, ref GrabServoProjection projection, ref Vector3Wide csi)
        {
            Vector3Wide.CrossWithoutOverlap(projection.Offset, csi, out var wsi);
            Symmetric3x3Wide.TransformWithoutOverlap(wsi, projection.Inertia.InverseInertiaTensor, out var change);
            Vector3Wide.Add(velocityA.Angular, change, out velocityA.Angular);

            Vector3Wide.Scale(csi, projection.Inertia.InverseMass, out change);
            Vector3Wide.Add(velocityA.Linear, change, out velocityA.Linear);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WarmStart(ref BodyVelocities velocityA, ref GrabServoProjection projection, ref Vector3Wide accumulatedImpulse)
        {
            ApplyImpulse(ref velocityA, ref projection, ref accumulatedImpulse);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Solve(ref BodyVelocities velocityA, ref GrabServoProjection projection, ref Vector3Wide accumulatedImpulse)
        {
            //csi = projection.BiasImpulse - accumulatedImpulse * projection.SoftnessImpulseScale - (csiaLinear + csiaAngular);
            Vector3Wide.CrossWithoutOverlap(velocityA.Angular, projection.Offset, out var angularCSV);
            Vector3Wide.Add(velocityA.Linear, angularCSV, out var csv);
            Vector3Wide.Subtract(projection.BiasVelocity, csv, out csv);

            Symmetric3x3Wide.TransformWithoutOverlap(csv, projection.EffectiveMass, out var csi);
            Vector3Wide.Scale(accumulatedImpulse, projection.SoftnessImpulseScale, out var softness);
            Vector3Wide.Subtract(csi, softness, out csi);

            //The motor has a limited maximum force, so clamp the accumulated impulse. Watch out for division by zero.
            var previous = accumulatedImpulse;
            Vector3Wide.Add(accumulatedImpulse, csi, out accumulatedImpulse);
            Vector3Wide.Length(accumulatedImpulse, out var impulseMagnitude);
            var newMagnitude = Vector.Min(impulseMagnitude, projection.MaximumImpulse);
            var scale = newMagnitude / impulseMagnitude;
            Vector3Wide.Scale(accumulatedImpulse, scale, out accumulatedImpulse);
            Vector3Wide.ConditionalSelect(Vector.GreaterThan(impulseMagnitude, Vector<float>.Zero), accumulatedImpulse, previous, out accumulatedImpulse);
            Vector3Wide.Subtract(accumulatedImpulse, previous, out csi);

            ApplyImpulse(ref velocityA, ref projection, ref csi);

        }

    }


    /// <summary>
    /// Handles the solve iterations of a bunch of ball socket constraints.
    /// </summary>
    public class GrabServoTypeProcessor : OneBodyTypeProcessor<GrabServoPrestepData, GrabServoProjection, Vector3Wide, GrabServoFunctions>
    {
        public const int BatchTypeId = 31;
    }
}
