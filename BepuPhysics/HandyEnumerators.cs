﻿using BepuPhysics.Constraints;
using BepuUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BepuPhysics
{
    /// <summary>
    /// Collects body handles associated with an active constraint.
    /// </summary>
    public unsafe struct ActiveConstraintBodyHandleCollector : IForEach<int> 
    {
        public Bodies Bodies;
        public int* Handles;
        public int Index;

        public ActiveConstraintBodyHandleCollector(Bodies bodies, int* handles)
        {
            Bodies = bodies;
            Handles = handles;
            Index = 0;
        }

        public void LoopBody(int bodyIndex)
        {
            Handles[Index++] = Bodies.ActiveSet.IndexToHandle[bodyIndex];
        }
    }
    /// <summary>
    /// Collects body references associated with a constraint. If the constraint is active, the references are in the form of indices into the active bodies set.
    /// If the constraint is inactive, the references are in the form of body handles.
    /// </summary>
    public unsafe struct ConstraintReferenceCollector : IForEach<int>
    {
        public int* References;
        public int Index;

        public ConstraintReferenceCollector(int* references)
        {
            References = references;
            Index = 0;
        }

        public void LoopBody(int reference)
        {
            References[Index++] = reference;
        }
    }
}
