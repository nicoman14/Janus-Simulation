// JanusVideoRecorder.cs
// Enregistrement vidéo de la simulation, utilisable dans l'application déployée (pas seulement dans l'éditeur).
//
// Principe : pendant l'enregistrement, la caméra rend dans une texture de taille fixe (largeur × hauteur),
// affichée à l'écran sous l'interface. Chaque image est relue sur le GPU (AsyncGPUReadback, sans bloquer)
// puis envoyée à un fil d'écriture :
//   - si ffmpeg est trouvé : encodage direct en MP4 (H.264) ;
//   - sinon : suite d'images PNG numérotées (à assembler plus tard, par ex. avec ffmpeg).
// L'interface (panneaux, boutons) n'apparaît pas dans la vidéo.
// Une image de vidéo = une image calculée : la vidéo a une cadence régulière même si le calcul est lent.
//
// ffmpeg : placer ffmpeg.exe (Windows) ou ffmpeg (macOS / Linux) dans Assets/StreamingAssets (recommandé :
// fonctionne dans l'éditeur et dans le build), ou à côté de l'exécutable, ou dans le PATH,
// ou renseigner son chemin dans « Chemin de ffmpeg ».
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class JanusVideoRecorder : MonoBehaviour
{
    [UIPanel("Vidéo", En = "Video")]
    [UILabel("Largeur (pixels)", En = "Width (pixels)")] [Live]
    [Tooltip("Largeur de la vidéo. Nombre pair (exigé par l'encodage H.264). 1920 = Full HD.")]
    [TooltipEn("Video width. Even number (required by H.264 encoding). 1920 = Full HD.")]
    public int width = 1920;
    [UILabel("Hauteur (pixels)", En = "Height (pixels)")] [Live]
    [Tooltip("Hauteur de la vidéo. Nombre pair. 1080 = Full HD.")]
    [TooltipEn("Video height. Even number. 1080 = Full HD.")]
    public int height = 1080;
    [UILabel("Images par seconde", En = "Frames per second")] [Live]
    [Tooltip("Cadence de la vidéo produite. Chaque image calculée devient une image de la vidéo.")]
    [TooltipEn("Frame rate of the produced video. Each computed frame becomes one video frame.")]
    public int fps = 30;
    [UILabel("Qualité (CRF)", En = "Quality (CRF)")] [Live] [Range(10, 35)]
    [Tooltip("Qualité de l'encodage MP4 : 18 = très bonne, 23 = standard, plus haut = fichier plus petit.")]
    [TooltipEn("MP4 encoding quality: 18 = very good, 23 = standard, higher = smaller file.")]
    public int crf = 18;
    [UILabel("Chemin de ffmpeg", En = "ffmpeg path")] [Live]
    [Tooltip("Vide = recherche automatique (à côté de l'exécutable, puis dans le PATH). " +
             "Sans ffmpeg, la vidéo est enregistrée en images PNG numérotées.")]
    [TooltipEn("Empty = automatic search (next to the executable, then in the PATH). Without ffmpeg, the video is saved as numbered PNG images.")]
    public string ffmpegPath = "";
    [UILabel("Retourner verticalement", En = "Flip vertically")] [Live]
    [Tooltip("À décocher si la vidéo produite est à l'envers (dépend de la carte graphique et de l'API : DirectX, Vulkan, Metal…).")]
    [TooltipEn("Turn off if the produced video is upside down (depends on the graphics card and API: DirectX, Vulkan, Metal…).")]
    public bool flipVertical = true;
    [UILabel("Dossier des vidéos", En = "Video folder")] [Live]
    [Tooltip("Vide = dossier JanusVideos à côté de l'application (ou du projet dans l'éditeur).")]
    [TooltipEn("Empty = JanusVideos folder next to the application (or the project in the editor).")]
    [FolderPath]
    public string outputFolder = "";

    [UIHide] public Camera targetCamera;

    public bool IsRecording { get; private set; }
    public string Status { get; private set; } = "";
    public int FramesWritten => framesWritten;

    RenderTexture rt;
    GameObject displayCanvas;
    Process ffmpeg;
    Stream ffmpegIn;
    Thread writer;
    BlockingCollection<byte[]> frames;
    int framesWritten, framesRequested, pending;
    int recW, recH;
    string pngFolder, outputPath;
    volatile bool writerFailed;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    public string OutputRoot
    {
        get
        {
            return string.IsNullOrEmpty(outputFolder)
                ? JanusWeb.DefaultFolder("JanusVideos")
                : outputFolder;
        }
    }

    // ------------------------------------------------------------ démarrage

    public void StartRecording()
    {
        if (IsRecording) return;
        if (JanusWeb.IsWeb)
        {
            Status = JanusLang.L("Vidéo indisponible dans la version web (utilisez l'enregistreur d'écran du système).",
                                 "Video is not available in the web version (use your system's screen recorder).");
            return;
        }
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) { Status = JanusLang.L("Aucune caméra à enregistrer.", "No camera to record."); return; }

        recW = Mathf.Max(16, width & ~1);
        recH = Mathf.Max(16, height & ~1);
        Directory.CreateDirectory(OutputRoot);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        frames = new BlockingCollection<byte[]>(90);   // ~3 s d'avance au maximum (limite la mémoire)
        framesWritten = framesRequested = pending = 0;
        writerFailed = false;

        string exe = FindFfmpeg();
        if (exe != null)
        {
            outputPath = Path.Combine(OutputRoot, "janus_" + stamp + ".mp4");
            string args = $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {recW}x{recH} -r {Mathf.Max(1, fps)} -i - " +
                          (flipVertical ? "-vf vflip " : "") + $"-c:v libx264 -preset medium -crf {crf} -pix_fmt yuv420p \"{outputPath}\"";
            try
            {
                ffmpeg = new Process();
                ffmpeg.StartInfo = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = false,
                    CreateNoWindow = true
                };
                ffmpeg.Start();
                ffmpegIn = ffmpeg.StandardInput.BaseStream;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Janus vidéo : ffmpeg n'a pas pu démarrer (" + e.Message + "), enregistrement en PNG.");
                ffmpeg = null; ffmpegIn = null;
            }
        }
        if (ffmpeg == null)
        {
            pngFolder = Path.Combine(OutputRoot, "janus_" + stamp + "_png");
            Directory.CreateDirectory(pngFolder);
            outputPath = pngFolder;
        }

        rt = new RenderTexture(recW, recH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();
        targetCamera.targetTexture = rt;
        ShowDisplay(true);

        writer = new Thread(WriterLoop) { IsBackground = true, Name = "JanusVideoWriter" };
        writer.Start();

        IsRecording = true;
        Status = ffmpeg != null ? JanusLang.L("Enregistrement MP4 en cours…", "Recording MP4…")
                                : JanusLang.L("Enregistrement PNG en cours (ffmpeg introuvable)…", "Recording PNG frames (ffmpeg not found)…");
        StartCoroutine(CaptureLoop());
    }

    string FindFfmpeg()
    {
        var candidates = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(ffmpegPath)) candidates.Add(ffmpegPath);
        // StreamingAssets : un seul emplacement pour l'éditeur ET l'application compilée
        // (Assets/StreamingAssets est recopié tel quel dans le build)
        candidates.Add(Path.Combine(Application.streamingAssetsPath, "ffmpeg.exe"));
        candidates.Add(Path.Combine(Application.streamingAssetsPath, "ffmpeg"));
        string appDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        candidates.Add(Path.Combine(appDir, "ffmpeg.exe"));
        candidates.Add(Path.Combine(appDir, "ffmpeg"));
        candidates.Add("ffmpeg");   // PATH
        foreach (var c in candidates)
        {
            try
            {
                if (c != "ffmpeg" && !File.Exists(c)) continue;
                using (var p = Process.Start(new ProcessStartInfo(c, "-version")
                       { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
                {
                    if (p != null && p.WaitForExit(4000) && p.ExitCode == 0) return c;
                }
            }
            catch { /* candidat suivant */ }
        }
        return null;
    }

    // ------------------------------------------------------------ capture

    IEnumerator CaptureLoop()
    {
        var wait = new WaitForEndOfFrame();
        while (IsRecording)
        {
            yield return wait;
            if (!IsRecording) break;
            if (writerFailed) { StopRecording(); break; }
            pending++;
            framesRequested++;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, OnFrame);
        }
    }

    void OnFrame(AsyncGPUReadbackRequest req)
    {
        pending--;
        if (req.hasError || frames == null || frames.IsAddingCompleted) return;
        byte[] buf = req.GetData<byte>().ToArray();
        try { frames.Add(buf); } catch (System.InvalidOperationException) { }
    }

    void WriterLoop()
    {
        try
        {
            foreach (var buf in frames.GetConsumingEnumerable())
            {
                if (ffmpegIn != null)
                {
                    ffmpegIn.Write(buf, 0, buf.Length);
                }
                else
                {
                    // PNG : la texture est relue de bas en haut, on remet l'image à l'endroit
                    int stride = recW * 4;
                    var flipped = buf;
                    if (flipVertical)
                    {
                        flipped = new byte[buf.Length];
                        for (int y = 0; y < recH; y++)
                            System.Buffer.BlockCopy(buf, y * stride, flipped, (recH - 1 - y) * stride, stride);
                    }
                    byte[] png = ImageConversion.EncodeArrayToPNG(flipped,
                        UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)recW, (uint)recH);
                    File.WriteAllBytes(Path.Combine(pngFolder, $"frame_{framesWritten:D6}.png"), png);
                }
                Interlocked.Increment(ref framesWritten);
            }
        }
        catch (System.Exception e)
        {
            writerFailed = true;
            Debug.LogError("Janus vidéo : écriture interrompue (" + e.Message + ")");
        }
    }

    // ------------------------------------------------------------ arrêt

    public void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        AsyncGPUReadback.WaitAllRequests();        // les dernières images arrivent dans OnFrame
        frames.CompleteAdding();
        writer?.Join(30000);
        if (ffmpegIn != null)
        {
            try { ffmpegIn.Flush(); ffmpegIn.Close(); } catch { }
            ffmpegIn = null;
        }
        if (ffmpeg != null)
        {
            try { if (!ffmpeg.WaitForExit(60000)) ffmpeg.Kill(); } catch { }
            ffmpeg.Dispose(); ffmpeg = null;
        }
        if (targetCamera != null) targetCamera.targetTexture = null;
        ShowDisplay(false);
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        frames.Dispose(); frames = null;
        Status = writerFailed
            ? JanusLang.L("Enregistrement interrompu (erreur d'écriture, voir la console).", "Recording interrupted (write error, see the console).")
            : JanusLang.L("Vidéo enregistrée", "Video saved") + $" ({framesWritten} " + JanusLang.L("images", "frames") + $") : {outputPath}";
        Debug.Log("Janus vidéo : " + Status);
    }

    // Pendant l'enregistrement, la caméra ne dessine plus à l'écran : on affiche sa texture en fond
    void ShowDisplay(bool on)
    {
        if (on && displayCanvas == null)
        {
            displayCanvas = new GameObject("JanusVideoDisplay");
            var canvas = displayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;                     // sous tous les panneaux
            var bg = new GameObject("Fond", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(displayCanvas.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = Color.black; bgImg.raycastTarget = false;
            var img = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            img.transform.SetParent(displayCanvas.transform, false);
            Stretch(img.GetComponent<RectTransform>());
            img.GetComponent<RawImage>().raycastTarget = false;
            var fit = img.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }
        if (displayCanvas == null) return;
        displayCanvas.SetActive(on);
        if (on)
        {
            var raw = displayCanvas.GetComponentInChildren<RawImage>(true);
            raw.texture = rt;
            raw.GetComponent<AspectRatioFitter>().aspectRatio = (float)recW / recH;
        }
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void OnDisable()
    {
        if (IsRecording) StopRecording();
    }
}
