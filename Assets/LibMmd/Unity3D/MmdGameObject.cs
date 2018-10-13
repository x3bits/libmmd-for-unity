using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibMMD.Model;
using LibMMD.Motion;
using LibMMD.Pyhsics;
using LibMMD.Reader;
using LibMMD.Unity3D.BonePose;
using LibMMD.Util;
using UnityEngine;

namespace LibMMD.Unity3D
{
    public class MmdGameObject : MonoBehaviour
    {
        private static readonly BonePoseCalculatorWorker BonePoseCalculatorWorker = new BonePoseCalculatorWorker();
        
        public enum MmdEvent
        {
            SlowPoseCalculation
        }
        public delegate void MmdEventDelegate(MmdEvent mmdEvent);
        public bool AutoPhysicsStepLength = true;
        public bool Playing;
        public int PhysicsCacheFrameSize = 300;
        public PhysicsModeEnum PhysicsMode = PhysicsModeEnum.Bullet;
        public float PhysicsFps = 120.0f;
        
        public MmdEventDelegate OnMmdEvent { get; set; }

        public string ModelName
        {
            get { return _model.Name; }
        }

        public string ModelPath { get; private set; }
        public string MotionPath { get; private set; }
        public string BonePoseFilePath { get; private set; }

        public MmdGameObject()
        {
            ModelPath = null;
            MotionPath = null;
            OnMmdEvent = mmdEvent => { };
        }

        public Mesh Mesh { get; private set; }
        public List<Mesh> PartMeshes { get; private set; }
        private GameObject[] _bones;
        private bool[] _bonePhysicsControlFlags;
        private const int DefaultMaxTextureSize = 1024;
        private MmdModel _model;
        private Poser _poser;
        private BulletPyhsicsReactor _physicsReactor;
        private MmdMotion _motion;
        private MotionPlayer _motionPlayer;
        private double _playTime;
        private List<List<int>> _partIndexes;
        private List<Vector3[]> _partMorphVertexCache;
        private MaterialLoader _materialLoader;
        private readonly ModelReadConfig _modelReadConfig = new ModelReadConfig {GlobalToonPath = ""};
        private double _restStepTime;
        private UnityEngine.Material[] _materials;
        private BonePosePreCalculator _bonePosePreCalculator;
        private Vector3[] _morphVertexCache;
        private BonePoseFileStorage _bonePoseFileStorage;

        private MmdUnityConfig _config = new MmdUnityConfig();
        private GameObject _boneRootGameObject;

        public static GameObject CreateGameObject(string name = "MMDGameObject")
        {
            var obj = new GameObject(name);
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MmdGameObject>();
            var skinnedMeshRenderer = obj.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.quality = SkinQuality.Bone4;
            return obj;
        }

        public enum PhysicsModeEnum
        {
            None,
            Unity,
            Bullet
        }

        public MmdUnityConfig GetConfig()
        {
            return _config;
        }

        public void UpdateConfig(MmdUnityConfig config)
        {
            _config = config;
            RefreshByConfig();
        }

        private void RefreshByConfig()
        {
            UpdateMaterialsConfig(_materials, _config);
        }

