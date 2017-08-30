using System;
using System.Collections.Generic;
using BulletSharp;
using BulletSharp.Math;
using LibMMD.Model;
using LibMMD.Motion;
using UnityEngine;
using CollisionFlags = BulletSharp.CollisionFlags;
using MathUtil = LibMMD.Util.MathUtil;
using Vector3 = BulletSharp.Math.Vector3;

#if MMD_PHYSICS_DEBUG
using LibMMDDebug;
#endif


namespace LibMMD.Pyhsics
{
    public class BulletPyhsicsReactor : PyhsicsReactor
    {
#if MMD_PHYSICS_DEBUG
        private UnityEngine.Material mat = new UnityEngine.Material(Shader.Find("Standard"));
#endif
        private static readonly BoneImage NullBoneImage = new BoneImage();
        private readonly DiscreteDynamicsWorld _world;

        private readonly RigidBody _groundRigidBody;

        private Vector3 _gravityDirection;
        private float _gravityStrength;

        private bool _hasFloor;

        private readonly Dictionary<Poser, List<PoserMotionState>> _motionStates =
            new Dictionary<Poser, List<PoserMotionState>>();

        private readonly Dictionary<Poser, List<RigidBody>> _rigidBodies = new Dictionary<Poser, List<RigidBody>>();

        private readonly Dictionary<Poser, List<Generic6DofSpringConstraint>> _constraints =
            new Dictionary<Poser, List<Generic6DofSpringConstraint>>();


        public BulletPyhsicsReactor()
        {
            _hasFloor = true;
            var configuration = new DefaultCollisionConfiguration();
            var dispatcher = new CollisionDispatcher(configuration);
            BroadphaseInterface broadphase = new DbvtBroadphase();
            var solver = new SequentialImpulseConstraintSolver();
            _world = new DiscreteDynamicsWorld(dispatcher, broadphase, solver, configuration);

            Vector3 gravity;
            _world.GetGravity(out gravity);
            _gravityDirection = new Vector3(0.0f, -1.0f, 0.0f);
            _gravityStrength = gravity.Length;
            var gravityToSet =
                _gravityDirection * gravity.Length *
                10.0f; // the world is scaled by 10, since MikuMikuDance is using 0.1m as its unit.

            _world.SetGravity(ref gravityToSet);
            CollisionShape groundShape = new StaticPlaneShape(new Vector3(0.0f, 1.0f, 0.0f), 0.0f);
            MotionState groundState = new DefaultMotionState();
            var info = new RigidBodyConstructionInfo(0.0f, groundState, groundShape,
                new Vector3(0.0f, 0.0f, 0.0f));
            info.LinearDamping = 0.0f;
            info.AngularDamping = 0.0f;
            info.Restitution = 0.0f;
            info.Friction = 0.265f;

            _groundRigidBody = new RigidBody(info);
            _groundRigidBody.CollisionFlags = _groundRigidBody.CollisionFlags | CollisionFlags.KinematicObject;
            _groundRigidBody.ActivationState = ActivationState.DisableDeactivation;

            _world.AddRigidBody(_groundRigidBody);
        }


