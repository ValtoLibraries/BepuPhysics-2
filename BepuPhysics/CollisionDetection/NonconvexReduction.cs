﻿using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace BepuPhysics.CollisionDetection
{
    public struct NonconvexReductionChild
    {
        public ConvexContactManifold Manifold;
        /// <summary>
        /// Offset from the origin of the first shape's parent to the child's location in world space. If there is no parent, this is the zero vector.
        /// </summary>
        public Vector3 OffsetA;
        public int ChildIndexA;
        /// <summary>
        /// Offset from the origin of the second shape's parent to the child's location in world space. If there is no parent, this is the zero vector.
        /// </summary>
        public Vector3 OffsetB;
        public int ChildIndexB;
    }

    public struct NonconvexReduction : ICollisionTestContinuation
    {
        public int ChildCount;
        public int CompletedChildCount;
        public Buffer<NonconvexReductionChild> Children;

        public void Create(int childManifoldCount, BufferPool pool)
        {
            ChildCount = childManifoldCount;
            CompletedChildCount = 0;
            pool.Take(childManifoldCount, out Children);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void AddContact(NonconvexContactManifold* manifold, ref NonconvexReductionChild sourceChild,
            ref Vector3 offset, float depth, ref Vector3 normal, int featureId)
        {
            ref var target = ref NonconvexContactManifold.Allocate(manifold);
            target.Offset = offset + sourceChild.OffsetA;
            target.Normal = normal;
            //Mix the convex-generated feature id with the child indices.
            target.FeatureId = (featureId ^ (sourceChild.ChildIndexA << 8)) ^ (sourceChild.ChildIndexB << 16);
            target.Depth = depth;
        }

        unsafe void ChooseBadly(NonconvexContactManifold* reducedManifold)
        {
            for (int i = 0; i < ChildCount; ++i)
            {
                ref var child = ref Children[i];
                ref var contactBase = ref child.Manifold.Contact0;
                for (int j = 0; j < child.Manifold.Count; ++j)
                {
                    ref var contact = ref Unsafe.Add(ref contactBase, j);
                    AddContact(reducedManifold, ref child, ref contact.Offset, contact.Depth, ref child.Manifold.Normal, contact.FeatureId);
                    if (reducedManifold->Count == 8)
                        break;
                }
                if (reducedManifold->Count == 8)
                    break;
            }

        }
        unsafe void ChooseDeepest(NonconvexContactManifold* manifold)
        {
            //Note that we're relying on stackalloc being efficient here. That's not actually a good idea unless you strip the stackalloc's init.
            var contactCountUpperBound = ChildCount * 4;
            int contactCount = 0;
            var depths = stackalloc float[contactCountUpperBound];
            var indices = stackalloc Int2[contactCountUpperBound];
            for (int i = 0; i < ChildCount; ++i)
            {
                ref var child = ref Children[i];
                ref var contactBase = ref child.Manifold.Contact0;
                for (int j = 0; j < child.Manifold.Count; ++j)
                {
                    var contactIndex = contactCount++;
                    ref var contactIndices = ref indices[contactIndex];
                    contactIndices.X = i;
                    contactIndices.Y = j;
                    depths[contactIndex] = Unsafe.Add(ref contactBase, j).Depth;
                }
            }

            var comparer = default(PrimitiveComparer<float>);
            QuickSort.Sort(ref *depths, ref *indices, 0, contactCount - 1, ref comparer);
            var newCount = contactCount < 8 ? contactCount : 8;
            var minimumIndex = contactCount - newCount;
            for (int i = contactCount - 1; i >= minimumIndex; --i)
            {
                ref var contactIndices = ref indices[i];
                ref var child = ref Children[contactIndices.X];
                ref var contact = ref Unsafe.Add(ref child.Manifold.Contact0, contactIndices.Y);
                AddContact(manifold, ref child, ref contact.Offset, contact.Depth, ref child.Manifold.Normal, contact.FeatureId);
            }


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe static void AddContact(ref NonconvexReductionChild child, int contactIndexInChild, NonconvexContactManifold* targetManifold)
        {
            ref var contact = ref Unsafe.Add(ref child.Manifold.Contact0, contactIndexInChild);
            ref var target = ref NonconvexContactManifold.Allocate(targetManifold);
            target.Offset = contact.Offset;
            target.Normal = child.Manifold.Normal;
            //Mix the convex-generated feature id with the child indices.
            target.FeatureId = contact.FeatureId ^ ((child.ChildIndexA << 8) ^ (child.ChildIndexB << 16));
            target.Depth = contact.Depth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe static void UseContact(ref QuickList<Int2, Buffer<Int2>> remainingChildren, int index, ref Buffer<NonconvexReductionChild> children, NonconvexContactManifold* targetManifold)
        {
            ref var childIndex = ref remainingChildren[index];
            ref var child = ref children[childIndex.X];
            AddContact(ref children[childIndex.X], childIndex.Y, targetManifold);
            remainingChildren.FastRemoveAt(index);
        }
        /// <summary>
        /// The maximum number of contacts that can exist within a nonconvex manifold.
        /// </summary>
        const int MaximumContactCount = 4;
        unsafe void ChooseMostConstraining(NonconvexContactManifold* manifold, BufferPool pool)
        {
            //The end goal of contact reduction is to choose a reasonably stable subset of contacts which offer the greatest degree of constraint.
            //Computing a globally optimal solution to this would be pretty expensive for any given scoring mechanism, so we'll make some simplifications:
            //1) Heuristically select a starting point that will be reasonably stable between frames. Add it to the reduced manifold.
            //2) Search for the contact in the unadded set which maximizes the constraint heuristic and add it. Remove it from the unadded set.
            //3) Repeat #2 until there is no room left in the reduced manifold or all remaining candidates are redundant with the reduced manifold.

            //We first compute some calibration data. Knowing the approximate center of the manifold and the maximum distance allows more informed thresholds.

            var extentAxis = new Vector3(0.280454652f, 0.55873544499f, 0.7804869574f);
            float minimumExtent = float.MaxValue;
            Vector3 minimumExtentPosition = default;
            for (int i = 0; i < ChildCount; ++i)
            {
                ref var child = ref Children[i];
                for (int j = 0; j < child.Manifold.Count; ++j)
                {
                    ref var position = ref Unsafe.Add(ref child.Manifold.Contact0, j).Offset;
                    var extent = Vector3.Dot(position, extentAxis);
                    if(extent < minimumExtent)
                    {
                        minimumExtent = extent;
                        minimumExtentPosition = position;
                    }
                }
            }
            float maximumDistanceSquared = 0;
            for (int i = 0; i < ChildCount; ++i)
            {
                ref var child = ref Children[i];
                for (int j = 0; j < child.Manifold.Count; ++j)
                {
                    var distanceSquared = (Unsafe.Add(ref child.Manifold.Contact0, j).Offset - minimumExtentPosition).LengthSquared();
                    if (distanceSquared > maximumDistanceSquared)
                        maximumDistanceSquared = distanceSquared;
                }
            }
            var maximumDistance = (float)Math.Sqrt(maximumDistanceSquared);
            float initialBestScore = -float.MaxValue;
            int initialBestScoreIndex = 0;
            QuickList<Int2, Buffer<Int2>>.Create(pool.SpecializeFor<Int2>(), ChildCount * 4, out var remainingChildren);
            //To reliably break the tie between multiple contacts in the same location (which often happens on triangle meshes), add a little bit extra to later contacts.
            //Not enough to significantly change the outcome under any circumstance- just enough to avoid swapping between two numerically near-identical starting points over and over.
            var biasPerIndex = maximumDistance * 1e-5f;
            var extremityScale = maximumDistance * 1e-4f;
            for (int i = 0; i < ChildCount; ++i)
            {
                ref var child = ref Children[i];
                ref var childContactsBase = ref child.Manifold.Contact0;
                for (int j = 0; j < child.Manifold.Count; ++j)
                {
                    ref var contact = ref Unsafe.Add(ref childContactsBase, j);
                    //Note that we only consider 'extreme' contacts that have positive depth to avoid selecting purely speculative contacts as a starting point.
                    //If there are no contacts with positive depth, it's fine to just rely on the 'deepest' speculative contact. 
                    //Feature id stability doesn't matter much if there is no stable contact.
                    float candidateScore;
                    if (contact.Depth >= 0)
                    {
                        //Note that we assume that the contact offsets have already been moved into the parent's space in compound pairs so that we can validly compare extents across manifolds.
                        var extent = Vector3.Dot(contact.Offset, extentAxis) - minimumExtent;
                        candidateScore = contact.Depth + extent * extremityScale + remainingChildren.Count * biasPerIndex;
                    }
                    else
                    {
                        //Speculative contact scores are simply based on depth.
                        candidateScore = contact.Depth;
                    }
                    if (candidateScore > initialBestScore)
                    {
                        initialBestScore = candidateScore;
                        initialBestScoreIndex = remainingChildren.Count;
                    }
                    ref var indices = ref remainingChildren.AllocateUnsafely();
                    indices.X = i;
                    indices.Y = j;
                }
            }

            Debug.Assert(remainingChildren.Count > 0, "This function should only be called when there are populated manifolds.");

            UseContact(ref remainingChildren, initialBestScoreIndex, ref Children, manifold);

            //We now have a decent starting point. Now, incrementally search for contacts which expand the manifold as much as possible.
            //This is going to be a greedy and nonoptimal search, but being consistent and good enough is more important than true optimality.

            //TODO: This could be significantly optimized. Many approximations would get 95% of the benefit, and even the full version could be vectorized in a few different ways.
            var depthScale = 15f / maximumDistance;
            var reducedContacts = &manifold->Contact0;
            while (remainingChildren.Count > 0 && manifold->Count < MaximumContactCount)
            {
                float bestScore = -1;
                int bestScoreIndex = 0;
                for (int remainingChildrenIndex = 0; remainingChildrenIndex < remainingChildren.Count; ++remainingChildrenIndex)
                {
                    ref var childIndex = ref remainingChildren[remainingChildrenIndex];
                    ref var child = ref Children[childIndex.X];
                    //Consider each candidate contact as an impulse to test against the so-far accumulated manifold.
                    //The candidate which has the greatest remaining impulse after applying the existing manifold's constraints is considered to be the most 'constraining' 
                    //potential addition. This can be thought of as an approximate constraint solve.
                    ref var contact = ref Unsafe.Add(ref child.Manifold.Contact0, childIndex.Y);
                    //We give contacts of higher depth greater impulses, so they'll tend to be chosen over low depth contacts.
                    var scaledDepth = contact.Depth * depthScale;
                    //Don't let speculative contacts wrap around into larger impulses.
                    if (scaledDepth < -1)
                        scaledDepth = -1;
                    Vector3 linear = (-1 - scaledDepth) * child.Manifold.Normal;
                    Vector3x.Cross(contact.Offset, linear, out var angular);
                    for (int i = 0; i < manifold->Count; ++i)
                    {
                        ref var reducedContact = ref reducedContacts[i];
                        Vector3x.Cross(reducedContact.Offset, reducedContact.Normal, out var angularJacobian);
                        var velocityAtContact = Vector3.Dot(linear, reducedContact.Normal) + Vector3.Dot(angularJacobian, angular);
                        if (velocityAtContact < 0)
                        {
                            //Note that we're assuming unit mass and inertia here.
                            var effectiveMass = 1f / (1 + angularJacobian.LengthSquared());
                            var constraintSpaceImpulse = effectiveMass * velocityAtContact;
                            linear -= constraintSpaceImpulse * reducedContact.Normal;
                            angular -= constraintSpaceImpulse * angularJacobian;
                        }
                    }
                    var score = linear.LengthSquared() + angular.LengthSquared();
                    //Heavily penalize speculative contacts. They can sometimes be worth it, but active contacts are almost always the priority unless they're redundant.
                    if (contact.Depth < 0)
                        score *= 0.2f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestScoreIndex = remainingChildrenIndex;
                    }
                }
                //TODO: Could probably detect redundant contacts in here using the constraint solve results, but the value is unclear versus the cost of testing.
                //Can't reliably just use a score threshold.
                UseContact(ref remainingChildren, bestScoreIndex, ref Children, manifold);
            }

            remainingChildren.Dispose(pool.SpecializeFor<Int2>());
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Flush<TCallbacks>(int pairId, ref CollisionBatcher<TCallbacks> batcher) where TCallbacks : struct, ICollisionCallbacks
        {
            Debug.Assert(ChildCount > 0);
            if (ChildCount == CompletedChildCount)
            {
                //This continuation is ready for processing. Find which contact manifold to report.
                int populatedChildManifolds = 0;
                //We cache an index in case there is only one populated manifold. Order of discovery doesn't matter- this value only gets used when there's one manifold.
                int samplePopulatedChildIndex = 0;
                int totalContactCount = 0;
                for (int i = 0; i < ChildCount; ++i)
                {
                    ref var child = ref Children[i];
                    var childManifoldCount = child.Manifold.Count;
                    if (childManifoldCount > 0)
                    {
                        totalContactCount += childManifoldCount;
                        ++populatedChildManifolds;
                        samplePopulatedChildIndex = i;
                        for (int j = 0; j < child.Manifold.Count; ++j)
                        {
                            //Push all contacts into the space of the parent object.
                            Unsafe.Add(ref child.Manifold.Contact0, j).Offset += child.OffsetA;
                        }
                    }
                }
                var sampleChild = (NonconvexReductionChild*)Children.Memory + samplePopulatedChildIndex;

                if (populatedChildManifolds > 1)
                {
                    //There are multiple contributing child manifolds, so just assume that the resulting manifold is going to be nonconvex.
                    NonconvexContactManifold reducedManifold;
                    //We should assume that the stack memory backing the reduced manifold is uninitialized. We rely on the count, so initialize it manually.
                    reducedManifold.Count = 0;

                    if (totalContactCount <= MaximumContactCount)
                    {
                        //No reduction required; we can fit every contact.
                        //TODO: If you have any redundant contact removal, you'd have to do it before running this.
                        for (int i = 0; i < ChildCount; ++i)
                        {
                            ref var child = ref Children[i];
                            for (int j = 0; j < child.Manifold.Count; ++j)
                            {
                                AddContact(ref child, j, &reducedManifold);
                            }
                        }
                    }
                    else
                    {
                        //ChooseBadly(&reducedManifold);
                        //ChooseDeepest(&reducedManifold);
                        ChooseMostConstraining(&reducedManifold, batcher.Pool);
                    }

                    //The manifold offsetB is the offset from shapeA origin to shapeB origin.
                    reducedManifold.OffsetB = sampleChild->Manifold.OffsetB - sampleChild->OffsetB + sampleChild->OffsetA;
                    batcher.Callbacks.OnPairCompleted(pairId, &reducedManifold);
                }
                else
                {
                    //Two possibilities here: 
                    //1) populatedChildManifolds == 1, and samplePopulatedChildIndex is the index of that sole populated manifold. We can directly report it.
                    //It's useful to directly report the convex child manifold for performance reasons- convex constraints do not require multiple normals and use a faster friction model.
                    //2) populatedChildManifolds == 0, and samplePopulatedChildIndex is 0. Given that we know this continuation is only used when there is at least one manifold expected
                    //and that we can only hit this codepath if all manifolds are empty, reporting manifold 0 is perfectly fine.
                    //The manifold offsetB is the offset from shapeA origin to shapeB origin.
                    sampleChild->Manifold.OffsetB = sampleChild->Manifold.OffsetB - sampleChild->OffsetB + sampleChild->OffsetA;
                    batcher.Callbacks.OnPairCompleted(pairId, &sampleChild->Manifold);
                }
                batcher.Pool.ReturnUnsafely(Children.Id);
#if DEBUG
                //This makes it a little easier to detect invalid accesses that occur after disposal.
                this = new NonconvexReduction();
#endif
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void OnChildCompleted<TCallbacks>(ref PairContinuation report, ConvexContactManifold* manifold, ref CollisionBatcher<TCallbacks> batcher)
            where TCallbacks : struct, ICollisionCallbacks
        {
            Children[report.ChildIndex].Manifold = *manifold;
            ++CompletedChildCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnChildCompletedEmpty<TCallbacks>(ref PairContinuation report, ref CollisionBatcher<TCallbacks> batcher) where TCallbacks : struct, ICollisionCallbacks
        {
            Children[report.ChildIndex].Manifold.Count = 0;
            ++CompletedChildCount;
        }

        public bool TryFlush<TCallbacks>(int pairId, ref CollisionBatcher<TCallbacks> batcher) where TCallbacks : struct, ICollisionCallbacks
        {
            if (CompletedChildCount == ChildCount)
            {
                Flush(pairId, ref batcher);
                return true;
            }
            return false;
        }
    }
}
