# Janus contre ΛCDM — simulateur de masses négatives

Simulateur gravitationnel particule-maillage (GPU, Unity) qui teste le modèle cosmologique Janus de J.-P. Petit
(masses négatives) face au modèle standard ΛCDM, avec les mêmes conditions initiales et les mêmes mesures.

* **Essayer en ligne** : https://nicoman14.github.io/janus-simulation/ (version web allégée, navigateur avec WebGPU : Chrome ou Edge récent)
* **Article** : description complète de la méthode et des résultats (lien à ajouter)
* **Manuel** : bouton « Manuel » dans le logiciel (français et anglais)

## Deux modes

* **Galaxie dans une cavité** : un disque d'étoiles et de gaz dans une cavité du milieu de masses négatives, comparé à des halos de matière noire.
* **Structure à grande échelle** : boîte périodique de 200 à 400 Mpc en expansion, de z ≈ 5,6 à aujourd'hui, comparée à ΛCDM (spectre de puissance, vides, lentille, amas, galaxies traceuses).

## Compiler soi-même

1. Installer Unity 6.6 (gratuit, via Unity Hub).
2. Ouvrir ce dossier comme projet, puis la scène principale.
3. **Application de bureau** : File → Build Profiles → Windows (ou macOS / Linux) → Build.
4. **Version web** : File → Build Profiles → Web ; Player Settings → Other Settings → décocher « Auto Graphics API » et placer **WebGPU** en premier ; Publishing Settings → Compression Format : **Disabled** (ou Gzip avec « Decompression Fallback »). Build dans le dossier `docs/`.

La vidéo MP4 utilise ffmpeg : placer `ffmpeg.exe` dans `Assets/StreamingAssets/` (non fourni dans le dépôt, trop volumineux). Sans lui, la vidéo est enregistrée en images PNG.

## Fichiers principaux

|Fichier|Rôle|
|-|-|
|`JanusPMSimulation.cs`|simulation, expansion, conditions initiales, mesures, campagnes, export CSV|
|`JanusPM.compute`|solveur PM sur GPU (dépôt CIC, FFT, Poisson, forces, intégration)|
|`JanusUI.cs`, `JanusUIAttributes.cs`|interface bilingue construite par le code|
|`JanusManual.cs`|texte du manuel intégré|
|`JanusWeb.cs`, `Plugins/WebGL/JanusWeb.jslib`|différences de la version web (dossiers, téléchargement des CSV)|
|`JanusVideoRecorder.cs`|enregistrement vidéo (bureau)|

## Licence

À choisir (par exemple MIT pour le code, CC BY 4.0 pour l'article).

\---

# Janus versus ΛCDM — negative-mass simulator

A GPU particle-mesh gravity simulator (Unity) testing J.-P. Petit's Janus cosmological model (negative masses) against
the standard ΛCDM model, with the same initial conditions and the same measurements. Try it online (web version,
WebGPU browser), read the article, or open the built-in Manual (French and English). Build instructions above:
Unity 6.6, desktop or Web target (WebGPU first, compression disabled, output to `docs/`).

