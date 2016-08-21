using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ProceduralStars
{
    struct ShadingObjectBuffer
    {
        public uint    id;
        public bool    isShooting;
        public Vector3 pos;
        public Vector3 dir;
        public float   power;
        public Color   color;
        public float   starSize;
        public float   hugeStarSize;
        public float   random;
        public float   twinkle;

        public ShadingObjectBuffer(uint id, Vector3 pos, Vector3 dir, float power, Color color, float starSize, float hugeStarSize, float twinkle)
        {
            this.id           = id;
            this.pos          = pos;
            this.dir          = dir;
            this.power        = power;
            this.color        = color;
            this.starSize     = starSize;
            this.hugeStarSize = hugeStarSize;
            this.twinkle      = twinkle;

            this.isShooting   = false;
            this.random       = 0f;
        }
    }

    [RequireComponent(typeof(MeshRenderer))]
    public class ProceduralStars : MonoBehaviour
    {
        [SerializeField]
        private ComputeShader computeShader;

        [SerializeField]
        private int starAmount = 3000;

        [SerializeField]
        [Tooltip("カラーバリエーション")]
        private List<Color> colors = new List<Color>() { Color.white };

        [SerializeField]
        [Tooltip("進行方向バリエーション")]
        private List<Vector3> directions = new List<Vector3>();

        [SerializeField]
        [Tooltip("スピードバリエーション")]
        [Range(0.01f, 1f)]
        private List<float> speeds = new List<float>();

        [SerializeField]
        [Tooltip("明滅速度係数")]
        [Range(0f, 50f)]
        private List<float> twinkles = new List<float>();

        [SerializeField]
        [Tooltip("星の大きさ")]
        [Range(0.1f, 1.0f)]
        private List<float> starSize = new List<float>();

        [SerializeField]
        [Tooltip("流れ星の大きさ")]
        [Range(0.1f, 1.0f)]
        private float shootingStarSize = 0.45f;

        [SerializeField]
        [Tooltip("巨大星の大きさ")]
        [Range(0.1f, 2.0f)]
        private List<float> hugeStarSize = new List<float>();

        [SerializeField]
        [Tooltip("巨大星確立")]
        private int hugeStarRatio = 1;

        [SerializeField]
        [Range(0f, 50f)]
        [Tooltip("流れ星インターバル")]
        private float shootingStarInterval = 30f;

        [SerializeField]
        [Tooltip("流れ星インターバルのブレ具合")]
        [Range(0f, 5f)]
        private float shootingStarRandomizeRange = 3f;

        [SerializeField]
        [Range(0f, 50f)]
        [Tooltip("流れ星増量時のインターバル")]
        private float fullShootingStarInterval = 2f;

        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("流れ星増量時のブレ具合")]
        private float fullShootingStarRandomizeRange = 2f;

        private enum ComputeKernels
        {
            Initialize,
            Iterator
        }

        private Dictionary<ComputeKernels, int> kernelMap = new Dictionary<ComputeKernels, int>();
        private Dictionary<Camera, CommandBuffer> cameraMap = new Dictionary<Camera, CommandBuffer>();

        private Material      material;
        private ComputeBuffer computeBuffer;

        private float shootingStarTimer;
        private int   shootId = 0;
        private bool  isFullOfShootingStars = false;

        private const int MAX_VERTICES = 65000;
        private const int SHOOTINGSTAR_CHACE = 5;
        private const CameraEvent camEvent = CameraEvent.BeforeForwardOpaque;
        private const string requiredShaderName = "Custom/StarLight";

        void OnDisable()
        {
            CleanUp();
        }

        void OnEnable()
        {
            CleanUp();
        }

        void Initialize()
        {
            kernelMap = System.Enum.GetValues(typeof(ComputeKernels))
                .Cast<ComputeKernels>()
                .ToDictionary(t => t, t => computeShader.FindKernel(t.ToString()));
            material = GetComponent<MeshRenderer>().material;
            SetShootingStarTimer(isFullOfShootingStars);
            InitialCheck();
            InitializeComputeBuffer();
            InitializeUniformBuffer();
        }

        void InitialCheck()
        {
            if (starAmount > MAX_VERTICES)
            {
                starAmount = MAX_VERTICES;
                Debug.LogWarningFormat("Star amount {0} is too large, please set under {1}.", starAmount, MAX_VERTICES);
            }
            if (material.shader.name != requiredShaderName)
            {
                Debug.LogErrorFormat("Please set the material using {0} shader", requiredShaderName);
                return;
            }
            if (hugeStarRatio > 0)
            {
                CheckCount(hugeStarSize, true);
            }
            CheckCount(colors, true);
            CheckCount(starSize, true);
            CheckCount(directions, true);
            CheckCount(speeds, true);
            CheckCount(twinkles, true);
        }

        void InitializeComputeBuffer()
        {
            Mesh mesh = GetComponent<MeshFilter>().mesh;

            float renderAreaMaxX = mesh.vertices.Select(v => v.x).Max();
            float renderAreaMinX = mesh.vertices.Select(v => v.x).Min();
            float renderAreaMaxZ = mesh.vertices.Select(v => v.z).Max();
            float renderAreaMinZ = mesh.vertices.Select(v => v.z).Min();

            computeBuffer = new ComputeBuffer(starAmount, Marshal.SizeOf(typeof(ShadingObjectBuffer)));

            ShadingObjectBuffer[] buf = new ShadingObjectBuffer[computeBuffer.count];

            for (uint i = 0; i < starAmount; i++)
            {
                buf[i] = new ShadingObjectBuffer(
                    i,
                    new Vector3(Random.Range(renderAreaMinX, renderAreaMaxX), 0f, Random.Range(renderAreaMinZ, renderAreaMaxZ)),
                    directions[Random.Range(0, directions.Count)].normalized,
                    speeds[Random.Range(0, speeds.Count)],
                    colors[Random.Range(0, colors.Count)],
                    starSize[Random.Range(0, starSize.Count)],
                    hugeStarSize[Random.Range(0, hugeStarSize.Count)],
                    twinkles[Random.Range(0,twinkles.Count)]
                    );
            }
            computeBuffer.SetData(buf);
            computeShader.SetInt("shootingStarCache", SHOOTINGSTAR_CHACE);
            computeShader.SetFloats("renderMaxArea", new float[] { renderAreaMaxX, 0f, renderAreaMaxZ });
            computeShader.SetFloats("renderMinArea", new float[] { renderAreaMinX, 0f, renderAreaMinZ });
            computeShader.SetBuffer(kernelMap[ComputeKernels.Initialize], "buf", computeBuffer);
            computeShader.Dispatch(kernelMap[ComputeKernels.Initialize], computeBuffer.count / 16 + 1, 1, 1);
        }

        void InitializeUniformBuffer()
        {
            material.SetInt("shootingStarCache", SHOOTINGSTAR_CHACE);
            material.SetFloat("shootingStarSize", shootingStarSize);
            material.SetFloat("aspect", transform.localScale.z / transform.localScale.x);
        }

        void CreateCommandBuffer(Camera cam)
        {
            CommandBuffer cBuf = new CommandBuffer();
            cBuf.name = "Galaxy Instancing";
            cameraMap[cam] = cBuf;
            cBuf.DrawProcedural(transform.localToWorldMatrix, material, 0, MeshTopology.Points, starAmount);
            cam.AddCommandBuffer(camEvent, cBuf);
        }

        void CleanUp()
        {
            foreach (var cam in cameraMap)
            {
                if (cam.Key)
                {
                    cam.Key.RemoveCommandBuffer(camEvent, cam.Value);
                }
            }
            if (computeBuffer != null)
            {
                computeBuffer.Release();
            }
        }

        void CheckCount<T>(IList<T> list, bool isErrorReturn, int min = 0)
        {
            if (list.Count() <= min)
            {
                if (isErrorReturn)
                {
                    Debug.LogErrorFormat("{0} count is {1}, Please set over {2}", list, list.Count, min);
                    return;
                }
                else
                {
                    Debug.LogWarningFormat("{0} count is {1}, Please set over {2}", list, list.Count, min);
                }
            }
        }

        void SetShootingStarTimer(bool isFull)
        {
            shootingStarTimer = (isFull) ? fullShootingStarInterval + Random.Range(0, fullShootingStarRandomizeRange) : shootingStarInterval + Random.Range(0, shootingStarRandomizeRange);
        }

        void OnWillRenderObject()
        {
            if (!gameObject.activeInHierarchy || !enabled)
            {
                CleanUp();
                return;
            }
            Camera cam = Camera.current;
            if (!cam) return;

            if (cameraMap.ContainsKey(cam)) return;
            CreateCommandBuffer(cam);
        }

        void OnRenderObject()
        {
            material.SetBuffer("buf", computeBuffer);
            material.SetInt("hugeStarRatio", (hugeStarRatio == 0) ? starAmount + 1 : Mathf.FloorToInt(starAmount / hugeStarRatio));
            computeShader.SetBuffer(kernelMap[ComputeKernels.Iterator], "buf", computeBuffer);
            computeShader.Dispatch(kernelMap[ComputeKernels.Iterator], computeBuffer.count / 16 + 1, 1, 1);
        }

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            shootingStarTimer -= Time.deltaTime;
            if (shootingStarTimer <= 0)
            {
                computeShader.SetInt("shootStar", 1);
                computeShader.SetInt("shootId", shootId);
                SetShootingStarTimer(isFullOfShootingStars);
                shootId++;
                if (shootId == SHOOTINGSTAR_CHACE) shootId = 0;
            }
            else
            {
                computeShader.SetInt("shootStar", 0);
            }
        }

        public void SetFullOfShootingStarMode()
        {
            isFullOfShootingStars = true;
            SetShootingStarTimer(isFullOfShootingStars);
        }

        public void SetNormalShootingStarMode()
        {
            isFullOfShootingStars = false;
            SetShootingStarTimer(isFullOfShootingStars);
        }
    }
}