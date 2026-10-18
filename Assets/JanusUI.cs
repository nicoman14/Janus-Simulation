// JanusUI.cs
// Interface de l'application, construite entièrement par code (aucun prefab, aucun réglage de scène).
//
// Les paramètres sont lus par réflexion sur JanusPMSimulation et JanusVideoRecorder :
// ajouter un champ public (avec [UILabel] et [Tooltip]) suffit pour qu'il apparaisse dans l'interface.
// Voir JanusUIAttributes.cs pour les attributs disponibles.
//
// Installation : un GameObject vide avec ce composant ; renseigner Sim (sinon il est cherché dans la scène).
// Le composant JanusVideoRecorder est ajouté automatiquement s'il manque.
//
// Organisation de l'écran (plusieurs Canvas) :
//   - barre du haut : Lecture / Pause / Stop, Référence, réglages enregistrés, vidéo, affichage ;
//   - panneau de gauche : un onglet par groupe de paramètres, aide du paramètre survolé en bas ;
//   - panneau de droite : mesures de la simulation (repliable) ;
//   - fond : rotation (clic gauche), déplacement (clic droit ou molette enfoncée) et zoom (molette) de la caméra.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JanusUI : MonoBehaviour
{
    public JanusPMSimulation sim;
    public JanusVideoRecorder recorder;
    [Tooltip("Caméra pilotée à la souris (vide = caméra principale)")]
    public Camera cam;
    [Tooltip("Taille globale de l'interface (1 = normale)")]
    [UIHide] [Range(0.6f, 2f)] public float uiScale = 1f;

    [UIPanel("Mesures et export", En = "Measurements & export")]
    [UILabel("Dossier des réglages", En = "Settings folder")] [Live] [FolderPath]
    [Tooltip("Dossier où « Sauver » et « Charger » rangent les réglages nommés. Vide = dossier JanusPresets à côté de l'application.")]
    [TooltipEn("Folder where 'Save' and 'Load' keep named settings. Empty = JanusPresets folder next to the application.")]
    public string presetFolder = "";

    // ------------------------------------------------------------ style

    static readonly Color CBar    = new Color(0.06f, 0.07f, 0.10f, 0.95f);
    static readonly Color CPanel  = new Color(0.08f, 0.09f, 0.13f, 0.92f);
    static readonly Color CTab    = new Color(0.13f, 0.15f, 0.20f, 1f);
    static readonly Color CTabOn  = new Color(0.22f, 0.36f, 0.62f, 1f);
    static readonly Color CField  = new Color(0.17f, 0.19f, 0.25f, 1f);
    static readonly Color CButton = new Color(0.20f, 0.23f, 0.30f, 1f);
    static readonly Color CAccent = new Color(0.36f, 0.62f, 1.00f, 1f);
    static readonly Color CPlay   = new Color(0.18f, 0.50f, 0.30f, 1f);
    static readonly Color CStop   = new Color(0.55f, 0.20f, 0.20f, 1f);
    static readonly Color CRec    = new Color(0.75f, 0.12f, 0.15f, 1f);
    static readonly Color CText   = new Color(0.90f, 0.92f, 0.96f, 1f);
    static readonly Color CDim    = new Color(0.60f, 0.64f, 0.72f, 1f);
    static readonly Color CWarn   = new Color(1.00f, 0.75f, 0.35f, 1f);
    static readonly Color CError  = new Color(1.00f, 0.45f, 0.45f, 1f);

    const float BarH = 52f, PanelW = 580f, TabW = 150f, HelpH = 170f, RowH = 32f, DiagW = 640f;

    Font font, mono;

    // ------------------------------------------------------------ modèle des paramètres

    class Entry
    {
        public string key, label, help, showIf;
        public bool live;
        public Type type;
        public object target;                 // objet évalué pour les conditions (simulation, enregistreur…)
        public Func<object> get;
        public Action<object> set;
        public bool hasRange; public float min, max;
        public GameObject row;
        public Action refresh;                // relit la valeur affichée
        public List<Selectable> selectables = new List<Selectable>();
        public List<InputField> inputs = new List<InputField>();
        public Image liveBar;
        public bool folder;                   // champ de dossier : bouton « Parcourir… »
        public Text labelText;
    }

    class Panel
    {
        public string key, title, showIf;      // key = titre français (identifiant), title = titre affiché
        public object target;
        public List<Entry> entries = new List<Entry>();
        public GameObject page;
        public Button tab;
    }

    readonly List<Panel> panels = new List<Panel>();
    Panel currentPanel;

    // ------------------------------------------------------------ éléments d'interface

    GameObject barCanvas, panelCanvas, diagCanvas, dragCanvas, showUiCanvas;
    Text statusText, helpTitle, helpBody, diagText, lockBanner;
    Button bPlay, bPause, bStop, bReference, bSave, bLoad, bRec, bDiag;
    Text tPlay, tPause, tRec;
    InputField presetName;
    RectTransform pagesRoot;
    ScrollRect scroll;
    float refreshTimer, fpsSmooth = 60f;
    bool uiVisible = true, diagVisible = true;

    // caméra
    bool orbitActive;
    float yaw, pitch, dist;
    Vector3 pan;
    Vector3 camStartPos; Quaternion camStartRot; Vector3 simStartOffset;

    // ------------------------------------------------------------ démarrage

    // Démarrage automatique : s'exécute à chaque lancement (éditeur ou application), même si aucun objet
    // ne porte le composant JanusUI. Si la scène contient une simulation sans interface, l'interface est créée.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        var s = FindObjectOfType<JanusPMSimulation>();
        if (s == null)
        {
            Debug.LogWarning("JanusUI : aucune JanusPMSimulation active dans la scène ouverte : pas d'interface.");
            return;
        }
        if (FindObjectOfType<JanusUI>() != null) return;
        Debug.Log("JanusUI : aucun objet ne porte JanusUI, création automatique de l'interface.");
        new GameObject("Janus UI").AddComponent<JanusUI>().sim = s;
    }

    void Awake()
    {
        JanusLang.English = PlayerPrefs.GetInt("JanusLang", 0) == 1;   // avant le démarrage de la simulation
        if (sim == null) sim = FindObjectOfType<JanusPMSimulation>();
        if (sim == null)
        {
            fatal = "JanusUI : aucun composant JanusPMSimulation dans la scène.";
            Debug.LogError(fatal);
            return;
        }
        if (recorder == null) recorder = FindObjectOfType<JanusVideoRecorder>();
        if (recorder == null) recorder = gameObject.AddComponent<JanusVideoRecorder>();
        if (cam == null) cam = Camera.main;
        if (recorder.targetCamera == null) recorder.targetCamera = cam;

        sim.legacyOnGUI = false;
        // copie du matériau : les réglages de rendu ne modifient pas l'asset du projet
        if (sim.instanceMaterial != null) sim.instanceMaterial = new Material(sim.instanceMaterial);
        EnsureEventSystem();
    }

    void Start()
    {
        if (sim == null) return;
        try
        {
            Build();
            int n = 0; foreach (var p in panels) n += p.entries.Count;
            Debug.Log($"JanusUI : interface construite ({panels.Count} onglets, {n} paramètres).");
        }
        catch (Exception ex)
        {
            fatal = "JanusUI : erreur pendant la construction de l'interface :\n" + ex;
            Debug.LogException(ex);
        }
    }

    // Message de secours si l'interface n'a pas pu être construite (visible même sans Canvas)
    string fatal;
    void OnGUI()
    {
        if (string.IsNullOrEmpty(fatal)) return;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        st.normal.textColor = new Color(1f, 0.5f, 0.5f);
        GUI.Box(new Rect(10, 10, Screen.width - 20, 220), GUIContent.none);
        GUI.Label(new Rect(20, 20, Screen.width - 40, 200), fatal, st);
    }

    void Build()
    {
        JanusLang.English = PlayerPrefs.GetInt("JanusLang", 0) == 1;
        font = LoadFont();
        if (font == null) Debug.LogWarning("JanusUI : police intégrée introuvable, les textes risquent d'être invisibles.");
        try
        {
            mono = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Cascadia Mono", "Courier New", "DejaVu Sans Mono", "Menlo", "Liberation Mono" }, 14);
        }
        catch { mono = null; }
        if (mono == null) mono = font;

        if (cam != null) { camStartPos = cam.transform.position; camStartRot = cam.transform.rotation; }
        simStartOffset = sim.CameraOffset;
        BuildContent();
    }

    // Construit (ou reconstruit après un changement de langue) tous les panneaux
    void BuildContent()
    {
        CollectEntries(sim);
        CollectRenderEntries();
        CollectEntries(recorder);
        CollectEntries(this);

        BuildDragLayer();
        BuildPanel();
        BuildBar();
        BuildDiag();
        BuildShowUiButton();
        BuildManual();

        SelectPanel(FirstVisiblePanel());
        RefreshAll();
        built = true;
    }

    void ToggleLanguage()
    {
        JanusLang.English = !JanusLang.English;
        PlayerPrefs.SetInt("JanusLang", JanusLang.English ? 1 : 0);
        PlayerPrefs.Save();
        string keep = currentPanel?.key;
        string keepPreset = presetName != null ? presetName.text : "";
        bool keepManual = manualOpen;
        foreach (var c in new[] { barCanvas, panelCanvas, diagCanvas, dragCanvas, showUiCanvas, browserCanvas, manualCanvas })
            if (c != null) Destroy(c);
        browserCanvas = null;
        panels.Clear();
        currentPanel = null; focusTrigger = null;
        uiVisible = true;
        BuildContent();
        presetName.SetTextWithoutNotify(keepPreset);
        foreach (var p in panels) if (p.key == keep && PanelVisible(p)) SelectPanel(p);
        ShowManual(keepManual);
    }

    bool built;

    static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return f;
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<StandaloneInputModule>();
#else
        // projet configuré avec le nouveau Input System uniquement
        var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null)
        {
            var m = go.AddComponent(t);
            t.GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.Instance)?.Invoke(m, null);
        }
        else go.AddComponent<StandaloneInputModule>();
