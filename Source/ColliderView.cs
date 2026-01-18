using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace DebugMod
{
    public class CollLayer
    {
        public int index;
        public bool isEnabled;
        public Color color;
        public string name;

        public CollLayer(int index, bool isEnabled, Color color, string name)
        {
            this.index = index;
            this.isEnabled = isEnabled;
            this.color = color;
            this.name = name;
        }
    }

    public class ColliderView : MonoBehaviour
    {
        private bool _isEnabled = false;
        private bool _showUI = false;
        private Material _lineMaterial;
        private List<Collider2D> _colliders = new List<Collider2D>();
        private float _nextScanTime = 0f;
        private Vector2 _scrollPosition;

        public static ColliderView Instance { get; private set; }

        private CollLayer[] _collPresets = new CollLayer[]
        {
            new CollLayer(0, true, new Color(1f, 1f, 1f, 0.3f), "Default"),
            new CollLayer(1, false, new Color(1f, 1f, 1f, 0.2f), "TransparentFX"),
            new CollLayer(2, false, new Color(1f, 1f, 1f, 0.2f), "Ignore Raycast"),
            new CollLayer(3, false, new Color(0.5f, 0.5f, 0.5f, 0.2f), "BackEffect"),
            new CollLayer(4, false,  new Color(0f, 0.5f, 1f, 0.5f), "Water"),
            new CollLayer(5, false, new Color(1f, 1f, 1f, 0.2f), "UI"),
            new CollLayer(6, true,  new Color(0f, 1f, 0f, 0.8f), "Player"),
            new CollLayer(7, true,  new Color(1f, 1f, 1f, 0.6f), "Ground"),
            new CollLayer(8, true,  new Color(1f, 0f, 0f, 0.8f), "Enemy"),
            new CollLayer(9, true,  new Color(1f, 1f, 1f, 0.6f), "Wall"),
            new CollLayer(10, true, new Color(0.7f, 0.7f, 0.7f, 0.8f), "HardWall"),
            new CollLayer(11, true, new Color(1f, 0.8f, 0.4f, 0.5f), "Object"),
            new CollLayer(12, false, new Color(1f, 0.9f, 0f, 0.9f), "EnemyWeakPoint"),
            new CollLayer(13, false, new Color(0.6f, 0.3f, 0f, 0.8f), "Door"),
            new CollLayer(14, false, new Color(1f, 1f, 1f, 0.2f), "FrontEffect"),
            new CollLayer(15, true, new Color(1f, 0.6f, 0f, 0.4f), "Area"),
            new CollLayer(16, true, new Color(1f, 0.5f, 0f, 0.8f), "Arrow"),
            new CollLayer(17, true, new Color(0f, 1f, 0.5f, 0.7f), "Platform"),
            new CollLayer(18, true, new Color(0.2f, 0.6f, 1f, 0.7f), "InteractiveWall"),
            new CollLayer(19, true, new Color(1f, 0f, 1f, 0.9f), "Boss"),
            new CollLayer(20, false, new Color(1f, 1f, 0f, 0.4f), "WeakRange"),
            new CollLayer(21, false, new Color(1f, 1f, 1f, 0.2f), "Effect"),
            new CollLayer(22, false, new Color(0.8f, 0.8f, 0.8f, 0.3f), "Steam"),
            new CollLayer(23, false, new Color(1f, 0f, 0f, 1f), "Spike"),
            new CollLayer(24, false, new Color(0.5f, 0.5f, 0.5f, 0.3f), "BufferMap"),
            new CollLayer(25, false, new Color(1f, 1f, 1f, 0.2f), "Front Props"),
            new CollLayer(26, false, new Color(0f, 1f, 1f, 0.8f), "ThrowShuriken"),
            new CollLayer(27, true, new Color(0f, 1f, 0.5f, 0.9f), "MovingPlatform"),
            new CollLayer(28, true, new Color(1f, 0.8f, 0f, 0.7f), "InteractiveObject"),
            new CollLayer(29, true, new Color(1f, 1f, 1f, 0.1f), "OilBackground"),
            new CollLayer(30, false, new Color(1f, 1f, 1f, 0.2f), "ForceRender")
        };

        private void Awake()
        {
            Instance = this;
            CreateLineMaterial();
            SceneManager.activeSceneChanged += (s1, s2) => ScanColliders();
        }

        private void OnEnable()
        {
            Camera.onPostRender += DrawCollidersLegacy;
            RenderPipelineManager.endContextRendering += DrawCollidersURP;
        }

        private void OnDisable()
        {
            Camera.onPostRender -= DrawCollidersLegacy;
            RenderPipelineManager.endContextRendering -= DrawCollidersURP;
        }

        private void Update()
        {
            // 偵測按下 F11 的瞬間
            if (Input.GetKeyDown(DebugMod.ColliderViewKey.Value))
            {
                // 如果同時按住 Control
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    _showUI = !_showUI;
                    // 如果打開選單時顯示還沒開，則自動幫忙開啟顯示
                    if (_showUI && !_isEnabled)
                    {
                        _isEnabled = true;
                        ScanColliders();
                    }
                }
                else // 單純只按下 F11
                {
                    Toggle();
                }
            }

            if (!_isEnabled) return;

            // 定時刷新
            if (Time.unscaledTime > _nextScanTime)
            {
                ScanColliders();
                _nextScanTime = Time.unscaledTime + 3f;
            }
        }

        public void Toggle()
        {
            _isEnabled = !_isEnabled;
            if (_isEnabled) ScanColliders();
        }

        private void OnGUI()
        {
            if (!_showUI) return;

            // 取得 DebugInfoView 位置，避免重疊
            float startY = 10f;
            if (DebugInfoView.CurrentWindowRect != Rect.zero)
            {
                startY = DebugInfoView.CurrentWindowRect.yMax + 10f;
            }

            Rect windowRect = new Rect(10, startY, 260, 450);

            _scrollPosition = GUI.BeginScrollView(
                new Rect(windowRect.x + 5, startY + 30, 245, 410),
                _scrollPosition,
                new Rect(0, 0, 220, _collPresets.Length * 25)
            );

            for (int i = 0; i < _collPresets.Length; i++)
            {
                var layer = _collPresets[i];
                GUIStyle textStyle = new GUIStyle(GUI.skin.toggle);
                Color uiColor = new Color(layer.color.r, layer.color.g, layer.color.b, 1.0f);

                textStyle.normal.textColor = uiColor;
                textStyle.onNormal.textColor = uiColor;
                textStyle.fontStyle = FontStyle.Bold;

                layer.isEnabled = GUI.Toggle(new Rect(0, i * 25, 200, 20), layer.isEnabled, $"{i}: {layer.name}", textStyle);
            }

            GUI.EndScrollView();
        }

        private void ScanColliders()
        {
            _colliders.Clear();
            _colliders.AddRange(Object.FindObjectsOfType<Collider2D>());
        }

        private void DrawLogic(Camera cam)
        {
            if (!_isEnabled || _colliders.Count == 0 || cam.name.Contains("UI")) return;

            if (_lineMaterial == null) CreateLineMaterial();
            _lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;

            foreach (var col in _colliders)
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                int layerIdx = col.gameObject.layer;
                if (layerIdx < 0 || layerIdx >= _collPresets.Length) continue;

                var config = _collPresets[layerIdx];
                if (!config.isEnabled) continue;

                DrawColliderShape(col, config.color);
            }
            GL.PopMatrix();
        }

        private void DrawColliderShape(Collider2D col, Color color)
        {
            GL.Begin(GL.LINES);
            GL.Color(color);

            if (col is BoxCollider2D box)
            {
                Vector2 size = box.size * 0.5f;
                Vector2 offset = box.offset;
                Vector3 p1 = col.transform.TransformPoint(offset + new Vector2(-size.x, -size.y));
                Vector3 p2 = col.transform.TransformPoint(offset + new Vector2(size.x, -size.y));
                Vector3 p3 = col.transform.TransformPoint(offset + new Vector2(size.x, size.y));
                Vector3 p4 = col.transform.TransformPoint(offset + new Vector2(-size.x, size.y));
                GL.Vertex(p1); GL.Vertex(p2); GL.Vertex(p2); GL.Vertex(p3); GL.Vertex(p3); GL.Vertex(p4); GL.Vertex(p4); GL.Vertex(p1);
            }
            else if (col is CircleCollider2D circle)
            {
                float r = circle.radius;
                Vector3 center = col.transform.TransformPoint(circle.offset);
                int seg = 24;
                for (int i = 0; i < seg; i++)
                {
                    float a1 = i * Mathf.PI * 2 / seg;
                    float a2 = (i + 1) * Mathf.PI * 2 / seg;
                    GL.Vertex(center + col.transform.TransformVector(new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0)));
                    GL.Vertex(center + col.transform.TransformVector(new Vector3(Mathf.Cos(a2) * r, Mathf.Sin(a2) * r, 0)));
                }
            }
            else if (col is PolygonCollider2D poly)
            {
                for (int i = 0; i < poly.pathCount; i++)
                {
                    Vector2[] path = poly.GetPath(i);
                    for (int j = 0; j < path.Length; j++)
                    {
                        GL.Vertex(col.transform.TransformPoint(path[j]));
                        GL.Vertex(col.transform.TransformPoint(path[(j + 1) % path.Length]));
                    }
                }
            }
            else if (col is EdgeCollider2D edge)
            {
                Vector2[] points = edge.points;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    GL.Vertex(col.transform.TransformPoint(points[i]));
                    GL.Vertex(col.transform.TransformPoint(points[i + 1]));
                }
            }
            GL.End();
        }

        private void CreateLineMaterial()
        {
            if (_lineMaterial != null) return;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            _lineMaterial.SetInt("_ZWrite", 0);
        }

        private void DrawCollidersLegacy(Camera cam) => DrawLogic(cam);
        private void DrawCollidersURP(ScriptableRenderContext context, List<Camera> cameras) { foreach (var cam in cameras) DrawLogic(cam); }
    }
}