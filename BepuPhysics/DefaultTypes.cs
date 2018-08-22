﻿using BepuPhysics.Constraints;
using BepuPhysics.CollisionDetection;
using BepuPhysics.CollisionDetection.CollisionTasks;
using BepuPhysics.Constraints.Contact;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection.SweepTasks;

namespace BepuPhysics
{
    /// <summary>
    /// Helper class to register the default types within a simulation instance.
    /// </summary>
    public static class DefaultTypes
    {
        /// <summary>
        /// Registers the set of constraints that are packaged in the engine.
        /// </summary>
        public static void RegisterDefaults(Solver solver, NarrowPhase narrowPhase)
        {
            solver.Register<BallSocket>();
            solver.Register<AngularHinge>();
            solver.Register<AngularSwivelHinge>();
            solver.Register<SwingLimit>();
            solver.Register<TwistServo>();
            solver.Register<TwistLimit>();
            solver.Register<TwistMotor>();
            solver.Register<AngularServo>();
            solver.Register<AngularMotor>();
            solver.Register<GrabServo>();

            solver.Register<Contact1OneBody>();
            solver.Register<Contact2OneBody>();
            solver.Register<Contact3OneBody>();
            solver.Register<Contact4OneBody>();
            solver.Register<Contact1>();
            solver.Register<Contact2>();
            solver.Register<Contact3>();
            solver.Register<Contact4>();
            solver.Register<Contact2NonconvexOneBody>();
            solver.Register<Contact3NonconvexOneBody>();
            solver.Register<Contact4NonconvexOneBody>();
            solver.Register<Contact5NonconvexOneBody>();
            solver.Register<Contact6NonconvexOneBody>();
            solver.Register<Contact7NonconvexOneBody>();
            solver.Register<Contact8NonconvexOneBody>();
            solver.Register<Contact2Nonconvex>();
            solver.Register<Contact3Nonconvex>();
            solver.Register<Contact4Nonconvex>();
            solver.Register<Contact5Nonconvex>();
            solver.Register<Contact6Nonconvex>();
            solver.Register<Contact7Nonconvex>();
            solver.Register<Contact8Nonconvex>();

            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact8Nonconvex, Contact8NonconvexAccumulatedImpulses, ContactImpulses8, ConstraintCache8>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact7Nonconvex, Contact7NonconvexAccumulatedImpulses, ContactImpulses7, ConstraintCache7>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact6Nonconvex, Contact6NonconvexAccumulatedImpulses, ContactImpulses6, ConstraintCache6>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact5Nonconvex, Contact5NonconvexAccumulatedImpulses, ContactImpulses5, ConstraintCache5>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact4Nonconvex, Contact4NonconvexAccumulatedImpulses, ContactImpulses4, ConstraintCache4>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact3Nonconvex, Contact3NonconvexAccumulatedImpulses, ContactImpulses3, ConstraintCache3>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexTwoBodyAccessor<Contact2Nonconvex, Contact2NonconvexAccumulatedImpulses, ContactImpulses2, ConstraintCache2>());

            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact8NonconvexOneBody, Contact8NonconvexAccumulatedImpulses, ContactImpulses8, ConstraintCache8>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact7NonconvexOneBody, Contact7NonconvexAccumulatedImpulses, ContactImpulses7, ConstraintCache7>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact6NonconvexOneBody, Contact6NonconvexAccumulatedImpulses, ContactImpulses6, ConstraintCache6>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact5NonconvexOneBody, Contact5NonconvexAccumulatedImpulses, ContactImpulses5, ConstraintCache5>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact4NonconvexOneBody, Contact4NonconvexAccumulatedImpulses, ContactImpulses4, ConstraintCache4>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact3NonconvexOneBody, Contact3NonconvexAccumulatedImpulses, ContactImpulses3, ConstraintCache3>());
            narrowPhase.RegisterContactConstraintAccessor(new NonconvexOneBodyAccessor<Contact2NonconvexOneBody, Contact2NonconvexAccumulatedImpulses, ContactImpulses2, ConstraintCache2>());