#endif
    }

    // ------------------------------------------------------------ lecture des paramètres (réflexion)

    static bool Supported(Type t) =>
        t == typeof(bool) || t == typeof(int) || t == typeof(float) || t == typeof(double) ||
        t == typeof(string) || t.IsEnum || t == typeof(int[]) || t == typeof(float[]);

    static string L(string fr, string en) => JanusLang.L(fr, en);

    // Un onglet par titre (il peut regrouper des paramètres de plusieurs composants)
    Panel GetPanel(string key, string en, object target)
    {
        foreach (var p in panels) if (p.key == key) return p;
        var np = new Panel { key = key, title = string.IsNullOrEmpty(en) ? key : L(key, en), target = target };
        panels.Add(np);
        return np;
    }

    void CollectEntries(object target)
    {
        if (target == null) return;
        var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));   // ordre de déclaration
        Panel current = null;
        foreach (var f in fields)
        {
            if (f.IsNotSerialized || f.IsDefined(typeof(UIHideAttribute), true) || !Supported(f.FieldType)) continue;
            Panel panel = current;
            var pa = f.GetCustomAttribute<UIPanelAttribute>();
            if (pa != null)
            {
                var p = GetPanel(pa.title, pa.En, target);
                if (!string.IsNullOrEmpty(pa.ShowIf) && string.IsNullOrEmpty(p.showIf)) p.showIf = pa.ShowIf;
                panel = p;
                if (!pa.Single) current = p;
            }
            if (panel == null) panel = current = GetPanel("Général", "General", target);

            var field = f;
            var e = new Entry
            {
                key = target.GetType().Name + "." + f.Name,
                label = LabelOf(f),
                help = HelpOf(f),
                showIf = f.GetCustomAttribute<ShowIfAttribute>()?.condition,
                live = f.IsDefined(typeof(LiveAttribute), true),
                type = f.FieldType,
                target = target,
                get = () => field.GetValue(target),
                set = v => field.SetValue(target, v)
            };
            var ra = f.GetCustomAttribute<RangeAttribute>();
            if (ra != null) { e.hasRange = true; e.min = ra.min; e.max = ra.max; }
            e.help = (e.help.Length > 0 ? e.help + "\n\n" : "") + L("Champ du script : ", "Script field: ") + f.Name;
            e.folder = f.IsDefined(typeof(FolderPathAttribute), true);
            panel.entries.Add(e);
        }
    }

    static string LabelOf(FieldInfo f)
    {
        var la = f.GetCustomAttribute<UILabelAttribute>();
        if (la == null) return Nicify(f.Name);
        return JanusLang.English && !string.IsNullOrEmpty(la.En) ? la.En : la.label;
    }

    static string HelpOf(FieldInfo f)
    {
        if (JanusLang.English)
        {
            var en = f.GetCustomAttribute<TooltipEnAttribute>();
            if (en != null) return en.text;
        }
        return f.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
    }

    // Réglages du shader de rendu (modifiables en direct)
    void CollectRenderEntries()
    {
        var mat = sim.instanceMaterial;
        if (mat == null) return;
        var p = GetPanel("Rendu", "Rendering", sim);
        void Add(string prop, string label, float min, float max, string help)
        {
            label = label.Split('|')[JanusLang.English ? 1 : 0];
            help = help.Split('|')[JanusLang.English ? 1 : 0];
            if (!mat.HasProperty(prop)) return;
            p.entries.Add(new Entry
            {
                key = "Material." + prop, label = label, type = typeof(float), target = sim, live = true,
                hasRange = true, min = min, max = max,
                help = help + "\n\n" + L("Propriété du shader : ", "Shader property: ") + prop,
                get = () => mat.GetFloat(prop), set = v => mat.SetFloat(prop, Convert.ToSingle(v))
            });
        }
        Add("_PointSize", "Taille des points (pixels)|Point size (pixels)", 1f, 8f,
            "Taille à l'écran de chaque particule.|On-screen size of each particle.");
        Add("_Intensity", "Luminosité générale|Overall brightness", 0.01f, 1f,
            "Luminosité de toutes les particules (mélange additif : plus de particules = plus clair).|Brightness of all particles (additive blending: more particles = brighter).");
        Add("_NegIntensity", "Luminosité du milieu|Medium brightness", 0f, 1f,
            "Luminosité relative des masses négatives et de la matière noire.|Relative brightness of negative masses and dark matter.");
        Add("_GasIntensity", "Luminosité du gaz|Gas brightness", 0f, 2f,
            "Luminosité relative du gaz (rose).|Relative brightness of the gas (pink).");
        Add("_YoungBoost", "Éclat des étoiles jeunes|Young-star glow", 0f, 10f,
            "Surbrillance des étoiles nouvellement formées (bleues).|Extra brightness of newly formed stars (blue).");
    }

    static string Nicify(string n)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            char c = n[i];
            if (i == 0) sb.Append(char.ToUpper(c));
            else if (char.IsUpper(c) && !char.IsUpper(n[i - 1])) sb.Append(' ').Append(char.ToLower(c));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ conditions d'affichage

    readonly Dictionary<string, MemberInfo> memberCache = new Dictionary<string, MemberInfo>();
    readonly HashSet<string> warned = new HashSet<string>();

    bool Eval(object target, string cond)
    {
        if (string.IsNullOrEmpty(cond)) return true;
        foreach (var orPart in cond.Split('|'))
        {
            bool all = true;
            foreach (var raw in orPart.Split('&'))
            {
                string t = raw.Trim();
                bool neg = t.StartsWith("!");
                if (neg) t = t.Substring(1).Trim();
                bool v = GetBool(target, t);
                if (neg) v = !v;
                if (!v) { all = false; break; }
            }
            if (all) return true;
        }
        return false;
    }

    bool GetBool(object target, string name)
    {
        string k = target.GetType().Name + "." + name;
        if (!memberCache.TryGetValue(k, out var m))
        {
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            m = (MemberInfo)target.GetType().GetField(name, bf) ?? target.GetType().GetProperty(name, bf);
            memberCache[k] = m;
        }
        object v = m is FieldInfo fi ? fi.GetValue(target) : m is PropertyInfo pi ? pi.GetValue(target) : null;
        if (v is bool b) return b;
        if (warned.Add(k)) Debug.LogWarning("JanusUI : condition inconnue ou non booléenne : " + k);
        return true;
    }

    bool CanEdit(Entry e)
    {
        if (e.live) return true;
        if (e.target is JanusVideoRecorder r) return !r.IsRecording;
        return !sim.IsRunning;
    }

    // ------------------------------------------------------------ fabrique d'éléments uGUI

    static GameObject Make(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform RT(GameObject g) => (RectTransform)g.transform;

    static void Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
    }

    static void Stretch(RectTransform r) => Place(r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    static Image Bg(GameObject g, Color c, bool raycast = true)
    {
        var i = g.AddComponent<Image>();
        i.color = c; i.raycastTarget = raycast;
        return i;
    }

    static LayoutElement LE(GameObject g, float prefW = -1, float prefH = -1, float flexW = -1, float minH = -1)
    {
        var le = g.GetComponent<LayoutElement>();      // pas de « ?? » : les objets Unity détruits/absents ne sont pas null en C#
        if (le == null) le = g.AddComponent<LayoutElement>();
        le.preferredWidth = prefW; le.preferredHeight = prefH; le.flexibleWidth = flexW; le.minHeight = minH;
        return le;
    }

    GameObject MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080) / Mathf.Max(0.3f, uiScale);
        sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    Text MakeText(Transform parent, string s, int size, Color c, TextAnchor a, Font f = null)
    {
        var go = Make("Texte", parent);
        var t = go.AddComponent<Text>();
        t.font = f ?? font; t.fontSize = size; t.color = c; t.alignment = a; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false; t.supportRichText = true;
        return t;
    }

    Button MakeButton(Transform parent, string label, Color c, Action onClick, float width, out Text text, int size = 16)
    {
        var go = Make("Bouton " + label, parent);
        var img = Bg(go, c);
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        b.colors = cb;
        b.onClick.AddListener(() =>
        {
            onClick();
            // désélectionne le bouton : sinon Espace ou Entrée le « recliquent » (ex. Pause qui relance la simulation)
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        });
        text = MakeText(go.transform, label, size, CText, TextAnchor.MiddleCenter);
        Stretch(RT(text.gameObject));
        if (width > 0) LE(go, width, BarH - 14);
        return b;
    }

    Button MakeButton(Transform parent, string label, Color c, Action onClick, float width) =>
        MakeButton(parent, label, c, onClick, width, out _);

    InputField MakeInput(Transform parent, string placeholder, Action<string> onEnd, Font f = null)
    {
        var go = Make("Saisie", parent);
        var img = Bg(go, CField);
        var inp = go.AddComponent<InputField>();
        inp.targetGraphic = img;
        var txt = MakeText(go.transform, "", 15, CText, TextAnchor.MiddleLeft, f);
        txt.supportRichText = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        Place(RT(txt.gameObject), Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
        var ph = MakeText(go.transform, placeholder, 15, CDim, TextAnchor.MiddleLeft, f);
        ph.fontStyle = FontStyle.Italic;
        Place(RT(ph.gameObject), Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
        inp.textComponent = txt;
        inp.placeholder = ph;
        inp.lineType = InputField.LineType.SingleLine;
        inp.caretColor = CText;
        inp.selectionColor = new Color(CAccent.r, CAccent.g, CAccent.b, 0.4f);
        inp.onEndEdit.AddListener(s => onEnd(s));
        return inp;
    }

    Toggle MakeToggle(Transform parent, Action<bool> onChange)
    {
        var go = Make("Case", parent);
        var box = Make("Boîte", go.transform);
        var r = RT(box);
        r.anchorMin = r.anchorMax = new Vector2(0, 0.5f); r.pivot = new Vector2(0, 0.5f);
        r.sizeDelta = new Vector2(22, 22); r.anchoredPosition = Vector2.zero;
        var bg = Bg(box, CField);
        var check = Make("Coche", box.transform);
        Place(RT(check), Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
        var ci = Bg(check, CAccent, false);
        var t = go.AddComponent<Toggle>();
        t.targetGraphic = bg; t.graphic = ci;
        t.onValueChanged.AddListener(v => onChange(v));
        return t;
    }

    Slider MakeSlider(Transform parent, float min, float max, bool whole, Action<float> onChange)
    {
        var go = Make("Curseur", parent);
        var s = go.AddComponent<Slider>();
        var back = Make("Fond", go.transform);
        Place(RT(back), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -3), new Vector2(0, 3));
        Bg(back, CField);
        var fillArea = Make("Zone", go.transform);
        Place(RT(fillArea), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -3), new Vector2(0, 3));
        var fill = Make("Rempli", fillArea.transform);
        Stretch(RT(fill));
        Bg(fill, new Color(CAccent.r, CAccent.g, CAccent.b, 0.7f), false);
        var handleArea = Make("Glissière", go.transform);
        Place(RT(handleArea), Vector2.zero, Vector2.one, new Vector2(7, 0), new Vector2(-7, 0));
        var handle = Make("Poignée", handleArea.transform);
        RT(handle).sizeDelta = new Vector2(14, 0);
        var hi = Bg(handle, CText);
        s.fillRect = RT(fill); s.handleRect = RT(handle); s.targetGraphic = hi;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = whole;
        s.onValueChanged.AddListener(v => onChange(v));
        return s;
    }

    // ------------------------------------------------------------ fond : caméra à la souris

    class CameraDrag : MonoBehaviour, IDragHandler, IScrollHandler, IBeginDragHandler
    {
        public JanusUI ui;
        public void OnBeginDrag(PointerEventData e) { ui.BeginOrbit(); }
        public void OnDrag(PointerEventData e) { ui.OnCameraDrag(e.button, e.delta); }
        public void OnScroll(PointerEventData e) { ui.OnCameraScroll(e.scrollDelta.y); }
    }

    void BuildDragLayer()
    {
        dragCanvas = MakeCanvas("Janus UI - caméra", -50);
        var catcher = Make("Zone souris", dragCanvas.transform);
        Stretch(RT(catcher));
        Bg(catcher, new Color(0, 0, 0, 0));                  // invisible, mais reçoit la souris
        catcher.AddComponent<CameraDrag>().ui = this;
    }

    void BeginOrbit()
    {
        if (orbitActive || cam == null) return;
        orbitActive = true;
        Vector3 target = sim.CameraTarget;
        Vector3 off = cam.transform.position - target;
        dist = Mathf.Max(1f, off.magnitude);
        Vector3 e = cam.transform.rotation.eulerAngles;
        pitch = e.x > 180f ? e.x - 360f : e.x; yaw = e.y;
        // point visé : devant la caméra, à la distance actuelle de la cible
        pan = cam.transform.position + cam.transform.forward * dist - target;
    }

    void OnCameraDrag(PointerEventData.InputButton button, Vector2 d)
    {
        BeginOrbit();
        sim.NotifyViewInteraction();
        if (button == PointerEventData.InputButton.Left)
        {
            yaw += d.x * 0.25f;
            pitch = Mathf.Clamp(pitch - d.y * 0.25f, -89f, 89f);
        }
        else if (cam != null)
        {
            float k = dist * 0.0016f;
            pan += (-cam.transform.right * d.x - cam.transform.up * d.y) * k;
        }
    }

    void OnCameraScroll(float s)
    {
        BeginOrbit();
        sim.NotifyViewInteraction();
        dist = Mathf.Clamp(dist * Mathf.Exp(-s * 0.12f), 1f, 5000f);
    }

    void ResetCamera()
    {
        orbitActive = false;
        if (cam == null) return;
        cam.transform.SetPositionAndRotation(camStartPos, camStartRot);
        sim.CameraOffset = simStartOffset;
    }

    void LateUpdate()
    {
        if (!orbitActive || cam == null || sim == null) return;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = pan + rot * new Vector3(0, 0, -dist);
        cam.transform.rotation = rot;
        if (sim.followCamera == cam.transform) sim.CameraOffset = offset;   // la simulation suit la galaxie
        cam.transform.position = sim.CameraTarget + offset;
    }

    // ------------------------------------------------------------ barre du haut

    void BuildBar()
    {
        barCanvas = MakeCanvas("Janus UI - barre", 20);
        var bar = Make("Barre", barCanvas.transform);
        Place(RT(bar), new Vector2(0, 1), Vector2.one, new Vector2(0, -BarH), Vector2.zero);
        Bg(bar, CBar);
        var h = bar.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(10, 10, 7, 7); h.spacing = 6;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        bPlay  = MakeButton(bar.transform, L("Lecture", "Play"), CPlay, OnPlay, 110, out tPlay);
        bPause = MakeButton(bar.transform, "Pause", CButton, OnPause, 100, out tPause);
        bStop  = MakeButton(bar.transform, "Stop", CStop, OnStop, 80);
        Spacer(bar.transform, 10);
        bReference = MakeButton(bar.transform, L("Référence", "Reference"), CButton, OnReference, 110);
        Spacer(bar.transform, 10);
        presetName = MakeInput(bar.transform, L("nom du réglage", "settings name"), s => { });
        LE(presetName.gameObject, 160, BarH - 14);
        bSave = MakeButton(bar.transform, L("Sauver", "Save"), CButton, OnSavePreset, 80);
        bLoad = MakeButton(bar.transform, L("Charger", "Load"), CButton, OnLoadPreset, 85);
        Spacer(bar.transform, 10);
        bRec = MakeButton(bar.transform, L("Enregistrer la vidéo", "Record video"), CRec, OnRecord, 190, out tRec);
        Spacer(bar.transform, 10);
        bDiag = MakeButton(bar.transform, L("Mesures", "Measurements"), CButton, () => { diagVisible = !diagVisible; diagCanvas.SetActive(diagVisible && uiVisible); }, 115);
        var bView = MakeButton(bar.transform, L("Vue initiale", "Reset view"), CButton, ResetCamera, 110);
        var bManual = MakeButton(bar.transform, L("Manuel", "Manual"), CButton, () => ShowManual(!manualOpen), 90);
        var bLang = MakeButton(bar.transform, L("English", "Français"), CButton, ToggleLanguage, 90);
        var bHide = MakeButton(bar.transform, L("Masquer", "Hide"), CButton, () => SetUiVisible(false), 85);
        var bQuit = MakeButton(bar.transform, L("Quitter", "Quit"), CButton, OnQuit, 80);
        Button bDownload = null;
        if (JanusWeb.IsWeb)
        {
            // version web : pas de vidéo ni de bouton Quitter ; les CSV sont proposés en téléchargement
            bRec.gameObject.SetActive(false);
            bQuit.gameObject.SetActive(false);
            bDownload = MakeButton(bar.transform, L("Télécharger les CSV", "Download CSV"), CButton, () =>
            {
                int n = JanusWeb.DownloadAll();
                ShowMessage(n > 0 ? L($"{n} fichier(s) proposé(s) au téléchargement.", $"{n} file(s) offered for download.")
                                  : L("Aucun fichier CSV pour l'instant : lancez une simulation avec l'export CSV activé.",
                                      "No CSV file yet: run a simulation with CSV export enabled."));
            }, 170);
            bDownload.transform.SetSiblingIndex(bQuit.transform.GetSiblingIndex());
        }

        statusText = MakeText(bar.transform, "", 15, CText, TextAnchor.MiddleRight);
        LE(statusText.gameObject, -1, BarH - 14, 1);

        AddHelp(bPlay.gameObject, L("Lecture", "Play"), L(
            "Lance la simulation avec les réglages affichés (ou la reprend après une pause). En mode campagne, lance toute la série de simulations.",
            "Starts the simulation with the displayed settings (or resumes it after a pause). In campaign mode, runs the whole series of simulations."));
        AddHelp(bPause.gameObject, "Pause", L(
            "Suspend le calcul ; l'affichage et la caméra restent actifs. Cliquer à nouveau pour reprendre.",
            "Suspends the computation; display and camera stay active. Click again to resume."));
        AddHelp(bStop.gameObject, "Stop", L(
            "Arrête et libère la simulation. Tous les paramètres redeviennent modifiables ; Lecture relance depuis l'état initial. Une campagne en cours est interrompue (les runs terminés restent écrits).",
            "Stops and releases the simulation. All settings become editable again; Play restarts from the initial state. A running campaign is interrupted (finished runs remain written)."));
        AddHelp(bReference.gameObject, L("Référence", "Reference"), L(
            "Applique la configuration de référence validée, pour le mode courant. Galaxie : tests de convergence " +
            "(300 000 étoiles, c−/c+ = 1). Grande échelle (validée le 15/10/2026) : modèle complet de Petit (c−/c+ = 0,1, " +
            "|M−| = 16 000 000, facteur d'échelle propre, vitesse initiale 1, q₀ = −0,09), boîte de 200 Mpc, 96³ + 112³ particules, " +
            "spectre réaliste 2LPT, σ₈ = 0,3 pour Janus et 0,81 pour ΛCDM (même contraste final), modèles Janus + ΛCDM, " +
            "listes de campagne vidées. Ne touche pas aux seeds ni au mode campagne.",
            "Applies the validated reference configuration for the current mode. Galaxy: convergence tests (300,000 stars, " +
            "c−/c+ = 1). Large scale (validated 15/10/2026): Petit's full model (c−/c+ = 0.1, |M−| = 16,000,000, own scale " +
            "factor, initial rate 1, q₀ = −0.09), 200 Mpc box, 96³ + 112³ particles, realistic 2LPT spectrum, σ₈ = 0.3 for Janus " +
            "and 0.81 for ΛCDM (same final contrast), Janus + ΛCDM models, campaign lists cleared. Seeds and campaign mode are left unchanged."));
        AddHelp(presetName.gameObject, L("Nom du réglage", "Settings name"), L(
            "Nom sous lequel sauver ou charger l'ensemble des paramètres (fichier texte dans le dossier des réglages, onglet Mesures et export).",
            "Name under which all settings are saved or loaded (text file in the settings folder, Measurements & export tab)."));
        AddHelp(bSave.gameObject, L("Sauver", "Save"), L("Enregistre tous les paramètres sous le nom indiqué.", "Saves all settings under the given name."));
        AddHelp(bLoad.gameObject, L("Charger", "Load"), L(
            "Recharge les paramètres enregistrés sous le nom indiqué (simulation arrêtée).",
            "Reloads the settings saved under the given name (simulation stopped)."));
        AddHelp(bRec.gameObject, L("Vidéo", "Video"), L(
            "Démarre ou arrête l'enregistrement vidéo de la vue 3D (sans l'interface). MP4 si ffmpeg est disponible, sinon images PNG numérotées. Réglages dans l'onglet Vidéo.",
            "Starts or stops video recording of the 3D view (without the interface). MP4 if ffmpeg is available, otherwise numbered PNG images. Settings in the Video tab."));
        AddHelp(bDiag.gameObject, L("Mesures", "Measurements"), L(
            "Affiche ou masque le panneau des mesures de la simulation.", "Shows or hides the simulation measurements panel."));
        AddHelp(bView.gameObject, L("Vue initiale", "Reset view"), L("Replace la caméra dans sa position de départ.", "Puts the camera back in its starting position."));
        AddHelp(bManual.gameObject, L("Manuel", "Manual"), L(
            "Ouvre le manuel : présentation du logiciel, théorie de Janus et de ΛCDM, méthode numérique, modes, mesures et fichiers, résultats et limites.",
            "Opens the manual: software overview, Janus and ΛCDM theory, numerical method, modes, measurements and files, results and limits."));
        AddHelp(bLang.gameObject, L("Langue", "Language"), L("Passe l'interface en anglais.", "Switches the interface to French."));
        AddHelp(bHide.gameObject, L("Masquer", "Hide"), L("Masque l'interface (un bouton permet de la réafficher).", "Hides the interface (a button brings it back)."));
        if (bDownload != null)
            AddHelp(bDownload.gameObject, L("Télécharger les CSV", "Download CSV"), L(
                "Version web : propose en téléchargement tous les fichiers CSV écrits pendant la session (un par run, plus les fichiers de campagne). Le navigateur peut demander d'autoriser plusieurs téléchargements.",
                "Web version: offers for download all CSV files written during the session (one per run, plus campaign files). The browser may ask to allow several downloads."));
        AddHelp(bQuit.gameObject, L("Quitter", "Quit"), L("Arrête la simulation et ferme l'application.", "Stops the simulation and closes the application."));
    }

    void Spacer(Transform parent, float w) => LE(Make("Espace", parent), w, 1);

    // ------------------------------------------------------------ panneau des paramètres

    void BuildPanel()
    {
        panelCanvas = MakeCanvas("Janus UI - paramètres", 10);
        var root = Make("Panneau", panelCanvas.transform);
        Place(RT(root), Vector2.zero, new Vector2(0, 1), new Vector2(0, 0), new Vector2(PanelW, -BarH));
        Bg(root, CPanel);

        // onglets
        var tabs = Make("Onglets", root.transform);
        Place(RT(tabs), Vector2.zero, new Vector2(0, 1), new Vector2(0, HelpH), new Vector2(TabW, 0));
        Bg(tabs, new Color(0.05f, 0.06f, 0.09f, 0.9f));
        var v = tabs.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(6, 6, 8, 8); v.spacing = 4;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false;

        // zone des pages, avec défilement
        var area = Make("Pages", root.transform);
        Place(RT(area), Vector2.zero, Vector2.one, new Vector2(TabW, HelpH), new Vector2(-14, 0));
        scroll = area.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        var viewport = Make("Vue", area.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        Bg(viewport, new Color(0, 0, 0, 0.01f));
        scroll.viewport = RT(viewport);
        pagesRoot = RT(Make("Contenu", viewport.transform));
        pagesRoot.anchorMin = new Vector2(0, 1); pagesRoot.anchorMax = Vector2.one; pagesRoot.pivot = new Vector2(0.5f, 1);
        pagesRoot.offsetMin = pagesRoot.offsetMax = Vector2.zero;
        var pv = pagesRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        pv.padding = new RectOffset(10, 8, 10, 10); pv.spacing = 4;
        pv.childControlWidth = pv.childControlHeight = true; pv.childForceExpandHeight = false;
        pagesRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = pagesRoot;

        var sbar = BuildScrollbar(root.transform);
        scroll.verticalScrollbar = sbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // bandeau d'avertissement (simulation en cours)
        lockBanner = MakeText(pagesRoot, "", 14, CWarn, TextAnchor.MiddleLeft);
        LE(lockBanner.gameObject, -1, 40);

        foreach (var p in panels)
        {
            var panel = p;
            p.tab = MakeButton(tabs.transform, p.title, CTab, () => SelectPanel(panel), -1);
            LE(p.tab.gameObject, -1, 34);
            var tabText = p.tab.GetComponentInChildren<Text>();
            tabText.fontSize = 15;
            tabText.resizeTextForBestFit = true; tabText.resizeTextMinSize = 10; tabText.resizeTextMaxSize = 15;

            p.page = Make("Page " + p.title, pagesRoot);
            var pl = p.page.AddComponent<VerticalLayoutGroup>();
            pl.spacing = 3; pl.childControlWidth = pl.childControlHeight = true; pl.childForceExpandHeight = false;
            var title = MakeText(p.page.transform, p.title.ToUpper(), 17, CAccent, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            LE(title.gameObject, -1, 30);
            foreach (var e in p.entries) BuildRow(p.page.transform, e);
        }

        // aide
        var help = Make("Aide", root.transform);
        Place(RT(help), Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, HelpH));
        Bg(help, new Color(0.04f, 0.05f, 0.08f, 0.97f));
        helpTitle = MakeText(help.transform, "", 16, CAccent, TextAnchor.UpperLeft);
        helpTitle.fontStyle = FontStyle.Bold;
        Place(RT(helpTitle.gameObject), new Vector2(0, 1), Vector2.one, new Vector2(12, -30), new Vector2(-12, -8));
        helpBody = MakeText(help.transform, "", 14, CText, TextAnchor.UpperLeft);
        Place(RT(helpBody.gameObject), Vector2.zero, Vector2.one, new Vector2(12, 8), new Vector2(-12, -32));
        ShowHelp(null);
    }

    Scrollbar BuildScrollbar(Transform parent)
    {
        var go = Make("Défilement", parent);
        Place(RT(go), new Vector2(1, 0), Vector2.one, new Vector2(-12, HelpH + 2), new Vector2(-2, -2));
        Bg(go, new Color(0.1f, 0.11f, 0.15f, 1f));
        var area = Make("Zone", go.transform);
        Stretch(RT(area));
        var handle = Make("Poignée", area.transform);
        Stretch(RT(handle));
        var hi = Bg(handle, new Color(0.35f, 0.4f, 0.5f, 1f));
        var sb = go.AddComponent<Scrollbar>();
        sb.handleRect = RT(handle); sb.targetGraphic = hi;
        sb.direction = Scrollbar.Direction.BottomToTop;
        return sb;
    }

    void BuildRow(Transform parent, Entry e)
    {
        var row = Make("Ligne " + e.label, parent);
        e.row = row;
        Bg(row, new Color(1, 1, 1, 0.025f));                 // reçoit le survol (aide)
        LE(row, -1, RowH, -1, RowH);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(0, 4, 2, 2); h.spacing = 8;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = true;
        h.childAlignment = TextAnchor.MiddleLeft;

        // barre bleue = modifiable pendant la simulation
        var bar = Make("Direct", row.transform);
        e.liveBar = Bg(bar, e.live ? CAccent : new Color(0, 0, 0, 0), false);
        LE(bar, 3, -1);

        e.labelText = MakeText(row.transform, e.label, 15, CText, TextAnchor.MiddleLeft);
        e.labelText.resizeTextForBestFit = true; e.labelText.resizeTextMinSize = 11; e.labelText.resizeTextMaxSize = 15;
        LE(e.labelText.gameObject, 205, -1);

        var ed = Make("Éditeur", row.transform);
        LE(ed, -1, -1, 1);
        BuildEditor(ed.transform, e);

        AddHelp(row, e);
        foreach (var s in e.selectables) AddHelp(s.gameObject, e);
    }

    void BuildEditor(Transform parent, Entry e)
    {
        var t = e.type;
        if (t == typeof(bool))
        {
            // interrupteur Oui / Non : tout le bouton est cliquable (plus fiable et plus lisible qu'une petite case)
            Text label = null;
            var b = MakeButton(parent, "", CField, () => Commit(e, !(bool)e.get()), -1, out label, 15);
            Stretch(RT(b.gameObject));
            var img = b.GetComponent<Image>();
            e.selectables.Add(b);
            e.refresh = () =>
            {
                bool on = (bool)e.get();
                label.text = on ? L("Oui", "Yes") : L("Non", "No");
                img.color = on ? CTabOn : CField;
            };
        }
        else if (t.IsEnum)
        {
            var names = Enum.GetNames(t);
            string Display(object v)   // nom lisible : [InspectorName] de la valeur, sinon son nom
            {
                var mi = t.GetField(v.ToString());
                if (mi == null) return v.ToString();
                var en = mi.GetCustomAttribute<EnNameAttribute>();
                if (JanusLang.English && en != null) return en.name;
                var ia = mi.GetCustomAttribute<InspectorNameAttribute>();
                return ia != null ? ia.displayName : v.ToString();
            }
            var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 3; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            Text valueText = null;
            void Step(int d)
            {
                int i = Array.IndexOf(names, e.get().ToString());
                i = ((i + d) % names.Length + names.Length) % names.Length;
                Commit(e, Enum.Parse(t, names[i]));
            }
            var prev = MakeButton(parent, "<", CButton, () => Step(-1), -1); LE(prev.gameObject, 28, -1);
            var mid = MakeButton(parent, "", CField, () => Step(1), -1, out valueText, 15); LE(mid.gameObject, -1, -1, 1);
            valueText.resizeTextForBestFit = true; valueText.resizeTextMinSize = 10; valueText.resizeTextMaxSize = 15;
            var next = MakeButton(parent, ">", CButton, () => Step(1), -1); LE(next.gameObject, 28, -1);
            e.selectables.Add(prev); e.selectables.Add(mid); e.selectables.Add(next);
            e.refresh = () => valueText.text = Display(e.get());
        }
        else if (e.hasRange && (t == typeof(float) || t == typeof(int) || t == typeof(double)))
        {
            var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            InputField inp = null;
            var sl = MakeSlider(parent, e.min, e.max, t == typeof(int), v => Commit(e, v));
            LE(sl.gameObject, -1, -1, 1);
            inp = MakeInput(parent, "", s => { if (TryParse(t, s, out var v)) Commit(e, v); else e.refresh(); });
            LE(inp.gameObject, 72, -1);
            e.selectables.Add(sl); e.selectables.Add(inp); e.inputs.Add(inp);
            e.refresh = () =>
            {
                float v = Convert.ToSingle(e.get());
                sl.SetValueWithoutNotify(v);
                if (!inp.isFocused) inp.SetTextWithoutNotify(Format(e.get()));
            };
        }
        else if (e.folder)
        {
            var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = false;
            var inp = MakeInput(parent, L("(vide = automatique)", "(empty = automatic)"), s => Commit(e, (s ?? "").Trim()));
            LE(inp.gameObject, -1, -1, 1);
            var browse = MakeButton(parent, L("Parcourir…", "Browse…"), CButton,
                () => { if (CanEdit(e)) OpenFolderBrowser((string)e.get(), path => Commit(e, path)); }, -1);
            LE(browse.gameObject, 92, -1);
            e.selectables.Add(inp); e.selectables.Add(browse); e.inputs.Add(inp);
            e.refresh = () => { if (!inp.isFocused) inp.SetTextWithoutNotify((string)e.get() ?? ""); };
        }
        else
        {
            var inp = MakeInput(parent, t == typeof(string) ? L("(vide = automatique)", "(empty = automatic)") : "",
                s =>
                {
                    if (TryParse(t, s, out var v)) Commit(e, v);
                    else
                    {
                        ShowMessage(L("Valeur non comprise pour « ", "Could not read the value for “") + e.label +
                                    L(" » : ", "”: ") + s + (t == typeof(float[])
                                        ? L("\nExemple : 0,98 ; 1 ; 1,2", "\nExample: 0.98 ; 1 ; 1.2") : ""));
                        e.refresh();
                    }
                });
            Stretch(RT(inp.gameObject));
            if (t == typeof(int)) inp.contentType = InputField.ContentType.IntegerNumber;
            e.selectables.Add(inp); e.inputs.Add(inp);
            e.refresh = () => { if (!inp.isFocused) inp.SetTextWithoutNotify(Format(e.get())); };
        }
    }

    static string Format(object v)
    {
        var ci = CultureInfo.InvariantCulture;
        switch (v)
        {
            case float f:  return f.ToString("0.######", ci);
            case double d: return d.ToString("0.######", ci);
            case int i:    return i.ToString(ci);
            case int[] a:  return string.Join(", ", Array.ConvertAll(a, x => x.ToString(ci)));
            case float[] fa: return string.Join(" ; ", Array.ConvertAll(fa, x => x.ToString("0.####", ci)));
            case null:     return "";
            default:       return v.ToString();
        }
    }

    static bool TryParse(Type t, string s, out object v)
    {
        var ci = CultureInfo.InvariantCulture;
        s = (s ?? "").Trim();
        v = null;
        if (t == typeof(string)) { v = s; return true; }
        if (t == typeof(float[]))
        {
            // Liste de décimaux, saisie souple :
            //   « 0,98 ; 1 ; 1,2 »  « 0.98 1 1.2 »  « 0,98, 1, 1,2 »  « 0.98,1,1.2 »
            // ';' et les espaces séparent toujours ; la virgule est décimale, sauf si elle est suivie
            // d'un espace (« 0,98, 1 ») ou si la saisie contient déjà des points (« 0.98,1 »).
            string w = s.Replace(", ", " ").Replace(",\t", " ");
            if (w.IndexOf('.') >= 0) w = w.Replace(',', ' ');
            var parts = w.Split(new[] { ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<float>();
            foreach (var p in parts)
            {
                string q = p.Trim(',').Replace(',', '.');
                if (q.Length == 0) continue;
                if (!float.TryParse(q, NumberStyles.Float, ci, out float x)) return false;
                list.Add(x);
            }
            v = list.ToArray(); return true;
        }
        if (t == typeof(int[]))
        {
            var parts = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();
            foreach (var p in parts) { if (!int.TryParse(p, NumberStyles.Integer, ci, out int x)) return false; list.Add(x); }
            v = list.ToArray(); return true;
        }
        string n = s.Replace(',', '.').Replace(" ", "");
        if (t == typeof(int))
        {
            if (int.TryParse(n, NumberStyles.Integer, ci, out int i)) { v = i; return true; }
            if (double.TryParse(n, NumberStyles.Float, ci, out double di)) { v = (int)Math.Round(di); return true; }
            return false;
        }
        if (t == typeof(float)) { if (float.TryParse(n, NumberStyles.Float, ci, out float f)) { v = f; return true; } return false; }
        if (t == typeof(double)) { if (double.TryParse(n, NumberStyles.Float, ci, out double d)) { v = d; return true; } return false; }
        if (t == typeof(bool)) { if (bool.TryParse(n, out bool b)) { v = b; return true; } return false; }
        if (t.IsEnum) { try { v = Enum.Parse(t, s); return true; } catch { return false; } }
        return false;
    }

    void Commit(Entry e, object v)
    {
        if (!CanEdit(e))
        {
            ShowMessage(L("« ", "“") + e.label + L(" » ne se modifie qu'à l'arrêt : appuyez sur Stop, puis saisissez la valeur.",
                                                  "” can only be changed while stopped: press Stop, then enter the value."));
            e.refresh?.Invoke(); return;
        }
        try
        {
            if (e.type == typeof(int)) v = Convert.ToInt32(Math.Round(Convert.ToDouble(v)));
            else if (e.type == typeof(float)) v = Convert.ToSingle(v);
            else if (e.type == typeof(double)) v = Convert.ToDouble(v);
            if (e.hasRange && (e.type == typeof(float) || e.type == typeof(int) || e.type == typeof(double)))
            {
                double d = Math.Max(e.min, Math.Min(e.max, Convert.ToDouble(v)));
                v = Convert.ChangeType(e.type == typeof(int) ? Math.Round(d) : d, e.type, CultureInfo.InvariantCulture);
            }
            e.set(v);
        }
        catch (Exception ex) { Debug.LogWarning("JanusUI : valeur refusée pour " + e.key + " (" + ex.Message + ")"); }
        RefreshAll();
    }

    // ------------------------------------------------------------ aide contextuelle

    class HelpTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public JanusUI ui; public Entry entry; public string title, text;
        public void OnPointerEnter(PointerEventData e) { ui.ShowHelp(this); }
        public void OnPointerExit(PointerEventData e)
        {
            // en quittant un élément, on revient à l'aide de l'élément survolé dessous (ou de celui qui a le focus)
            var go = e.pointerCurrentRaycast.gameObject;
            var under = go != null ? go.GetComponentInParent<HelpTrigger>() : null;
            ui.ShowHelp(under != null ? under : ui.focusTrigger);
        }
        public void OnSelect(BaseEventData e) { ui.focusTrigger = this; ui.ShowHelp(this); }
        public void OnDeselect(BaseEventData e) { if (ui.focusTrigger == this) ui.focusTrigger = null; }
    }

    HelpTrigger focusTrigger;

    HelpTrigger GetTrigger(GameObject go)
    {
        var h = go.GetComponent<HelpTrigger>();
        if (h == null) h = go.AddComponent<HelpTrigger>();
        return h;
    }

    void AddHelp(GameObject go, Entry e)
    {
        var h = GetTrigger(go);
        h.ui = this; h.entry = e;
    }

    void AddHelp(GameObject go, string title, string text)
    {
        var h = GetTrigger(go);
        h.ui = this; h.title = title; h.text = text;
    }

    void ShowHelp(HelpTrigger h)
    {
        if (helpTitle == null) return;
        if (h == null)
        {
            helpTitle.text = L("Aide", "Help");
            helpBody.text = L("Survolez ou sélectionnez un paramètre pour afficher son explication.\n" +
                              "Barre bleue à gauche d'un paramètre : modifiable pendant la simulation. " +
                              "Les autres se règlent simulation arrêtée (Stop), puis Lecture.\n" +
                              "Vue 3D : clic gauche = rotation, clic droit = déplacement, molette = zoom.",
                              "Hover or select a setting to display its explanation.\n" +
                              "Blue bar on the left of a setting: editable while the simulation runs. " +
                              "The others are set with the simulation stopped (Stop), then Play.\n" +
                              "3D view: left click = rotate, right click = pan, wheel = zoom.");
            return;
        }
        if (h.entry != null)
        {
            var e = h.entry;
            helpTitle.text = e.label;
            string state = e.live ? "<color=#5C9EFF>" + L("Modifiable pendant la simulation.", "Editable while the simulation runs.") + "</color>"
                                  : "<color=#FFBF59>" + L("Appliqué au prochain lancement : modifiable simulation arrêtée.",
                                                          "Applied at the next start: editable with the simulation stopped.") + "</color>";
            helpBody.text = state + "\n" + e.help;
        }
        else
        {
            helpTitle.text = h.title;
            helpBody.text = h.text;
        }
    }

    // ------------------------------------------------------------ panneau des mesures

    void BuildDiag()
    {
        diagCanvas = MakeCanvas("Janus UI - mesures", 5);
        var root = Make("Mesures", diagCanvas.transform);
        Place(RT(root), new Vector2(1, 0), Vector2.one, new Vector2(-DiagW, 8), new Vector2(-8, -BarH - 8));
        Bg(root, new Color(0.03f, 0.04f, 0.06f, 0.72f), false);
        diagText = MakeText(root.transform, "", 14, CText, TextAnchor.UpperLeft, mono);
        diagText.supportRichText = false;
        Place(RT(diagText.gameObject), Vector2.zero, Vector2.one, new Vector2(12, 10), new Vector2(-12, -10));
    }

    // ------------------------------------------------------------ manuel (bouton « Manuel »)

    GameObject manualCanvas;
    Text manualBody, manualTitle;
    ScrollRect manualScroll;
    readonly List<Image> manualTabs = new List<Image>();
    bool manualOpen;
    int manualSection;

    void BuildManual()
    {
        manualTabs.Clear();
        manualCanvas = MakeCanvas("Janus UI - manuel", 40);
        var shade = Make("Fond", manualCanvas.transform);
        Stretch(RT(shade));
        Bg(shade, new Color(0, 0, 0, 0.55f));                                // bloque la souris vers la vue 3D
        var root = Make("Manuel", manualCanvas.transform);
        Place(RT(root), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);
        Bg(root, new Color(0.06f, 0.07f, 0.10f, 0.98f));

        // en-tête
        var head = Make("En-tête", root.transform);
        Place(RT(head), new Vector2(0, 1), Vector2.one, new Vector2(0, -48), Vector2.zero);
        Bg(head, CBar);
        var title = MakeText(head.transform, L("Manuel — Simulation Janus contre ΛCDM", "Manual — Janus versus ΛCDM simulation"), 20, CText, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        Place(RT(title.gameObject), Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-130, 0));
        var close = MakeButton(head.transform, L("Fermer", "Close"), CButton, () => ShowManual(false), -1);
        Place(RT(close.gameObject), new Vector2(1, 0), Vector2.one, new Vector2(-120, 8), new Vector2(-10, -8));

        // sommaire
        var toc = Make("Sommaire", root.transform);
        Place(RT(toc), Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(230, -48));
        Bg(toc, new Color(0.05f, 0.06f, 0.09f, 1f));
        var tv = toc.AddComponent<VerticalLayoutGroup>();
        tv.padding = new RectOffset(8, 8, 10, 10); tv.spacing = 5;
        tv.childControlWidth = tv.childControlHeight = true; tv.childForceExpandHeight = false;
        for (int i = 0; i < JanusManual.Sections.Length; i++)
        {
            int idx = i;
            var sec = JanusManual.Sections[i];
            var b = MakeButton(toc.transform, $"{i + 1}. " + L(sec.titleFr, sec.titleEn), CTab, () => SelectManualSection(idx), -1);
            LE(b.gameObject, -1, 36);
            var bt = b.GetComponentInChildren<Text>();
            bt.alignment = TextAnchor.MiddleLeft; bt.fontSize = 15;
            RT(bt.gameObject).offsetMin = new Vector2(10, 0);
            manualTabs.Add(b.GetComponent<Image>());
        }

        // texte avec défilement
        var area = Make("Zone", root.transform);
        Place(RT(area), Vector2.zero, Vector2.one, new Vector2(240, 10), new Vector2(-24, -58));
        manualScroll = area.AddComponent<ScrollRect>();
        manualScroll.horizontal = false; manualScroll.movementType = ScrollRect.MovementType.Clamped;
        manualScroll.scrollSensitivity = 40f;
        var viewport = Make("Vue", area.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        Bg(viewport, new Color(0, 0, 0, 0.01f));
        manualScroll.viewport = RT(viewport);
        var content = RT(Make("Contenu", viewport.transform));
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.padding = new RectOffset(16, 16, 12, 24); cv.spacing = 10;
        cv.childControlWidth = cv.childControlHeight = true; cv.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        manualScroll.content = content;
        manualTitle = MakeText(content, "", 22, CAccent, TextAnchor.UpperLeft);
        manualTitle.fontStyle = FontStyle.Bold;
        manualBody = MakeText(content, "", 16, CText, TextAnchor.UpperLeft);
        manualBody.verticalOverflow = VerticalWrapMode.Overflow;
        manualBody.lineSpacing = 1.1f;

        var sb = Make("Défilement", area.transform);
        Place(RT(sb), new Vector2(1, 0), Vector2.one, new Vector2(4, 0), new Vector2(14, 0));
        Bg(sb, new Color(0.1f, 0.11f, 0.15f, 1f));
        var sArea = Make("Zone", sb.transform); Stretch(RT(sArea));
        var handle = Make("Poignée", sArea.transform); Stretch(RT(handle));
        var hi = Bg(handle, new Color(0.35f, 0.4f, 0.5f, 1f));
        var bar = sb.AddComponent<Scrollbar>();
        bar.handleRect = RT(handle); bar.targetGraphic = hi; bar.direction = Scrollbar.Direction.BottomToTop;
        manualScroll.verticalScrollbar = bar;
        manualScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        SelectManualSection(Mathf.Clamp(manualSection, 0, JanusManual.Sections.Length - 1));
        manualCanvas.SetActive(false);
        manualOpen = false;
    }

    void SelectManualSection(int i)
    {
        if (manualBody == null || JanusManual.Sections.Length == 0) return;
        manualSection = i;
        var sec = JanusManual.Sections[i];
        manualTitle.text = $"{i + 1}. " + L(sec.titleFr, sec.titleEn);
        manualBody.text = L(sec.fr, sec.en);
        for (int k = 0; k < manualTabs.Count; k++) manualTabs[k].color = k == i ? CTabOn : CTab;
        if (manualScroll != null) manualScroll.verticalNormalizedPosition = 1f;
    }

    void ShowManual(bool on)
    {
        manualOpen = on;
        if (manualCanvas != null) manualCanvas.SetActive(on);
        if (on) SelectManualSection(manualSection);
    }

    void BuildShowUiButton()
    {
        showUiCanvas = MakeCanvas("Janus UI - afficher", 30);
        var b = MakeButton(showUiCanvas.transform, L("Afficher l'interface", "Show interface"), new Color(0.15f, 0.17f, 0.22f, 0.8f), () => SetUiVisible(true), -1);
        var r = RT(b.gameObject);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.sizeDelta = new Vector2(190, 34); r.anchoredPosition = new Vector2(8, -8);
        showUiCanvas.SetActive(false);
    }

    void SetUiVisible(bool on)
    {
        uiVisible = on;
        barCanvas.SetActive(on);
        panelCanvas.SetActive(on);
        diagCanvas.SetActive(on && diagVisible);
        showUiCanvas.SetActive(!on);
    }

    // ------------------------------------------------------------ onglets et rafraîchissement

    bool PanelVisible(Panel p)
    {
        if (!Eval(p.target, p.showIf)) return false;
        foreach (var e in p.entries) if (Eval(e.target, e.showIf)) return true;
        return false;
    }

    Panel FirstVisiblePanel()
    {
        foreach (var p in panels) if (PanelVisible(p)) return p;
        return panels.Count > 0 ? panels[0] : null;
    }

    void SelectPanel(Panel p)
    {
        currentPanel = p;
        foreach (var q in panels)
        {
            q.page.SetActive(q == p);
            q.tab.GetComponent<Image>().color = q == p ? CTabOn : CTab;
        }
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    void RefreshAll()
    {
        bool anyRunning = sim.IsRunning;
        foreach (var p in panels)
        {
            bool vis = PanelVisible(p);
            p.tab.gameObject.SetActive(vis);
            foreach (var e in p.entries)
            {
                bool show = Eval(e.target, e.showIf);
                if (e.row.activeSelf != show) e.row.SetActive(show);
                if (!show) continue;
                bool can = CanEdit(e);
                foreach (var s in e.selectables) s.interactable = can;
                e.labelText.color = can ? CText : CDim;
                try { e.refresh?.Invoke(); } catch (Exception ex) { Debug.LogWarning("JanusUI : " + e.key + " : " + ex.Message); }
            }
        }
        if (currentPanel == null || !PanelVisible(currentPanel)) SelectPanel(FirstVisiblePanel());

        string estimate = anyRunning ? "" : sim.RunEstimate;
        lockBanner.text = anyRunning
            ? L("Simulation en cours : seuls les paramètres à barre bleue sont modifiables. Stop pour régler les autres.",
                "Simulation running: only settings with a blue bar can be changed. Stop to edit the others.")
            : estimate;
        lockBanner.color = anyRunning ? CWarn : CAccent;
        lockBanner.gameObject.SetActive(lockBanner.text.Length > 0);
    }

    void Update()
    {
        if (!built || sim == null) return;
        fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) { refreshTimer = 0.25f; RefreshAll(); }

        // boutons
        bool running = sim.IsRunning, stopped = !running;
        bPlay.interactable = stopped || sim.paused;
        tPlay.text = running && sim.paused ? L("Reprendre", "Resume") : L("Lecture", "Play");
        bPause.interactable = running;
        tPause.text = sim.paused ? L("Reprendre", "Resume") : "Pause";
        bStop.interactable = running;
        bReference.interactable = bLoad.interactable = stopped;
        tRec.text = recorder.IsRecording ? L("Arrêter la vidéo", "Stop video") + $" ({recorder.FramesWritten})" : L("Enregistrer la vidéo", "Record video");

        // état
        string state;
        Color sc = CText;
        if (!string.IsNullOrEmpty(sim.LastError) && stopped) { state = L("Erreur : ", "Error: ") + sim.LastError; sc = CError; }
        else if (running) state = (sim.paused ? L("En pause", "Paused") : L("En cours", "Running")) + $" — t = {sim.SimTime:F2} — " +
                                  L("pas", "step") + $" {sim.StepCount}" + (sim.CampaignProgress.Length > 0 ? " — " + sim.CampaignProgress : "");
        else if (sim.IsFinished) { state = L("Campagne terminée", "Campaign finished"); sc = CPlay; }
        else state = L("Arrêtée", "Stopped");
        statusText.text = state + $"   {fpsSmooth:F0} " + L("img/s", "fps");
        statusText.color = sc;

        // mesures
        if (diagCanvas.activeSelf)
        {
            string d = sim.DiagnosticText;
            if (!string.IsNullOrEmpty(recorder.Status)) d += "\n\n" + L("VIDÉO : ", "VIDEO: ") + recorder.Status;
            diagText.text = d;
        }
    }

    // ------------------------------------------------------------ actions

    void OnPlay()
    {
        if (sim.IsRunning && sim.paused) { sim.paused = false; return; }
        if (!sim.IsRunning) sim.StartSimulation();
        RefreshAll();
    }

    void OnPause()
    {
        if (sim.IsRunning) sim.paused = !sim.paused;
    }

    void OnStop()
    {
        sim.StopSimulation();
        RefreshAll();
    }

    void OnReference()
    {
        if (sim.IsRunning)
        {
            ShowMessage(L("Référence non appliquée : arrête d'abord la simulation (Stop).",
                          "Reference not applied: stop the simulation first (Stop)."));
            return;
        }
        bool lss = sim.mode == JanusPMSimulation.InitMode.LargeScaleStructure;
        sim.ApplyReference();
        RefreshAll();
        ShowMessage(lss
            ? L("Référence GRANDE ÉCHELLE appliquée (15/10/2026) : c−/c+ = 0,1, |M−| = 1,6e7, 884 736 + 1 404 928 particules, " +
                "spectre réaliste 2LPT, boîte 200 Mpc, Janus σ₈ = 0,3, ΛCDM σ₈ = 0,81, q₀ = −0,09 (z de départ ≈ 5,56). " +
                "Campagne : Janus + ΛCDM, listes vides.",
                "LARGE-SCALE reference applied (15/10/2026): c−/c+ = 0.1, |M−| = 1.6e7, 884,736 + 1,404,928 particles, " +
                "realistic 2LPT spectrum, 200 Mpc box, Janus σ₈ = 0.3, ΛCDM σ₈ = 0.81, q₀ = −0.09 (start z ≈ 5.56). " +
                "Campaign: Janus + ΛCDM, empty lists.")
            : L("Référence GALAXIE appliquée : 300 000 étoiles, 480 000 particules de milieu, c−/c+ = 1. " +
                "ATTENTION : si tu voulais la référence grande échelle, passe d'abord le mode sur « Grande échelle » " +
                "puis clique de nouveau sur Référence (changer de mode après coup ne la remplace pas).",
                "GALAXY reference applied: 300,000 stars, 480,000 medium particles, c−/c+ = 1. " +
                "WARNING: if you wanted the large-scale reference, first switch the mode to 'Large scale' " +
                "then click Reference again (changing the mode afterwards does not replace it)."));
    }

    void OnRecord()
    {
        if (recorder.IsRecording) recorder.StopRecording(); else recorder.StartRecording();
    }

    void OnQuit()
    {
        if (recorder.IsRecording) recorder.StopRecording();
        sim.StopSimulation();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ explorateur de dossiers
    // Fenêtre intégrée (fonctionne dans l'application déployée, sans boîte de dialogue du système) :
    // lecteurs et dossiers usuels, navigation, création de dossier, saisie directe d'un chemin.

    GameObject browserCanvas;
    InputField browserPath, browserNewName;
    RectTransform browserList;
    Text browserMsg;
    ScrollRect browserScroll;
    string browserDir;
    Action<string> browserPick;

    static string AppFolder => JanusWeb.AppFolder;

    void OpenFolderBrowser(string start, Action<string> onPick)
    {
        if (browserCanvas == null) BuildFolderBrowser();
        browserPick = onPick;
        string d = string.IsNullOrEmpty(start) ? AppFolder : start;
        try { d = Path.GetFullPath(d); } catch { d = AppFolder; }
        while (!Directory.Exists(d))
        {
            var parent = Directory.GetParent(d);
            if (parent == null) { d = AppFolder; break; }
            d = parent.FullName;
        }
        browserCanvas.SetActive(true);
        BrowseTo(d);
    }

    void BuildFolderBrowser()
    {
        browserCanvas = MakeCanvas("Janus UI - dossiers", 40);
        var dim = Make("Fond", browserCanvas.transform);
        Stretch(RT(dim));
        Bg(dim, new Color(0, 0, 0, 0.6f));                     // bloque les clics derrière la fenêtre
        var win = Make("Fenêtre", dim.transform);
        var wr = RT(win);
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(860, 640);
        Bg(win, CPanel);
        var v = win.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(16, 16, 14, 14); v.spacing = 8;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false;

        var title = MakeText(win.transform, L("Choisir un dossier", "Choose a folder"), 18, CAccent, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        LE(title.gameObject, -1, 28);

        // chemin + dossier parent
        var row = Row(win.transform, 34);
        browserPath = MakeInput(row, L("chemin du dossier", "folder path"), s => { if (Directory.Exists(s)) BrowseTo(s); });
        LE(browserPath.gameObject, -1, -1, 1);
        LE(MakeButton(row, L("Dossier parent", "Parent folder"), CButton, () =>
        {
            var parent = browserDir != null ? Directory.GetParent(browserDir) : null;
            if (parent != null) BrowseTo(parent.FullName);
        }, -1).gameObject, 150, -1);

        // raccourcis : dossiers usuels et lecteurs
        var quick = Row(win.transform, 30);
        void Quick(string label, string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            LE(MakeButton(quick, label, CTab, () => BrowseTo(path), -1, out var t, 14).gameObject, -1, -1, 1);
        }
        Quick(L("Application", "Application"), AppFolder);
        Quick(L("Personnel", "Home"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        Quick(L("Documents", "Documents"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Quick(L("Bureau", "Desktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        try
        {
            int n = 0;
            foreach (var drive in Directory.GetLogicalDrives())
            {
                if (++n > 6) break;
                Quick(drive, drive);
            }
        }
        catch { }

        // liste des sous-dossiers
        var area = Make("Liste", win.transform);
        LE(area, -1, 380, -1, 380);
        Bg(area, new Color(0.04f, 0.05f, 0.08f, 1f));
        browserScroll = area.AddComponent<ScrollRect>();
        browserScroll.horizontal = false; browserScroll.scrollSensitivity = 30f;
        browserScroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Make("Vue", area.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        Bg(viewport, new Color(0, 0, 0, 0.01f));
        browserScroll.viewport = RT(viewport);
        browserList = RT(Make("Contenu", viewport.transform));
        browserList.anchorMin = new Vector2(0, 1); browserList.anchorMax = Vector2.one; browserList.pivot = new Vector2(0.5f, 1);
        browserList.offsetMin = browserList.offsetMax = Vector2.zero;
        var lv = browserList.gameObject.AddComponent<VerticalLayoutGroup>();
        lv.padding = new RectOffset(6, 6, 6, 6); lv.spacing = 2;
        lv.childControlWidth = lv.childControlHeight = true; lv.childForceExpandHeight = false;
        browserList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        browserScroll.content = browserList;

        browserMsg = MakeText(win.transform, "", 14, CWarn, TextAnchor.MiddleLeft);
        LE(browserMsg.gameObject, -1, 22);

        // nouveau dossier
        var nrow = Row(win.transform, 34);
        browserNewName = MakeInput(nrow, L("nom du nouveau dossier", "new folder name"), s => { });
        LE(browserNewName.gameObject, -1, -1, 1);
        LE(MakeButton(nrow, L("Créer le dossier", "Create folder"), CButton, () =>
        {
            string name = (browserNewName.text ?? "").Trim();
            if (name.Length == 0 || browserDir == null) return;
            try
            {
                string path = Path.Combine(browserDir, name);
                Directory.CreateDirectory(path);
                browserNewName.SetTextWithoutNotify("");
                BrowseTo(path);
            }
            catch (Exception ex) { browserMsg.text = L("Création impossible : ", "Cannot create: ") + ex.Message; }
        }, -1).gameObject, 170, -1);

        // validation
        var brow = Row(win.transform, 40);
        LE(Make("Espace", brow), -1, -1, 1);
        LE(MakeButton(brow, L("Annuler", "Cancel"), CButton, () => browserCanvas.SetActive(false), -1).gameObject, 130, -1);
        LE(MakeButton(brow, L("Choisir ce dossier", "Use this folder"), CPlay, () =>
        {
            if (browserDir != null) browserPick?.Invoke(browserDir);
            browserCanvas.SetActive(false);
        }, -1).gameObject, 200, -1);
    }

    Transform Row(Transform parent, float h)
    {
        var row = Make("Rangée", parent);
        LE(row, -1, h, -1, h);
        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 6; hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        return row.transform;
    }

    void BrowseTo(string dir)
    {
        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(dir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            browserMsg.text = L("Accès impossible : ", "Cannot open: ") + ex.Message;
            return;
        }
        browserDir = dir;
        browserPath.SetTextWithoutNotify(dir);
        browserMsg.text = "";
        foreach (Transform c in browserList) Destroy(c.gameObject);
        int shown = 0;
        foreach (var d in dirs)
        {
            string name = Path.GetFileName(d);
            if (name.StartsWith(".") || name.StartsWith("$")) continue;          // dossiers cachés / système
            if (++shown > 500) { browserMsg.text = L("Liste limitée à 500 dossiers.", "List limited to 500 folders."); break; }
            string target = d;
            var b = MakeButton(browserList, "   " + name, CField, () => BrowseTo(target), -1, out var t, 15);
            t.alignment = TextAnchor.MiddleLeft;
            LE(b.gameObject, -1, 28);
        }
        if (shown == 0) browserMsg.text = L("Aucun sous-dossier.", "No subfolder.");
        browserScroll.verticalNormalizedPosition = 1f;
    }

    // ------------------------------------------------------------ réglages enregistrés

    string PresetFolder => string.IsNullOrEmpty(presetFolder)
        ? JanusWeb.DefaultFolder("JanusPresets") : presetFolder;

    string PresetPath()
    {
        string n = presetName.text.Trim();
        if (n.Length == 0) n = "reglage";
        foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return Path.Combine(PresetFolder, n + ".txt");
    }

    void OnSavePreset()
    {
        try
        {
            Directory.CreateDirectory(PresetFolder);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Réglages Janus PM — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            foreach (var p in panels)
                foreach (var e in p.entries)
                    sb.AppendLine(e.key + " = " + Format(e.get()));
            File.WriteAllText(PresetPath(), sb.ToString()); JanusWeb.Sync();
            ShowMessage(L("Réglages sauvés : ", "Settings saved: ") + PresetPath());
        }
        catch (Exception ex) { ShowMessage(L("Sauvegarde impossible : ", "Cannot save: ") + ex.Message); }
    }

    void OnLoadPreset()
    {
        if (sim.IsRunning) return;
        string path = PresetPath();
        if (!File.Exists(path)) { ShowMessage(L("Réglage introuvable : ", "Settings not found: ") + path); return; }
        var map = new Dictionary<string, Entry>();
        foreach (var p in panels) foreach (var e in p.entries) map[e.key] = e;
        int n = 0;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("#")) continue;
            int i = line.IndexOf('=');
            if (i < 0) continue;
            string k = line.Substring(0, i).Trim(), val = line.Substring(i + 1).Trim();
            if (map.TryGetValue(k, out var e) && TryParse(e.type, val, out var v)) { e.set(v); n++; }
        }
        RefreshAll();
        ShowMessage($"{n} " + L("paramètres chargés depuis ", "settings loaded from ") + path);
    }

    void ShowMessage(string msg)
    {
        Debug.Log("JanusUI : " + msg);
        helpTitle.text = L("Réglages", "Settings");
        helpBody.text = msg;
    }
}
