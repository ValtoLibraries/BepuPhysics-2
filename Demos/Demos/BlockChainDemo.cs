﻿using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using DemoRenderer;
using DemoRenderer.UI;
using DemoUtilities;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Demos.Demos
{
    /// <summary>
    /// Shows how to build a decentralized ledger of transactions out of a sequence of connected blocks which could be used to revolutionize the backbone of finance while offering
    /// a new form of trust management that can be applied to a wide range of industry problems to achieve a more #secure and sustainable network of verifiable #future connections
    /// on which the accelerated development of #blockchain #solutions on the #cloud to magnify your business impact to imagine what could be achieve more by taking advantage of 
    /// truly #BigData with modern analytics augmented by #blockchain #technology #to make some boxes act like a multipendulum.
    /// </summary>
    public class BlockChainDemo : Demo
    {
        public unsafe override void Initialize(Camera camera)
        {
            camera.Position = new Vector3(-30, 8, -60);
            camera.Yaw = MathHelper.Pi * 3f / 4;

            Simulation = Simulation.Create(BufferPool, new TestCallbacks());
            Simulation.PoseIntegrator.Gravity = new Vector3(0, -10, 0);

            var boxShape = new Box(1, 1, 1);
            boxShape.ComputeInertia(1, out var boxInertia);
            var boxIndex = Simulation.Shapes.Add(ref boxShape);
            const int forkCount = 20;
            const int blocksPerChain = 20;
            int[] blockHandles = new int[blocksPerChain];
            for (int forkIndex = 0; forkIndex < forkCount; ++forkIndex)
            {
                //Build the blocks.
                for (int blockIndex = 0; blockIndex < blocksPerChain; ++blockIndex)
                {
                    var bodyDescription = new BodyDescription
                    {
                        //Make the uppermost block kinematic to hold up the rest of the chain.
                        LocalInertia = blockIndex == blocksPerChain - 1 ? new BodyInertia() : boxInertia,
                        Pose = new RigidPose
                        {
                            Position = new Vector3(0,
                                5 + blockIndex * (boxShape.Height + 1),
                                (forkIndex - forkCount * 0.5f) * (boxShape.Length + 4)),
                            Orientation = BepuUtilities.Quaternion.Identity
                        },
                        Activity = new BodyActivityDescription { MinimumTimestepCountUnderThreshold = 32, SleepThreshold = .01f },
                        Collidable = new CollidableDescription { Shape = boxIndex, SpeculativeMargin = .1f },
                        Velocity = new BodyVelocity { Linear = blockIndex == blocksPerChain - 1 ? new Vector3() : new Vector3(0, -1, 0) }
                    };
                    blockHandles[blockIndex] = Simulation.Bodies.Add(ref bodyDescription);
                }
                //Build the chains.
                for (int i = 1; i < blocksPerChain; ++i)
                {
                    var ballSocket = new BallSocket
                    {
                        LocalOffsetA = new Vector3(0, 1f, 0),
                        LocalOffsetB = new Vector3(0, -1f, 0),
                        SpringSettings = new SpringSettings(30, 1)
                    };
                    Simulation.Solver.Add(blockHandles[i - 1], blockHandles[i], ref ballSocket);
                }
            }

            var staticShape = new Box(200, 1, 200);
            var staticShapeIndex = Simulation.Shapes.Add(ref staticShape);

            var staticDescription = new StaticDescription
            {
                Collidable = new CollidableDescription
                {
                    Continuity = new ContinuousDetectionSettings { Mode = ContinuousDetectionMode.Discrete },
                    Shape = staticShapeIndex,
                    SpeculativeMargin = 0.1f
                },
                Pose = new RigidPose
                {
                    Position = new Vector3(1, -0.5f, 1),
                    Orientation = BepuUtilities.Quaternion.Identity
                }
            };
            Simulation.Statics.Add(ref staticDescription);

            //Build the coin description for the ponz-I mean ICO.
            var coinShape = new Sphere(1f); //TODO: Obviously, when cylinders get added, this needs to be changed.
            coinShape.ComputeInertia(1, out var coinInertia);
            coinDescription = new BodyDescription
            {
                LocalInertia = coinInertia,
                Pose = new RigidPose
                {
                    Orientation = BepuUtilities.Quaternion.Identity
                },
                Activity = new BodyActivityDescription { MinimumTimestepCountUnderThreshold = 32, SleepThreshold = .01f },
                Collidable = new CollidableDescription { Shape = Simulation.Shapes.Add(ref coinShape), SpeculativeMargin = .1f },
            };
        }

        BodyDescription coinDescription;
        Random random = new Random(5);
        public override void Update(Input input, float dt)
        {
            if (input.WasPushed(OpenTK.Input.Key.Q))
            {
                //INVEST TODAY FOR INCREDIBLE RETURNS DON'T MISS OUT LOOK AT THE COINS THERE ARE A LOT OF THEM AND THEY COULD BE YOURS
                var origin = new Vector3(-30, 5, -30) + new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()) * new Vector3(60, 30, 60);
                for (int i = 0; i < 250; ++i)
                {
                    var direction = new Vector3(-1) + 2 * new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                    var length = direction.Length();
                    if (length > 1e-7f)
                        direction /= length;
                    else
                        direction = new Vector3(0, 1, 0);

                    coinDescription.Pose.Position = origin + direction * 10 * (float)random.NextDouble();
                    coinDescription.Velocity.Linear = direction * (5 + 30 * (float)random.NextDouble());
                    Simulation.Bodies.Add(ref coinDescription);
                }
            }
            base.Update(input, dt);
        }

        public override void Render(Renderer renderer, TextBuilder text, Font font)
        {
            text.Clear().Append("Press Q to create an ICO.");
            renderer.TextBatcher.Write(text, new Vector2(20, renderer.Surface.Resolution.Y - 20), 16, new Vector3(1, 1, 1), font);
            base.Render(renderer, text, font);
        }

    }
}