            narrowPhase.RegisterContactConstraintAccessor(new ConvexTwoBodyAccessor<Contact4, Contact4AccumulatedImpulses, ContactImpulses4, ConstraintCache4>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexTwoBodyAccessor<Contact3, Contact3AccumulatedImpulses, ContactImpulses3, ConstraintCache3>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexTwoBodyAccessor<Contact2, Contact2AccumulatedImpulses, ContactImpulses2, ConstraintCache2>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexTwoBodyAccessor<Contact1, Contact1AccumulatedImpulses, ContactImpulses1, ConstraintCache1>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexOneBodyAccessor<Contact4OneBody, Contact4AccumulatedImpulses, ContactImpulses4, ConstraintCache4>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexOneBodyAccessor<Contact3OneBody, Contact3AccumulatedImpulses, ContactImpulses3, ConstraintCache3>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexOneBodyAccessor<Contact2OneBody, Contact2AccumulatedImpulses, ContactImpulses2, ConstraintCache2>());
            narrowPhase.RegisterContactConstraintAccessor(new ConvexOneBodyAccessor<Contact1OneBody, Contact1AccumulatedImpulses, ContactImpulses1, ConstraintCache1>());

        }

        /// <summary>
        /// Creates a task registry containing the default collision pair types.
        /// </summary>
        public static CollisionTaskRegistry CreateDefaultCollisionTaskRegistry()
        {
            var defaultTaskRegistry = new CollisionTaskRegistry();
            defaultTaskRegistry.Register(new ConvexCollisionTask<Sphere, SphereWide, Sphere, SphereWide, SpherePair, SpherePairWide, Convex1ContactManifoldWide, SpherePairTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Sphere, SphereWide, Capsule, CapsuleWide, SphereIncludingPair, SphereIncludingPairWide<Capsule, CapsuleWide>, Convex1ContactManifoldWide, SphereCapsuleTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Sphere, SphereWide, Box, BoxWide, SphereIncludingPair, SphereIncludingPairWide<Box, BoxWide>, Convex1ContactManifoldWide, SphereBoxTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Sphere, SphereWide, Triangle, TriangleWide, SphereIncludingPair, SphereIncludingPairWide<Triangle, TriangleWide>, Convex1ContactManifoldWide, SphereTriangleTester>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Sphere, Compound, ConvexCompoundOverlapFinder<Sphere, SphereWide, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Sphere, BigCompound, ConvexCompoundOverlapFinder<Sphere, SphereWide, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshCollisionTask<Sphere, Mesh, ConvexCompoundOverlapFinder<Sphere, SphereWide, Mesh>>());

            defaultTaskRegistry.Register(new ConvexCollisionTask<Capsule, CapsuleWide, Capsule, CapsuleWide, FliplessPair, FliplessPairWide<Capsule, CapsuleWide>, Convex2ContactManifoldWide, CapsulePairTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Capsule, CapsuleWide, Box, BoxWide, CollisionPair, ConvexPairWide<Capsule, CapsuleWide, Box, BoxWide>, Convex2ContactManifoldWide, CapsuleBoxTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Capsule, CapsuleWide, Triangle, TriangleWide, CollisionPair, ConvexPairWide<Capsule, CapsuleWide, Triangle, TriangleWide>, Convex2ContactManifoldWide, CapsuleTriangleTester>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Capsule, Compound, ConvexCompoundOverlapFinder<Capsule, CapsuleWide, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Capsule, BigCompound, ConvexCompoundOverlapFinder<Capsule, CapsuleWide, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshCollisionTask<Capsule, Mesh, ConvexCompoundOverlapFinder<Capsule, CapsuleWide, Mesh>>());

            defaultTaskRegistry.Register(new ConvexCollisionTask<Box, BoxWide, Box, BoxWide, FliplessPair, FliplessPairWide<Box, BoxWide>, Convex4ContactManifoldWide, BoxPairTester>());
            defaultTaskRegistry.Register(new ConvexCollisionTask<Box, BoxWide, Triangle, TriangleWide, CollisionPair, ConvexPairWide<Box, BoxWide, Triangle, TriangleWide>, Convex4ContactManifoldWide, BoxTriangleTester>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Box, Compound, ConvexCompoundOverlapFinder<Box, BoxWide, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Box, BigCompound, ConvexCompoundOverlapFinder<Box, BoxWide, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshCollisionTask<Box, Mesh, ConvexCompoundOverlapFinder<Box, BoxWide, Mesh>>());

            defaultTaskRegistry.Register(new ConvexCollisionTask<Triangle, TriangleWide, Triangle, TriangleWide, FliplessPair, FliplessPairWide<Triangle, TriangleWide>, Convex4ContactManifoldWide, TrianglePairTester>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Triangle, Compound, ConvexCompoundOverlapFinder<Triangle, TriangleWide, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundCollisionTask<Triangle, BigCompound, ConvexCompoundOverlapFinder<Triangle, TriangleWide, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshCollisionTask<Triangle, Mesh, ConvexCompoundOverlapFinder<Triangle, TriangleWide, Mesh>>());

            defaultTaskRegistry.Register(new CompoundPairCollisionTask<Compound, Compound, CompoundPairOverlapFinder<Compound, Compound>>());
            defaultTaskRegistry.Register(new CompoundPairCollisionTask<Compound, BigCompound, CompoundPairOverlapFinder<Compound, BigCompound>>());
            defaultTaskRegistry.Register(new CompoundMeshCollisionTask<Compound, Mesh, CompoundPairOverlapFinder<Compound, Mesh>>());

            defaultTaskRegistry.Register(new CompoundPairCollisionTask<BigCompound, BigCompound, CompoundPairOverlapFinder<BigCompound, BigCompound>>());
            defaultTaskRegistry.Register(new CompoundMeshCollisionTask<BigCompound, Mesh, CompoundPairOverlapFinder<BigCompound, Mesh>>());

            //TODO: No mesh-mesh at the moment.
            return defaultTaskRegistry;
        }

        /// <summary>
        /// Creates a task registry containing the default sweep task types.
        /// </summary>
        public static SweepTaskRegistry CreateDefaultSweepTaskRegistry()
        {
            var defaultTaskRegistry = new SweepTaskRegistry();
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Sphere, SphereWide, Sphere, SphereWide, SpherePairDistanceTester>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Sphere, SphereWide, Capsule, CapsuleWide, SphereCapsuleDistanceTester>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Sphere, SphereWide, Box, BoxWide, SphereBoxDistanceTester>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Sphere, SphereWide, Triangle, TriangleWide, SphereTriangleDistanceTester>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Sphere, SphereWide, Compound, ConvexCompoundSweepOverlapFinder<Sphere, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Sphere, SphereWide, BigCompound, ConvexCompoundSweepOverlapFinder<Sphere, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshSweepTask<Sphere, SphereWide, Mesh, ConvexCompoundSweepOverlapFinder<Sphere, Mesh>>());

            defaultTaskRegistry.Register(new ConvexPairSweepTask<Capsule, CapsuleWide, Capsule, CapsuleWide, CapsulePairDistanceTester>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Capsule, CapsuleWide, Box, BoxWide, CapsuleBoxDistanceTester>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Capsule, CapsuleWide, Triangle, TriangleWide, CapsuleTriangleDistanceTester>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Capsule, CapsuleWide, Compound, ConvexCompoundSweepOverlapFinder<Capsule, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Capsule, CapsuleWide, BigCompound, ConvexCompoundSweepOverlapFinder<Capsule, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshSweepTask<Capsule, CapsuleWide, Mesh, ConvexCompoundSweepOverlapFinder<Capsule, Mesh>>());

            defaultTaskRegistry.Register(new ConvexPairSweepTask<Box, BoxWide, Box, BoxWide, GJKDistanceTester<Box, BoxWide, BoxSupportFinder, Box, BoxWide, BoxSupportFinder>>());
            defaultTaskRegistry.Register(new ConvexPairSweepTask<Box, BoxWide, Triangle, TriangleWide, GJKDistanceTester<Box, BoxWide, BoxSupportFinder, Triangle, TriangleWide, TriangleSupportFinder>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Box, BoxWide, Compound, ConvexCompoundSweepOverlapFinder<Box, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Box, BoxWide, BigCompound, ConvexCompoundSweepOverlapFinder<Box, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshSweepTask<Box, BoxWide, Mesh, ConvexCompoundSweepOverlapFinder<Box, Mesh>>());

            defaultTaskRegistry.Register(new ConvexPairSweepTask<Triangle, TriangleWide, Triangle, TriangleWide, GJKDistanceTester<Triangle, TriangleWide, TriangleSupportFinder, Triangle, TriangleWide, TriangleSupportFinder>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Triangle, TriangleWide, Compound, ConvexCompoundSweepOverlapFinder<Triangle, Compound>>());
            defaultTaskRegistry.Register(new ConvexCompoundSweepTask<Triangle, TriangleWide, BigCompound, ConvexCompoundSweepOverlapFinder<Triangle, BigCompound>>());
            defaultTaskRegistry.Register(new ConvexMeshSweepTask<Triangle, TriangleWide, Mesh, ConvexCompoundSweepOverlapFinder<Triangle, Mesh>>());

            defaultTaskRegistry.Register(new CompoundPairSweepTask<Compound, Compound, CompoundPairSweepOverlapFinder<Compound, Compound>>());
            defaultTaskRegistry.Register(new CompoundPairSweepTask<Compound, BigCompound, CompoundPairSweepOverlapFinder<Compound, BigCompound>>());
            defaultTaskRegistry.Register(new CompoundMeshSweepTask<Compound, Mesh, CompoundPairSweepOverlapFinder<Compound, Mesh>>());

            defaultTaskRegistry.Register(new CompoundPairSweepTask<BigCompound, BigCompound, CompoundPairSweepOverlapFinder<BigCompound, BigCompound>>());
            defaultTaskRegistry.Register(new CompoundMeshSweepTask<BigCompound, Mesh, CompoundPairSweepOverlapFinder<BigCompound, Mesh>>());
            
            //TODO: No mesh-mesh at the moment.
            return defaultTaskRegistry;
        }
    }
}