        public override void AddPoser(Poser poser)
        {
            var model = poser.Model;
            if (_rigidBodies.ContainsKey(poser))
            {
                return;
            }
            poser.ResetPosing();
            var motionStates = new List<PoserMotionState>();
            _motionStates.Add(poser, motionStates);
            var rigidBodies = new List<RigidBody>();
            _rigidBodies.Add(poser, rigidBodies);
            var constraints = new List<Generic6DofSpringConstraint>();
            _constraints.Add(poser, constraints);

            foreach (var body in model.Rigidbodies)
            {
                var bodyDimension = body.Dimemsions;

                CollisionShape btShape = null;

                var btMass = 0.0f;
                var btLocalInertia = new Vector3(0.0f, 0.0f, 0.0f);

                switch (body.Shape)
                {
                    case MmdRigidBody.RigidBodyShape.RigidShapeSphere:
                        btShape = new SphereShape(bodyDimension.x);
                        break;
                    case MmdRigidBody.RigidBodyShape.RigidShapeBox:
                        btShape = new BoxShape(new Vector3(bodyDimension.x, bodyDimension.y,
                            bodyDimension.z));
                        break;
                    case MmdRigidBody.RigidBodyShape.RigidShapeCapsule:
                        btShape = new CapsuleShape(bodyDimension.x, bodyDimension.y);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (body.Type != MmdRigidBody.RigidBodyType.RigidTypeKinematic)
                {
                    btMass = body.Mass;
                    btShape.CalculateLocalInertia(btMass, out btLocalInertia);
                }

                var bodyTransform = MathUtil.QuaternionToMatrix4X4(MathUtil.YxzToQuaternion(body.Rotation));
                MathUtil.SetTransToMatrix4X4(body.Position, ref bodyTransform);
                var btBodyTransform = new Matrix();
                MathUtil.UnityMatrixToBulletMatrix(bodyTransform, ref btBodyTransform);

                var btMotionState = new PoserMotionState(poser, body, btBodyTransform);
                var btInfo =
                    new RigidBodyConstructionInfo(btMass, btMotionState, btShape, btLocalInertia)
                    {
                        LinearDamping = body.TranslateDamp,
                        AngularDamping = body.RotateDamp,
                        Restitution = body.Restitution,
                        Friction = body.Friction
                    };

                var btRigidBody = new RigidBody(btInfo) {ActivationState = ActivationState.DisableDeactivation};

                if (body.Type == MmdRigidBody.RigidBodyType.RigidTypeKinematic)
                {
                    btRigidBody.CollisionFlags = btRigidBody.CollisionFlags | CollisionFlags.KinematicObject;
                }
                _world.AddRigidBody(btRigidBody, (short) (1 << body.CollisionGroup), (short) body.CollisionMask);
#if MMD_PHYSICS_DEBUG
                CreateUnityCollisionObjectProxy(btRigidBody, body.Name);
#endif
                motionStates.Add(btMotionState);
                rigidBodies.Add(btRigidBody);
            }

            foreach (var constraint in model.Constraints)
            {
                var btBody1 = rigidBodies[constraint.AssociatedRigidBodyIndex[0]];
                var btBody2 = rigidBodies[constraint.AssociatedRigidBodyIndex[1]];

                var positionLowLimit = constraint.PositionLowLimit;
                var positionHiLimit = constraint.PositionHiLimit;
                var rotationLoLimit = constraint.RotationLowLimit;
                var rotationHiLimit = constraint.RotationHiLimit;

                var constraintTransform = MathUtil.QuaternionToMatrix4X4(MathUtil.YxzToQuaternion(constraint.Rotation));
                MathUtil.SetTransToMatrix4X4(constraint.Position, ref constraintTransform);
                
                var btConstraintTransform = new Matrix();
                MathUtil.UnityMatrixToBulletMatrix(constraintTransform, ref btConstraintTransform);
                
                var btLocalizationTransform1 =  btConstraintTransform * Matrix.Invert(btBody1.WorldTransform); //TODO 验证这个和mmdlib里算出来的是否一样
                var btLocalizationTransform2 =  btConstraintTransform * Matrix.Invert(btBody2.WorldTransform);

                var btConstraint = new Generic6DofSpringConstraint(btBody1, btBody2, btLocalizationTransform1,
                    btLocalizationTransform2, true)
                {
                    LinearLowerLimit =
                        new Vector3(positionLowLimit.x, positionLowLimit.y, positionLowLimit.z),
                    LinearUpperLimit =
                        new Vector3(positionHiLimit.x, positionHiLimit.y, positionHiLimit.z),
                    AngularLowerLimit =
                        new Vector3(rotationLoLimit.x, rotationLoLimit.y, rotationLoLimit.z),
                    AngularUpperLimit =
                        new Vector3(rotationHiLimit.x, rotationHiLimit.y, rotationHiLimit.z)
                };

                for (var j = 0; j < 3; ++j)
                {
                    btConstraint.SetStiffness(j, constraint.SpringTranslate[j]);
                    btConstraint.EnableSpring(j, true);
                    btConstraint.SetStiffness(j+3, constraint.SpringRotate[j]);
                    btConstraint.EnableSpring(j+3, true);
                }
                
                _world.AddConstraint(btConstraint);
                constraints.Add(btConstraint);
            }
        }
        
#if MMD_PHYSICS_DEBUG

        public GameObject CreateUnityCollisionObjectProxy(CollisionObject body, string name) {
            if (body is GhostObject)
            {
                Debug.Log("ghost obj");
            }
            GameObject go = new GameObject(name);
            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh m = mf.mesh;
            MeshFactory2.CreateShape(body.CollisionShape, m);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            BulletRigidBodyProxy rbp = go.AddComponent<BulletRigidBodyProxy>();
            rbp.target = body;
            return go;
        }
#endif
        
        public override void RemovePoser(Poser poser)
        {
            if (!_rigidBodies.ContainsKey(poser))
            {
                return;
            }

            var constraints = _constraints[poser];
            var rigidBodies = _rigidBodies[poser];

            foreach (var constraint in constraints)
            {
                _world.RemoveConstraint(constraint);
            }
            foreach (var rigidBody in rigidBodies)
            {
                _world.RemoveRigidBody(rigidBody);
            }
            _constraints.Remove(poser);
            _rigidBodies.Remove(poser);
            _motionStates.Remove(poser);
        }

        public override void Reset()
        {
            foreach (var entry in _motionStates)
            {
                var poserMotionStates = entry.Value;
                foreach (var motionState in poserMotionStates)
                {
                    motionState.Reset();
                }
            }
            foreach (var entry in _rigidBodies)
            {
                var poserRigidBodies = entry.Value;
                foreach (var rigidBody in poserRigidBodies)
                {
                    Matrix transform;
                    rigidBody.MotionState.GetWorldTransform(out transform);
                    rigidBody.CenterOfMassTransform = transform;
                    rigidBody.InterpolationWorldTransform = transform;
                    rigidBody.AngularVelocity = Vector3.Zero;
                    rigidBody.InterpolationAngularVelocity = Vector3.Zero;
                    rigidBody.LinearVelocity = Vector3.Zero;
                    rigidBody.InterpolationLinearVelocity = Vector3.Zero;
                    rigidBody.ClearForces();
                }
            }
            _world.Broadphase.ResetPool(_world.Dispatcher);
            _world.ConstraintSolver.Reset();
        }

        public override void React(float step, int maxSubSteps = 10 , float fixStepTime = 1.0f / 60.0f)
        {
            _world.StepSimulation(step, maxSubSteps, fixStepTime);
            foreach (var entry in _motionStates)
            {
                var poserMotionStates = entry.Value;
                foreach (var motionState in poserMotionStates)
                {
                    motionState.Synchronize();
                }
                foreach (var motionState in poserMotionStates)
                {
                    motionState.Fix();
                }
            }
        }

        public override void SetGravityStrength(float strength)
        {
            _gravityStrength = strength;
            var gravity = _gravityDirection * _gravityStrength * 10.0f;
            _world.SetGravity(ref gravity);
        }

        public override void SetGravityDirection(Vector3 direction)
        {
            var d = Vector3.Normalize(direction);
            _gravityDirection = d;
            var gravity = _gravityDirection * _gravityStrength * 10.0f;
            _world.SetGravity(ref gravity);            
        }

        public override float GetGravityStrength()
        {
            return _gravityStrength;
        }

        public override Vector3 GetGravityDirection()
        {
            return _gravityDirection;
        }

        public override void SetFloor(bool hasFloor)
        {
            if (hasFloor == _hasFloor)
            {
                return;
            }
            _hasFloor = hasFloor;
            if (_hasFloor)
            {
                _world.AddRigidBody(_groundRigidBody);
            }
            else
            {
                _world.RemoveRigidBody(_groundRigidBody);
            }
        }

        public override bool IsHasFloor()
        {
            return _hasFloor;
        }


        public static BoneImage GetPoserBoneImage(Poser poser, int index)
        {
            return index >= poser.BoneImages.Length ? NullBoneImage : poser.BoneImages[index];
        }
    }
}