        public bool LoadModel(string path)
        {
            ModelPath = path;
            try
            {
                DoLoadModel(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }

            if (_bonePoseFileStorage != null)
            {
                _bonePoseFileStorage.Release();
                _bonePoseFileStorage = null;
            }

            Utils.ClearAllTransformChild(transform);
            _bones = CreateBones(gameObject);
            if (PhysicsMode == PhysicsModeEnum.Unity)
            {
                CreateUnityPhysicsCompoments();
            }

            _morphVertexCache = new Vector3[_model.Vertices.Length];
            if (Mesh != null)
            {
                LoadMaterials();
                GetComponent<MeshFilter>().mesh = Mesh;
                BuildBindpose(Mesh, GetComponent<SkinnedMeshRenderer>(), true);
            }
            else
            {
                GetComponent<MeshFilter>().mesh = null;
                CreatePartObjects();
                _partMorphVertexCache = new List<Vector3[]>();
                foreach (var partIndex in _partIndexes)
                {
                    _partMorphVertexCache.Add(new Vector3[partIndex.Count]);
                }
            }
            if (_motion != null)
            {
                ResetMotionPlayer();
            }
            _playTime = 0.0;
            RestartBonePoseCalculation(0.0f, 1 / PhysicsFps);
            UpdateBones();
            return true;
        }

        private void RestartBonePoseCalculation(double startTimePos, double stepLength)
        {
            StopBonePoseCalculation();
            StartBonePoseCalculation(startTimePos, stepLength);
        }

        private void RestartBonePoseCalculation(MmdPose pose, double stepLength)
        {
            StopBonePoseCalculation();
            StartBonePoseCalculation(pose, stepLength);
        }

        private void StartBonePoseCalculation(double startTimePos, double stepLength)
        {
            if (PhysicsMode != PhysicsModeEnum.Bullet || _poser == null || _motionPlayer == null ||
                _physicsReactor == null)
            {
                return;
            }
            _bonePosePreCalculator =
                new BonePosePreCalculator(BonePoseCalculatorWorker, _poser, _physicsReactor, _motionPlayer, (float)stepLength, (float)startTimePos,
                    PhysicsCacheFrameSize, AutoPhysicsStepLength);
            _bonePosePreCalculator.Start();
        }

        private void StartBonePoseCalculation(MmdPose pose, double stepLength)
        {
            if (PhysicsMode != PhysicsModeEnum.Bullet || _model == null || _physicsReactor == null)
            {
                return;
            }
            _bonePosePreCalculator =
                new BonePosePreCalculator(BonePoseCalculatorWorker, pose, _poser, _physicsReactor, (float)stepLength, 0.0f, PhysicsCacheFrameSize,
                    AutoPhysicsStepLength);
            _bonePosePreCalculator.Start();
        }

        private void StopBonePoseCalculation()
        {
            if (PhysicsMode != PhysicsModeEnum.Bullet)
            {
                return;
            }
            if (_bonePosePreCalculator != null)
            {
                _bonePosePreCalculator.Stop();
            }
            _bonePosePreCalculator = null;
        }

        public void LoadMotion(string path)
        {
            if (_model == null)
            {
                throw new InvalidOperationException("model not loaded yet");
            }
            ReleaseBonePoseFile();
            MotionPath = path;
            var hasMotionData = LoadMotionKernal(path);
            _playTime = 0.0;
            UpdateBones();
            UpdateMesh(_playTime);
            if (hasMotionData)
            {
                RestartBonePoseCalculation(_playTime, 1.0f / PhysicsFps);   
            }
        }

        public void LoadPose(string path)
        {
            MotionPath = path;
            ReleaseBonePoseFile();
            var pose = VpdReader.Read(path);
            _playTime = 0.0;
            UpdateBones();
            ResetMesh();
            RestartBonePoseCalculation(pose, 1.0f / PhysicsFps);
        }

        public void LoadBonePoseFile(string path)
        {
            BonePoseFilePath = path;
            if (_model == null)
            {
                Debug.LogWarning("model not loaded yet, skip LoadBonePoseFile");
                return;
            }
            ReleaseBonePoseFile();
            _bonePoseFileStorage = new BonePoseFileStorage(_model, path);
            StopBonePoseCalculation();
        }

        private void ReleaseBonePoseFile()
        {
            if (_bonePoseFileStorage == null) return;
            _bonePoseFileStorage.Release();
            _bonePoseFileStorage = null;
            BonePoseFilePath = null;
        } 

        public double MotionPos
        {
            get { return GetMotionPos(); }
            set { SetMotionPos(value); }
        }

        private void Update()
        {
            if (!Playing)
            {
                return;
            }
            var deltaTime = Time.deltaTime;
            _playTime += deltaTime;
            if (_bonePoseFileStorage != null)
            {
                var poses = _bonePoseFileStorage.GetBonePose(_playTime);
                //var start = Environment.TickCount;
                UpdateBonesByBonePoseImage(poses);
                //Debug.LogFormat("read pose from file cost {0} ms", Environment.TickCount - start);
                UpdateMesh(_playTime);
            }
            else if (PhysicsMode == PhysicsModeEnum.Bullet)
            {
                double poseTime;
                if (UpdateBonesByPreCalculate(_playTime, out poseTime))
                {
                    UpdateMesh(poseTime);
                }
            }
            else
            {
                _motionPlayer.SeekTime(_playTime);
                _poser.PrePhysicsPosing(false);
                _poser.PostPhysicsPosing();
                if (PhysicsMode == PhysicsModeEnum.Unity)
                {
                    UpdateBones(true);
                }
                else
                {
                    UpdateBones();
                }
                UpdateMesh(_playTime);
            }
        }

        private void OnDestroy()
        {
            Release();
        }

        private void CreatePartObjects()
        {
            var partMeshes = PartMeshes;
            LoadMaterials();
            var modelRootTransform = new GameObject("Model").transform;
            for (var i = 0; i < partMeshes.Count; i++)
            {
                var part = newMeshPart("Part" + i);
                part.GetComponent<MeshFilter>().mesh = partMeshes[i];
                var skinnedMeshRenderer = part.GetComponent<SkinnedMeshRenderer>();
                skinnedMeshRenderer.rootBone = modelRootTransform;
                skinnedMeshRenderer.material = _materials[i];
                part.transform.SetParent(transform, false);
                BuildBindpose(partMeshes[i], skinnedMeshRenderer, false);
            }
        }

        private GameObject newMeshPart(string partName)
        {
            var ret = new GameObject(partName);
            ret.AddComponent<MeshFilter>();
            ret.AddComponent<SkinnedMeshRenderer>();
            return ret;
        }

        private void LoadMaterials()
        {
            ReleasePreviousMaterials();
            _materials = LoadMaterials(_config);
            ReorderRender(_materials);
        }

        private void ReleasePreviousMaterials()
        {
            if (_materials == null)
            {
                return;
            }
            foreach (var mat in _materials)
            {
                Destroy(mat);
            }
        }

        private void DoLoadModel(string filePath)
        {
            Debug.LogFormat("start load model {0}", filePath);
            _model = ModelReader.LoadMmdModel(filePath, _modelReadConfig);
            Release();
            var directoryInfo = new FileInfo(filePath).Directory;
            if (directoryInfo == null)
            {
                throw new MmdFileParseException(filePath + " does not belong to any directory.");
            }
            var relativePath = directoryInfo.FullName;
            _materialLoader = new MaterialLoader(new TextureLoader(relativePath, DefaultMaxTextureSize));
            var vertCount = _model.Vertices.Length;
            if (vertCount <= 65535)
            {
                var mesh = new Mesh
                {
                    vertices = new Vector3[vertCount],
                    normals = new Vector3[vertCount]
                };
                var triangleCount = _model.TriangleIndexes.Length / 3;
                var triangles = _model.TriangleIndexes;
                FillSubMesh(mesh, triangleCount, triangles);
                var uv = ExtratUv(_model.Vertices);
                var uvVec = new Vector2[vertCount];
                Utils.MmdUvToUnityUv(uv, uvVec);
                mesh.uv = uvVec;
                mesh.boneWeights = _model.Vertices.Select(x => ConvertBoneWeight(x.SkinningOperator)).ToArray();
                ReleasePreviousMeshes();
                Mesh = mesh;
                mesh.RecalculateBounds();
                _partIndexes = null;
            }
            else
            {
                var triangleCount = _model.TriangleIndexes.Length / 3;
                var triangles = _model.TriangleIndexes;
                var uv = ExtratUv(_model.Vertices);
                ;
                var uvVec = new Vector2[vertCount];
                Utils.MmdUvToUnityUv(uv, uvVec);
                FillPartMeshes(triangleCount, triangles, uv);
            }
            _poser = new Poser(_model);
            _physicsReactor = new BulletPyhsicsReactor();
            _physicsReactor.AddPoser(_poser);
            InitMesh();
            Debug.LogFormat("load model finished {0}", filePath);
        }

        private static float[] ExtratUv(Vertex[] verts)
        {
            var ret = new float[verts.Length * 2];
            for (var i = 0; i < verts.Length; ++i)
            {
                ret[2 * i] = verts[i].UvCoordinate.x;
                ret[2 * i + 1] = verts[i].UvCoordinate.y;
            }
            return ret;
        }

        public UnityEngine.Material[] LoadMaterials(MmdUnityConfig config)
        {
            var ret = new UnityEngine.Material[_model.Parts.Length];
            for (var i = 0; i < _model.Parts.Length; i++)
            {
                ret[i] = _materialLoader.LoadMaterial(_model.Parts[i].Material, config);
                //Debug.LogFormat("material {0} render queue {1}", i, ret[i].renderQueue);
            }
            return ret;
        }

        public void UpdateMaterialsConfig(UnityEngine.Material[] materials, MmdUnityConfig config)
        {
            if (_model == null)
            {
                return;
            }
            if (materials.Length != _model.Parts.Length)
            {
                throw new ArgumentException(
                    "UpdateMaterialsConfig: material count should be equal to model part count");
            }
            for (var i = 0; i < _model.Parts.Length; i++)
            {
                _materialLoader.RefreshMaterialConfig(_model.Parts[i].Material, config, materials[i]);
            }
        }

        private void FillSubMesh(Mesh mesh, int triangleCount, int[] triangles)
        {
            mesh.subMeshCount = _model.Parts.Length;
            for (var i = 0; i < _model.Parts.Length; i++)
            {
                var modelPart = _model.Parts[i];
                if ((modelPart.BaseShift + modelPart.TriangleIndexNum) / 3 > triangleCount)
                {
                    Debug.LogError("too many triangles in model part " + i);
                    continue;
                }
                mesh.SetTriangles(Utils.ArrayToList(triangles, modelPart.BaseShift, modelPart.TriangleIndexNum), i);
            }
        }

        private void FillPartMeshes(int triangleCount, int[] triangles, float[] uv)
        {
            ReleasePreviousMeshes();
            var nPart = _model.Parts.Length;

            PartMeshes = new List<Mesh>();
            _partIndexes = new List<List<int>>();
            for (var i = 0; i < nPart; i++)
            {
                var modelPart = _model.Parts[i];
                if ((modelPart.BaseShift + modelPart.TriangleIndexNum) / 3 > triangleCount)
                {
                    Debug.LogError("too many triangles in model part " + i);
                    continue;
                }
                var thisPartIndex = new List<int>();
                Dictionary<int, int> vertMap = GetVertMap(triangles, modelPart.BaseShift,
                    modelPart.TriangleIndexNum,
                    thisPartIndex);
                var meshTriangles =
                    GetPartTriangles(triangles, modelPart.BaseShift, modelPart.TriangleIndexNum, vertMap);
                var mesh = new Mesh();
                mesh.vertices = new Vector3[thisPartIndex.Count];
                mesh.normals = new Vector3[thisPartIndex.Count];
                mesh.triangles = meshTriangles.ToArray();
                mesh.uv = PickupUv(uv, thisPartIndex);
                PartMeshes.Add(mesh);
                _partIndexes.Add(thisPartIndex);
            }
        }

        private void ReleasePreviousMeshes()
        {
            if (PartMeshes != null)
            {
                foreach (var mesh in PartMeshes)
                {
                    Destroy(mesh);
                }
                PartMeshes = null;
            }
            if (Mesh == null) return;
            Destroy(Mesh);
            Mesh = null;
        }

        private static Dictionary<int, int> GetVertMap(int[] triangles, int fromIndex, int size, List<int> indexList)
        {
            var ret = new Dictionary<int, int>();
            indexList.Clear();
            for (var i = fromIndex; i < fromIndex + size; i++)
            {
                var src = triangles[i];
                if (ret.ContainsKey(src)) continue;
                ret.Add(src, indexList.Count);
                indexList.Add(src);
            }
            return ret;
        }

        private static List<int> GetPartTriangles(int[] triangles, int fromIndex, int size,
            Dictionary<int, int> vertMap)
        {
            var ret = new List<int>(size);
            for (var i = fromIndex; i < fromIndex + size; i++)
            {
                ret.Add(vertMap[triangles[i]]);
            }
            return ret;
        }

        private bool LoadMotionKernal(string filePath)
        {
            _motion = new VmdReader().Read(filePath);
            if (_motion.Length == 0)
            {
                StopBonePoseCalculation();
                _poser.ResetPosing();
                ResetMotionPlayer();
                return false;
            }
            ResetMotionPlayer();
            //_poser.Deform();
            return true;
        }

        private void ResetMotionPlayer()
        {
            _motionPlayer = new MotionPlayer(_motion, _poser);
            _motionPlayer.SeekFrame(0);
            _poser.PrePhysicsPosing();
            _physicsReactor.Reset();
            _poser.PostPhysicsPosing();
        }

        private void Step(double time, float fixStepTime, int maxStep = 10)
        {
            if (time <= 0.0f)
            {
                return;
            }

            time += _restStepTime;
            var nStep = Math.Floor(time / fixStepTime);
            if (nStep > maxStep)
            {
                Debug.Log("too many play steps, time= " + time + ", fixStepTime= " + fixStepTime);
                nStep = maxStep;
                fixStepTime = (float) (time / nStep);
                _restStepTime = 0.0f;
            }
            else
            {
                _restStepTime = time - nStep * fixStepTime;
            }
            for (var i = 0; i < nStep; i++)
            {
                _playTime += fixStepTime;
                _motionPlayer.SeekTime(_playTime);
                _poser.PrePhysicsPosing();
                _physicsReactor.React(fixStepTime, 2, fixStepTime);
                _poser.PostPhysicsPosing();
            }
            //_poser.Deform();
        }

        private void UpdateBones(bool skipPhysicsControlBones = false)
        {
            if (CanNotUpdateBone())
            {
                Debug.LogError("illegal argument for UpdateBones");
                return;
            }
            for (var i = 0; i < _bones.Length; ++i)
            {
                if (skipPhysicsControlBones && _bonePhysicsControlFlags[i])
                {
                    continue;
                }
                UpdateBone(i);
            }
        }

        private bool CanNotUpdateBone()
        {
            return _bones == null || _poser == null || _model == null || _poser.BoneImages.Length != _bones.Length ||
                   _model.Bones.Length != _bones.Length;
        }

        private void UpdateBone(int i)
        {
            //var localToWorldMatrix = _boneRootGameObject.transform.localToWorldMatrix;
            var rootTrans = _boneRootGameObject.transform;
            var transMatrix = _poser.BoneImages[i].SkinningMatrix;
            _bones[i].transform.position =
                rootTrans.TransformPoint(transMatrix.MultiplyPoint3x4(_model.Bones[i].Position));
            _bones[i].transform.rotation = rootTrans.rotation * transMatrix.ExtractRotation();
            _bones[i].transform.localScale = Vector3.one;
        }

        private bool UpdateBonesByPreCalculate(double timePos, out double poseTime)
        {
            if (_bonePosePreCalculator == null)
            {
                poseTime = -1.0;
                return false;
            }
            bool notCalculatedYet;
            var poses = _bonePosePreCalculator.Take(timePos, out notCalculatedYet, out poseTime);
            if (notCalculatedYet)
            {
                #if UNITY_EDITOR
                Debug.LogFormat("pose calculation too slow");
                #endif
                OnMmdEvent(MmdEvent.SlowPoseCalculation);
            }
            if (poses == null)
            {
                return false;
            }
            if (poses.Length != _bones.Length)
            {
                throw new ArgumentException("poses.Length != _bones.Length");
            }
            UpdateBonesByBonePoseImage(poses);
            return true;
        }

        private void UpdateBonesByBonePoseImage(BonePoseImage[] poses)
        {
            var trans = _boneRootGameObject.transform;

            for (var i = 0; i < _bones.Length; ++i)
            {
                _bones[i].transform.position = trans.TransformPoint(poses[i].Position);
                _bones[i].transform.rotation = trans.rotation * poses[i].Rotation;
                _bones[i].transform.localScale = Vector3.one;
            }
        }

        private void UpdateMesh(double poseTime)
        {
            if (_motionPlayer == null || _model == null)
            {
                return;
            }
            _motionPlayer.CalculateMorphVertexOffset(_model, poseTime, _morphVertexCache);

            var verticesLength = _model.Vertices.Length;
            var modelVertices = _model.Vertices;
            for (var i = 0; i < verticesLength; ++i)
            {
                _morphVertexCache[i] = modelVertices[i].Coordinate + _morphVertexCache[i];
            }

            RefreshMeshByMorphVertexCache();
        }

        private void RefreshMeshByMorphVertexCache()
        {
            if (Mesh != null)
            {
                Mesh.vertices = _morphVertexCache;
            }
            else
            {
                for (var i = 0; i < PartMeshes.Count; i++)
                {
                    var partMesh = PartMeshes[i];
                    var vertexIndexList = _partIndexes[i];
                    var vertexCache = _partMorphVertexCache[i];
                    for (var j = 0; j < vertexIndexList.Count; ++j)
                    {
                        vertexCache[j] = _morphVertexCache[vertexIndexList[j]];
                    }
                    partMesh.vertices = vertexCache;
                }
            }
        }

        private void ResetMesh()
        {
            var verticesLength = _model.Vertices.Length;
            var modelVertices = _model.Vertices;
            for (var i = 0; i < verticesLength; ++i)
            {
                _morphVertexCache[i] = modelVertices[i].Coordinate;
            }

            RefreshMeshByMorphVertexCache();
        }

        private void InitMesh()
        {
            var verts = _model.Vertices.Select(x => x.Coordinate).ToArray();
            var normals = _model.Vertices.Select(x => x.Normal).ToArray();
            if (Mesh != null)
            {
                Mesh.vertices = verts;
                Mesh.normals = normals;
                Mesh.RecalculateBounds();
            }
            else
            {
                for (var i = 0; i < PartMeshes.Count; i++)
                {
                    var partMesh = PartMeshes[i];
                    var indexes = _partIndexes[i];
                    partMesh.vertices = PickupVecs3(verts, indexes);
                    partMesh.normals = PickupVecs3(normals, indexes);
                    partMesh.boneWeights = PickupBoneWeight(_model.Vertices, indexes);
                    partMesh.RecalculateBounds();
                }
            }
        }

        private static BoneWeight[] PickupBoneWeight(Vertex[] vertices, List<int> indexes)
        {
            var ret = new BoneWeight[indexes.Count];
            for (var i = 0; i < indexes.Count; i++)
            {
                var index = indexes[i];
                var vertex = vertices[index];
                var boneWeight = ConvertBoneWeight(vertex.SkinningOperator);
                ret[i] = boneWeight;
            }
            return ret;
        }

        private static Vector3[] PickupVecs3(Vector3[] vecCoords, List<int> indexes)
        {
            var ret = new Vector3[indexes.Count];
            for (var i = 0; i < indexes.Count; i++)
            {
                var iCoord = indexes[i];
                ret[i] = vecCoords[iCoord];
            }
            return ret;
        }

        private static Vector2[] PickupUv(float[] uv, List<int> indexes)
        {
            var ret = new Vector2[indexes.Count];
            for (var i = 0; i < indexes.Count; i++)
            {
                var iCoord = indexes[i] * 2;
                ret[i] = new Vector2(uv[iCoord], 1.0f - uv[iCoord + 1]);
            }
            return ret;
        }

        private void Release()
        {
            if (_materialLoader != null)
            {
                _materialLoader.Dispose();
                _materialLoader = null;
            }

            if (_bonePoseFileStorage != null)
            {
                _bonePoseFileStorage.Release();
                _bonePoseFileStorage = null;
            }

            StopBonePoseCalculation();
        }

        public void ResetMotion()
        {
            if (_motionPlayer == null)
            {
                return;
            }
            StopBonePoseCalculation();
            _playTime = 0.0;
            _restStepTime = 0.0f;
            _motionPlayer.SeekFrame(0);
            _poser.PrePhysicsPosing();
            _physicsReactor.Reset();
            _poser.PostPhysicsPosing();
            StartBonePoseCalculation(0.0, 1.0f / PhysicsFps);
            UpdateMesh(_playTime);
            UpdateBones();
            //_poser.Deform();
        }

        public double GetMotionPos()
        {
            return _playTime;
        }

        public void SetMotionPos(double pos)
        {
            StopBonePoseCalculation();
            _playTime = pos;
            _restStepTime = 0.0f;
            _motionPlayer.SeekTime(_playTime);
            _poser.PrePhysicsPosing();
            _physicsReactor.Reset();
            _poser.PostPhysicsPosing();
            StartBonePoseCalculation(0.0, 1.0f / PhysicsFps);
            //_poser.Deform();
        }

        private GameObject[] CreateBones(GameObject rootGameObject)
        {
            if (_model == null)
            {
                return new GameObject[0];
            }
            var bones = EntryAttributeForBones();
            AttachParentsForBone(rootGameObject, bones);
            return bones;
        }

        private GameObject[] EntryAttributeForBones()
        {
            return _model.Bones.Select(x =>
            {
                var gameObject = new GameObject(x.Name);
                gameObject.transform.position = x.Position;
                return gameObject;
            }).ToArray();
        }

        private void AttachParentsForBone(GameObject rootGameObject, GameObject[] bones)
        {
            var rootObj = new GameObject("Model");
            _boneRootGameObject = rootObj;
            var modelRootTransform = rootObj.transform;
            GetComponent<SkinnedMeshRenderer>().rootBone = modelRootTransform;
            modelRootTransform.parent = rootGameObject.transform;
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            rootObj.transform.localScale = Vector3.one;

            for (int i = 0, iMax = _model.Bones.Length; i < iMax; ++i)
            {
                var parentBoneIndex = _model.Bones[i].ParentIndex;
                bones[i].transform.parent = parentBoneIndex < bones.Length && parentBoneIndex >= 0
                    ? bones[parentBoneIndex].transform
                    : modelRootTransform;
            }
        }

        private static BoneWeight ConvertBoneWeight(SkinningOperator op)
        {
            var ret = new BoneWeight();
            switch (op.Type)
            {
                case SkinningOperator.SkinningType.SkinningBdef1:
                    var bdef1 = (SkinningOperator.Bdef1) op.Param;
                    ret.boneIndex0 = bdef1.BoneId;
                    ret.weight0 = 1.0f;
                    break;
                case SkinningOperator.SkinningType.SkinningBdef2:
                    var bdef2 = (SkinningOperator.Bdef2) op.Param;
                    ret.boneIndex0 = bdef2.BoneId[0];
                    ret.boneIndex1 = bdef2.BoneId[1];
                    ret.weight0 = bdef2.BoneWeight;
                    ret.weight1 = 1 - bdef2.BoneWeight;
                    break;
                case SkinningOperator.SkinningType.SkinningBdef4:
                    var bdef4 = (SkinningOperator.Bdef4) op.Param;
                    ret.boneIndex0 = bdef4.BoneId[0];
                    ret.boneIndex1 = bdef4.BoneId[1];
                    ret.boneIndex2 = bdef4.BoneId[2];
                    ret.boneIndex3 = bdef4.BoneId[3];
                    ret.weight0 = bdef4.BoneWeight[0];
                    ret.weight1 = bdef4.BoneWeight[1];
                    ret.weight2 = bdef4.BoneWeight[2];
                    ret.weight3 = bdef4.BoneWeight[3];
                    break;
                case SkinningOperator.SkinningType.SkinningSdef:
                    var sdef = (SkinningOperator.Sdef) op.Param;
                    ret.boneIndex0 = sdef.BoneId[0];
                    ret.boneIndex1 = sdef.BoneId[1];
                    ret.weight0 = sdef.BoneWeight;
                    ret.weight1 = 1 - sdef.BoneWeight;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return ret;
        }

        private void BuildBindpose(Mesh mesh, SkinnedMeshRenderer smr, bool fillMaterials)
        {
            var bindposes = _bones.Select(x => x.transform.worldToLocalMatrix).ToArray();
            mesh.bindposes = bindposes;
            smr.sharedMesh = mesh;
            smr.bones = _bones.Select(x => x.transform).ToArray();
            if (fillMaterials)
            {
                smr.materials = _materials;
            }
        }

        private void CreateUnityPhysicsCompoments()
        {
            var rigids = CreateRigids();
            AssignRigidbodyToBone(_bones, rigids);
            SetRigidsSettings(_bones, rigids);
            SetBoneKinematicFlags();
            var joints = SetupConfigurableJoint(rigids);
            GlobalizeRigidbody(joints);

            var ignoreGroups = SettingIgnoreRigidGroups();
            var groupTarget = GetRigidbodyGroupTargets();

            IgnoreCollisions(rigids, groupTarget, ignoreGroups);
        }

        private GameObject[] CreateRigids()
        {
            var result = _model.Rigidbodies.Select(ConvertRigidbody).ToArray();
            for (uint i = 0, iMax = (uint) result.Length; i < iMax; ++i)
            {
                result[i].GetComponent<Collider>().material = CreatePhysicMaterial(_model.Rigidbodies, i);
            }
            return result;
        }

        private static GameObject ConvertRigidbody(MmdRigidBody mmdRigidBody)
        {
            var ret = new GameObject("r" + mmdRigidBody.Name);

            ret.transform.position = mmdRigidBody.Position;
            ret.transform.rotation = Quaternion.Euler(mmdRigidBody.Rotation * Mathf.Rad2Deg);

            switch (mmdRigidBody.Shape)
            {
                case MmdRigidBody.RigidBodyShape.RigidShapeSphere:
                    EntrySphereCollider(mmdRigidBody, ret);
                    break;
                case MmdRigidBody.RigidBodyShape.RigidShapeBox:
                    EntryBoxCollider(mmdRigidBody, ret);
                    break;
                case MmdRigidBody.RigidBodyShape.RigidShapeCapsule:
                    EntryCapsuleCollider(mmdRigidBody, ret);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return ret;
        }

        private static void EntrySphereCollider(MmdRigidBody mmdRigidBody, GameObject obj)
        {
            var unityCollider = obj.AddComponent<SphereCollider>();
            unityCollider.radius = mmdRigidBody.Dimemsions.x;
        }

        private static void EntryBoxCollider(MmdRigidBody mmdRigidBody, GameObject obj)
        {
            var unityCollider = obj.AddComponent<BoxCollider>();
            unityCollider.size = mmdRigidBody.Dimemsions * 2.0f;
        }

        private static void EntryCapsuleCollider(MmdRigidBody mmdRigidBody, GameObject obj)
        {
            var unityCollider = obj.AddComponent<CapsuleCollider>();
            unityCollider.radius = mmdRigidBody.Dimemsions.x;
            unityCollider.height = mmdRigidBody.Dimemsions.y + mmdRigidBody.Dimemsions.x * 2.0f;
        }

        private PhysicMaterial CreatePhysicMaterial(MmdRigidBody[] rigidbodys, uint index)
        {
            var mmdRigidBody = rigidbodys[index];

            return new PhysicMaterial(_model.Name + "_r" + mmdRigidBody.Name)
            {
                bounciness = mmdRigidBody.Restitution,
                staticFriction = mmdRigidBody.Friction,
                dynamicFriction = mmdRigidBody.Friction
            };
        }

        private void AssignRigidbodyToBone(GameObject[] bones, GameObject[] rigids)
        {
            var physicsRootTransform = new GameObject("Physics").transform;
            physicsRootTransform.parent = gameObject.transform;

            for (int i = 0, iMax = rigids.Length; i < iMax; ++i)
            {
                var relBoneIndex = GetRelBoneIndexFromNearbyRigidbody(i);
                rigids[i].transform.parent =
                    relBoneIndex < bones.Length ? bones[relBoneIndex].transform : physicsRootTransform;
            }
        }


        private int GetRelBoneIndexFromNearbyRigidbody(int rigidbodyIndex)
        {
            var boneCount = _model.Bones.Length;
            var result = _model.Rigidbodies[rigidbodyIndex].AssociatedBoneIndex;
            if (result < boneCount)
            {
                return result;
            }
            var jointAList = _model.Constraints.Where(x => x.AssociatedRigidBodyIndex[1] == rigidbodyIndex)
                .Where(x => x.AssociatedRigidBodyIndex[0] < boneCount)
                .Select(x => x.AssociatedRigidBodyIndex[0]);
            foreach (var jointA in jointAList)
            {
                result = GetRelBoneIndexFromNearbyRigidbody(jointA);
                if (result < boneCount)
                {
                    return result;
                }
            }

            var jointBList = _model.Constraints.Where(x => x.AssociatedRigidBodyIndex[0] == rigidbodyIndex)
                .Where(x => x.AssociatedRigidBodyIndex[1] < boneCount)
                .Select(x => x.AssociatedRigidBodyIndex[1]);
            foreach (var jointB in jointBList)
            {
                result = GetRelBoneIndexFromNearbyRigidbody(jointB);
                if (result < boneCount)
                {
                    return result;
                }
            }

            result = int.MaxValue;
            return result;
        }

        private void SetRigidsSettings(GameObject[] bones, GameObject[] rigid)
        {
            var boneCount = _model.Bones.Length;
            for (int i = 0, iMax = _model.Rigidbodies.Length; i < iMax; ++i)
            {
                var mmdRigidBody = _model.Rigidbodies[i];
                var target = mmdRigidBody.AssociatedBoneIndex < boneCount
                    ? bones[mmdRigidBody.AssociatedBoneIndex]
                    : rigid[i];
                UnityRigidbodySetting(mmdRigidBody, target);
            }
        }

        private void SetBoneKinematicFlags()
        {
            _bonePhysicsControlFlags = new bool[_bones.Length];
            for (int i = 0, iMax = _bones.Length; i < iMax; i++)
            {
                var bone = _bones[i];
                var boneRigidbody = bone.GetComponent<Rigidbody>();
                if (boneRigidbody == null)
                {
                    continue;
                }
                if (!boneRigidbody.isKinematic)
                {
                    _bonePhysicsControlFlags[i] = true;
                }
            }
        }

        private static void UnityRigidbodySetting(MmdRigidBody mmdRigidBody, GameObject target)
        {
            var unityRigidBody = target.GetComponent<Rigidbody>();
            if (null != unityRigidBody)
            {
                unityRigidBody.mass += mmdRigidBody.Mass;
                unityRigidBody.drag = (unityRigidBody.drag + mmdRigidBody.TranslateDamp) * 0.5f;
                unityRigidBody.angularDrag = (unityRigidBody.angularDrag + mmdRigidBody.RotateDamp) * 0.5f;
            }
            else
            {
                unityRigidBody = target.AddComponent<Rigidbody>();
                unityRigidBody.isKinematic = MmdRigidBody.RigidBodyType.RigidTypeKinematic == mmdRigidBody.Type;
                unityRigidBody.mass = Mathf.Max(float.Epsilon, mmdRigidBody.Mass);
                unityRigidBody.drag = mmdRigidBody.TranslateDamp;
                unityRigidBody.angularDrag = mmdRigidBody.RotateDamp;
            }
        }

        private GameObject[] SetupConfigurableJoint(GameObject[] rigids)
        {
            var resultList = new List<GameObject>();
            foreach (var joint in _model.Constraints)
            {
                var transformA = rigids[joint.AssociatedRigidBodyIndex[0]].transform;
                var rigidbodyA = transformA.GetComponent<Rigidbody>();
                if (null == rigidbodyA)
                {
                    rigidbodyA = transformA.parent.GetComponent<Rigidbody>();
                }
                var transformB = rigids[joint.AssociatedRigidBodyIndex[1]].transform;
                var rigidbodyB = transformB.GetComponent<Rigidbody>();
                if (null == rigidbodyB)
                {
                    rigidbodyB = transformB.parent.GetComponent<Rigidbody>();
                }
                if (rigidbodyA == rigidbodyB) continue;
                var configJoint = rigidbodyB.gameObject.AddComponent<ConfigurableJoint>();
                configJoint.connectedBody = rigidbodyA;
                SetAttributeConfigurableJoint(joint, configJoint);

                resultList.Add(configJoint.gameObject);
            }
            return resultList.ToArray();
        }

        private void SetAttributeConfigurableJoint(Constraint joint, ConfigurableJoint conf)
        {
            SetMotionAngularLock(joint, conf);
            SetDrive(joint, conf);
        }

        private static void SetMotionAngularLock(Constraint joint, ConfigurableJoint conf)
        {
            SoftJointLimit jlim;

            if (Math.Abs(joint.PositionLowLimit.x) < Tools.MmdMathConstEps &&
                Math.Abs(joint.PositionHiLimit.x) < Tools.MmdMathConstEps)
            {
                conf.xMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.xMotion = ConfigurableJointMotion.Limited;
            }

            if (Math.Abs(joint.PositionLowLimit.y) < Tools.MmdMathConstEps &&
                Math.Abs(joint.PositionHiLimit.y) < Tools.MmdMathConstEps)
            {
                conf.yMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.yMotion = ConfigurableJointMotion.Limited;
            }

            if (Math.Abs(joint.PositionLowLimit.z) < Tools.MmdMathConstEps &&
                Math.Abs(joint.PositionHiLimit.z) < Tools.MmdMathConstEps)
            {
                conf.zMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.zMotion = ConfigurableJointMotion.Limited;
            }

            if (Math.Abs(joint.RotationLowLimit.x) < Tools.MmdMathConstEps &&
                Math.Abs(joint.RotationHiLimit.x) < Tools.MmdMathConstEps)
            {
                conf.angularXMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.angularXMotion = ConfigurableJointMotion.Limited;
                var hlim = Mathf.Max(-joint.RotationLowLimit.x, -joint.RotationHiLimit.x);
                var llim = Mathf.Min(-joint.RotationLowLimit.x, -joint.RotationHiLimit.x);
                var jhlim = new SoftJointLimit {limit = Mathf.Clamp(hlim * Mathf.Rad2Deg, -180.0f, 180.0f)};
                conf.highAngularXLimit = jhlim;

                var jllim = new SoftJointLimit {limit = Mathf.Clamp(llim * Mathf.Rad2Deg, -180.0f, 180.0f)};
                conf.lowAngularXLimit = jllim;
            }

            if (Math.Abs(joint.RotationLowLimit.y) < Tools.MmdMathConstEps &&
                Math.Abs(joint.RotationHiLimit.y) < Tools.MmdMathConstEps)
            {
                conf.angularYMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.angularYMotion = ConfigurableJointMotion.Limited;
                conf.angularYMotion = ConfigurableJointMotion.Limited;
                var lim = Mathf.Min(Mathf.Abs(joint.RotationLowLimit.y), Mathf.Abs(joint.RotationHiLimit.y));
                jlim = new SoftJointLimit {limit = lim * Mathf.Clamp(Mathf.Rad2Deg, 0.0f, 180.0f)};
                conf.angularYLimit = jlim;
            }

            if (Math.Abs(joint.RotationLowLimit.z) < Tools.MmdMathConstEps &&
                Math.Abs(joint.RotationHiLimit.z) < Tools.MmdMathConstEps)
            {
                conf.angularZMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                conf.angularZMotion = ConfigurableJointMotion.Limited;
                var lim = Mathf.Min(Mathf.Abs(-joint.RotationLowLimit.z), Mathf.Abs(-joint.RotationHiLimit.z));
                jlim = new SoftJointLimit {limit = Mathf.Clamp(lim * Mathf.Rad2Deg, 0.0f, 180.0f)};
                conf.angularZLimit = jlim;
            }
        }

        private void SetDrive(Constraint joint, ConfigurableJoint conf)
        {
            JointDrive drive;

            // Position
            if (Math.Abs(joint.Position.x) > Tools.MmdMathConstEps)
            {
                drive = new JointDrive {positionSpring = joint.Position.x};
                conf.xDrive = drive;
            }
            if (Math.Abs(joint.Position.y) > Tools.MmdMathConstEps)
            {
                drive = new JointDrive {positionSpring = joint.Position.y};
                conf.yDrive = drive;
            }
            if (Math.Abs(joint.Position.z) > Tools.MmdMathConstEps)
            {
                drive = new JointDrive {positionSpring = joint.Position.z};
                conf.zDrive = drive;
            }

            // Angular
            if (Math.Abs(joint.Rotation.x) > Tools.MmdMathConstEps)
            {
                drive = new JointDrive
                {
                    //mode = JointDriveMode.PositionAndVelocity,
                    positionSpring = joint.Rotation.x
                };
                conf.angularXDrive = drive;
            }
            if (Math.Abs(joint.Rotation.y) > Tools.MmdMathConstEps ||
                Math.Abs(joint.Rotation.z) > Tools.MmdMathConstEps)
            {
                drive = new JointDrive
                {
                    //mode = JointDriveMode.PositionAndVelocity,
                    positionSpring = (joint.Rotation.y + joint.Rotation.z) * 0.5f
                };
                conf.angularYZDrive = drive;
            }
        }

        private void GlobalizeRigidbody(GameObject[] joints)
        {
            var physicsRootTransform = gameObject.transform.Find("Physics");

            if (null == joints || 0 >= joints.Length) return;

            foreach (ConfigurableJoint joint in joints.Where(x => !x.GetComponent<Rigidbody>().isKinematic)
                .Select(x => x.GetComponent<ConfigurableJoint>()))
            {
                joint.transform.parent = physicsRootTransform;
            }
        }

        private List<int>[] SettingIgnoreRigidGroups()
        {
            const int maxGroup = 16;
            var result = new List<int>[maxGroup];
            for (int i = 0, iMax = maxGroup; i < iMax; ++i)
            {
                result[i] = new List<int>();
            }
            for (int i = 0, iMax = _model.Rigidbodies.Length; i < iMax; ++i)
            {
                result[_model.Rigidbodies[i].CollisionGroup].Add(i);
            }
            return result;
        }

        private int[] GetRigidbodyGroupTargets()
        {
            return _model.Rigidbodies.Select(x => (int) x.CollisionMask).ToArray();
        }

        private static void IgnoreCollisions(IList<GameObject> rigids, IList<int> groupTarget, List<int>[] ignoreList)
        {
            for (var i = 0; i < rigids.Count; i++)
            {
                for (var shift = 0; shift < 16; shift++)
                {
                    if ((groupTarget[i] & (1 << shift)) != 0) continue;
                    for (var j = 0; j < ignoreList[shift].Count; j++)
                    {
                        var ignoreIndex = ignoreList[shift][j];
                        if (i == ignoreIndex) continue;
                        Physics.IgnoreCollision(rigids[i].GetComponent<Collider>(),
                            rigids[ignoreIndex].GetComponent<Collider>(), true);
                    }
                }
            }
        }
        
        private void ReorderRender(UnityEngine.Material[] materials) {
            if (materials.Length == 0) {
                return;
            }
            var order = new UnityEngine.Material[materials.Length];
            Array.Copy (materials, order, materials.Length);
            order.OrderBy (mat => mat.renderQueue);
            var lastQueue = int.MinValue;
            foreach (var mat in order) {
                if (lastQueue >= mat.renderQueue) {
                    mat.renderQueue = lastQueue++;
                } else {
                    lastQueue = mat.renderQueue;
                }
            }
        }
    }
}