// JanusWeb.jslib — fonctions JavaScript appelées par JanusWeb.cs dans la version web.
mergeInto(LibraryManager.library, {
  // Propose un fichier texte en téléchargement (CSV des simulations)
  JanusDownloadText: function (namePtr, contentPtr) {
    var name = UTF8ToString(namePtr);
    var content = UTF8ToString(contentPtr);
    var blob = new Blob([content], { type: 'text/csv;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url; a.download = name;
    document.body.appendChild(a); a.click(); document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 2000);
  },
  // Enregistre le stockage persistant du navigateur (réglages sauvés, fichiers)
  JanusSyncFS: function () {
    if (typeof FS !== 'undefined' && FS.syncfs) FS.syncfs(false, function (err) { if (err) console.warn('JanusSyncFS', err); });
  }
});
