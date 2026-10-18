// JanusWeb.cs
// Ce qui change entre l'application de bureau et la version web (navigateur, WebGPU).
// Le même code sert aux deux : seules ces fonctions savent sur quelle plateforme on tourne.
//   - dossiers : à côté de l'application (bureau) ou dans le stockage du navigateur (web) ;
//   - fichiers CSV : écrits sur le disque (bureau) ou proposés en téléchargement (web) ;
//   - configuration de référence allégée pour le web (mémoire GPU des navigateurs limitée).
// Le fichier Plugins/WebGL/JanusWeb.jslib fournit le téléchargement et la sauvegarde du stockage navigateur.

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class JanusWeb
{
    // Vrai dans le navigateur (build Web), faux dans l'éditeur et l'application de bureau
    public static bool IsWeb
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    // Dossier racine des sorties : à côté du projet / de l'exécutable, ou stockage persistant du navigateur
    public static string AppFolder => IsWeb ? Application.persistentDataPath
                                            : Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    public static string DefaultFolder(string name) => Path.Combine(AppFolder, name);

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void JanusDownloadText(string fileName, string content);
    [DllImport("__Internal")] static extern void JanusSyncFS();
#else
    static void JanusDownloadText(string fileName, string content) { }
    static void JanusSyncFS() { }
#endif

    // Fichiers écrits pendant la session (proposés au téléchargement dans la version web)
    static readonly List<string> written = new List<string>();
    public static IReadOnlyList<string> Written => written;

    public static void Register(string path)
    {
        if (string.IsNullOrEmpty(path) || written.Contains(path)) return;
        written.Add(path);
    }

    // Enregistre le stockage du navigateur (IndexedDB) après une écriture ; sans effet sur le bureau
    public static void Sync()
    {
        if (!IsWeb) return;
        try { JanusSyncFS(); } catch { }
    }

    // Télécharge un fichier texte (web) ; renvoie faux sur le bureau
    public static bool Download(string path)
    {
        if (!IsWeb || !File.Exists(path)) return false;
        try { JanusDownloadText(Path.GetFileName(path), File.ReadAllText(path)); return true; }
        catch { return false; }
    }

    // Télécharge tous les fichiers de la session ; renvoie le nombre de fichiers proposés
    public static int DownloadAll()
    {
        int n = 0;
        foreach (var p in written) if (Download(p)) n++;
        return n;
    }
}
