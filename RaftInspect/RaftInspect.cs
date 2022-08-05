using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace pp.RaftMods.dbg
{
    public class RaftInspector : Mod
    {
        public const string VERSION     = "0.0.1";
        public const string APP_NAME    = "RaftInspector";

        public static string DataDirectory => Path.Combine(Application.persistentDataPath, "Mods", APP_NAME);
        public static bool Loaded = false;

        public void Start()
        {
            //need early entry point. QModManager injects into GameInput.Awake
            var patch = new RaftInspector();
            patch.Load();
        }

        private static DebugPanel m_goBrowser;

        private void Load()
        {
            if (Loaded)
            {
                Util.LogW("Already loaded. Doing nothing.");
                return;
            }

            if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);

            m_goBrowser = DebugPanel.CreateNew();
            Util.Log("Raft inspector " + VERSION + " intialized!");
            Loaded = true;
        }

        private void OnDestroy()
        {
            if (m_goBrowser)
            {
                GameObject.Destroy(m_goBrowser);
            }
            Loaded = false;
        }

        [ConsoleCommand(name: "reflectItemID", docs: "Provided an Item_Base ID, prints the item objects member names and values to the console.")]
        private static void ReflectItemDefinitionID(string[] _args)
        {
            if(_args.Length != 1)
            {
                Debug.LogError("Invalid command synthax. \"reflectItemID <ID_integer>\" expected.");
                return;
            }

            if(!int.TryParse(_args[0], out int _id))
            {
                Debug.LogError("The provided ID " + _args[0] + " could not be parsed to an integer. Check your input.");
                return;
            }

            Item_Base item = ItemManager.GetItemByIndex(_id);

            if(!item)
            {
                Debug.LogWarning("No item found for ID " + _id);
                return;
            }

            PrintObjectReflection(item);
        }

        [ConsoleCommand(name: "reflectItemName", docs: "Provided an Item_Base unique name, prints the item objects member names and values to the console.")]
        private static void ReflectItemDefinitionName(string[] _args)
        {
            if (_args.Length != 1)
            {
                Debug.LogError("Invalid command synthax. \"reflectItemName <UniqueName_string>\" expected.");
                return;
            }

            Item_Base item = ItemManager.GetItemByName(_args[0]);

            if (!item)
            {
                Debug.LogWarning("No item found for Name \"" + _args[0] + "\"");
                return;
            }

            PrintObjectReflection(item);
        }

        [ConsoleCommand(name: "listBuildableItems", docs: "Lists all buildable items.")]
        private static void ListBuildableItems(string[] _args)
        {
            List<Item_Base> items = ItemManager.GetBuildableItems();

            if (!items.Any())
            {
                Debug.LogWarning("No buildable items found.");
                return;
            }

            Debug.Log("### Buildable items (" + items.Count + ") ###\n- " + 
                string.Join("\n- ", items.OrderBy(_o => _o.UniqueName).Select(_o => _o.UniqueName + " (" + _o.UniqueIndex + ")")));
        }

        [ConsoleCommand(name: "listAllItems", docs: "Lists all buildable items.")]
        private static void ListAllItems(string[] _args)
        {
            List<Item_Base> items = ItemManager.GetAllItems();

            if (!items.Any())
            {
                Debug.LogWarning("No items found.");
                return;
            }

            Debug.Log("### All items (" + items.Count + ") ###\n- " +
                string.Join("\n- ", items.OrderBy(_o => _o.UniqueName).Select(_o => _o.UniqueName + " (" + _o.UniqueIndex + ")")));
        }

        [ConsoleCommand(name: "listCreatorItems", docs: "Lists all buildable items.")]
        private static void ListCreatorItemsItems(string[] _args)
        {
            List<Item_Base> items = Traverse.Create(typeof(BlockCreator)).Field("buildableItems").GetValue<List<Item_Base>>();

            if (!items.Any())
            {
                Debug.LogWarning("No items found.");
                return;
            }

            Debug.Log("### All items (" + items.Count + ") ###\n- " +
                string.Join("\n- ", items.OrderBy(_o => _o.UniqueName).Select(_o => _o.UniqueName + " (" + _o.UniqueIndex + ")")));
        }

        private static void PrintObjectReflection(Item_Base _object)
        {
            var fields = _object.GetType().GetFields();
            if (!fields.Any())
            {
                Debug.LogWarning(_object.UniqueName + " is empty. No fields found.");
                return;
            }
            Debug.Log("### Fields \"" + _object.UniqueName + "\" ###");
            Debug.Log(
                string.Join("\n- ", 
                    fields.Select(_o => _o.Name + "\n\t\t-- " +
                        string.Join("\n\t\t-- ", _o.GetValue(_object).GetType().GetProperties().Select(_n => _n.Name + ": " + _n.GetValue(_o.GetValue(_object)))))));
        }
    }

    public class DebugPanel : MonoBehaviour
    {
        internal static DebugPanel CreateNew() => new GameObject("_debug_").AddComponent<DebugPanel>();
        public static DebugPanel Get = null;

        public const float PANEL_PADDING_TOP = 5f;
        public const float PANEL_PADDING_LEFT = 5f;
        public const float OPT_BUTTON_HEIGHT = 25f;
        public const float OPT_BUTTON_WIDTH = 25f;

        public const float DEFAULT_TXT_FIELD_WIDTH = 35f;
        public const float SETTINGS_HEIGHT = 25f;
        public const float SETTINGS_OFFSET_BOTTOM = 5f;

        public const float PANEL_MIN_WIDTH = 290f;
        public const float PANEL_MIN_HEIGHT = 50f;

        public Config PanelConfig => m_config;

        public Browser BrowserDrawer => m_browserDrawer;

        internal Vector2 Position
        {
            get
            {
                return m_panelPosition;
            }
            set
            {
                m_panelPosition = new Vector2(
                    Mathf.Round(Mathf.Clamp(value.x, 0, Screen.width)),
                    Mathf.Round(Mathf.Clamp(value.y, 0, Screen.height)));
            }
        }

        internal Vector2 Size
        {
            get
            {
                return m_panelSize;
            }
            set
            {
                m_panelSize = new Vector2(
                    Mathf.Round(Mathf.Max(value.x, PANEL_MIN_WIDTH)),
                    Mathf.Round(Mathf.Max(value.y, PANEL_MIN_HEIGHT)));
            }
        }

        private bool m_showConsole = true;
        private bool m_showGOBrowser = false;
        private bool m_showSettings = false;

        private Vector2 m_panelPosition = Vector2.one * 55f;
        private Vector2 m_panelSize = new Vector2(Screen.width * 0.5f, Screen.height * 0.75f);

        private Browser m_browserDrawer = new Browser();

        //private GUIStyle m_consoleStyle = new GUIStyle();
        private GUIStyle m_panelStyle = new GUIStyle();
        private GUIStyle m_browserStyle = new GUIStyle();
        private GUIStyle m_settingsStyle = new GUIStyle();

        //private Texture2D m_consoleBackgroundColor;
        private Texture2D m_browserBackgroundColor;
        private Texture2D m_panelBackgroundTexture;

        private Vector2 m_oldPanelPos;
        private Vector2 m_oldPanelSize;

        private bool m_changePanelPosition;
        private bool m_changePanelSize;

        private Config m_config;

        private bool m_errorOccurred;

        public void ToggleVisibility()
        {
            m_config.ConsoleVisible = !m_config.ConsoleVisible;

            if (!m_config.ConsoleVisible)
            {
                m_changePanelSize = false;
                m_changePanelPosition = false;
            }
        }

        #region ENGINE_CALLBACKS
        private void Awake()
        {
            try
            {
                if (Get != null)
                {
                    Util.LogW("There is already another active instance of GOBrowser.");
                    Destroy(gameObject);
                    return;
                }

                Get = this;
                DontDestroyOnLoad(this);

                m_config = Config.Load();

                Position = m_config.Position;
                Size = m_config.Size;

                var col = Color.gray * 1.2f;
                col.a = 0.25f;
                m_panelBackgroundTexture = Util.CreateTextureFromColor(col);
                //m_consoleBackgroundColor = Util.CreateTextureFromColor(Color.black);
                col = Color.gray * 0.75f;
                col.a = 0.25f;
                m_browserBackgroundColor = Util.CreateTextureFromColor(col);

                m_panelStyle.normal.background = m_panelBackgroundTexture;
                //m_consoleStyle.normal.background = m_consoleBackgroundColor;
                m_settingsStyle.normal.background = m_browserBackgroundColor;
                m_browserStyle.normal.background = m_browserBackgroundColor;

                //m_consoleStyle.fontSize = m_config.ConsoleFontSize;

                m_panelStyle.normal.textColor = Color.black;

                m_settingsStyle.fontSize = 16;
                m_settingsStyle.normal.textColor = Color.black;
                m_settingsStyle.alignment = TextAnchor.MiddleLeft;

                m_browserDrawer.Start();
            }
            catch (System.Exception _e)
            {
                Util.LogE("Startup error occurred: " + _e.Message);
                Util.LogE(_e.StackTrace);
                m_errorOccurred = true;
            }
        }

        private void OnDestroy()
        {
            try
            {
                m_config.Position = Position;
                m_config.Size = Size;
                m_config?.Save();

                if (m_panelBackgroundTexture != null)
                    Destroy(m_panelBackgroundTexture);
                Get = null;
            }
            catch (System.Exception _e)
            {
                Util.LogE("Shutdown error occurred: " + _e.Message);
                Util.LogE(_e.StackTrace);
                m_errorOccurred = true;
            }
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F4))
            {
                ToggleVisibility();
            }

            if(Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.C))
            {
                RAPI.ToggleCursor(!Cursor.visible);
            }

            if (m_errorOccurred || !m_config.ConsoleVisible) return;
        }

        private void OnGUI()
        {
            if (!m_config.ConsoleVisible) return;

            GUILayout.Label("Size " + Size);
            GUILayout.Label("Position " + Position);

            if (m_errorOccurred)
            {
                m_panelStyle.normal.textColor = Color.red;
                GUI.Box(new Rect(70f, 50f, 275f, 50f), "The console encountered an unexpected error!\nCheck the log files for further information.\nPress F6 to hide this message.", m_panelStyle);
                return;
            }

            try
            {
                GUI.Box(new Rect(m_panelPosition.x, m_panelPosition.y, m_panelSize.x, m_panelSize.y), "", m_panelStyle);
                GUILayout.BeginArea(new Rect(m_panelPosition.x, m_panelPosition.y, m_panelSize.x, m_panelSize.y));
                GUILayout.BeginHorizontal(GUILayout.Width(m_panelSize.x));
                GUILayout.Space(PANEL_PADDING_LEFT);
                GUILayout.BeginVertical(GUILayout.Height(m_panelSize.y));
                GUILayout.Space(PANEL_PADDING_TOP);

                GUILayout.BeginHorizontal();
                m_changePanelPosition = GUILayout.Toggle(m_changePanelPosition, new GUIContent("", "Change panel position"), GUILayout.Width(OPT_BUTTON_WIDTH), GUILayout.Width(OPT_BUTTON_HEIGHT));
                if (m_changePanelPosition)
                {
                    Reposition();
                }

                var pnlSize = GUILayout.Toggle(m_changePanelSize, new GUIContent("", "Change panel size"), GUILayout.Width(OPT_BUTTON_WIDTH), GUILayout.Width(OPT_BUTTON_HEIGHT));
                if (pnlSize != m_changePanelSize)
                {
                    m_oldPanelSize = Size;
                    m_oldPanelPos = Position;
                    m_changePanelSize = pnlSize;
                }

                if (m_changePanelSize)
                {
                    Resize();
                }

                GUILayout.Label("RaftInspector (v. " + RaftInspector.VERSION + ")");

                GUILayout.FlexibleSpace();
                //if (GUILayout.Button($"{(m_showSettings ? "Hide" : "Show")} settings"))
                //{
                //    m_showSettings = !m_showSettings;
                //}
                if (GUILayout.Button("X"))
                {
                    ToggleVisibility();
                }
                GUILayout.EndHorizontal();

                //if (m_showSettings)
                //{
                //    DrawSettings();
                //    GUILayout.Space(SETTINGS_OFFSET_BOTTOM);
                //}

                m_browserDrawer.Draw(m_browserStyle);

                GUILayout.Space(PANEL_PADDING_TOP);
                GUILayout.EndVertical();
                GUILayout.Space(PANEL_PADDING_LEFT);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            catch (System.Exception _e)
            {
                Util.LogE("UI render error occurred: " + _e.Message);
                Util.LogE(_e.StackTrace);
                m_errorOccurred = true;
            }
        }
        #endregion

        private void Resize()
        {
            var pos = new Vector2(
                            Input.mousePosition.x - OPT_BUTTON_WIDTH * 1.65f - PANEL_PADDING_LEFT,
                            Screen.height - Input.mousePosition.y - PANEL_PADDING_TOP - OPT_BUTTON_HEIGHT * 0.5f);
            Position = new Vector2(Mathf.Clamp(pos.x, 0f, m_oldPanelPos.x + m_oldPanelSize.x * 0.5f), Mathf.Clamp(pos.y, 0f, m_oldPanelPos.y + m_oldPanelSize.y * 0.5f));
            var size = m_oldPanelSize - (m_panelPosition - m_oldPanelPos);
            Size = new Vector2(Mathf.Clamp(size.x, PANEL_MIN_WIDTH, Screen.width), Mathf.Clamp(size.y, PANEL_MIN_HEIGHT, Screen.height));
        }

        private void Reposition()
        {
            var actualY = Screen.height - Input.mousePosition.y;
            var pos = new Vector2(
                Input.mousePosition.x - OPT_BUTTON_WIDTH * 0.5f - PANEL_PADDING_LEFT,
                actualY - PANEL_PADDING_TOP - OPT_BUTTON_HEIGHT * 0.5f);
            Position = new Vector2(Mathf.Clamp(pos.x, 0f, Screen.width - Size.x), Mathf.Clamp(pos.y, 0f, Screen.height - Size.y));
        }

        //private void DrawSettings()
        //{
        //    GUILayout.BeginHorizontal(m_settingsStyle, GUILayout.Height(SETTINGS_HEIGHT));
        //    try
        //    {
        //        var val = string.IsNullOrEmpty(m_config.ConsoleFontSize.ToString()) ? "0" : m_config.ConsoleFontSize.ToString();
        //        var fontSize = int.Parse(GUILayout.TextField(val, GUILayout.Width(DEFAULT_TXT_FIELD_WIDTH), GUILayout.ExpandHeight(true)));
        //        if (fontSize != m_config.ConsoleFontSize)
        //        {
        //            m_consoleStyle.fontSize = fontSize;
        //            m_config.ConsoleFontSize = fontSize;
        //        }
        //    }
        //    catch { }
        //    GUILayout.Label("Browser height");
        //    GUILayout.FlexibleSpace();
        //    try
        //    {
        //        var val = string.IsNullOrEmpty(m_config.ConsoleFontSize.ToString()) ? "0" : m_config.ConsoleFontSize.ToString();
        //        var fontSize = int.Parse(GUILayout.TextField(val, GUILayout.Width(DEFAULT_TXT_FIELD_WIDTH), GUILayout.ExpandHeight(true)));
        //        if (fontSize != m_config.ConsoleFontSize)
        //        {
        //            m_consoleStyle.fontSize = fontSize;
        //            m_config.ConsoleFontSize = fontSize;
        //        }
        //    }
        //    catch { }
        //    GUILayout.Label("Font size");
        //    m_config.ConsoleAutoScroll = GUILayout.Toggle(m_config.ConsoleAutoScroll, $"Auto scroll", GUILayout.ExpandHeight(true));
        //    GUILayout.Space(10f);
        //    m_config.ConsoleShowType = GUILayout.Toggle(m_config.ConsoleShowType, $"Show type", GUILayout.ExpandHeight(true));
        //    m_config.ConsoleShowTime = GUILayout.Toggle(m_config.ConsoleShowTime, $"Show time", GUILayout.ExpandHeight(true));
        //    GUILayout.Space(10f);
        //    m_config.BrowserShowValueChangeButtons = GUILayout.Toggle(m_config.BrowserShowValueChangeButtons, $"Show value buttons", GUILayout.ExpandHeight(true));
        //    GUILayout.Space(10f);
        //    m_config.ConsoleShowType = GUILayout.Toggle(m_config.ConsoleShowType, $"Info", GUILayout.ExpandHeight(true));
        //    m_config.ConsoleShowTime = GUILayout.Toggle(m_config.ConsoleShowTime, $"Warning", GUILayout.ExpandHeight(true));
        //    m_config.ConsoleAutoScroll = GUILayout.Toggle(m_config.ConsoleAutoScroll, $"Error", GUILayout.ExpandHeight(true));

        //    GUILayout.EndHorizontal();
        //}
    }

    public class Browser
    {
        private static Dictionary<System.Type, ITypeDrawer> s_typeDrawers = new Dictionary<System.Type, ITypeDrawer>();

        private Scene[] m_activeScenes;

        private GameObject m_selectedGameObject;

        private Vector2 m_gameObjectScroll  = Vector2.zero;
        private Vector2 m_componentScroll   = Vector2.zero;

        private Dictionary<GameObject, bool> m_objectFoldState      = new Dictionary<GameObject, bool>();
        private Dictionary<Component, bool> m_componentFoldStates   = new Dictionary<Component, bool>();

        private Dictionary<string, GameObject[]> m_rootObjects;

        private bool m_paused;

        public static void RegisterDrawer(System.Type _type, ITypeDrawer _drawer)
        {
            if(s_typeDrawers.ContainsKey(_type))
            {
                Util.LogW("Failed to register type drawer. Type \"" + _type.Name + "\" is already registered.");
                return;
            }
            s_typeDrawers.Add(_type, _drawer);
        }

        #region ENGINE_CALLBACKS
        public void Start()
        {
            SceneManager.sceneLoaded -= OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnActiveSceneChanged;

            RegisterDrawer(typeof(bool),            new BoolDrawer());
            RegisterDrawer(typeof(short),           new ShortDrawer());
            RegisterDrawer(typeof(double),          new DoubleDrawer());
            RegisterDrawer(typeof(float),           new FloatDrawer());
            RegisterDrawer(typeof(long),            new LongDrawer());
            RegisterDrawer(typeof(int),             new IntDrawer());
            RegisterDrawer(typeof(string),          new StringDrawer());
            RegisterDrawer(typeof(Vector3),         new Vector3Drawer());
            RegisterDrawer(typeof(Vector2),         new Vector2Drawer());
            RegisterDrawer(typeof(Quaternion),      new QuaternionDrawer());
            RegisterDrawer(typeof(GameObject),      new GameObjectDrawer());
            RegisterDrawer(typeof(Color),           new ColorDrawer());
            RegisterDrawer(typeof(Object),          new ObjectDrawer());
            RegisterDrawer(typeof(System.Enum),     new EnumDrawer());
            RegisterDrawer(typeof(System.Array),    new ArrayDrawer());
        }

        public void Draw(GUIStyle _browserStyle)
        {
            GUILayout.BeginVertical(_browserStyle, GUILayout.ExpandHeight(true));

            if (m_rootObjects != null && m_rootObjects.Count > 0)
            {
                DrawObjectTree();
            }
            else
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                GUILayout.Label(">> No GameObjects in scene. Try refreshing. <<");
                if(GUILayout.Button("Refresh", GUILayout.Width(100f)))
                {
                    ReloadScenes();
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }

            GUILayout.EndVertical();
        }

        private void OnActiveSceneChanged(Scene _scene, LoadSceneMode _mode)
        {
            ReloadScenes();
        }
        #endregion

        private void ReloadScenes()
        {
            ResetSelection();

            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                scenes.Add(SceneManager.GetSceneAt(i));
            }

            m_activeScenes  = scenes.ToArray();
            m_rootObjects   = m_activeScenes
                .Select(_o => new KeyValuePair<string, GameObject[]>(_o.name, _o.GetRootGameObjects()))
                .ToDictionary(_o => _o.Key, _o => _o.Value);
        }

        private void ResetSelection()
        {
            m_objectFoldState.Clear();
            m_rootObjects           = new Dictionary<string, GameObject[]>();
            m_selectedGameObject    = null;
        }

        private void DrawObjectTree()
        {
            GUILayout.BeginHorizontal();

                GUILayout.BeginVertical(/*GUILayout.MinWidth((DebugPanel.Get.Size.x - DebugPanel.PANEL_PADDING_LEFT * 2f) * 0.6f)*/);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"### Hierarchy ({m_activeScenes?.Length ?? 0} scene(s) loaded) ###", GUILayout.ExpandWidth(true));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Refresh scenes"))
                    {
                        ReloadScenes();
                    }
                    if(GUILayout.Button(m_paused ? "Resume game" : "Pause game"))
                    {
                        m_paused = !m_paused;
                        Time.timeScale = m_paused ? 0f : 1f; 
                    }

                    GUILayout.EndHorizontal();

                    m_gameObjectScroll = GUILayout.BeginScrollView(m_gameObjectScroll, GUILayout.ExpandWidth(true));
                    foreach(var scene in m_rootObjects)
                    {
                        foreach (var obj in scene.Value)
                        {
                            DrawObjectTreeItem(new KeyValuePair<string, GameObject>(scene.Key, obj));
                        }
                    }
                    GUILayout.EndScrollView();
                GUILayout.EndVertical();


               GUILayout.BeginVertical(GUILayout.ExpandWidth(true) /*GUILayout.MinWidth((DebugPanel.Get.Size.x - DebugPanel.PANEL_PADDING_LEFT * 2f) * 0.4f)*/);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"### Inspector ###", GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                DebugPanel.Get.PanelConfig.BrowserInspectorDebug = GUILayout.Toggle(DebugPanel.Get.PanelConfig.BrowserInspectorDebug, "Debug");
                GUILayout.EndHorizontal();

                if (m_selectedGameObject != null)
                {
                    m_componentScroll = GUILayout.BeginScrollView(m_componentScroll, GUILayout.ExpandWidth(true));
                        DrawObjectInspector(m_selectedGameObject);
                    GUILayout.EndScrollView();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("<< No GameObject selected. >>");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
               GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawObjectTreeItem(KeyValuePair<string, GameObject> _object)
        {
            var go = _object.Value;

            if (!go) return;

            if (!m_objectFoldState.ContainsKey(go))
            {
                m_objectFoldState.Add(go, false);
            }

            GUILayout.BeginHorizontal();
                GUI.color = (go == m_selectedGameObject && go.activeInHierarchy ? Color.green :
                                go == m_selectedGameObject && !go.activeInHierarchy ? Color.green * 0.6f :
                                go.activeInHierarchy ? Color.white : Color.gray);

                if (go.transform.childCount <= 0)
                {
                    GUILayout.Space(20f);
                    GUILayout.Label(go.name);
                }
                else
                {
                    m_objectFoldState[go] = GUILayout.Toggle(m_objectFoldState[go], go.name);
                }
                GUI.color = Color.white;
                if(GUILayout.Button(go.activeSelf ? "Disable" : "Enable"))
                {
                    go.SetActive(!go.activeSelf);
                }
                if (GUILayout.Button(m_selectedGameObject == go ? "Deselect" : "Select"))
                {
                    m_selectedGameObject = (m_selectedGameObject == go ? null : go);
                }
                GUILayout.Label("Scene: " + _object.Key);
                GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (m_objectFoldState[go])
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15f);
                GUILayout.BeginVertical();
                if (go.transform.childCount <= 0)
                {
                    GUILayout.Label("No children!");
                }
                else
                {
                    for (int i = 0; i < go.transform.childCount; ++i)
                    {
                        DrawObjectTreeItem(new KeyValuePair<string, GameObject>(_object.Key, go.transform.GetChild(i).gameObject));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawObjectInspector(GameObject _object)
        {
            GUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(50f));
                _object.name = GUILayout.TextField(_object.name ?? "", GUILayout.MinWidth(150f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
                GUILayout.Label("Layer: "   + LayerMask.LayerToName(_object.layer));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Tag");
                _object.tag = GUILayout.TextField(_object.tag ?? "", GUILayout.Width(120f));
            GUILayout.EndHorizontal();

            GUILayout.Box("Transform", GUILayout.ExpandWidth(true), GUILayout.Height(20f));
            GUI.color = Color.white;

            GUILayout.BeginVertical();
            _object.transform.position      = DrawType("Position",  _object.transform.position);
            _object.transform.rotation      = DrawType("Rotation",  _object.transform.rotation);
            _object.transform.localScale    = DrawType("Scale",     _object.transform.localScale);
            GUILayout.EndVertical();

            var cmps = _object.GetComponents<Component>();
            foreach(var cmp in cmps)
            {
                DrawComponent(cmp);
            }
        }

        private void DrawComponent(Component _component)
        {
            if (!(_component is Behaviour)) return;

            var behaviour = _component as Behaviour;
            try
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(behaviour.enabled ? "Disable" : "Enable", GUILayout.Width(60f)))
                {
                    behaviour.enabled = !behaviour.enabled;
                }
                GUI.color = behaviour.enabled ? Color.white : Color.gray;
                GUILayout.Box(behaviour.GetType().Name, GUILayout.ExpandWidth(true), GUILayout.Height(20f));
                GUI.color = Color.white;
                if (GUILayout.Button("x", GUILayout.Width(25f)))
                {
                    Component.Destroy(_component);
                    return;
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginVertical();
            var bFlags = BindingFlags.Public | BindingFlags.Instance;
            if (DebugPanel.Get.PanelConfig.BrowserInspectorDebug)
                bFlags |= BindingFlags.NonPublic | BindingFlags.Static;

            var properties  = _component.GetType().GetProperties(bFlags).ToArray();
            var fields      = _component.GetType().GetFields(bFlags).ToArray();

            foreach(var prop in properties)
            {
                if (!prop.CanRead) continue;
                if(!prop.CanWrite)
                {
                    DrawType(prop.Name, prop.PropertyType, prop.GetValue(_component, null));
                    continue;
                }
                prop.SetValue(_component, DrawType(prop.Name, prop.PropertyType, prop.GetValue(_component, null)), null);
            }

            foreach (var field in fields)
            {
                if(field.IsLiteral)
                {
                    DrawType(field.Name, field.FieldType, field.GetValue(_component));
                    continue;
                }
                field.SetValue(_component, DrawType(field.Name, field.FieldType, field.GetValue(_component)));
            }

            if(DebugPanel.Get.PanelConfig.BrowserInspectorDebug)
            {
                var methods = _component.GetType().GetMethods(bFlags);
                foreach(var meth in methods)
                {
                    DrawComponentMethod(meth);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawComponentMethod(MethodInfo _method)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{(_method.IsStatic ? "static " : "")}{_method.ReturnType} - {_method.Name} ({string.Join(", ", _method.GetParameters().Select(_o => $"{_o.Name} : {_o.ParameterType}").ToArray())})");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private object DrawType(string _label, System.Type _type, object _value)
        {
            if (!s_typeDrawers.ContainsKey(_type))
            {
                var kvP = s_typeDrawers.FirstOrDefault(_o => _type.IsSubclassOf(_o.Key));
                if (kvP.Key != null && kvP.Value != null)
                {
                    _type = kvP.Key;
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(_label);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(_type.Name);
                    GUILayout.EndHorizontal();
                    return _value;
                }
            }
            return s_typeDrawers[_type].Draw(_label, _value);
        }

        private T DrawType<T>(string _label, T _value)
        {
            var type = typeof(T);
            if (!s_typeDrawers.ContainsKey(type))
            {
                var kvP = s_typeDrawers.FirstOrDefault(_o => type.IsSubclassOf(_o.Key));
                if (kvP.Key != null && kvP.Value != null)
                {
                    type = kvP.Key;
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(_label);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(typeof(T).Name);
                    GUILayout.EndHorizontal();
                    return _value;
                }
            }
            return (T)s_typeDrawers[type].Draw(_label, _value);
        }

        private enum EUIState
        {
            ROOTS,
            GAME_OBJECT
        }
    }

    #region DRAWER
    public class DrawerHelper
    {
        public static float DrawCheckFloatField(string _label, float _value)
        {
            float val;
            if (!string.IsNullOrEmpty(_label))
                GUILayout.Label(_label);
            if (DebugPanel.Get.PanelConfig.BrowserShowValueChangeButtons && GUILayout.RepeatButton("<"))
            {
                _value -= 0.01f;
            }
            if (!float.TryParse(GUILayout.TextField($"{_value}", GUILayout.Width(DebugPanel.DEFAULT_TXT_FIELD_WIDTH)), out val))
                return _value;
            if (DebugPanel.Get.PanelConfig.BrowserShowValueChangeButtons && GUILayout.RepeatButton(">"))
            {
                val += 0.01f;
            }
            return val;
        }
    }
    
    public abstract class ATypeDrawer<T> : ITypeDrawer
    {
        public object Draw(string _label, object _object)
        {
            if (!(_object is T))
                return Draw(_label, default(T));
            return Draw(_label, (T)_object);
        }

        protected abstract T Draw(string _label, T _object);
    }

    public interface ITypeDrawer
    {
        object Draw(string _label, object _object);
    }

    public class EnumDrawer : ATypeDrawer<System.Enum>
    {
        protected override System.Enum Draw(string _label, System.Enum _enum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_enum.ToString());
            GUILayout.EndHorizontal();
            return _enum;
        }
    }

    public class ObjectDrawer : ATypeDrawer<Object>
    {
        protected override Object Draw(string _label, Object _unityObject)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(!_unityObject ? "null" : _unityObject.name);
            GUILayout.EndHorizontal();
            return _unityObject;
        }
    }

    public class GameObjectDrawer : ATypeDrawer<GameObject>
    {
        protected override GameObject Draw(string _label, GameObject _gameObject)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(_label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(!_gameObject ? "null" : _gameObject.GetHierarchyPath());
            GUILayout.EndHorizontal();
            return _gameObject;
        }
    }

    public class ColorDrawer : ATypeDrawer<Color>
    {
        protected override Color Draw(string _label, Color _color)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return new Color(
                    DrawerHelper.DrawCheckFloatField("R", _color.r),
                    DrawerHelper.DrawCheckFloatField("G", _color.g),
                    DrawerHelper.DrawCheckFloatField("B", _color.b),
                    DrawerHelper.DrawCheckFloatField("A", _color.a));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class QuaternionDrawer : ATypeDrawer<Quaternion>
    {
        protected override Quaternion Draw(string _label, Quaternion _rotation)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return Quaternion.Euler(
                    DrawerHelper.DrawCheckFloatField("X", _rotation.eulerAngles.x),
                    DrawerHelper.DrawCheckFloatField("Y", _rotation.eulerAngles.y),
                    DrawerHelper.DrawCheckFloatField("Z", _rotation.eulerAngles.z));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class Vector2Drawer : ATypeDrawer<Vector2>
    {
        protected override Vector2 Draw(string _label, Vector2 _vector2)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return new Vector2(
                    DrawerHelper.DrawCheckFloatField("X", _vector2.x),
                    DrawerHelper.DrawCheckFloatField("Y", _vector2.y));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class Vector3Drawer : ATypeDrawer<Vector3>
    {
        protected override Vector3 Draw(string _label, Vector3 _vector3)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return new Vector3(
                    DrawerHelper.DrawCheckFloatField("X", _vector3.x),
                    DrawerHelper.DrawCheckFloatField("Y", _vector3.y),
                    DrawerHelper.DrawCheckFloatField("Z", _vector3.z));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class StringDrawer : ATypeDrawer<string>
    {
        protected override string Draw(string _label, string _string)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return GUILayout.TextField(_string ?? "", GUILayout.MinWidth(75f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class IntDrawer : ATypeDrawer<int>
    {
        protected override int Draw(string _label, int _int)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                int result;
                if (!int.TryParse(GUILayout.TextField($"{_int}", GUILayout.MinWidth(50f)), out result))
                    return 0;
                return result;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class LongDrawer : ATypeDrawer<long>
    {
        protected override long Draw(string _label, long _long)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                long result;
                if (!long.TryParse(GUILayout.TextField($"{_long}", GUILayout.MinWidth(50f)), out result))
                    return 0;
                return result;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class FloatDrawer : ATypeDrawer<float>
    {
        protected override float Draw(string _label, float _float)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return DrawerHelper.DrawCheckFloatField("", _float);
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class DoubleDrawer : ATypeDrawer<double>
    {
        protected override double Draw(string _label, double _double)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                double result;
                if (!double.TryParse(GUILayout.TextField($"{_double}", GUILayout.MinWidth(50f)), out result))
                    return 0;
                return result;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class ShortDrawer : ATypeDrawer<short>
    {
        protected override short Draw(string _label, short _short)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                short result;
                if (!short.TryParse(GUILayout.TextField($"{_short}", GUILayout.MinWidth(50f)), out result))
                    return 0;
                return result;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class BoolDrawer : ATypeDrawer<bool>
    {
        protected override bool Draw(string _label, bool _bool)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                return GUILayout.Toggle(_bool, "", GUILayout.MinWidth(50f));
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }

    public class ArrayDrawer : ATypeDrawer<System.Array>
    {
        protected override System.Array Draw(string _label, System.Array _array)
        {
            try
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_label);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_array.GetType().GetElementType().Name + "[" + _array.Length + "]");
                return _array;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }
    #endregion

    #region UTIL
    public static class Util
    {
        public static string GetHierarchyPath(this GameObject _source)
        {
            string path = "";
            Transform target = _source.transform;
            for (int i = 0; i < _source.transform.hierarchyCount; ++i)
            {
                path = target.name + (i <= 0 ? "" : "/") + path;
                target = target.parent;
                if (!target)
                    break;
            }
            return path;
        }

        public static Texture2D CreateTextureFromColor(Color _color)
        {
            var tx = new Texture2D(1, 1, TextureFormat.RGB24, false);
            tx.SetPixel(0, 0, _color);
            tx.Apply();
            return tx;
        }

        public static void Log(string _message)
        {
            Debug.Log($"[RaftInspector] {_message}");
        }

        public static void LogW(string _message)
        {
            Debug.LogWarning($"[RaftInspector] {_message}");
        }

        public static void LogE(string _message)
        {
            Debug.LogError($"[RaftInspector] {_message}");
        }
    }
    
    [System.Serializable]
    public class Config
    {
        public const string CONFIG_FILE_NAME = "config.xml";
        public static string ConfigFilePath => Path.Combine(RaftInspector.DataDirectory, CONFIG_FILE_NAME);

        public SerializableVector Position = new SerializableVector(50f, 50f);
        public SerializableVector Size = new SerializableVector(Screen.width * 0.5f, Screen.height * 0.5f);

        public bool BrowserShowValueChangeButtons = true;
        public float BrowserValueButtonChangeStep = 0.01f;

        public bool ConsoleVisible = false;

        public bool BrowserInspectorDebug = false;

        public void Save()
        {
            try
            {
                using (var stream = File.Open(ConfigFilePath, FileMode.Create))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(Config));
                    xSer.Serialize(stream, this);
                }
            }
            catch (System.Exception _e)
            {
                Util.LogE($"Failed to save debug panel config: {_e.Message}");
            }
        }

        public static Config Load()
        {
            if (!File.Exists(ConfigFilePath))
            {
                Util.LogW("Config file does not exist. Creating new config.");
                return new Config();
            }

            try
            {
                using (var stream = File.Open(ConfigFilePath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(Config));
                    return xSer.Deserialize(stream) as Config ?? throw new System.Exception("Failed to deserialize config data.");
                }
            }
            catch (System.Exception _e)
            {
                Util.LogE($"Failed to load debug panel config: {_e.Message}");
                return new Config();
            }
        }
    }

    [System.Serializable]
    public class SerializableVector
    {
        public float X;
        public float Y;

        public SerializableVector() { }
        public SerializableVector(float _X, float _Y)
        {
            X = _X;
            Y = _Y;
        }

        public static implicit operator Vector2(SerializableVector _vector)
        {
            return new Vector2(_vector.X, _vector.Y);
        }

        public static implicit operator SerializableVector(Vector2 _vector)
        {
            return new SerializableVector(_vector.x, _vector.y);
        }
    }
    #endregion
}