// JanusManual.cs
// Contenu du manuel intégré (bouton « Manuel » de la barre du haut), en français et en anglais.
// Texte enrichi Unity : <b>, <i>, <color>, <size>. Chaque section reste sous ~12 000 caractères
// (limite de sommets d'un composant Text).

public static class JanusManual
{
    public struct Section
    {
        public string titleFr, titleEn, fr, en;
        public Section(string tf, string te, string f, string e) { titleFr = tf; titleEn = te; fr = f; en = e; }
    }

    const string H = "<color=#5C9EFF><b>";
    const string HE = "</b></color>";

    public static readonly Section[] Sections =
    {
        // ------------------------------------------------------------------ 1
        new Section("Présentation", "Overview",
H + "Ce que fait le logiciel" + HE + @"
Ce logiciel simule la gravitation de millions de particules pour tester le modèle cosmologique Janus de J.-P. Petit, qui remplace la matière noire et l'énergie noire par des masses négatives. Chaque simulation Janus peut être comparée, avec exactement les mêmes conditions initiales et les mêmes mesures, à un témoin : le modèle standard ΛCDM, ou des halos de matière noire.

" + H + "Deux modes" + HE + @"
• <b>Galaxie dans une cavité</b> : un disque d'étoiles et de gaz placé dans une cavité du milieu de masses négatives. On regarde s'il reste confiné, si sa courbe de rotation est plate, s'il forme une barre ou des bras.
• <b>Structure à grande échelle</b> : une boîte périodique de 200 à 400 Mpc en expansion, depuis z ≈ 5,6 jusqu'à aujourd'hui. On regarde la toile cosmique : spectre de puissance, vides, amas, lentille gravitationnelle.

" + H + "Principe de comparaison" + HE + @"
Une graine (seed) fixe entièrement le tirage initial. Pour une graine donnée, Janus et son témoin partent du même champ ; seule la physique change. Une <b>campagne</b> enchaîne automatiquement plusieurs graines et plusieurs modèles, et écrit un fichier CSV par simulation plus une synthèse (moyenne ± écart-type entre graines).

" + H + "Ce que le logiciel n'est pas" + HE + @"
La gravité est newtonienne (limite de faible champ), sans relativité générale, sans pression ni rayonnement à grande échelle. Les « galaxies » de la grande échelle sont des groupes de particules ordinaires, pas des galaxies avec étoiles et gaz. Les résultats décrivent donc une réalisation précise du modèle, et non une prédiction définitive.",
H + "What the software does" + HE + @"
This software simulates the gravity of millions of particles to test J.-P. Petit's Janus cosmological model, which replaces dark matter and dark energy with negative masses. Each Janus simulation can be compared, with exactly the same initial conditions and the same measurements, to a control: the standard ΛCDM model, or dark-matter haloes.

" + H + "Two modes" + HE + @"
• <b>Galaxy in a cavity</b>: a disk of stars and gas placed in a cavity of the negative-mass medium. We check whether it stays confined, whether its rotation curve is flat, whether it forms a bar or arms.
• <b>Large-scale structure</b>: a periodic, expanding box of 200 to 400 Mpc, from z ≈ 5.6 to today. We look at the cosmic web: power spectrum, voids, clusters, gravitational lensing.

" + H + "Comparison principle" + HE + @"
A seed fully fixes the initial draw. For a given seed, Janus and its control start from the same field; only the physics changes. A <b>campaign</b> automatically runs several seeds and models, and writes one CSV file per simulation plus a summary (mean ± standard deviation across seeds).

" + H + "What the software is not" + HE + @"
Gravity is Newtonian (weak-field limit), without general relativity, pressure or radiation at large scale. The large-scale « galaxies » are groups of ordinary particles, not galaxies with stars and gas. Results therefore describe one specific realisation of the model, not a definitive prediction."),

        // ------------------------------------------------------------------ 2
        new Section("Prise en main", "Getting started",
H + "La barre du haut" + HE + @"
• <b>Lecture / Pause / Stop</b> : lance, suspend ou arrête la simulation (ou la campagne). Stop libère tout : les réglages redeviennent modifiables. En grande échelle, le premier clic sur Lecture affiche le plan de lancement (c−/c+, |M−|, ā/a, σ₈ et H final prévu pour chaque modèle) ; un second clic lance. Le lancement est bloqué si un H final prévu s'écarte de plus de 20 % de 67,7 km/s/Mpc (case « Ignorer le garde-fou H₀ » pour forcer).
• <b>Référence</b> : applique la configuration validée <i>du mode affiché</i>. Passez d'abord dans le bon mode (Scénario → mode), puis cliquez. Un message confirme « GALAXIE » ou « GRANDE ÉCHELLE ».
• <b>Sauver / Charger</b> : enregistre ou recharge tous les réglages sous un nom. Utilisez-le systématiquement : Unity remet les valeurs de la scène après chaque recompilation.
• <b>Enregistrer la vidéo</b> (onglet Vidéo : « Accélération » ne garde qu'une image sur N pour une vidéo N fois plus rapide), <b>Mesures</b> (panneau de texte à droite), <b>Vue initiale</b>, <b>Manuel</b>, langue, masquer, quitter.

" + H + "Le panneau de gauche" + HE + @"
Les onglets regroupent les paramètres : d'abord les réglages communs aux deux modèles (Scénario, Simulation, Univers en grande échelle, Galaxie et Gaz en mode galaxie), puis un onglet <b>Janus</b> et un onglet <b>ΛCDM</b> séparés, puis Mesures et export, Campagne, Rendu et Vidéo. Le réglage « Modèles : Janus et ΛCDM seulement » (onglet Scénario, activé par défaut) masque les anciens témoins. « Référence grande échelle » choisit la variante appliquée par le bouton Référence : Janus selon Petit (par défaut), Meilleur spectre ou Historique. Survolez un champ pour lire son aide en bas du panneau. Pendant une simulation, seuls les champs marqués d'une barre bleue sont modifiables.

" + H + "La vue 3D" + HE + @"
• Clic gauche glissé : rotation autour de la cible.
• Clic droit (ou milieu) glissé : déplacement.
• Molette : zoom.
Pendant que vous manipulez la vue, le calcul ralentit automatiquement (un pas par image) pour garder la vue fluide. En campagne, l'affichage de 2 millions de particules reste coûteux : masquer le milieu (onglet Rendu) allège beaucoup la vue.

" + H + "Lancer une campagne" + HE + @"
1. Choisir le mode, cliquer Référence, vérifier le message.
2. Onglet Campagne : activer la campagne, cocher les modèles, donner les graines (ex. 1 ; 2 ; 3) et, si besoin, des listes (σ₈, c−/c+, vitesses) séparées par « ; ».
3. Sauver sous un nom, puis Lecture.
4. Les CSV s'écrivent dans le dossier choisi (onglet Mesures et export) : un fichier par run, un fichier « _runs » et un fichier « _synthese » par campagne.

" + H + "Pièges à éviter" + HE + @"
• Ne remplacez pas les scripts pendant une campagne : Unity recompile et arrête tout.
• Une même graine redonne exactement le même résultat : relancer ne sert à rien, changez de graine.
• Vérifiez dans le CSV des runs les colonnes clés (box_size_mpc, light_speed_ratio, negative_total_mass, sigma8) avant d'interpréter.",
H + "The top bar" + HE + @"
• <b>Play / Pause / Stop</b>: starts, suspends or stops the simulation (or campaign). Stop releases everything: settings become editable again. In large-scale mode, the first click on Play shows the launch plan (c−/c+, |M−|, ā/a, σ₈ and predicted final H for each model); a second click launches. Launch is blocked if a predicted final H differs by more than 20% from 67.7 km/s/Mpc ('Ignore the H₀ guard' box to force).
• <b>Reference</b>: applies the validated configuration <i>of the displayed mode</i>. Switch to the right mode first (Scenario → mode), then click. A message confirms « GALAXY » or « LARGE SCALE ».
• <b>Save / Load</b>: saves or reloads all settings under a name. Use it every time: Unity restores the scene values after each recompilation.
• <b>Record video</b> (Video tab: <i>Speed-up</i> keeps only one frame in N for an N times faster video), <b>Measurements</b> (text panel on the right), <b>Reset view</b>, <b>Manual</b>, language, hide, quit.

" + H + "The left panel" + HE + @"
Tabs group the parameters: first the settings common to both models (Scenario, Simulation, Universe at large scale, Galaxy and Gas in galaxy mode), then separate <b>Janus</b> and <b>ΛCDM</b> tabs, then Measurements & export, Campaign, Rendering and Video. The 'Models: Janus and ΛCDM only' setting (Scenario tab, on by default) hides the old controls. 'Large-scale reference' chooses the variant applied by the Reference button: Janus per Petit (default), Best spectrum or Historical. Hover over a field to read its help at the bottom of the panel. While a simulation runs, only fields marked with a blue bar can be changed.

" + H + "The 3D view" + HE + @"
• Left drag: rotate around the target.
• Right (or middle) drag: pan.
• Wheel: zoom.
While you move the view, the computation automatically slows down (one step per frame) to keep the view smooth. In a campaign, drawing 2 million particles stays costly: hiding the medium (Rendering tab) lightens the view a lot.

" + H + "Running a campaign" + HE + @"
1. Choose the mode, click Reference, check the message.
2. Campaign tab: enable the campaign, tick the models, give the seeds (e.g. 1; 2; 3) and, if needed, lists (σ₈, c−/c+, rates) separated by « ; ».
3. Save under a name, then Play.
4. CSV files are written to the chosen folder (Measurements & export tab): one file per run, one « _runs » and one « _synthese » file per campaign.

" + H + "Pitfalls" + HE + @"
• Do not replace the scripts during a campaign: Unity recompiles and stops everything.
• The same seed gives exactly the same result: rerunning is useless, change the seed.
• Check the key columns of the runs CSV (box_size_mpc, light_speed_ratio, negative_total_mass, sigma8) before interpreting."),

        // ------------------------------------------------------------------ 3
        new Section("Théorie : Janus", "Theory: Janus",
H + "Deux secteurs de masse" + HE + @"
Le modèle Janus décrit l'univers par deux métriques couplées. L'une porte les masses positives (matière ordinaire, lumière), l'autre des masses négatives. À la limite newtonienne :
• deux masses positives s'attirent ;
• deux masses négatives s'attirent ;
• une masse positive et une masse négative se repoussent.

" + H + "Équations simulées" + HE + @"
On calcule deux potentiels à partir des densités (moyenne soustraite) :
   ∇²φ_A = 4πG (ρ+ − ρ̄+)      ∇²φ_B = 4πG (|ρ−| − |ρ̄−|)
avec F_A = −∇φ_A et F_B = −∇φ_B. Les accélérations comobiles sont :
   masses positives :  ẍ + 2H ẋ = (F_A − κ² F_B) / a³
   masses négatives :  ẍ + 2H̄ ẋ = (F_B − F_A / κ²) / ā³
κ = c−/c+ est le rapport des vitesses de la lumière des deux secteurs. Une masse positive voit les négatives avec le poids κ², une négative voit les positives avec le poids 1/κ². Ces poids viennent de l'énergie E = ρc²a³ + ρ̄c̄²ā³ du modèle.

" + H + "Conséquence de κ ≠ 1" + HE + @"
Les forces entre secteurs ne sont plus égales et opposées : la quantité de mouvement totale n'est pas conservée. Une galaxie dans une cavité un peu asymétrique s'auto-accélère (visible en mode galaxie avec κ = 0,1).

" + H + "Expansion du secteur positif" + HE + @"
Solution de Petit pour un univers ouvert dominé par une énergie négative, prise au rebond (a = 1, H = 0) :
   a = ch²(v)     t = s (v + sh(2v)/2)     H = th(v) / (s ch²(v))
   s = √( 3 / (8πG (κ²|ρ̄−| − ρ̄+)) )
L'expansion n'existe que si κ²|ρ̄−| > ρ̄+. Le paramètre de décélération vaut q = −1/(2(a − 1)). Le run s'arrête quand q atteint q₀ (−0,09 par défaut), soit a_fin = 1 + 1/(2|q₀|) = 6,556 et un départ à z = 5,556.

" + H + "Facteur d'échelle propre du secteur négatif" + HE + @"
   d²ā/dt² = −(4πG/3) (|ρ̄−| − ρ̄+/κ²) / ā²
intégré par Runge-Kutta 4, avec ā(0) = 1 et une vitesse initiale égale à λ fois la vitesse parabolique (λ = 1 par défaut). Les masses négatives subissent l'expansion ā, leur gravité est divisée par ā³.

" + H + "Choix de la masse négative" + HE + @"
Pour retrouver H₀ ≈ 67 km/s/Mpc en fin de run, il faut κ²|M−| ≈ M+ (1 + 2|q₀|/Ω_b) ≈ 4,6 M+. Avec M+ = 35 000 et κ = 0,1 : |M−| = 1,6·10⁷. Changer κ en gardant κ²|M−| fixe garde la même expansion.",
H + "Two mass sectors" + HE + @"
The Janus model describes the universe with two coupled metrics. One carries positive masses (ordinary matter, light), the other negative masses. In the Newtonian limit:
• two positive masses attract;
• two negative masses attract;
• a positive and a negative mass repel each other.

" + H + "Simulated equations" + HE + @"
Two potentials are computed from the densities (mean subtracted):
   ∇²φ_A = 4πG (ρ+ − ρ̄+)      ∇²φ_B = 4πG (|ρ−| − |ρ̄−|)
with F_A = −∇φ_A and F_B = −∇φ_B. Comoving accelerations are:
   positive masses:  ẍ + 2H ẋ = (F_A − κ² F_B) / a³
   negative masses:  ẍ + 2H̄ ẋ = (F_B − F_A / κ²) / ā³
κ = c−/c+ is the ratio of the speeds of light of the two sectors. A positive mass sees the negative ones with weight κ², a negative one sees the positive ones with weight 1/κ². These weights come from the model's energy E = ρc²a³ + ρ̄c̄²ā³.

" + H + "Consequence of κ ≠ 1" + HE + @"
Forces between sectors are no longer equal and opposite: total momentum is not conserved. A galaxy in a slightly asymmetric cavity self-accelerates (visible in galaxy mode with κ = 0.1).

" + H + "Expansion of the positive sector" + HE + @"
Petit's solution for an open universe dominated by negative energy, taken at the bounce (a = 1, H = 0):
   a = cosh²(v)     t = s (v + sinh(2v)/2)     H = tanh(v) / (s cosh²(v))
   s = √( 3 / (8πG (κ²|ρ̄−| − ρ̄+)) )
Expansion exists only if κ²|ρ̄−| > ρ̄+. The deceleration parameter is q = −1/(2(a − 1)). The run stops when q reaches q₀ (−0.09 by default), i.e. a_end = 1 + 1/(2|q₀|) = 6.556 and a start at z = 5.556.

" + H + "Own scale factor of the negative sector" + HE + @"
   d²ā/dt² = −(4πG/3) (|ρ̄−| − ρ̄+/κ²) / ā²
integrated with Runge-Kutta 4, with ā(0) = 1 and an initial rate equal to λ times the parabolic rate (λ = 1 by default). Negative masses follow the expansion ā; their gravity is divided by ā³.

" + H + "Choice of the negative mass" + HE + @"
To recover H₀ ≈ 67 km/s/Mpc at the end of the run, κ²|M−| ≈ M+ (1 + 2|q₀|/Ω_b) ≈ 4.6 M+ is needed. With M+ = 35,000 and κ = 0.1: |M−| = 1.6·10⁷. Changing κ while keeping κ²|M−| fixed keeps the same expansion."),

        // ------------------------------------------------------------------ 4
        new Section("Théorie : ΛCDM", "Theory: ΛCDM",
H + "Le témoin standard" + HE + @"
Le témoin ΛCDM remplace les masses négatives par une matière noire froide <b>positive</b> de masse (Ω_m/Ω_b − 1) M+ ≈ 5,33 M+ (Ω_m = 0,31, Ω_b = 0,049), portée par les mêmes particules. Toutes les masses s'attirent.

" + H + "Expansion" + HE + @"
Loi exacte d'un univers plat matière + constante cosmologique :
   a³ = a_fin³ (sh u / sh u_fin)²     u = (3/2) H_Λ t     ch² u_fin = 1/Ω_m
   H_Λ² = (Ω_Λ/Ω_m) (8πG/3) ρ̄_m / a_fin³
Le run s'arrête au même a_fin que Janus. En fin de run : H₀ = 67,7 km/s/Mpc, q₀ = Ω_m/2 − Ω_Λ = −0,535.

" + H + "Pourquoi ce témoin" + HE + @"
ΛCDM reproduit les observations de référence (fond diffus, amas, lentille faible). Si le témoin simulé redonne les résultats publiés du modèle standard, la chaîne de calcul est validée, et les écarts de Janus ne viennent pas d'un défaut numérique.

" + H + "Témoins historiques" + HE + @"
• <b>Matière noire (même masse)</b> : la masse négative remplacée par la même masse positive, sans constante cosmologique.
• <b>Matière noire réaliste</b> : matière noire de 5 × M+, sans constante cosmologique (H₀ trop faible).
• <b>Positives seules</b> : le milieu devient des traceurs sans masse.
Ils ont servi au développement ; ΛCDM est la vraie référence.

" + H + "Halos (mode galaxie)" + HE + @"
En mode galaxie, les témoins sont des halos de matière noire : NFW, isotherme à cœur, sphère uniforme, libres (particules qui bougent) ou figés.",
H + "The standard control" + HE + @"
The ΛCDM control replaces the negative masses by a <b>positive</b> cold dark matter of mass (Ω_m/Ω_b − 1) M+ ≈ 5.33 M+ (Ω_m = 0.31, Ω_b = 0.049), carried by the same particles. All masses attract.

" + H + "Expansion" + HE + @"
Exact law of a flat matter + cosmological constant universe:
   a³ = a_end³ (sinh u / sinh u_end)²     u = (3/2) H_Λ t     cosh² u_end = 1/Ω_m
   H_Λ² = (Ω_Λ/Ω_m) (8πG/3) ρ̄_m / a_end³
The run stops at the same a_end as Janus. At the end: H₀ = 67.7 km/s/Mpc, q₀ = Ω_m/2 − Ω_Λ = −0.535.

" + H + "Why this control" + HE + @"
ΛCDM reproduces the reference observations (CMB, clusters, weak lensing). If the simulated control gives back the published results of the standard model, the computation chain is validated, and Janus's differences do not come from a numerical defect.

" + H + "Historical controls" + HE + @"
• <b>Dark matter (same mass)</b>: the negative mass replaced by the same positive mass, without a cosmological constant.
• <b>Realistic dark matter</b>: dark matter of 5 × M+, without a cosmological constant (H₀ too low).
• <b>Positive only</b>: the medium becomes massless tracers.
They were used during development; ΛCDM is the true reference.

" + H + "Haloes (galaxy mode)" + HE + @"
In galaxy mode, the controls are dark-matter haloes: NFW, cored isothermal, uniform sphere, free (moving particles) or frozen."),

        // ------------------------------------------------------------------ 5
        new Section("Méthode numérique", "Numerical method",
H + "Solveur particule-maillage (PM)" + HE + @"
À chaque pas, sur la carte graphique :
1. <b>Dépôt</b> des masses sur une grille 256³ par la méthode CIC (chaque particule répartie sur 8 cellules), dans des tableaux séparés : positives, négatives (ou matière noire), halo.
2. <b>Poisson</b> par FFT : les deux densités sont placées dans les parties réelle et imaginaire d'un même champ complexe, transformé une seule fois. Dans l'espace de Fourier, φ_k = −4πG ρ_k / k² · exp(−k² ε²), ε = longueur de lissage. Le mode k = 0 est annulé (moyenne soustraite).
3. <b>Gradient</b> par différences centrées, puis interpolation CIC de la force à la position de chaque particule.

" + H + "Intégration" + HE + @"
Saute-mouton à pas variable : dérive x ← x + v Δt (boîte périodique), calcul des forces, puis impulsion
   v ← v · exp(−2H Δt) + (g / a³) Δt
Le facteur exp(−2H Δt) est le freinage de Hubble exact sur un pas ; le secteur négatif utilise ā et H̄. En grande échelle, le pas est adaptatif : Δt = min(dt, 0,1 t_dyn), t_dyn = (4πG ρ_ressenti)^(−1/2), ρ_ressenti = max(|ρ̄−|, ρ̄+/κ²) / ā³.

" + H + "Unités physiques" + HE + @"
Le code utilise G = 1, une boîte de 400 unités et M+ = 35 000. Correspondances :
• longueur : boîte = 200 Mpc comobiles (réglable) ;
• densité : ρ+ du code ≡ Ω_b ρ_crit (1 + z_départ)³ (Planck, H₀ = 67,7) ;
• temps : t_unité = √(G ρ+_code / (G_SI ρ_b,SI)), d'où les Gyr et les km/s/Mpc ;
• masse d'une particule ordinaire : Ω_b ρ_crit h² V / N+ (5,6·10¹⁰ M☉ pour 96³ particules dans 200 Mpc) ;
• nombres d'onde en h/Mpc, h = 0,677.

" + H + "Conditions initiales réalistes" + HE + @"
• Spectre P(k) = A k^n_s T(k)², T d'Eisenstein et Hu sans oscillations, n_s = 0,965.
• Normalisation par σ₈ (sphère de 8 Mpc/h), multipliée par D(z_départ)², D = croissance linéaire de ΛCDM.
• Champ gaussien tiré sur 128³, coupé au nombre d'onde de Nyquist du réseau de particules.
• Déplacement des particules depuis un réseau au 2e ordre (2LPT) : ψ₁ = −∇∇⁻²δ, ψ₂ = −(3/7)∇φ₂.
• Vitesses ΛCDM : f H ψ₁ + f₂ H ψ₂. Janus part au rebond (H = 0) ; par défaut avec des vitesses nulles.
• Théorie linéaire de Janus (deux fluides, mêmes équations que le calcul N corps) : δ̈+ + 2H δ̇+ = 4πG/a³ (ρ+ δ+ − κ² ρ− δ−) et δ̈− + 2H̄ δ̇− = 4πG/ā³ (ρ− δ− − ρ+ δ+ / κ²). Elle donne la croissance linéaire de Janus (affichée au départ de chaque run) et, avec « Janus : conditions initiales = mode croissant linéaire », le mode qui croît le plus vite au départ : même champ pour les deux secteurs, δ− = r δ+ (r < 0 : anticorrélé) et vitesses δ̇ = γ δ. « σ₈ = valeur linéaire aujourd'hui » choisit l'amplitude de départ pour que la théorie linéaire donne σ₈ en fin de run, comme en ΛCDM.
• Champ des masses négatives : indépendant (autre graine) ou anticorrélé (opposé de celui des baryons).
• <b>Test de convergence</b> (onglet Campagne, « niveaux de résolution ») : 1 = grille 128³ et 64³ + 75³ particules, 2 = 256³ et 96³ + 112³, 3 = 256³ et 128³ + 149³ ; mêmes modes initiaux à tous les niveaux (coupure au niveau le plus grossier).
• <b>Vitesse initiale de ā fixée par la courbure k̄ = −1</b> (onglet Janus) : la vitesse d'expansion de départ du secteur négatif découle de sa propre équation de Friedmann, avec la même courbure que notre secteur (λ ≈ 1 pour c−/c+ ≤ 0,1, ≈ 10 pour c−/c+ = 10 et ā/a = 0,01).
• <b>Horloge T−/T+</b> (onglet Janus, exploratoire) : le temps propre du secteur négatif s'écoule T−/T+ fois plus vite que le nôtre ; toute la dynamique des masses négatives est accélérée d'autant, sans changer notre expansion. Listes de campagne « horloges T−/T+ » et « indices spectraux n_s » (Janus seulement).
• <b>Janus phénoménologique</b> (onglet Janus) : test de principe issu de l'étude inverse, ce n'est plus le modèle de Petit. Expansion de ΛCDM imposée ; couplage croisé multiplié par s(z), attractif avant z ≈ 3,4 (le secteur négatif joue la matière noire), répulsif ensuite ; conditions initiales de ΛCDM.
Le calcul se fait en tâche de fond au démarrage (le bouton Lecture devient actif à la fin).",
H + "Particle-mesh (PM) solver" + HE + @"
At each step, on the graphics card:
1. <b>Deposit</b> of masses on a 256³ grid with the CIC method (each particle spread over 8 cells), in separate arrays: positive, negative (or dark matter), halo.
2. <b>Poisson</b> by FFT: both densities are placed in the real and imaginary parts of one complex field, transformed once. In Fourier space, φ_k = −4πG ρ_k / k² · exp(−k² ε²), ε = smoothing length. The k = 0 mode is set to zero (mean subtracted).
3. <b>Gradient</b> by centred differences, then CIC interpolation of the force at each particle position.

" + H + "Integration" + HE + @"
Variable-step leapfrog: drift x ← x + v Δt (periodic box), force computation, then kick
   v ← v · exp(−2H Δt) + (g / a³) Δt
The exp(−2H Δt) factor is the exact Hubble drag over one step; the negative sector uses ā and H̄. At large scale, the step is adaptive: Δt = min(dt, 0.1 t_dyn), t_dyn = (4πG ρ_felt)^(−1/2), ρ_felt = max(|ρ̄−|, ρ̄+/κ²) / ā³.

" + H + "Physical units" + HE + @"
The code uses G = 1, a box of 400 units and M+ = 35,000. Correspondences:
• length: box = 200 comoving Mpc (adjustable);
• density: code ρ+ ≡ Ω_b ρ_crit (1 + z_start)³ (Planck, H₀ = 67.7);
• time: t_unit = √(G ρ+_code / (G_SI ρ_b,SI)), giving Gyr and km/s/Mpc;
• mass of an ordinary particle: Ω_b ρ_crit h² V / N+ (5.6·10¹⁰ M☉ for 96³ particles in 200 Mpc);
• wavenumbers in h/Mpc, h = 0.677.

" + H + "Realistic initial conditions" + HE + @"
• Spectrum P(k) = A k^n_s T(k)², Eisenstein & Hu no-wiggle T, n_s = 0.965.
• Normalised by σ₈ (8 Mpc/h sphere), multiplied by D(z_start)², D = ΛCDM linear growth.
• Gaussian field drawn on 128³, cut at the Nyquist wavenumber of the particle lattice.
• Particles displaced from a lattice at second order (2LPT): ψ₁ = −∇∇⁻²δ, ψ₂ = −(3/7)∇φ₂.
• ΛCDM velocities: f H ψ₁ + f₂ H ψ₂. Janus starts at the bounce (H = 0); by default with zero velocities.
• Janus linear theory (two fluids, same equations as the N-body computation): δ̈+ + 2H δ̇+ = 4πG/a³ (ρ+ δ+ − κ² ρ− δ−) and δ̈− + 2H̄ δ̇− = 4πG/ā³ (ρ− δ− − ρ+ δ+ / κ²). It gives Janus's linear growth (shown at the start of each run) and, with 'Janus: initial conditions = linear growing mode', the fastest-growing mode at the start: the same field for both sectors, δ− = r δ+ (r < 0: anticorrelated) and velocities δ̇ = γ δ. 'σ₈ = linear value today' sets the starting amplitude so that linear theory gives σ₈ at the end of the run, as in ΛCDM.
• Negative-mass field: independent (other seed) or anticorrelated (opposite of the baryons').
• <b>Convergence test</b> (Campaign tab, 'resolution levels'): 1 = 128³ grid and 64³ + 75³ particles, 2 = 256³ and 96³ + 112³, 3 = 256³ and 128³ + 149³; same initial modes at every level (cut at the coarsest level).
• <b>Initial rate of ā set by the curvature k̄ = −1</b> (Janus tab): the starting expansion rate of the negative sector follows from its own Friedmann equation, with the same curvature as our sector (λ ≈ 1 for c−/c+ ≤ 0.1, ≈ 10 for c−/c+ = 10 and ā/a = 0.01).
• <b>Clock T−/T+</b> (Janus tab, exploratory): the negative sector's proper time runs T−/T+ times faster than ours; all negative-mass dynamics is sped up accordingly, without changing our expansion. Campaign lists 'clocks T−/T+' and 'spectral indices n_s' (Janus only).
• <b>Phenomenological Janus</b> (Janus tab): proof-of-principle test from the inverse study, no longer Petit's model. ΛCDM expansion imposed; cross coupling multiplied by s(z), attractive before z ≈ 3.4 (the negative sector plays dark matter), repulsive afterwards; ΛCDM initial conditions.
This is computed in the background at start-up (the Play button becomes active when done)."),

        // ------------------------------------------------------------------ 6
        new Section("Mode galaxie", "Galaxy mode",
H + "Dispositif" + HE + @"
Sans expansion, dans une boîte périodique.
• <b>Disque</b> : particules positives, profil exponentiel (rayon, épaisseur, paramètre de Toomre Q réglables), une fraction de gaz.
• <b>Gaz</b> : la vitesse de chaque particule de gaz est ramenée vers la vitesse moyenne locale, sans descendre sous la vitesse du son (refroidissement). Le gaz dense forme des étoiles jeunes ; après leur durée de vie, une partie retourne au gaz.
• <b>Milieu</b> : particules négatives emplissant la boîte hors d'une cavité sphérique centrée sur le disque, avec une dispersion de vitesses.
• <b>Libre / figé</b> : le milieu bouge ou reste immobile (contrôle).
• <b>Témoins</b> : halos de matière noire (NFW, isotherme, sphère uniforme).

" + H + "Mesures" + HE + @"
• fraction d'étoiles confinées ;
• épaisseur du disque et dispersion verticale ;
• modes m = 2 (barre), 3, 4 dans les parties intérieure et extérieure ;
• vitesse circulaire à 10 rayons (courbe de rotation) ;
• masse du milieu dans le disque ;
• position et vitesse de dérive du centre ;
• fraction de gaz et d'étoiles jeunes.

" + H + "Référence" + HE + @"
300 000 étoiles, 480 000 particules de milieu, M+ = 35 000, |M−| = 2,5·10⁶, c−/c+ = 1, dt = 0,02, durée 100. Pour tester le modèle complet de Petit : c−/c+ = 0,1 et |M−| = 2,5·10⁸ (même κ²|M−|).

" + H + "Ce qu'on a trouvé" + HE + @"
Le confinement et la courbe de rotation plate tiennent pour κ = 1 et κ = 0,1. Avec κ = 0,1 et le milieu libre, la galaxie s'auto-accélère (forces non réciproques).",
H + "Setup" + HE + @"
No expansion, in a periodic box.
• <b>Disk</b>: positive particles, exponential profile (radius, thickness, Toomre Q adjustable), a gas fraction.
• <b>Gas</b>: each gas particle's velocity is pulled toward the local mean velocity, without going below the sound speed (cooling). Dense gas forms young stars; after their lifetime, part returns to the gas.
• <b>Medium</b>: negative particles filling the box outside a spherical cavity centred on the disk, with a velocity dispersion.
• <b>Free / frozen</b>: the medium moves or stays still (control).
• <b>Controls</b>: dark-matter haloes (NFW, isothermal, uniform sphere).

" + H + "Measurements" + HE + @"
• fraction of confined stars;
• disk thickness and vertical dispersion;
• modes m = 2 (bar), 3, 4 in the inner and outer parts;
• circular velocity at 10 radii (rotation curve);
• medium mass inside the disk;
• position and drift speed of the centre;
• gas and young-star fractions.

" + H + "Reference" + HE + @"
300,000 stars, 480,000 medium particles, M+ = 35,000, |M−| = 2.5·10⁶, c−/c+ = 1, dt = 0.02, duration 100. To test Petit's full model: c−/c+ = 0.1 and |M−| = 2.5·10⁸ (same κ²|M−|).

" + H + "What we found" + HE + @"
Confinement and the flat rotation curve hold for κ = 1 and κ = 0.1. With κ = 0.1 and a free medium, the galaxy self-accelerates (non-reciprocal forces)."),

        // ------------------------------------------------------------------ 7
        new Section("Mode grande échelle", "Large-scale mode",
H + "Référence (16/10/2026)" + HE + @"
Boîte de 400 unités = 200 Mpc, grille 256³, 96³ particules ordinaires (M+ = 35 000) et 112³ particules de milieu. Janus : c−/c+ = 0,1, |M−| = 1,6·10⁷, facteur d'échelle propre (λ = 1), q₀ = −0,09, σ₈ = 0,3, champ négatif indépendant. ΛCDM : σ₈ = 0,81. Départ z = 5,556, fin aujourd'hui. Modèles : Janus + ΛCDM, 3 graines.

" + H + "Pourquoi σ₈ = 0,3 pour Janus" + HE + @"
σ₈ n'est pas fixé par la théorie de Janus. On le choisit pour que le contraste final des baryons (σ_b ≈ 2 sur des cellules de 6,25 Mpc) égale celui de ΛCDM. On compare ainsi la <i>forme</i> des structures à toile cosmique également développée. En boîte de 400 Mpc, la même égalité demande σ₈ ≈ 0,25.

" + H + "Listes de campagne" + HE + @"
• <b>σ₈ de Janus</b> : plusieurs amplitudes (noms LSS_Janus_s8_0.25…).
• <b>c−/c+</b> : plusieurs κ, la masse négative étant ajustée pour garder κ²|M−| (donc l'expansion) constant.
• <b>Vitesses initiales</b> du secteur négatif.
• <b>Deux champs négatifs</b> : chaque modèle lancé avec un champ indépendant (_ind) et anticorrélé (_anti).
Les listes se combinent (produit cartésien) ; les témoins gardent les valeurs de l'onglet.

" + H + "Taille de boîte" + HE + @"
• 200 Mpc : meilleure résolution (particules de 5,6·10¹⁰ M☉), galaxies traceuses à la densité de BOSS.
• 400 Mpc : 8 fois plus de volume pour les grands vides et les amas massifs (particules de 4,5·10¹¹ M☉).
Changez la taille en Mpc (onglet Univers), pas la taille en unités du code.",
H + "Reference (16/10/2026)" + HE + @"
Box of 400 units = 200 Mpc, 256³ grid, 96³ ordinary particles (M+ = 35,000) and 112³ medium particles. Janus: c−/c+ = 0.1, |M−| = 1.6·10⁷, own scale factor (λ = 1), q₀ = −0.09, σ₈ = 0.3, independent negative field. ΛCDM: σ₈ = 0.81. Start z = 5.556, end today. Models: Janus + ΛCDM, 3 seeds.

" + H + "Why σ₈ = 0.3 for Janus" + HE + @"
σ₈ is not fixed by the Janus theory. It is chosen so that the final baryon contrast (σ_b ≈ 2 on 6.25 Mpc cells) equals ΛCDM's. We thus compare the <i>shape</i> of structures with an equally developed cosmic web. In a 400 Mpc box, the same equality requires σ₈ ≈ 0.25.

" + H + "Campaign lists" + HE + @"
• <b>Janus σ₈</b>: several amplitudes (names LSS_Janus_s8_0.25…).
• <b>c−/c+</b>: several κ, the negative mass being adjusted to keep κ²|M−| (hence expansion) constant.
• <b>Initial rates</b> of the negative sector.
• <b>Both negative fields</b>: each model run with an independent (_ind) and an anticorrelated (_anti) field.
Lists combine (Cartesian product); controls keep the tab values.

" + H + "Box size" + HE + @"
• 200 Mpc: better resolution (5.6·10¹⁰ M☉ particles), tracer galaxies at BOSS density.
• 400 Mpc: 8 times more volume for large voids and massive clusters (4.5·10¹¹ M☉ particles).
Change the size in Mpc (Large-scale tab), not the size in code units."),

        // ------------------------------------------------------------------ 8
        new Section("Mesures et fichiers", "Measurements and files",
H + "Contraste et spectre" + HE + @"
• <b>sigma_b, void_frac_b, max_delta_b</b> : écart-type du contraste des baryons sur 32³, fraction de cellules quasi vides (δ < −0,8), surdensité maximale.
• <b>D2b_l…, r_l…</b> : Δ²(k) et corrélation baryons–milieu par longueur d'onde.
• <b>k_hmpc, Pb_mpch3, Plin_lcdm, Pb_over_lin, Pb_shape</b> : spectre en (Mpc/h)³, spectre linéaire ΛCDM au même z, rapport, et forme (rapport normalisé à k = 0,1 h/Mpc).

" + H + "Vides" + HE + @"
• Catalogue : minima du contraste lissé (64³, δ < −0,5), centre au point le plus éloigné de la matière dans un plateau vide, R_v au contraste cumulé de −0,5, sans recouvrement (distance des centres ≥ R₁ + R₂).
• <b>rv_…</b> : profils empilés en x = r/R_v (15 intervalles de 0,2 jusqu'à 3), crête (maximum de δ pour x ≥ 1) par tiers de taille.
• <b>rvb5_10, rvb10_20, rvb20_40</b> : mêmes profils par tranche fixe de R_v en Mpc/h, comme les relevés.
• <b>vsf_…</b> : fonction de taille, dn/dln R en (h/Mpc)³.

" + H + "Lentille des vides" + HE + @"
• Source vue par la lumière = celle qui accélère les positives : δ_eff = (Ω_b/Ω_m)(δ_ord + w δ_mil), w = −κ²|M−|/M+ pour Janus, 5,33 pour ΛCDM.
• Projection exacte du profil empilé, puis ΔΣ(R) = Σ̄(<R) − Σ(R), en h M☉/pc².
• <b>lens_…</b> et <b>…_lens_dsigma_…</b>.

" + H + "Galaxies, vides de galaxies, amas" + HE + @"
• <b>gal_…</b> : groupes friends-of-friends (liaison 0,2 × séparation moyenne, ≥ 20 particules), les plus massifs gardés jusqu'à 3·10⁻⁴ (h/Mpc)³ (densité de BOSS).
• <b>gv_…</b> : vides sphériques dans ces galaxies (seuil −0,8), fonction de taille et profils par tranche 10–20, 20–40, 40–80 Mpc/h.
• <b>cl_…</b> : fonction de masse des amas, M200m (densité moyenne = 200 fois la moyenne de matière). Masse effective = celle qui attire la matière (pour Janus, un déficit de masse négative compte comme une masse en plus) ; masse ordinaire dans le même rayon. <b>cl_fb_…</b> : fraction de baryons δM_ord / δM_eff (fond retiré) des halos ≥ 10¹³ h⁻¹ M☉, à R200m et à 1, 2, 3 et 5 Mpc (ΛCDM ≈ Ω_b/Ω_m ≈ 0,16 ; amas observés : gaz + étoiles ≈ 0,15). <b>cl_fbproj_…</b> : la même fraction en projection, dans un cylindre de 1 ou 2 Mpc de rayon et ±50 Mpc de profondeur, comme le voit une lentille (ce qui est devant et derrière l'amas compte). <b>cl_negprof_…</b> : densité du milieu (masses négatives pour Janus, matière noire pour ΛCDM) autour des halos ≥ 10¹² h⁻¹ M☉, rapportée à sa moyenne, dans des coquilles de 0–1, 1–2, 2–3 et 3–5 Mpc.
Ces mesures sont faites à la mesure finale seulement.

" + H + "Fichiers" + HE + @"
• <b>janus_…_seedN.csv</b> : toutes les mesures à chaque instant d'un run.
• <b>campagne_…_runs.csv</b> : paramètres et valeurs finales de chaque run.
• <b>campagne_…_synthese.csv</b> : moyenne et écart-type entre graines (colonnes _final_mean, _final_std).",
H + "Contrast and spectrum" + HE + @"
• <b>sigma_b, void_frac_b, max_delta_b</b>: standard deviation of the baryon contrast on 32³, fraction of nearly empty cells (δ < −0.8), maximum overdensity.
• <b>D2b_l…, r_l…</b>: Δ²(k) and baryon–medium correlation by wavelength.
• <b>k_hmpc, Pb_mpch3, Plin_lcdm, Pb_over_lin, Pb_shape</b>: spectrum in (Mpc/h)³, linear ΛCDM spectrum at the same z, ratio, and shape (ratio normalised at k = 0.1 h/Mpc).

" + H + "Voids" + HE + @"
• Catalogue: minima of the smoothed contrast (64³, δ < −0.5), centre at the point farthest from matter in an empty plateau, R_v at cumulative contrast −0.5, no overlap (centre distance ≥ R₁ + R₂).
• <b>rv_…</b>: stacked profiles in x = r/R_v (15 bins of 0.2 up to 3), ridge (maximum of δ for x ≥ 1) by size tercile.
• <b>rvb5_10, rvb10_20, rvb20_40</b>: same profiles by fixed R_v bin in Mpc/h, as in surveys.
• <b>vsf_…</b>: size function, dn/dln R in (h/Mpc)³.

" + H + "Void lensing" + HE + @"
• Source seen by light = the one accelerating positive masses: δ_eff = (Ω_b/Ω_m)(δ_ord + w δ_med), w = −κ²|M−|/M+ for Janus, 5.33 for ΛCDM.
• Exact projection of the stacked profile, then ΔΣ(R) = Σ̄(<R) − Σ(R), in h M☉/pc².
• <b>lens_…</b> and <b>…_lens_dsigma_…</b>.

" + H + "Galaxies, galaxy voids, clusters" + HE + @"
• <b>gal_…</b>: friends-of-friends groups (linking 0.2 × mean separation, ≥ 20 particles), the most massive kept up to 3·10⁻⁴ (h/Mpc)³ (BOSS density).
• <b>gv_…</b>: spherical voids in these galaxies (threshold −0.8), size function and profiles in bins 10–20, 20–40, 40–80 Mpc/h.
• <b>cl_…</b>: cluster mass function, M200m (mean density = 200 times the mean matter density). Effective mass = the one attracting matter (for Janus, a deficit of negative mass counts as extra mass); ordinary mass within the same radius. <b>cl_fb_…</b>: baryon fraction δM_ord / δM_eff (background removed) of halos ≥ 10¹³ h⁻¹ M☉, at R200m and at 1, 2, 3 and 5 Mpc (ΛCDM ≈ Ω_b/Ω_m ≈ 0.16; observed clusters: gas + stars ≈ 0.15). <b>cl_fbproj_…</b>: the same fraction in projection, in a cylinder of 1 or 2 Mpc radius and ±50 Mpc depth, as a lens sees it (what lies in front of and behind the cluster counts). <b>cl_negprof_…</b>: density of the medium (negative masses for Janus, dark matter for ΛCDM) around halos ≥ 10¹² h⁻¹ M☉, relative to its mean, in shells of 0–1, 1–2, 2–3 and 3–5 Mpc.
These are computed at the final measurement only.

" + H + "Files" + HE + @"
• <b>janus_…_seedN.csv</b>: all measurements at each time of a run.
• <b>campagne_…_runs.csv</b>: parameters and final values of each run.
• <b>campagne_…_synthese.csv</b>: mean and standard deviation across seeds (columns _final_mean, _final_std)."),

        // ------------------------------------------------------------------ web
        new Section("Version web", "Web version",
H + "Même logiciel, version allégée" + HE + @"
La version web est compilée à partir du même code que l'application de bureau. Elle tourne dans le navigateur grâce à WebGPU (Chrome ou Edge récents ; Firefox et Safari selon la version), sans rien installer.

" + H + "Différences" + HE + @"
• <b>Référence allégée</b> : grille 128³ et moins de particules (64³ + 75³ en grande échelle, 150 000 + 240 000 en galaxie), car la mémoire GPU d'un navigateur est limitée. Les résultats sont moins précis que ceux de l'article, mais montrent les mêmes tendances.
• <b>Fichiers</b> : les CSV restent dans le navigateur ; le bouton « Télécharger les CSV » les propose en téléchargement. Les réglages sauvés sont gardés dans le stockage du navigateur.
• <b>Conditions initiales</b> : calculées sans fil de calcul séparé ; la page se fige quelques secondes au lancement d'un run.
• <b>Vidéo</b> : indisponible (utilisez l'enregistreur d'écran du système).
Pour les campagnes de l'étude, utilisez l'application de bureau ou compilez le projet vous-même (code sur GitHub).",
H + "Same software, lighter version" + HE + @"
The web version is built from the same code as the desktop application. It runs in the browser thanks to WebGPU (recent Chrome or Edge; Firefox and Safari depending on version), with nothing to install.

" + H + "Differences" + HE + @"
• <b>Lighter reference</b>: 128³ grid and fewer particles (64³ + 75³ at large scale, 150,000 + 240,000 in galaxy mode), because a browser's GPU memory is limited. Results are less precise than in the article, but show the same trends.
• <b>Files</b>: CSV files stay in the browser; the « Download CSV » button offers them for download. Saved settings are kept in the browser storage.
• <b>Initial conditions</b>: computed without a separate thread; the page freezes for a few seconds when a run starts.
• <b>Video</b>: unavailable (use your system's screen recorder).
For the study's campaigns, use the desktop application or build the project yourself (code on GitHub)."),

        // ------------------------------------------------------------------ 9
        new Section("Résultats et limites", "Results and limits",
H + "Principaux résultats (16/10/2026)" + HE + @"
À contraste égal avec ΛCDM :
• spectre de Janus presque plat aux petites échelles (montée ×1,2 contre ×6,9 entre k = 0,1 et 1,3 h/Mpc) et excès ×2 à k = 0,1 ;
• déficit d'objets massifs : ÷15 au-dessus de 9·10¹² M☉ de matière ordinaire, ÷2 au-dessus de 10¹² M☉ ;
• vides plus vides, parois 1,7 à 3 fois plus hautes, lentille des petits vides environ 2 fois plus faible ;
• masse négative concentrée au centre des grands vides ;
• en mode galaxie, confinement et courbe plate conservés pour κ = 0,1, avec une auto-accélération.
Face aux observations (eROSITA, KiDS), ΛCDM reproduit mieux l'abondance des amas et la puissance aux petites échelles.

" + H + "Limites" + HE + @"
• Gravité newtonienne, sans relativité générale ni hydrodynamique à grande échelle.
• Paramètres non fixés par la théorie : κ, |M−|, λ, σ₈, champ négatif initial ; spectre initial d'origine ΛCDM.
• Rapport ā/a final de 3,6, loin du 1/100 annoncé par Petit.
• Résolution : cellules de 0,78 Mpc, particules de 5,6·10¹⁰ M☉ ou plus ; galaxies individuelles non résolues.
• Comparaisons aux observations qualitatives ou d'ordre de grandeur (pas de cône de lumière ni de masque de relevé).

" + H + "Références principales" + HE + @"
Petit & d'Agostini 2014, Ap&SS (DOI 10.1007/s10509-014-2106-5) ; D'Agostini & Petit 2018, Ap&SS 363, 139 ; Eisenstein & Hu 1998, ApJ 496, 605 ; Planck 2018 VI ; Hamaus, Sutter & Wandelt 2014, PRL 112, 251302 ; Ghirardini et al. 2024 (eROSITA) ; Wright et al. 2025 (KiDS-Legacy) ; Davis et al. 1985, ApJ 292, 371 (friends-of-friends).",
H + "Main results (16/10/2026)" + HE + @"
At equal contrast with ΛCDM:
• Janus spectrum nearly flat at small scales (rise ×1.2 versus ×6.9 between k = 0.1 and 1.3 h/Mpc) and a ×2 excess at k = 0.1;
• deficit of massive objects: ÷15 above 9·10¹² M☉ of ordinary matter, ÷2 above 10¹² M☉;
• emptier voids, walls 1.7 to 3 times higher, small-void lensing about 2 times weaker;
• negative mass concentrated at the centre of large voids;
• in galaxy mode, confinement and flat curve preserved for κ = 0.1, with self-acceleration.
Against observations (eROSITA, KiDS), ΛCDM better reproduces cluster abundance and small-scale power.

" + H + "Limits" + HE + @"
• Newtonian gravity, no general relativity or hydrodynamics at large scale.
• Parameters not fixed by the theory: κ, |M−|, λ, σ₈, initial negative field; initial spectrum of ΛCDM origin.
• Final ā/a ratio of 3.6, far from the 1/100 stated by Petit.
• Resolution: 0.78 Mpc cells, particles of 5.6·10¹⁰ M☉ or more; individual galaxies not resolved.
• Comparisons with observations are qualitative or order-of-magnitude (no light cone or survey mask).

" + H + "Main references" + HE + @"
Petit & d'Agostini 2014, Ap&SS (DOI 10.1007/s10509-014-2106-5); D'Agostini & Petit 2018, Ap&SS 363, 139; Eisenstein & Hu 1998, ApJ 496, 605; Planck 2018 VI; Hamaus, Sutter & Wandelt 2014, PRL 112, 251302; Ghirardini et al. 2024 (eROSITA); Wright et al. 2025 (KiDS-Legacy); Davis et al. 1985, ApJ 292, 371 (friends-of-friends)."),
    };
}