﻿using BepuPhysics.CollisionDetection;
using BepuUtilities;
using BepuUtilities.Collections;
using BepuUtilities.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace BepuPhysics
{
    /// <summary>
    /// Provides functionality for efficiently activating previously deactivated bodies and their associated islands.
    /// </summary>
    public class IslandActivator
    {
        Solver solver;
        Statics statics;
        Bodies bodies;
        BroadPhase broadPhase;
        Deactivator deactivator;
        BufferPool pool;
        internal PairCache pairCache;

        public IslandActivator(Bodies bodies, Statics statics, Solver solver, BroadPhase broadPhase, Deactivator deactivator, BufferPool pool)
        {
            this.bodies = bodies;
            this.statics = statics;
            this.solver = solver;
            this.broadPhase = broadPhase;
            this.deactivator = deactivator;
            this.pool = pool;

            this.phaseOneWorkerDelegate = PhaseOneWorker;
            this.phaseTwoWorkerDelegate = PhaseTwoWorker;
        }

        /// <summary>
        /// Activates a body if it is inactive. All bodies that can be found by traversing the constraint graph from the body will also be activated.
        /// If the body is already active, this does nothing.
        /// </summary>
        /// <param name="bodyHandle">Handle of the body to activate.</param>
        public void ActivateBody(int bodyHandle)
        {
            bodies.ValidateExistingHandle(bodyHandle);
            ActivateSet(bodies.HandleToLocation[bodyHandle].SetIndex);
        }

        /// <summary>
        /// Activates any inactive bodies associated with a constraint. All bodies that can be found by traversing the constraint graph from the constraint referenced bodies will also be activated.
        /// If all bodies associated with the constraint are already active, this does nothing.
        /// </summary>
        /// <param name="constraintHandle">Handle of the constraint to activate.</param>
        public void ActivateConstraint(int constraintHandle)
        {
            ActivateSet(solver.HandleToConstraint[constraintHandle].SetIndex);
        }

        /// <summary>
        /// Activates all bodies and constraints within a set. Doesn't do anything if the set is active (index zero).
        /// </summary>
        /// <param name="setIndex">Index of the set to activate.</param>
        public void ActivateSet(int setIndex)
        {
            if (setIndex > 0)
            {
                ValidateInactiveSetIndex(setIndex);
                //TODO: Some fairly pointless work here- spans or other approaches could help with the API.
                QuickList<int, Buffer<int>>.Create(pool.SpecializeFor<int>(), 1, out var list);
                list.AddUnsafely(setIndex);
                ActivateSets(ref list);
                list.Dispose(pool.SpecializeFor<int>());
            }
        }

        /// <summary>
        /// Activates a list of set indices.
        /// </summary>
        /// <param name="setIndices">List of set indices to activate.</param>
        /// <param name="threadDispatcher">Thread dispatcher to use when activating the bodies. Pass null to run on a single thread.</param>
        public void ActivateSets(ref QuickList<int, Buffer<int>> setIndices, IThreadDispatcher threadDispatcher = null)
        {
            QuickList<int, Buffer<int>>.Create(pool.SpecializeFor<int>(), setIndices.Count, out var uniqueSetIndices);
            var uniqueSet = new IndexSet(pool, bodies.Sets.Length);
            AccumulateUniqueIndices(ref setIndices, ref uniqueSet, ref uniqueSetIndices, pool);
            uniqueSet.Dispose(pool);

            //Note that we use the same codepath as multithreading, we just don't use a multithreaded dispatch to execute jobs.
            //TODO: It would probably be a good idea to add a little heuristic to avoid doing multithreaded dispatches if there are only like 5 total bodies.
            //Shouldn't matter too much- the threaded variant should only really be used when doing big batched changes, so having a fixed constant cost isn't that bad.
            int threadCount = threadDispatcher == null ? 1 : threadDispatcher.ThreadCount;
            //Note that direct activations always reset activity states. I suspect this is sufficiently universal that no one will ever want the alternative,
            //even though the narrowphase does avoid resetting deactivation states for the sake of faster resleeping when possible.
            var (phaseOneJobCount, phaseTwoJobCount) = PrepareJobs(ref uniqueSetIndices, true, threadCount);

            if (threadCount > 1)
            {
                this.jobIndex = -1;
                this.jobCount = phaseOneJobCount;
                threadDispatcher.DispatchWorkers(phaseOneWorkerDelegate);
            }
            else
            {
                for (int i = 0; i < phaseOneJobCount; ++i)
                {
                    ExecutePhaseOneJob(i);
                }
            }

            if (threadCount > 1)
            {
                this.jobIndex = -1;
                this.jobCount = phaseTwoJobCount;
                threadDispatcher.DispatchWorkers(phaseTwoWorkerDelegate);
            }
            else
            {
                for (int i = 0; i < phaseTwoJobCount; ++i)
                {
                    ExecutePhaseTwoJob(i);
                }
            }

            DisposeForCompletedActivations(ref uniqueSetIndices);

            uniqueSetIndices.Dispose(pool.SpecializeFor<int>());
        }

        //Note that the worker loop and its supporting fields are only used when the island activator is used in isolation by an external call.
        //The engine's own use of the activator takes place in the narrowphase, where the activation jobs are scheduled alongside other jobs for greater parallelism.
        int jobIndex;
        int jobCount;
        //TODO: once again, we repeat this worker pattern. We've done this, what, seven times? It wouldn't even be that difficult to centralize it. We'll get around to that at some point.
        Action<int> phaseOneWorkerDelegate;
        Action<int> phaseTwoWorkerDelegate;
        internal void PhaseOneWorker(int workerIndex)
        {
            while (true)
            {
                var index = Interlocked.Increment(ref jobIndex);
                if (index >= jobCount)
                    break;
                ExecutePhaseOneJob(index);
            }
        }
        internal void PhaseTwoWorker(int workerIndex)
        {
            while (true)
            {
                var index = Interlocked.Increment(ref jobIndex);
                if (index >= jobCount)
                    break;
                ExecutePhaseTwoJob(index);
            }
        }
        enum PhaseOneJobType
        {
            PairCache,
            UpdateBatchReferencedHandles,
            CopyBodyRegion
        }

        struct PhaseOneJob
        {
            public PhaseOneJobType Type;
            public int BatchIndex; //Only used by batch reference update jobs.
            //Only used by body region copy.
            public int SourceSet;
            public int SourceStart;
            public int TargetStart;
            public int Count;
            //could union these, but it's preeeetty pointless.
        }
        enum PhaseTwoJobType
        {
            BroadPhase,
            CopyConstraintRegion,
        }
        struct PhaseTwoJob
        {
            public PhaseTwoJobType Type;
            public int SourceStart;
            public int TargetStart;
            public int Count;
            public int SourceSet;
            public int TypeId;
            public int Batch;
            public int SourceTypeBatch;
            public int TargetTypeBatch;
        }

        bool resetActivityStates;
        QuickList<int, Buffer<int>> uniqueSetIndices;
        QuickList<PhaseOneJob, Buffer<PhaseOneJob>> phaseOneJobs;
        QuickList<PhaseTwoJob, Buffer<PhaseTwoJob>> phaseTwoJobs;
        internal unsafe void ExecutePhaseOneJob(int index)
        {
            ref var job = ref phaseOneJobs[index];
            switch (job.Type)
            {
                case PhaseOneJobType.PairCache:
                    {
                        //Updating the pair cache is locally sequential because it modifies the global overlap mapping, which at the moment is a hash table.
                        //The other per-type caches could be separated from this job and handled in an internally multithreaded way, but that would add complexity that is likely unnecessary.
                        //We'll assume the other jobs can balance things out until proven otherwise.
                        for (int i = 0; i < uniqueSetIndices.Count; ++i)
                        {
                            pairCache.ActivateSet(uniqueSetIndices[i]);
                        }
                    }
                    break;
                case PhaseOneJobType.UpdateBatchReferencedHandles:
                    {
                        //Note that the narrow phase will schedule this job alongside another worker which reads existing batch referenced handles.
                        //There will be some cache line sharing sometimes. That's not wonderful for performance, but it should not cause bugs:
                        //1) The narrowphase's speculative batch search is readonly, so there are no competing writes.
                        //2) Activation requires that a new constraint has been created involving an inactive body.
                        //The speculative search will then try to find a batch for that constraint, and it may end up erroneously
                        //finding a batch slot that gets blocked by this activation procedure. 
                        //But the speculative process is *speculative*; it is fine for it to be wrong, so long as it isn't wrong in a way that makes it choose a higher batch index.

                        //Note that this is parallel over different batches, regardless of which source set they're from.
                        ref var targetBatchReferencedHandles = ref solver.batchReferencedHandles[job.BatchIndex];
                        for (int i = 0; i < uniqueSetIndices.Count; ++i)
                        {
                            var setIndex = uniqueSetIndices[i];
                            ref var sourceSet = ref solver.Sets[setIndex];
                            if (sourceSet.Batches.Count > job.BatchIndex)
                            {
                                ref var batch = ref sourceSet.Batches[job.BatchIndex];
                                for (int typeBatchIndex = 0; typeBatchIndex < batch.TypeBatches.Count; ++typeBatchIndex)
                                {
                                    ref var typeBatch = ref batch.TypeBatches[typeBatchIndex];
                                    solver.TypeProcessors[typeBatch.TypeId].AddInactiveBodyHandlesToBatchReferences(ref typeBatch, ref targetBatchReferencedHandles);
                                }
                            }
                        }
                    }
                    break;
                case PhaseOneJobType.CopyBodyRegion:
                    {
                        //Since we already preallocated everything during the job preparation, all we have to do is copy from the inactive set location.
                        ref var sourceSet = ref bodies.Sets[job.SourceSet];
                        ref var targetSet = ref bodies.ActiveSet;
                        sourceSet.Collidables.CopyTo(job.SourceStart, ref targetSet.Collidables, job.TargetStart, job.Count);
                        sourceSet.Constraints.CopyTo(job.SourceStart, ref targetSet.Constraints, job.TargetStart, job.Count);
                        sourceSet.LocalInertias.CopyTo(job.SourceStart, ref targetSet.LocalInertias, job.TargetStart, job.Count);
                        sourceSet.Poses.CopyTo(job.SourceStart, ref targetSet.Poses, job.TargetStart, job.Count);
                        sourceSet.Velocities.CopyTo(job.SourceStart, ref targetSet.Velocities, job.TargetStart, job.Count);
                        sourceSet.Activity.CopyTo(job.SourceStart, ref targetSet.Activity, job.TargetStart, job.Count);
                        if (resetActivityStates)
                        {
                            for (int targetIndex = job.TargetStart + job.Count - 1; targetIndex >= job.TargetStart; --targetIndex)
                            {
                                ref var targetActivity = ref targetSet.Activity[targetIndex];
                                targetActivity.TimestepsUnderThresholdCount = 0;
                                targetActivity.DeactivationCandidate = false;
                            }
                        }
                        sourceSet.IndexToHandle.CopyTo(job.SourceStart, ref targetSet.IndexToHandle, job.TargetStart, job.Count);
                        for (int i = 0; i < job.Count; ++i)
                        {
                            ref var bodyLocation = ref bodies.HandleToLocation[sourceSet.IndexToHandle[job.SourceStart + i]];
                            bodyLocation.SetIndex = 0;
                            bodyLocation.Index = job.TargetStart + i;
                        }
                    }
                    break;
            }
        }

        internal unsafe void ExecutePhaseTwoJob(int index)
        {
            ref var job = ref phaseTwoJobs[index];
            switch (job.Type)
            {
                case PhaseTwoJobType.BroadPhase:
                    {
                        //Note that the broad phase add/remove has a dependency on the body copies; that's why it's in the second phase.
                        //Also note that the broad phase update cannot be split into two parallel jobs because removals update broad phase indices of moved leaves,
                        //which in context might belong to activated collidables that have not yet been removed.
                        //If the indices are sorted, this could be avoided and this could be split into two jobs. Only bother if performance numbers suggest it.
                        ref var activeSet = ref bodies.ActiveSet;
                        for (int i = 0; i < uniqueSetIndices.Count; ++i)
                        {
                            ref var inactiveBodySet = ref bodies.Sets[uniqueSetIndices[i]];
                            for (int j = 0; j < inactiveBodySet.Count; ++j)
                            {
                                //Note that we have to go grab the active version of the collidable, since the active version is potentially modified by removals.
                                ref var bodyLocation = ref bodies.HandleToLocation[inactiveBodySet.IndexToHandle[j]];
                                Debug.Assert(bodyLocation.SetIndex == 0);
                                //The broad phase index value is currently the static index, either copied from the inactive set or modified by a removal below.
                                //We'll update it, so just take a reference.
                                ref var broadPhaseIndex = ref activeSet.Collidables[bodyLocation.Index].BroadPhaseIndex;
                                if (broadPhaseIndex >= 0)
                                {
                                    broadPhase.GetStaticBoundsPointers(broadPhaseIndex, out var minPointer, out var maxPointer);
                                    BoundingBox bounds;
                                    bounds.Min = *minPointer;
                                    bounds.Max = *maxPointer;
                                    var staticBroadPhaseIndexToRemove = broadPhaseIndex;
                                    broadPhaseIndex = broadPhase.AddActive(broadPhase.staticLeaves[broadPhaseIndex], ref bounds);

                                    if (broadPhase.RemoveStaticAt(staticBroadPhaseIndexToRemove, out var movedLeaf))
                                    {
                                        if (movedLeaf.Mobility == Collidables.CollidableMobility.Static)
                                        {
                                            statics.Collidables[statics.HandleToIndex[movedLeaf.Handle]].BroadPhaseIndex = staticBroadPhaseIndexToRemove;
                                        }
                                        else
                                        {
                                            //Note that the moved leaf cannot refer to one of the collidables that we've already moved into the active set, because all such collidables
                                            //have already been removed. 
                                            bodies.UpdateCollidableBroadPhaseIndex(movedLeaf.Handle, staticBroadPhaseIndexToRemove);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
                case PhaseTwoJobType.CopyConstraintRegion:
                    {
                        //Constraints are a little more complicated than bodies for a few reasons:
                        //1) Constraints are stored in AOSOA format. We cannot simply copy with bundle alignment with no preparation, since that may leave a gap.
                        //To avoid this issue, we instead use the last bundle of the source batch to pad out any incomplete bundles ahead of the target copy region.
                        //The scheduler attempts to maximize the number of jobs which are pure bundle copies.
                        //2) Inactive constraints store their body references as body *handles* rather than body indices.
                        //Pulling the type batches back into the active set requires translating those body handles to body indices.  
                        //3) The translation from body handle to body index requires that the bodies already have an active set identity, which is why the constraints wait until the second phase.
                        ref var sourceTypeBatch = ref solver.Sets[job.SourceSet].Batches[job.Batch].TypeBatches[job.SourceTypeBatch];
                        ref var targetTypeBatch = ref solver.ActiveSet.Batches[job.Batch].TypeBatches[job.TargetTypeBatch];
                        Debug.Assert(targetTypeBatch.TypeId == sourceTypeBatch.TypeId);
                        solver.TypeProcessors[job.TypeId].CopyInactiveToActive(
                            job.SourceSet, job.Batch, job.SourceTypeBatch, job.Batch, job.TargetTypeBatch,
                            job.SourceStart, job.TargetStart, job.Count, bodies, solver);
                    }
                    break;
            }

        }

        internal void AccumulateUniqueIndices(ref QuickList<int, Buffer<int>> candidateSetIndices, ref IndexSet uniqueSet, ref QuickList<int, Buffer<int>> uniqueSetIndices, BufferPool pool)
        {
            for (int i = 0; i < candidateSetIndices.Count; ++i)
            {
                var candidateSetIndex = candidateSetIndices[i];
                if (!uniqueSet.Contains(candidateSetIndex))
                {
                    uniqueSet.Add(candidateSetIndex, pool);
                    uniqueSetIndices.Add(candidateSetIndex, pool.SpecializeFor<int>());
                }
            }
        }
        [Conditional("DEBUG")]
        void ValidateInactiveSetIndex(int setIndex)
        {
            Debug.Assert(setIndex >= 1 && setIndex < bodies.Sets.Length && setIndex < solver.Sets.Length && setIndex < pairCache.InactiveSets.Length);
            //Note that pair cache sets are not guaranteed to be allocated if there are no pairs, and solver sets are not guaranteed to exist if there are no constraints.
            Debug.Assert(bodies.Sets[setIndex].Allocated);
        }
        [Conditional("DEBUG")]
        void ValidateUniqueSets(ref QuickList<int, Buffer<int>> setIndices)
        {
            var set = new IndexSet(pool, bodies.Sets.Length);
            for (int i = 0; i < setIndices.Count; ++i)
            {
                var setIndex = setIndices[i];
                ValidateInactiveSetIndex(setIndex);
                Debug.Assert(!set.Contains(setIndex));
                set.Add(setIndex, pool);
            }
            set.Dispose(pool);

        }

        //This is getting into the realm of Fizzbuzz Enterprise. 
        interface ITypeCount
        {
            void Add<T>(T other) where T : ITypeCount;
        }
        struct ConstraintCount : ITypeCount
        {
            public int Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(T other) where T : ITypeCount
            {
                Debug.Assert(typeof(T) == typeof(ConstraintCount));
                Count += Unsafe.As<T, ConstraintCount>(ref other).Count;
            }
        }
        struct PairCacheCount : ITypeCount
        {
            public int ElementSizeInBytes;
            public int ByteCount;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add<T>(T other) where T : ITypeCount
            {
                Debug.Assert(typeof(T) == typeof(PairCacheCount));
                ref var pairCacheOther = ref Unsafe.As<T, PairCacheCount>(ref other);
                Debug.Assert(ElementSizeInBytes == 0 || ElementSizeInBytes == pairCacheOther.ElementSizeInBytes);
                ElementSizeInBytes = pairCacheOther.ElementSizeInBytes;
                ByteCount += pairCacheOther.ByteCount;
            }
        }

        struct TypeAllocationSizes<T> where T : ITypeCount
        {
            public Buffer<T> TypeCounts;
            public int HighestOccupiedTypeIndex;
            public TypeAllocationSizes(BufferPool pool, int maximumTypeCount)
            {
                pool.SpecializeFor<T>().Take(maximumTypeCount, out TypeCounts);
                TypeCounts.Clear(0, maximumTypeCount);
                HighestOccupiedTypeIndex = 0;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(int typeId, T typeCount)
            {
                TypeCounts[typeId].Add(typeCount);
                if (typeId > HighestOccupiedTypeIndex)
                    HighestOccupiedTypeIndex = typeId;
            }
            public void Dispose(BufferPool pool)
            {
                pool.SpecializeFor<T>().Return(ref TypeCounts);
            }
        }


        internal (int phaseOneJobCount, int phaseTwoJobCount) PrepareJobs(ref QuickList<int, Buffer<int>> setIndices, bool resetActivityStates, int threadCount)
        {
            ValidateUniqueSets(ref setIndices);
            this.uniqueSetIndices = setIndices;
            this.resetActivityStates = resetActivityStates;

            //We have three main jobs in this function:
            //1) Ensure that the active set in the bodies, solver, and pair cache can hold the newly activated islands without resizing or accessing a buffer pool.
            //2) Allocate space in the active set for the bodies and constraints.
            //(Pair caches are left to be handled in a locally sequential task right now due to the overlap mapping being global.
            //The type caches could be updated in parallel, but we just didn't split it out. You can consider doing that if there appears to be a reason to do so.)
            //3) Schedule actual jobs for the two work phases.

            int newBodyCount = 0;
            int highestNewBatchCount = 0;
            int highestRequiredTypeCapacity = 0;
            for (int i = 0; i < setIndices.Count; ++i)
            {
                var setIndex = setIndices[i];
                newBodyCount += bodies.Sets[setIndex].Count;
                var setBatchCount = solver.Sets[setIndex].Batches.Count;
                if (highestNewBatchCount < setBatchCount)
                    highestNewBatchCount = setBatchCount;
                ref var constraintSet = ref solver.Sets[setIndex];
                for (int batchIndex = 0; batchIndex < constraintSet.Batches.Count; ++batchIndex)
                {
                    ref var batch = ref constraintSet.Batches[batchIndex];
                    for (int typeBatchIndex = 0; typeBatchIndex < batch.TypeBatches.Count; ++typeBatchIndex)
                    {
                        ref var typeBatch = ref batch.TypeBatches[typeBatchIndex];
                        if (highestRequiredTypeCapacity < typeBatch.TypeId)
                            highestRequiredTypeCapacity = typeBatch.TypeId;
                    }
                }

            }
            //We accumulated indices above; add one to get the capacity requirement.
            ++highestRequiredTypeCapacity;
            pool.SpecializeFor<TypeAllocationSizes<ConstraintCount>>().Take(highestNewBatchCount, out var constraintCountPerTypePerBatch);
            for (int batchIndex = 0; batchIndex < highestNewBatchCount; ++batchIndex)
            {
                constraintCountPerTypePerBatch[batchIndex] = new TypeAllocationSizes<ConstraintCount>(pool, highestRequiredTypeCapacity);
            }
            var narrowPhaseConstraintCaches = new TypeAllocationSizes<PairCacheCount>(pool, PairCache.CollisionConstraintTypeCount);
            var narrowPhaseCollisionCaches = new TypeAllocationSizes<PairCacheCount>(pool, PairCache.CollisionTypeCount);

            void AccumulatePairCacheTypeCounts(ref Buffer<InactiveCache> sourceTypeCaches, ref TypeAllocationSizes<PairCacheCount> counts)
            {
                for (int j = 0; j < sourceTypeCaches.Length; ++j)
                {
                    ref var sourceCache = ref sourceTypeCaches[j];
                    if (sourceCache.List.Buffer.Allocated)
                        counts.Add(sourceCache.TypeId, new PairCacheCount { ByteCount = sourceCache.List.ByteCount, ElementSizeInBytes = sourceCache.List.ElementSizeInBytes });
                    else
                        break; //Encountering an unallocated slot is a termination condition. Used instead of explicitly storing cache counts, which are only rarely useful.
                }
            }
            int newPairCount = 0;
            for (int i = 0; i < setIndices.Count; ++i)
            {
                var setIndex = setIndices[i];
                ref var constraintSet = ref solver.Sets[setIndex];
                for (int batchIndex = 0; batchIndex < constraintSet.Batches.Count; ++batchIndex)
                {
                    ref var constraintCountPerType = ref constraintCountPerTypePerBatch[batchIndex];
                    ref var batch = ref constraintSet.Batches[batchIndex];
                    for (int typeBatchIndex = 0; typeBatchIndex < batch.TypeBatches.Count; ++typeBatchIndex)
                    {
                        ref var typeBatch = ref batch.TypeBatches[typeBatchIndex];
                        constraintCountPerType.Add(typeBatch.TypeId, new ConstraintCount { Count = typeBatch.ConstraintCount });
                    }
                }

                ref var sourceSet = ref pairCache.InactiveSets[setIndex];
                newPairCount += sourceSet.Pairs.Count;
                AccumulatePairCacheTypeCounts(ref sourceSet.ConstraintCaches, ref narrowPhaseConstraintCaches);
                AccumulatePairCacheTypeCounts(ref sourceSet.CollisionCaches, ref narrowPhaseCollisionCaches);
            }

            //We now know how many new bodies, constraint batch entries, and pair cache entries are going to be added.
            //Ensure capacities on all systems:
            //bodies,
            bodies.EnsureCapacity(bodies.ActiveSet.Count + newBodyCount);
            //broad bphase, (technically overestimating, not every body has a collidable, but vast majority do and shrug)
            broadPhase.EnsureCapacity(broadPhase.ActiveTree.LeafCount + newBodyCount, broadPhase.StaticTree.LeafCount);
            //constraints,
            solver.ActiveSet.Batches.EnsureCapacity(highestNewBatchCount, pool.SpecializeFor<ConstraintBatch>());
            solver.batchReferencedHandles.EnsureCapacity(highestNewBatchCount, pool.SpecializeFor<IndexSet>());
            for (int batchIndex = solver.ActiveSet.Batches.Count; batchIndex < highestNewBatchCount; ++batchIndex)
            {
                solver.ActiveSet.Batches.AllocateUnsafely() = new ConstraintBatch(pool);
                solver.batchReferencedHandles.AllocateUnsafely() = new IndexSet(pool, bodies.HandlePool.HighestPossiblyClaimedId + 1);
            }
            for (int batchIndex = 0; batchIndex < highestNewBatchCount; ++batchIndex)
            {
                ref var constraintCountPerType = ref constraintCountPerTypePerBatch[batchIndex];
                ref var batch = ref solver.ActiveSet.Batches[batchIndex];
                batch.EnsureTypeMapSize(pool, constraintCountPerType.HighestOccupiedTypeIndex);
                for (int typeId = 0; typeId <= constraintCountPerType.HighestOccupiedTypeIndex; ++typeId)
                {
                    var countForType = constraintCountPerType.TypeCounts[typeId].Count;
                    if (countForType > 0)
                    {
                        var typeProcessor = solver.TypeProcessors[typeId];
                        ref var typeBatch = ref batch.GetOrCreateTypeBatch(typeId, typeProcessor, countForType, pool);
                        var targetCapacity = countForType + typeBatch.ConstraintCount;
                        if (targetCapacity > typeBatch.IndexToHandle.Length)
                        {
                            typeProcessor.Resize(ref typeBatch, targetCapacity, pool);
                        }
                    }
                }
            }
            //and narrow phase pair caches.
            ref var targetPairCache = ref pairCache.GetCacheForActivation();
            void EnsurePairCacheTypeCapacities(ref TypeAllocationSizes<PairCacheCount> cacheSizes, ref Buffer<UntypedList> targetCaches, BufferPool cachePool)
            {
                for (int typeIndex = 0; typeIndex <= cacheSizes.HighestOccupiedTypeIndex; ++typeIndex)
                {
                    ref var pairCacheCount = ref cacheSizes.TypeCounts[typeIndex];
                    if (pairCacheCount.ByteCount > 0)
                    {
                        ref var targetSubCache = ref targetCaches[typeIndex];
                        targetSubCache.EnsureCapacityInBytes(pairCacheCount.ElementSizeInBytes, targetSubCache.ByteCount + pairCacheCount.ByteCount, cachePool);
                    }
                }
            }
            EnsurePairCacheTypeCapacities(ref narrowPhaseConstraintCaches, ref targetPairCache.constraintCaches, targetPairCache.pool);
            EnsurePairCacheTypeCapacities(ref narrowPhaseCollisionCaches, ref targetPairCache.collisionCaches, targetPairCache.pool);
            pairCache.Mapping.EnsureCapacity(pairCache.Mapping.Count + newPairCount,
                pool.SpecializeFor<CollidablePair>(), pool.SpecializeFor<CollidablePairPointers>(), pool.SpecializeFor<int>());

            var phaseOneJobPool = pool.SpecializeFor<PhaseOneJob>();
            var phaseTwoJobPool = pool.SpecializeFor<PhaseTwoJob>();
            QuickList<PhaseOneJob, Buffer<PhaseOneJob>>.Create(phaseOneJobPool, Math.Max(32, highestNewBatchCount + 1), out phaseOneJobs);
            QuickList<PhaseTwoJob, Buffer<PhaseTwoJob>>.Create(phaseTwoJobPool, 32, out phaseTwoJobs);
            //Finally, create actual jobs. Note that this involves actually allocating space in the bodies set and in type batches for the workers to fill in.
            //(Pair caches are currently handled in a locally sequential way and do not require preallocation.)

            phaseOneJobs.AllocateUnsafely() = new PhaseOneJob { Type = PhaseOneJobType.PairCache };
            for (int batchIndex = 0; batchIndex < highestNewBatchCount; ++batchIndex)
            {
                phaseOneJobs.AllocateUnsafely() = new PhaseOneJob { Type = PhaseOneJobType.UpdateBatchReferencedHandles, BatchIndex = batchIndex };
            }
            phaseTwoJobs.AllocateUnsafely() = new PhaseTwoJob { Type = PhaseTwoJobType.BroadPhase };

            ref var activeBodySet = ref bodies.ActiveSet;
            ref var activeSolverSet = ref solver.ActiveSet;
            //TODO: The job sizes are a little goofy for single threaded execution. Easy enough to resolve with special case or dynamic size.
            for (int i = 0; i < uniqueSetIndices.Count; ++i)
            {
                var sourceSetIndex = uniqueSetIndices[i];
                {
                    const int bodyJobSize = 64;
                    ref var sourceSet = ref bodies.Sets[sourceSetIndex];
                    var setJobCount = Math.Max(1, sourceSet.Count / bodyJobSize);
                    var baseBodiesPerJob = sourceSet.Count / setJobCount;
                    var remainder = sourceSet.Count - baseBodiesPerJob * setJobCount;
                    phaseOneJobs.EnsureCapacity(phaseOneJobs.Count + setJobCount, phaseOneJobPool);
                    var previousSourceEnd = 0;
                    for (int jobIndex = 0; jobIndex < setJobCount; ++jobIndex)
                    {
                        ref var job = ref phaseOneJobs.AllocateUnsafely();
                        job.Type = PhaseOneJobType.CopyBodyRegion;
                        job.SourceSet = sourceSetIndex;
                        job.SourceStart = previousSourceEnd;
                        job.TargetStart = activeBodySet.Count;
                        job.Count = jobIndex >= remainder ? baseBodiesPerJob : baseBodiesPerJob + 1;
                        previousSourceEnd += job.Count;
                        activeBodySet.Count += job.Count;
                    }
                    Debug.Assert(previousSourceEnd == sourceSet.Count);
                    Debug.Assert(activeBodySet.Count <= activeBodySet.IndexToHandle.Length);
                }
                {
                    const int constraintJobSize = 32;
                    ref var sourceSet = ref solver.Sets[sourceSetIndex];
                    for (int batchIndex = 0; batchIndex < sourceSet.Batches.Count; ++batchIndex)
                    {
                        ref var sourceBatch = ref sourceSet.Batches[batchIndex];
                        ref var targetBatch = ref activeSolverSet.Batches[batchIndex];
                        for (int sourceTypeBatchIndex = 0; sourceTypeBatchIndex < sourceBatch.TypeBatches.Count; ++sourceTypeBatchIndex)
                        {
                            ref var sourceTypeBatch = ref sourceBatch.TypeBatches[sourceTypeBatchIndex];
                            var targetTypeBatchIndex = targetBatch.TypeIndexToTypeBatchIndex[sourceTypeBatch.TypeId];
                            ref var targetTypeBatch = ref targetBatch.TypeBatches[targetTypeBatchIndex];
                            //TODO: It would be nice to be a little more clever about scheduling start and end points for the sake of avoiding partial bundles.
                            var jobCount = Math.Max(1, sourceTypeBatch.ConstraintCount / constraintJobSize);
                            var baseConstraintsPerJob = sourceTypeBatch.ConstraintCount / jobCount;
                            var remainder = sourceTypeBatch.ConstraintCount - baseConstraintsPerJob * jobCount;
                            phaseTwoJobs.EnsureCapacity(phaseTwoJobs.Count + jobCount, phaseTwoJobPool);

                            var previousSourceEnd = 0;
                            for (int jobIndex = 0; jobIndex < jobCount; ++jobIndex)
                            {
                                ref var job = ref phaseTwoJobs.AllocateUnsafely();
                                job.Type = PhaseTwoJobType.CopyConstraintRegion;
                                job.TypeId = sourceTypeBatch.TypeId;
                                job.Batch = batchIndex;
                                job.SourceSet = sourceSetIndex;
                                job.SourceTypeBatch = sourceTypeBatchIndex;
                                job.TargetTypeBatch = targetTypeBatchIndex;
                                job.Count = jobIndex >= remainder ? baseConstraintsPerJob : baseConstraintsPerJob + 1;
                                job.SourceStart = previousSourceEnd;
                                job.TargetStart = targetTypeBatch.ConstraintCount;
                                previousSourceEnd += job.Count;
                                targetTypeBatch.ConstraintCount += job.Count;
                            }
                            Debug.Assert(previousSourceEnd == sourceTypeBatch.ConstraintCount);
                            Debug.Assert(targetTypeBatch.ConstraintCount <= targetTypeBatch.IndexToHandle.Length);
                        }
                    }
                }
            }

            return (phaseOneJobs.Count, phaseTwoJobs.Count);
        }


        internal void DisposeForCompletedActivations(ref QuickList<int, Buffer<int>> setIndices)
        {
            for (int i = 0; i < setIndices.Count; ++i)
            {
                var setIndex = setIndices[i];
                ref var bodySet = ref bodies.Sets[setIndex];
                //Note that neither the constraint set nor the pair cache set necessarily exist. It is possible for bodies to go inactive by themselves.
                ref var constraintSet = ref solver.Sets[setIndex];
                ref var pairCacheSet = ref pairCache.InactiveSets[setIndex];
                Debug.Assert(bodySet.Allocated);
                bodySet.DisposeBuffers(pool);
                if (constraintSet.Allocated)
                    constraintSet.Dispose(pool);
                if (pairCacheSet.Allocated)
                    pairCacheSet.Dispose(pool);
                this.uniqueSetIndices = new QuickList<int, Buffer<int>>();
                deactivator.ReturnSetId(setIndex);
            }
            phaseOneJobs.Dispose(pool.SpecializeFor<PhaseOneJob>());
            phaseTwoJobs.Dispose(pool.SpecializeFor<PhaseTwoJob>());
        }
    }
}
