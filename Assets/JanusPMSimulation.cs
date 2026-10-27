using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

// Simulation Janus en boîte périodique, masses positives ET négatives libres.
// Aucun champ de confinement n'est imposé : tout vient des interactions.
// Valeur d'énumération historique : masquée par l'interface quand « simplifiedModels » est vrai
[System.AttributeUsage(System.AttributeTargets.Field)]
public class UILegacyAttribute : System.Attribute { }

public class JanusPMSimulation : MonoBehaviour
{
    public enum InitMode
    {
        [InspectorName("Galaxie dans une cavité"), EnName("Galaxy in a cavity")] GalaxyInCavity,   // disque positif dans une cavité d'un milieu négatif libre
        [InspectorName("Structure à grande échelle"), EnName("Large-scale structure")] LargeScaleStructure   // boîte uniforme : formation des structures à grande échelle (toile cosmique)
    }

    [Header("Scénario")]
    [UILabel("Mode de simulation", En = "Simulation mode")]
    [Tooltip("GalaxyInCavity : un disque galactique dans une cavité du milieu de masses négatives (ou dans un halo de matière noire). LargeScaleStructure : boîte uniforme, formation des grandes structures (toile cosmique).")]
    [TooltipEn("Galaxy in a cavity: a galactic disk inside a cavity of the negative-mass medium (or inside a dark-matter halo). Large-scale structure: uniform box, formation of large structures (cosmic web).")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public InitMode mode = InitMode.GalaxyInCavity;

    [UIPanel("Scénario", En = "Scenario", Single = true)]
    [UILabel("Modèles : Janus et ΛCDM seulement", En = "Models: Janus and ΛCDM only")]
    [Tooltip("Oui : l'interface ne propose plus que Janus et le modèle standard (ΛCDM en grande échelle, halo NFW en mode galaxie). " +
             "Les anciens témoins (matière noire de même masse, positives seules, matière noire réaliste, halos isotherme et sphère, " +
             "versions figées) sont masqués et ignorés en campagne. Non : tous les modèles historiques redeviennent disponibles.")]
    [TooltipEn("Yes: the interface only offers Janus and the standard model (ΛCDM at large scale, NFW halo in galaxy mode). The old controls (same-mass dark matter, positive only, realistic dark matter, isothermal and sphere halos, frozen versions) are hidden and ignored in campaigns. No: all historical models become available again.")]
    public bool simplifiedModels = true;

    public enum LssReference
    {
        [InspectorName("Janus selon Petit (c−/c+ = 10, ā/a = 0,01)"), EnName("Janus per Petit (c−/c+ = 10, ā/a = 0.01)")] Petit,
        [InspectorName("Meilleur spectre (départ z = 20, q₀ = −0,025)"), EnName("Best spectrum (start z = 20, q₀ = −0.025)")] MeilleurSpectre,
        [InspectorName("Historique du 15/10 (c−/c+ = 0,1, ā/a = 1)"), EnName("Historical 15/10 (c−/c+ = 0.1, ā/a = 1)")] Historique
    }
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    [UILabel("Référence grande échelle", En = "Large-scale reference")]
    [ShowIf("IsLss")]
    [Tooltip("Configuration appliquée par le bouton Référence en grande échelle (campagnes du 21 au 24/10/2026). " +
             "Janus selon Petit : valeurs publiées les plus probables (c−/c+ = 10, ā/a = 1/100), q₀ = −0,09 ajusté sur les supernovae, " +
             "σ₈ Janus = 0,04 qui donne le contraste de ΛCDM (σ_b ≈ 0,85) — le choix le plus fidèle au modèle. " +
             "Meilleur spectre : départ à z = 20 (q₀ = −0,025, hors de l'ajustement des supernovae), σ₈ Janus = 0,1 : la forme du " +
             "spectre la plus proche de ΛCDM. Historique : configuration du 15/10 (contraste trop fort, σ_b ≈ 1,1). " +
             "Aucune ne reproduit les amas observés.")]
    [TooltipEn("Configuration applied by the Reference button at large scale (campaigns of 21–24/10/2026). Janus per Petit: most likely published values (c−/c+ = 10, ā/a = 1/100), q₀ = −0.09 fitted to supernovae, Janus σ₈ = 0.04 giving the ΛCDM contrast (σ_b ≈ 0.85) — the choice most faithful to the model. Best spectrum: start at z = 20 (q₀ = −0.025, outside the supernova fit), Janus σ₈ = 0.1: the spectrum shape closest to ΛCDM. Historical: configuration of 15/10 (contrast too strong, σ_b ≈ 1.1). None reproduces the observed clusters.")]
    public LssReference lssReference = LssReference.Petit;
    [UILabel("Graine aléatoire (seed)", En = "Random seed")]
    [Tooltip("Même seed = mêmes conditions initiales : le résultat est exactement reproductible. Ignorée en campagne (liste de seeds).")]
    [ShowIf("!batchMode")]
    [TooltipEn("Same seed = same initial conditions: the result is exactly reproducible. Ignored in campaign mode (list of seeds).")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public int seed = 1;

    [Header("Boîte et grille")]
    [UILabel("Taille de la boîte (unités du code)", En = "Box size (code units)")]
    [Tooltip("Côté de la boîte périodique, en unités de longueur du code (400 conseillé). Ce n'est PAS la taille physique : " +
             "en mode grande échelle, la taille en Mpc se règle dans l'onglet Univers, « Taille de la boîte (Mpc comobiles) ».")]
    [TooltipEn("Side of the periodic box, in code length units (400 advised). This is NOT the physical size: in large-scale mode, the size in Mpc is set in the Universe tab, 'Box size (comoving Mpc)'.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public float boxSize = 400f;
    [Tooltip("Puissance de 2 entre 16 et 256 (128 recommandé)")]
    [UILabel("Grille de calcul (N³)", En = "Computation grid (N³)")]
    [TooltipEn("Power of 2 between 16 and 256 (128 recommended).")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public int gridSize = 128;
    [Tooltip("Lissage gaussien de la force (adoucissement)")]
    [UILabel("Lissage de la force", En = "Force smoothing")]
    [Live]
    [TooltipEn("Gaussian smoothing of the force (softening).")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public float smoothingLength = 1.5f;

    [Header("Particules")]
    [UILabel("Particules ordinaires", En = "Ordinary particles")]
    [Tooltip("Nombre de particules de matière ordinaire (étoiles et gaz). Plus = plus précis, plus lent.")]
    [TooltipEn("Number of ordinary-matter particles (stars and gas). More = more accurate, slower.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public int positiveCount = 60000;
    [UILabel("Particules du milieu", En = "Medium particles")]
    [Tooltip("Nombre de particules du milieu : masses négatives (Janus), halo ou matière noire (témoins).")]
    [TooltipEn("Number of medium particles: negative masses (Janus), halo or dark matter (controls).")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public int negativeCount = 240000;
    [UILabel("Masse ordinaire totale", En = "Total ordinary mass")]
    [Tooltip("Masse totale de la matière ordinaire.")]
    [TooltipEn("Total mass of ordinary matter.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public float positiveTotalMass = 20000f;
    [Tooltip("|M−| total. Doit dépasser M+ pour qu'un confinement existe.")]
    [UILabel("Masse du milieu |M−|", En = "Medium mass |M−|")]
    [TooltipEn("Total |M−| (absolute value; the sign comes from the model). Must exceed M+ for confinement to exist.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float negativeTotalMass = 1500000f;

    [Header("Physique")]
    [UILabel("Constante de gravitation G", En = "Gravitational constant G")]
    [Live]
    [Tooltip("G = 1 fixe le système d'unités du code. La modifier revient à changer l'échelle de temps.")]
    [TooltipEn("G = 1 sets the code unit system. Changing it amounts to changing the time scale.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public float G = 1f;
    [UILabel("Pas de temps dt", En = "Time step dt")]
    [Live]
    [Tooltip("Pas d'intégration. Plus petit = plus précis mais plus lent.")]
    [TooltipEn("Integration step. Smaller = more accurate but slower.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public float dt = 0.02f;
    [UILabel("Pas de calcul par image", En = "Steps per frame")]
    [Live]
    [Tooltip("Nombre de pas calculés à chaque image affichée (hors campagne). Plus = plus rapide, image moins fluide.")]
    [ShowIf("!batchMode")]
    [TooltipEn("Number of steps computed per displayed frame (outside campaigns). More = faster, less smooth display.")]
    [UIPanel("Simulation", En = "Simulation", Single = true)]
    public int stepsPerFrame = 2;
    [Tooltip("Expérience témoin : les masses négatives restent figées à leur position initiale. " +
             "Même force moyenne dans la cavité, mais le milieu ne réagit plus au disque.")]
    [UILabel("Milieu figé (témoin)", En = "Frozen medium (control)")]
    [ShowIf("!simplifiedModels & IsGalaxy & !batchMode")]
    [TooltipEn("Control experiment: the negative masses stay frozen at their initial positions. Same mean force inside the cavity, but the medium no longer reacts to the disk.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool freezeNegatives = false;

    [Header("Hypothèse matière noire")]
    [Tooltip("Remplace le milieu de masses négatives par un halo sphérique de matière noire " +
             "(particules de masse positive, libres). Le halo est réglé pour exercer sur le disque " +
             "la même force moyenne que la cavité Janus : même courbe de rotation au départ. " +
             "negativeCount devient le nombre de particules du halo.")]
    [UILabel("Matière noire au lieu de Janus", En = "Dark matter instead of Janus")]
    [ShowIf("IsGalaxy & !batchMode")]
    [TooltipEn("Replaces the negative-mass medium with a spherical dark-matter halo (free positive-mass particles), tuned to exert the same mean force on the disk as the Janus cavity: same initial rotation curve. 'Medium particles' becomes the number of halo particles.")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public bool darkMatterHalo = false;

    public enum HaloProfile
    {
        [InspectorName("Sphère homogène"), EnName("Uniform sphere"), UILegacy] UniformSphere,     // sphère homogène (essais précédents), copie exacte de la force Janus
        [InspectorName("NFW"), EnName("NFW")] NFW,               // Navarro-Frenk-White : profil issu des simulations cosmologiques
        [InspectorName("Isotherme à cœur"), EnName("Cored isothermal"), UILegacy] CoredIsothermal    // isotherme à cœur : profil ajusté sur les courbes de rotation observées
    }
    [Tooltip("UniformSphere = sphère homogène (essais précédents). NFW = profil cosmologique. " +
             "CoredIsothermal = profil isotherme à cœur, ajusté sur les observations.")]
    [UILabel("Profil du halo", En = "Halo profile")]
    [ShowIf("IsGalaxy & darkMatterHalo & !batchMode")]
    [TooltipEn("Uniform sphere = homogeneous sphere (earlier tests). NFW = cosmological profile. Cored isothermal = profile fitted to observed rotation curves.")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    public HaloProfile dmProfile = HaloProfile.NFW;
    [Tooltip("UniformSphere : rayon de la sphère (doit dépasser le rayon du disque)")]
    [UILabel("Rayon de la sphère", En = "Sphere radius")]
    [ShowIf("!simplifiedModels & IsGalaxy & IsUniformSphere | !simplifiedModels & IsGalaxy & batchMode")]
    [TooltipEn("Uniform sphere: sphere radius (must exceed the disk radius).")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    public float dmHaloRadius = 100f;
    [Tooltip("NFW : rayon d'échelle rs. CoredIsothermal : rayon de cœur rc. " +
             "0 = valeur automatique proche de la Voie lactée (NFW : rs ≈ 6,6 × longueur d'échelle du disque ; " +
             "isotherme : rc ≈ 2 × longueur d'échelle du disque)")]
    [UILabel("Rayon d'échelle (0 = auto)", En = "Scale radius (0 = auto)")]
    [ShowIf("IsGalaxy & darkMatterHalo & !IsUniformSphere | IsGalaxy & batchMode")]
    [TooltipEn("NFW: scale radius rs. Cored isothermal: core radius rc. 0 = automatic value close to the Milky Way (NFW: rs ≈ 6.6 × disk scale length; isothermal: rc ≈ 2 × disk scale length).")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    public float dmScaleRadius = 0f;
    [Tooltip("NFW / isotherme : rayon de troncature du halo (coupure exponentielle douce au-delà). " +
             "Doit rester sous la demi-boîte pour limiter les interactions avec les images périodiques.")]
    [UILabel("Rayon de troncature", En = "Truncation radius")]
    [ShowIf("IsGalaxy & darkMatterHalo & !IsUniformSphere | IsGalaxy & batchMode")]
    [TooltipEn("NFW / isothermal: halo truncation radius (smooth exponential cut-off beyond). Must stay below half the box to limit interactions with periodic images.")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    public float dmTruncationRadius = 150f;

    public enum LssModel
    {
        [InspectorName("Janus"), EnName("Janus")] Janus,            // masses positives + masses négatives (lois Janus)
        [InspectorName("Matière noire (même masse)"), EnName("Dark matter (same mass)"), UILegacy] MatiereNoire,     // témoin modèle standard : la même masse, mais positive (matière noire attractive)
        [InspectorName("Positives seules"), EnName("Positive only"), UILegacy] PositivesSeules,  // témoin : masses positives seules (le milieu devient des traceurs sans masse)
        [InspectorName("Matière noire réaliste"), EnName("Realistic dark matter"), UILegacy] MatiereNoireRealiste,  // témoin réaliste : matière noire = lssRealisticRatio × masse ordinaire (≈ 5 dans l'univers)
        [InspectorName("ΛCDM (modèle standard)"), EnName("ΛCDM (standard model)")] LambdaCDM   // modèle standard complet : matière noire (Ω_m/Ω_b − 1) × M+ et constante cosmologique
    }
    [Header("Structure à grande échelle (mode LargeScaleStructure)")]
    [Tooltip("Janus = positives + négatives. MatiereNoire = la masse négative remplacée par la même masse " +
             "positive (matière noire). PositivesSeules = la matière ordinaire seule. " +
             "MatiereNoireRealiste = matière noire de masse lssRealisticRatio × la masse ordinaire.")]
    [UILabel("Modèle (grande échelle)", En = "Model (large scale)")]
    [ShowIf("IsLss & !batchMode")]
    [TooltipEn("Janus = positive + negative masses. Dark matter (same mass) = the negative mass replaced by the same positive mass. Positive only = ordinary matter alone. Realistic dark matter = dark-matter mass equal to the ratio × the ordinary mass.")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public LssModel lssModel = LssModel.Janus;
    [Tooltip("Agitation initiale du milieu (négatives ou matière noire). Faible = départ froid, comme en cosmologie")]
    [UILabel("Agitation initiale du milieu", En = "Initial medium velocity spread")]
    [TooltipEn("Initial velocity spread of the medium (negative masses or dark matter). Low = cold start, as in cosmology.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    [ShowIf("IsLss")]
    public float lssMediumSigma = 0.5f;
    [Tooltip("MatiereNoireRealiste : masse de matière noire / masse ordinaire (≈ 5,4 dans le modèle standard)")]
    [UILabel("Rapport matière noire / ordinaire", En = "Dark / ordinary matter ratio")]
    [TooltipEn("Realistic dark matter: dark-matter mass / ordinary mass (≈ 5.4 in the standard model).")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    [ShowIf("!simplifiedModels & IsLss")]
    public float lssRealisticRatio = 5f;

    public enum ExpansionLaw
    {
        [InspectorName("Aucune (boîte statique)"), EnName("None (static box)")] Aucune,             // boîte statique
        [InspectorName("Einstein-de Sitter"), EnName("Einstein–de Sitter")] EinsteinDeSitter,   // modèle standard dominé par la matière : a ∝ t^(2/3), expansion qui ralentit
        [InspectorName("Janus (Petit)"), EnName("Janus (Petit)")] JanusPetit,         // secteur positif de Janus : a = ch²(v), t ∝ v + sh(2v)/2, expansion accélérée
        [InspectorName("ΛCDM (matière + constante cosmologique)"), EnName("ΛCDM (matter + cosmological constant)")] LambdaCDM   // a³ ∝ sh²(3/2 H_Λ t)
    }
    [Tooltip("Expansion auto-cohérente (Friedmann) : le taux d'expansion découle des densités simulées. " +
             "Janus suit sa loi a = ch²(v) avec s = √(3 / 8πG(|ρ−| − ρ+)) ; les témoins suivent Einstein-de Sitter " +
             "avec H₀ = √(8πG ρ_total / 3). expansionLaw et expansionDuration sont alors ignorés, " +
             "et chaque run s'arrête quand a atteint expansionFactorEnd.")]
    [UILabel("Expansion auto-cohérente", En = "Self-consistent expansion")]
    [TooltipEn("Self-consistent (Friedmann) expansion: the expansion rate follows from the simulated densities. Janus follows its law a = ch²(v) with s = √(3 / 8πG(κ²|ρ−| − ρ+)); the controls follow Einstein–de Sitter with H₀ = √(8πG ρ_total / 3). The imposed law and duration are then ignored, and each run stops when a reaches the final scale factor.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public bool expansionSelfConsistent = true;
    [Tooltip("Loi d'expansion appliquée à toutes les particules (coordonnées comobiles). " +
             "Même loi pour tous les modèles = comparaison à expansion identique.")]
    [UILabel("Loi d'expansion imposée", En = "Imposed expansion law")]
    [ShowIf("!expansionSelfConsistent")]
    [TooltipEn("Expansion law applied to all particles (comoving coordinates). Same law for all models = comparison at identical expansion.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public ExpansionLaw expansionLaw = ExpansionLaw.Aucune;
    [Tooltip("Facteur d'échelle atteint à t = expansionDuration (a = 1 au départ)")]
    [UILabel("Facteur d'échelle final a", En = "Final scale factor a")]
    [ShowIf("!UseQ0")]
    [TooltipEn("Scale factor reached at the end (a = 1 at the start).")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float expansionFactorEnd = 5f;
    [Tooltip("Durée sur laquelle a passe de 1 à expansionFactorEnd (ignorée en expansion auto-cohérente)")]
    [UILabel("Durée de l'expansion", En = "Expansion duration")]
    [ShowIf("!expansionSelfConsistent")]
    [TooltipEn("Time over which a goes from 1 to the final scale factor (ignored with self-consistent expansion).")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float expansionDuration = 100f;
    [Tooltip("Expansion auto-cohérente : nombre de mesures, régulièrement espacées en a entre 1 et expansionFactorEnd")]
    [UILabel("Nombre de mesures", En = "Number of measurements")]
    [ShowIf("expansionSelfConsistent")]
    [TooltipEn("Self-consistent expansion: number of measurements, evenly spaced in a between 1 and the final scale factor.")]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public int lssSamples = 20;
    [Tooltip("Expansion auto-cohérente : moyennes de campagne calculées pour a ≥ cette valeur")]
    [UILabel("Moyennes de campagne dès a =", En = "Campaign averages from a =")]
    [ShowIf("expansionSelfConsistent & batchMode")]
    [TooltipEn("Self-consistent expansion: campaign averages computed for a ≥ this value.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float lssSummaryFromA = 3f;
    [Tooltip("Correspondance indicative en unités réelles : décalage vers le rouge z au départ (a = 1). " +
             "La densité de matière ordinaire simulée est identifiée à Ω_b ρ_crit (1 + z)³ (Planck : Ω_b = 0,049, H₀ = 67,7). " +
             "Avec z = 4 et expansionFactorEnd = 5, la fin du run correspond à aujourd'hui.")]
    [UILabel("Redshift de départ z", En = "Starting redshift z")]
    [ShowIf("!UseQ0")]
    [TooltipEn("Indicative conversion to real units: redshift z at the start (a = 1). The simulated ordinary-matter density is identified with Ω_b ρ_crit (1 + z)³ (Planck: Ω_b = 0.049, H₀ = 67.7). With z = 4 and a final = 5, the end of the run is today.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float lssStartRedshift = 4f;
    [UILabel("Taille de la boîte (Mpc comobiles)", En = "Box size (comoving Mpc)")]
    [ShowIf("IsLss")]
    [Tooltip("Côté de la boîte en Mpc comobiles (valeur d'aujourd'hui). Avec le spectre réaliste, c'est une vraie échelle " +
             "physique : le spectre initial y est exprimé en Mpc, donc les rayons de vides en Mpc sont comparables aux " +
             "observations. Avec le tirage aléatoire, ce n'est qu'une convention (la taille des structures dépend alors de la " +
             "résolution). Espacement des particules ordinaires ≈ taille / (nombre)^(1/3) : il doit rester nettement plus " +
             "petit que les vides étudiés (10–40 Mpc).")]
    [TooltipEn("Box side in comoving Mpc (today's value). With the realistic spectrum this is a true physical scale: the initial spectrum is expressed in Mpc, so void radii in Mpc are comparable with observations. With random initial conditions it is only a convention (structure sizes then depend on resolution). Ordinary-particle spacing ≈ size / (count)^(1/3): it must stay well below the voids studied (10–40 Mpc).")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float lssBoxMpc = 400f;

    public enum LssInitial
    {
        [InspectorName("Tirage aléatoire (bruit blanc)"), EnName("Random (white noise)")] Aleatoire,
        [InspectorName("Spectre réaliste (Zel'dovich)"), EnName("Realistic spectrum (Zel'dovich)")] SpectreRealiste
    }
    [UILabel("Conditions initiales", En = "Initial conditions")]
    [ShowIf("IsLss")]
    [Tooltip("Tirage aléatoire : particules placées au hasard (bruit blanc) ; la taille des structures dépend de la résolution. " +
             "Spectre réaliste : méthode standard des simulations cosmologiques. Les particules partent d'un réseau régulier, " +
             "déplacées selon l'approximation de Zel'dovich par un champ gaussien de spectre P(k) ∝ k^n_s T²(k) " +
             "(fonction de transfert d'Eisenstein & Hu, Planck : Ω_m = 0,31, Ω_b = 0,049, h = 0,677), " +
             "normalisé par σ₈ ramené au redshift de départ. Même champ (même seed) pour tous les modèles : seule la physique " +
             "de l'évolution diffère. Vitesses initiales : f·H·ψ pour Einstein-de Sitter, nulles pour Janus (départ au rebond, H = 0).")]
    [TooltipEn("Random: particles placed at random (white noise); structure sizes depend on resolution. Realistic spectrum: standard method of cosmological simulations. Particles start on a regular lattice, displaced with the Zel'dovich approximation by a Gaussian field with spectrum P(k) ∝ k^n_s T²(k) (Eisenstein & Hu transfer function, Planck: Ω_m = 0.31, Ω_b = 0.049, h = 0.677), normalised by σ₈ scaled back to the starting redshift. Same field (same seed) for every model: only the physics of the evolution differs. Initial velocities: f·H·ψ for Einstein–de Sitter, zero for Janus (start at the bounce, H = 0).")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public LssInitial lssInitialConditions = LssInitial.Aleatoire;
    [UILabel("σ₈ des témoins (ΛCDM, matière noire)", En = "σ₈ of the controls (ΛCDM, dark matter)")]
    [ShowIf("IsLss & IsLss & IsRealisticIC")]
    [Range(0.05f, 3f)]
    [Tooltip("Amplitude des fluctuations initiales : σ₈ qu'aurait le champ aujourd'hui s'il croissait comme dans le modèle " +
             "standard (Planck : 0,81). L'amplitude réellement imposée au départ est σ₈ × D(z_départ), D étant le facteur de " +
             "croissance ΛCDM ; elle est affichée dans le diagnostic. Petit ne fixe pas cette amplitude pour Janus : " +
             "c'est un paramètre libre à faire varier.")]
    [TooltipEn("Amplitude of the initial fluctuations: the σ₈ the field would have today if it grew as in the standard model (Planck: 0.81). The amplitude actually imposed at the start is σ₈ × D(z_start), D being the ΛCDM growth factor; it is shown in the diagnostics. Petit does not fix this amplitude for Janus: it is a free parameter to vary.")]
    [UIPanel("ΛCDM", En = "ΛCDM", Single = true)]
    public float lssSigma8 = 0.81f;
    [UILabel("σ₈ des modèles Janus", En = "σ₈ of the Janus models")]
    [ShowIf("IsLss & IsLss & IsRealisticIC & IsJanusLss")]
    [Range(0.05f, 3f)]
    [Tooltip("Amplitude initiale propre aux modèles Janus (le σ₈ ci-dessus sert aux témoins ΛCDM et matière noire). " +
             "0,3 donne à Janus le même contraste final que ΛCDM avec σ₈ = 0,81 (campagne du 15/10/2026, σ_b ≈ 2,0 " +
             "sur des cellules de 6 Mpc). Petit ne fixe pas cette amplitude.")]
    [TooltipEn("Initial amplitude specific to the Janus models (the σ₈ above is for the ΛCDM and dark-matter controls). 0.3 gives Janus the same final contrast as ΛCDM with σ₈ = 0.81 (campaign of 15/10/2026, σ_b ≈ 2.0 on 6 Mpc cells). Petit does not fix this amplitude.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float lssSigma8Janus = 0.3f;
    [UILabel("Indice spectral n_s", En = "Spectral index n_s")]
    [ShowIf("IsLss & IsRealisticIC")]
    [Range(0.8f, 1.2f)]
    [Tooltip("Pente du spectre primordial P(k) ∝ k^n_s (Planck : 0,965 ; 1 = Harrison-Zel'dovich).")]
    [TooltipEn("Slope of the primordial spectrum P(k) ∝ k^n_s (Planck: 0.965; 1 = Harrison–Zel'dovich).")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float lssSpectralIndex = 0.965f;
    public enum LssNegField
    {
        [InspectorName("Indépendant"), EnName("Independent")] Independant,
        [InspectorName("Anticorrélé (−δ des baryons)"), EnName("Anticorrelated (−δ of baryons)")] Anticorrele
    }
    [UILabel("Champ initial du secteur négatif", En = "Initial field of the negative sector")]
    [ShowIf("IsLss & IsRealisticIC & IsJanusLss")]
    [Tooltip("Janus : fluctuations initiales des masses négatives. Indépendant : autre tirage, même spectre et même amplitude. " +
             "Anticorrélé : exactement l'opposé du champ des baryons (là où il y a un excès de matière ordinaire, il manque " +
             "de la masse négative) : ségrégation dès le départ. Les témoins matière noire utilisent toujours le même champ " +
             "que les baryons, comme dans le modèle standard.")]
    [TooltipEn("Janus: initial fluctuations of the negative masses. Independent: another draw, same spectrum and amplitude. Anticorrelated: exactly the opposite of the baryon field (where ordinary matter is in excess, negative mass is missing): segregation from the start. Dark-matter controls always use the same field as baryons, as in the standard model.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public LssNegField lssNegField = LssNegField.Independant;
    [UILabel("Conditions initiales au 2e ordre (2LPT)", En = "Second-order initial conditions (2LPT)")]
    [ShowIf("IsLss & IsRealisticIC")]
    [Tooltip("Ajoute la correction du 2e ordre de la théorie lagrangienne des perturbations (2LPT) au déplacement de " +
             "Zel'dovich : les non-linéarités déjà présentes au départ (z ≈ 5,6) sont mieux représentées, ce qui évite les " +
             "transitoires qui faussent les petites échelles. Standard des simulations cosmologiques. Calcul initial ≈ 3 fois plus long.")]
    [TooltipEn("Adds the second-order Lagrangian perturbation theory (2LPT) correction to the Zel'dovich displacement: the non-linearities already present at the start (z ≈ 5.6) are better represented, avoiding transients that bias small scales. Standard in cosmological simulations. Initial computation ≈ 3 times longer.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public bool lss2LPT = false;
    public enum LssJanusIc
    {
        [InspectorName("Vitesses nulles (historique)"), EnName("Zero velocities (historical)")] VitessesNulles,
        [InspectorName("Mode croissant linéaire"), EnName("Linear growing mode")] ModeCroissant
    }
    [UILabel("Janus : conditions initiales", En = "Janus: initial conditions")]
    [ShowIf("IsLss & IsRealisticIC & IsJanusLss")]
    [Tooltip("Vitesses nulles : chaque secteur reçoit son champ de déplacement (même amplitude), sans vitesse, comme jusqu'ici. " +
             "Mode croissant linéaire : la théorie linéaire à deux fluides de Janus (mêmes équations que le calcul N corps) " +
             "donne, au départ, le mode qui croît le plus vite : le secteur négatif reçoit le même champ que les baryons, " +
             "multiplié par le rapport δ−/δ+ de ce mode (négatif : anticorrélé), et les deux secteurs reçoivent les vitesses " +
             "cohérentes (δ̇ = γ δ). Le réglage « Champ initial du secteur négatif » est alors ignoré.")]
    [TooltipEn("Zero velocities: each sector gets its displacement field (same amplitude), with no velocity, as so far. Linear growing mode: Janus's two-fluid linear theory (same equations as the N-body computation) gives, at the start, the fastest-growing mode: the negative sector gets the same field as the baryons, multiplied by the δ−/δ+ ratio of that mode (negative: anticorrelated), and both sectors get the consistent velocities (δ̇ = γ δ). The 'Initial field of the negative sector' setting is then ignored.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public LssJanusIc lssJanusIc = LssJanusIc.VitessesNulles;
    [UILabel("Janus : σ₈ = valeur linéaire aujourd'hui", En = "Janus: σ₈ = linear value today")]
    [ShowIf("IsLss & IsRealisticIC & IsJanusLss")]
    [Tooltip("Non (historique) : l'amplitude de départ de Janus vaut σ₈ × D_ΛCDM(z_départ), comme si Janus grandissait comme ΛCDM. " +
             "Oui : l'amplitude de départ est choisie pour que la théorie linéaire de Janus donne σ₈ à la fin du run pour la " +
             "matière ordinaire, exactement la convention de ΛCDM. σ₈ a alors le même sens pour les deux modèles " +
             "(observé : ≈ 0,81).")]
    [TooltipEn("No (historical): Janus's starting amplitude is σ₈ × D_ΛCDM(z_start), as if Janus grew like ΛCDM. Yes: the starting amplitude is chosen so that Janus's linear theory gives σ₈ at the end of the run for ordinary matter, exactly the ΛCDM convention. σ₈ then has the same meaning for both models (observed: ≈ 0.81).")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool lssJanusSigma8Today = false;

    [UILabel("Rapport des vitesses de la lumière c−/c+ (grande échelle)", En = "Speed-of-light ratio c−/c+ (large scale)")]
    [ShowIf("IsLss")]
    [Range(0.05f, 10f)]
    [Tooltip("Dans le modèle complet de Petit, le secteur négatif a sa propre vitesse de la lumière : c−/c+ ≈ 1/10 " +
             "d'après l'éq. (15) de Petit, d'Agostini & Debergh (Progress in Physics 2019), mais ≈ 10 d'après Petit (2018) et le " +
             "« gain de 1 000 » du même article de 2019 (ā/a ≈ 1/100 × c−/c+ ≈ 10) : 10 est probablement la valeur voulue. Sa relation d'énergie ρc²a³ + ρ̄c̄²ā³ = E (Petit, d'Agostini & Debergh 2024, " +
             "éq. 96) donne, à la limite newtonienne : une masse positive voit les masses négatives avec le poids (c−/c+)², " +
             "une masse négative voit les masses positives avec le poids (c+/c−)². Chaque secteur s'attire lui-même normalement. " +
             "1 = lois Janus symétriques utilisées jusqu'ici. Avec 0,1, il faut |M−| > 100 M+ pour que le milieu domine.")]
    [TooltipEn("In Petit's full model the negative sector has its own speed of light: c−/c+ ≈ 1/10 according to eq. (15) of Petit, d'Agostini & Debergh (Progress in Physics 2019), but ≈ 10 according to Petit (2018) and the 'gain of 1000' in the same 2019 paper (ā/a ≈ 1/100 × c−/c+ ≈ 10): 10 is probably the intended value. His energy relation ρc²a³ + ρ̄c̄²ā³ = E (Petit, d'Agostini & Debergh 2024, eq. 96) gives, in the Newtonian limit: a positive mass sees negative masses with weight (c−/c+)², a negative mass sees positive masses with weight (c+/c−)². Each sector attracts itself normally. 1 = the symmetric Janus laws used so far. With 0.1, |M−| > 100 M+ is needed for the medium to dominate.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float lightSpeedRatio = 1f;                 // mode grande échelle
    [UILabel("Rapport des vitesses de la lumière c−/c+ (galaxie)", En = "Speed-of-light ratio c−/c+ (galaxy)")]
    [ShowIf("IsGalaxy")]
    [Range(0.05f, 1f)]
    [Tooltip("Même grandeur que pour la grande échelle, mais réglée séparément pour le mode galaxie " +
             "(chaque mode garde sa propre valeur quand on passe de l'un à l'autre). " +
             "1 = lois Janus symétriques, avec lesquelles la galaxie de référence a été validée. " +
             "Avec une valeur c−/c+, il faut |M−| × (c−/c+)² > M+ pour que la cavité confine la galaxie.")]
    [TooltipEn("Same quantity as for large scale, but set separately for galaxy mode (each mode keeps its own value when switching). 1 = symmetric Janus laws, with which the reference galaxy was validated. With a value c−/c+, |M−| × (c−/c+)² > M+ is needed for the cavity to confine the galaxy.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float galaxyLightSpeedRatio = 1f;           // mode galaxie
    [UILabel("Facteur d'échelle propre au secteur négatif", En = "Own scale factor for the negative sector")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss")]
    [Tooltip("Non : les deux secteurs partagent le même facteur d'échelle (approximation utilisée jusqu'ici). " +
             "Oui : le secteur négatif a son propre facteur d'échelle ā, obtenu par sa propre équation de Friedmann " +
             "(Petit 2024, éq. 98b) : d²ā/dt² = −(4πG/3)(|ρ−| − ρ+ (c+/c−)²)/ā². Les masses négatives suivent alors ā " +
             "(freinage de Hubble et force divisée par ā³), les positives suivent a.")]
    [TooltipEn("No: both sectors share the same scale factor (approximation used so far). Yes: the negative sector has its own scale factor ā, from its own Friedmann equation (Petit 2024, eq. 98b): d²ā/dt² = −(4πG/3)(|ρ−| − ρ+ (c+/c−)²)/ā². Negative masses then follow ā (Hubble drag and force divided by ā³), positive ones follow a.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool negativeOwnScaleFactor = false;
    public enum NegScaleTarget
    {
        [InspectorName("Fixé au départ"), EnName("Fixed at start")] AuDepart,
        [InspectorName("Visé à la fin (aujourd'hui)"), EnName("Targeted at the end (today)")] ALaFin
    }
    [UILabel("Rapport ā/a imposé", En = "Imposed ā/a ratio")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss & negativeOwnScaleFactor")]
    [Tooltip("Le rapport ā/a évolue pendant toute la simulation (chaque secteur suit sa propre équation d'expansion). " +
             "Fixé au départ : on choisit ā/a initial et la vitesse d'expansion initiale du secteur négatif. " +
             "Visé à la fin : on choisit ā/a final (Petit ≈ 1/100 aujourd'hui, d'après les fluctuations du fond " +
             "cosmologique) et la simulation calcule la vitesse d'expansion initiale du secteur négatif qui y mène.")]
    [TooltipEn("The ā/a ratio evolves throughout the simulation (each sector follows its own expansion equation). Fixed at start: choose the initial ā/a and the initial expansion rate of the negative sector. Targeted at the end: choose the final ā/a (Petit ≈ 1/100 today, from the cosmic microwave background fluctuations) and the simulation computes the initial expansion rate of the negative sector that leads to it.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public NegScaleTarget negativeScaleTarget = NegScaleTarget.AuDepart;
    [UILabel("Rapport ā/a final visé", En = "Targeted final ā/a")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss & negativeOwnScaleFactor & NegTargetAtEnd")]
    [Tooltip("Rapport des distances des deux secteurs en fin de run (aujourd'hui si le redshift de départ vaut " +
             "a final − 1). Petit : ≈ 0,01. Plus il est petit, plus le milieu négatif devient dense et rapide, et plus le " +
             "calcul est long (voir l'estimation affichée en haut du panneau, simulation arrêtée).")]
    [TooltipEn("Distance ratio of the two sectors at the end of the run (today if the starting redshift equals a final − 1). Petit: ≈ 0.01. The smaller it is, the denser and faster the negative medium becomes, and the longer the computation (see the estimate at the top of the panel, simulation stopped).")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float negativeScaleRatioEnd = 0.3f;
    [UILabel("Rapport ā/a au départ", En = "Initial ā/a ratio")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss & negativeOwnScaleFactor")]
    [Tooltip("Rapport des facteurs d'échelle des deux secteurs au départ. Petit propose ā/a ≈ 1/100 aujourd'hui : les distances " +
             "y sont 100 fois plus courtes, la densité physique du milieu 10⁶ fois plus grande et son évolution beaucoup plus " +
             "rapide. Une valeur très petite impose un pas de temps minuscule (un avertissement s'affiche si dt est trop grand).")]
    [TooltipEn("Ratio of the scale factors of the two sectors at the start. Petit's ≈ 1/100 refers to today. A very small value makes the negative medium very dense and requires a tiny time step.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float negativeScaleRatio = 1f;
    [UILabel("Vitesse d'expansion initiale du secteur négatif", En = "Initial expansion rate of the negative sector")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss & negativeOwnScaleFactor & !NegTargetAtEnd")]
    [Tooltip("Taux d'expansion de départ du secteur négatif, en multiple du taux « parabolique » (juste suffisant pour ne pas " +
             "se recontracter). 1 = parabolique ; > 1 = univers ouvert (courbure k = −1, comme dans la solution de Petit). " +
             "Sans effet si le milieu est dominé par la répulsion des positives (il accélère alors de lui-même).")]
    [TooltipEn("Initial expansion rate of the negative sector, as a multiple of the 'parabolic' rate (just enough not to recollapse). 1 = parabolic; > 1 = open universe (curvature k = −1, as in Petit's solution); < 1 = bound sector that eventually recollapses.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float negativeExpansionRate = 1.2f;
    [UILabel("Vitesse initiale de ā fixée par la courbure k̄ = −1", En = "Initial rate of ā set by the curvature k̄ = −1")]
    [ShowIf("IsLss & expansionSelfConsistent & IsJanusLss & negativeOwnScaleFactor & !NegTargetAtEnd")]
    [Tooltip("Oui : la vitesse d'expansion initiale du secteur négatif n'est plus un réglage libre, elle découle de l'équation " +
             "de Friedmann du secteur négatif avec la même courbure k̄ = −1 et les mêmes coordonnées comobiles que notre secteur " +
             "(Petit & d'Agostini 2014, Petit, Margnat & Zejli 2024, éq. 94 et 97) : dā/dt² = (8πG/3)ρ̄_eff/ā + (c−/c+)² C, " +
             "où C = (8πG/3)(κ²ρ− − ρ+) est le terme de courbure de notre secteur au rebond. λ ≈ 1 pour c−/c+ ≤ 0,1, ≈ 1,4 pour 1, " +
             "≈ 10 pour c−/c+ = 10 et ā/a = 0,01. Non : la valeur « Vitesse d'expansion initiale » ci-dessus.")]
    [TooltipEn("Yes: the initial expansion rate of the negative sector is no longer a free setting; it follows from the negative sector's Friedmann equation with the same curvature k̄ = −1 and the same comoving coordinates as our sector (Petit & d'Agostini 2014; Petit, Margnat & Zejli 2024, eqs. 94 and 97): dā/dt² = (8πG/3)ρ̄_eff/ā + (c−/c+)² C, where C = (8πG/3)(κ²ρ− − ρ+) is our sector's curvature term at the bounce. λ ≈ 1 for c−/c+ ≤ 0.1, ≈ 1.4 for 1, ≈ 10 for c−/c+ = 10 and ā/a = 0.01. No: the 'Initial expansion rate' value above.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool negativeRateFromCurvature = false;
    [UILabel("Horloge du secteur négatif T−/T+ (exploratoire)", En = "Negative-sector clock T−/T+ (exploratory)")]
    [ShowIf("IsLss & IsJanusLss")]
    [Tooltip("Petit & d'Agostini (2014) annoncent des « facteurs d'échelle de temps » T+ et T− sans les publier. Ici, test " +
             "exploratoire : le temps propre du secteur négatif s'écoule T−/T+ fois plus vite que le nôtre (dτ− = (T−/T+) dt). " +
             "Toute la dynamique des masses négatives (forces subies, expansion de ā) est accélérée d'autant ; les forces que " +
             "subit la matière ordinaire et l'expansion de notre secteur ne changent pas (H₀ et q₀ inchangés). 1 = sans effet. " +
             "Ce n'est pas une dérivation des équations de Petit : c'est un test de sensibilité.")]
    [TooltipEn("Petit & d'Agostini (2014) announce 'time scale factors' T+ and T− without publishing them. Here, exploratory test: the proper time of the negative sector runs T−/T+ times faster than ours (dτ− = (T−/T+) dt). All the dynamics of the negative masses (forces they feel, expansion of ā) is sped up accordingly; the forces felt by ordinary matter and our sector's expansion do not change (H₀ and q₀ unchanged). 1 = no effect. This is not a derivation from Petit's equations: it is a sensitivity test.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float negativeClockRatio = 1f;
    double Tau => IsLss && lssModel == LssModel.Janus && !JanusPhenoOn ? System.Math.Max(1e-3, (double)negativeClockRatio) : 1.0;

    // vitesse initiale de ā imposée par la courbure k̄ = −1, en multiple de la vitesse parabolique
    double CurvatureLambda(double r0)
    {
        double c8 = 8 * System.Math.PI * G / 3;
        double C = c8 * (KappaSq * RhoMedium - RhoPos);                     // (c/R)² de notre secteur (au rebond, H = 0)
        double par = c8 * System.Math.Abs(NegRhoEff) / System.Math.Max(1e-12, r0);
        if (par <= 0) return 1;
        double rate2 = KappaSq * System.Math.Max(0.0, C) + (NegRhoEff >= 0 ? par : -par);
        return System.Math.Sqrt(System.Math.Max(rate2, 1e-12 * par) / par);
    }
    [UILabel("Pas de temps automatique", En = "Automatic time step")]
    [ShowIf("IsLss & IsJanusLss")]
    [Tooltip("Réduit le pas de temps quand le milieu négatif devient trop dense ou trop rapide pour dt " +
             "(pas ≤ 0,1 × son temps dynamique). Sans cela, les résultats deviennent faux. Le pas ne dépasse jamais dt.")]
    [TooltipEn("Reduces the time step when the negative medium becomes too dense or too fast for dt (step ≤ 0.1 × its dynamical time). Without it, results become wrong. The step never exceeds dt.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool adaptiveTimestep = true;

    public enum JanusCalibration
    {
        [InspectorName("Densités simulées (Friedmann)"), EnName("Simulated densities (Friedmann)")] Densites,
        [InspectorName("H₀ observé en fin de run"), EnName("Observed H₀ at end of run")] H0Observe
    }
    [UILabel("Calage de l'expansion Janus", En = "Janus expansion calibration")]
    [ShowIf("IsLss & expansionSelfConsistent")]
    [Tooltip("Densités simulées : l'échelle de temps de la loi de Petit découle de |ρ−| − ρ+, avec un seul facteur d'échelle " +
             "et une seule vitesse de la lumière pour les deux secteurs (simplification de cette simulation). " +
             "H₀ observé : l'échelle de temps est choisie pour que le taux d'expansion en fin de run (aujourd'hui si le " +
             "redshift de départ vaut expansionFactorEnd − 1) soit égal au H₀ mesuré (67,7 km/s/Mpc), quel que soit |ρ−|. " +
             "C'est le cas du modèle complet de Petit, où les deux secteurs ont des facteurs d'échelle et des vitesses " +
             "de la lumière différents, ce que cette simulation ne représente pas.")]
    [TooltipEn("Simulated densities: the time scale of Petit's law follows from κ²|ρ−| − ρ+, with a single scale factor and a single speed of light for both sectors unless set otherwise. Observed H₀: the time scale is chosen so that the expansion rate at the end of the run (today) equals the measured H₀ (67.7 km/s/Mpc), whatever |ρ−|.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public JanusCalibration janusCalibration = JanusCalibration.Densites;

    [UILabel("Durée fixée par q₀ (accélération actuelle)", En = "Duration set by q₀ (current acceleration)")]
    [ShowIf("expansionSelfConsistent")]
    [Tooltip("Avec la loi de Petit (départ au rebond, a = 1, H = 0), le paramètre de décélération vaut " +
             "q = −1 / (2 (a − 1)). Oui : le facteur d'échelle final est choisi pour que q soit égal à q₀ visé en fin de run, " +
             "et le redshift de départ pour que la fin du run soit aujourd'hui (z_départ = a_final − 1). " +
             "Le même a final est appliqué à tous les modèles de la campagne.")]
    [TooltipEn("With Petit's law (start at the bounce, a = 1, H = 0), the deceleration parameter is q = −1 / (2 (a − 1)). Yes: the final scale factor is chosen so that q equals the targeted q₀ at the end of the run, and the starting redshift so that the end of the run is today (z_start = a_final − 1). The same final a is used for all models of the campaign.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public bool janusMatchQ0 = false;
    [UILabel("q₀ visé", En = "Targeted q₀")]
    [ShowIf("expansionSelfConsistent & janusMatchQ0")]
    [Tooltip("Paramètre de décélération actuel (négatif = expansion accélérée). " +
             "≈ −0,09 : ajustement de Petit et d'Agostini sur les supernovae ; ≈ −0,55 : modèle standard ΛCDM. " +
             "Pour garder H₀ = 67,7 km/s/Mpc avec le calage sur les densités, il faut (c−/c+)² |M−| ≈ M+ (1 + 2|q₀| / Ω_b) : " +
             "l'estimation affichée en haut du panneau indique la masse nécessaire.")]
    [TooltipEn("Current deceleration parameter (negative = accelerated expansion). ≈ −0.09: fit by Petit and d'Agostini to supernovae; ≈ −0.55: standard ΛCDM model. To keep H₀ = 67.7 km/s/Mpc with density calibration, (c−/c+)² |M−| ≈ M+ (1 + 2|q₀| / Ω_b) is needed: the estimate at the top of the panel gives the required mass.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float janusQ0Target = -0.09f;

    [UILabel("Janus phénoménologique (couplage qui change de signe)", En = "Phenomenological Janus (sign-changing coupling)")]
    [ShowIf("IsLss")]
    [Tooltip("Test de principe, ce n'est plus le modèle de Petit. Expansion imposée = celle de ΛCDM (plate, H₀ = 67,7), " +
             "et le couplage croisé entre secteurs est multiplié par s(z) : s ≈ −(Ω_m/Ω_b − 1) / (κ²|M−|/M+) avant z_bascule " +
             "(le secteur négatif attire la matière ordinaire avec exactement le poids de la matière noire de ΛCDM), puis s → +1 " +
             "(répulsion de Janus) après. C'est la condition nécessaire trouvée par l'étude inverse (bascule vers z ≈ 3,4). " +
             "Conseillé : c−/c+ = 1 et |M−| ≈ 4,6 M+, mêmes conditions initiales que ΛCDM (σ₈ Janus = 0,81).")]
    [TooltipEn("Proof-of-principle test, no longer Petit's model. Imposed expansion = that of ΛCDM (flat, H₀ = 67.7), and the cross coupling between sectors is multiplied by s(z): s ≈ −(Ω_m/Ω_b − 1) / (κ²|M−|/M+) before z_switch (the negative sector attracts ordinary matter with exactly the weight of ΛCDM dark matter), then s → +1 (Janus repulsion) afterwards. This is the necessary condition found by the inverse study (switch near z ≈ 3.4). Advised: c−/c+ = 1 and |M−| ≈ 4.6 M+, same initial conditions as ΛCDM (Janus σ₈ = 0.81).")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public bool janusPheno = false;
    [UILabel("Redshift de bascule", En = "Switch redshift")]
    [ShowIf("IsLss & janusPheno")]
    [Tooltip("Redshift où le couplage croisé passe de l'attraction à la répulsion (s = moyenne des deux valeurs). " +
             "≈ 3,4 d'après l'étude inverse (H(z) de ΛCDM).")]
    [TooltipEn("Redshift where the cross coupling switches from attraction to repulsion (s = mean of the two values). ≈ 3.4 from the inverse study (ΛCDM H(z)).")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float janusPhenoZ = 3.4f;
    [UILabel("Raideur de la bascule", En = "Switch sharpness")]
    [ShowIf("IsLss & janusPheno")]
    [Tooltip("s(x) = s_tôt + (1 − s_tôt) / (1 + (x/x_b)^n), x = 1 + z. 4 ≈ forme trouvée par l'étude inverse ; plus grand = plus brutal.")]
    [TooltipEn("s(x) = s_early + (1 − s_early) / (1 + (x/x_s)^n), x = 1 + z. 4 ≈ shape found by the inverse study; larger = more abrupt.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    public float janusPhenoSharpness = 4f;

    bool JanusPhenoOn => IsLss && lssModel == LssModel.Janus && janusPheno && SelfConsistent;
    // poids précoce : le secteur négatif joue la matière noire de ΛCDM (κ²|M−| × |s| = (Ω_m/Ω_b − 1) M+)
    double PhenoSEarly => -LcdmDmRatio / System.Math.Max(1e-9, KappaSq * negativeTotalMass / System.Math.Max(1e-30, (double)positiveTotalMass));
    double PhenoS(double a)
    {
        if (!JanusPhenoOn) return 1.0;
        double x = (1 + lssStartRedshift) / System.Math.Max(1e-9, a);          // 1 + z
        double xb = 1 + System.Math.Max(0.0, janusPhenoZ);
        double se = PhenoSEarly;
        return se + (1 - se) / (1 + System.Math.Pow(x / xb, System.Math.Max(0.5, janusPhenoSharpness)));
    }

    [Header("Gaz du disque")]
    [Tooltip("Fraction de la masse du disque sous forme de gaz (0 = disque d'étoiles seul, comme avant). " +
             "Réglage commun à tous les modèles, pour garder la comparaison équitable.")]
    [UILabel("Fraction de gaz", En = "Gas fraction")]
    [TooltipEn("Fraction of the disk mass in the form of gas (0 = stellar disk only). Common to all models, to keep the comparison fair.")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    [Range(0f, 0.5f)] public float gasFraction = 0.15f;
    [Tooltip("Épaisseur initiale du disque de gaz (plus fin que les étoiles)")]
    [UILabel("Épaisseur du gaz", En = "Gas thickness")]
    [ShowIf("HasGas")]
    [TooltipEn("Initial thickness of the gas disk (thinner than the stars).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float gasThickness = 0.4f;
    [Tooltip("Agitation initiale du gaz, en fraction de la vitesse circulaire")]
    [UILabel("Agitation du gaz", En = "Gas velocity spread")]
    [ShowIf("HasGas")]
    [TooltipEn("Initial velocity spread of the gas, as a fraction of the circular velocity.")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float gasVelocityDispersion = 0.04f;
    [Tooltip("Vitesse du son du gaz : dispersion minimale conservée (pression). Plus bas = gaz plus froid, plus grumeleux")]
    [UILabel("Vitesse du son", En = "Sound speed")]
    [ShowIf("HasGas")]
    [TooltipEn("Gas sound speed: minimum velocity spread kept (pressure). Lower = colder, clumpier gas.")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float gasSoundSpeed = 1.5f;
    [Tooltip("Temps de dissipation de l'agitation du gaz (refroidissement). Plus court = gaz plus froid")]
    [UILabel("Temps de refroidissement", En = "Cooling time")]
    [ShowIf("HasGas")]
    [TooltipEn("Dissipation time of the gas velocity spread (cooling). Shorter = colder gas.")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float gasCoolingTime = 1f;
    [Tooltip("Résolution de la grille du gaz (puissance de 2, 64 ou 128)")]
    [UILabel("Grille du gaz", En = "Gas grid")]
    [ShowIf("HasGas")]
    [TooltipEn("Resolution of the gas grid (power of 2, 64 or 128).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public int gasGridSize = 128;
    [UILabel("Formation d'étoiles", En = "Star formation")]
    [Tooltip("Le gaz dense se transforme progressivement en étoiles jeunes (bleues au rendu).")]
    [ShowIf("HasGas")]
    [TooltipEn("Dense gas gradually turns into young stars (blue in the display).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public bool starFormation = true;
    [Tooltip("Temps caractéristique de transformation du gaz dense en étoiles")]
    [UILabel("Temps de formation", En = "Formation time")]
    [ShowIf("HasGas & starFormation")]
    [TooltipEn("Characteristic time for dense gas to turn into stars.")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float starFormationTime = 30f;
    [Tooltip("Densité de gaz minimale pour former des étoiles (particules de gaz par cellule de la grille du gaz)")]
    [UILabel("Seuil de densité", En = "Density threshold")]
    [ShowIf("HasGas & starFormation")]
    [TooltipEn("Minimum gas density to form stars (gas particles per cell of the gas grid).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float starFormationThreshold = 30f;
    [Tooltip("Durée pendant laquelle une étoile reste « jeune » (brillante au rendu)")]
    [UILabel("Durée de vie « jeune »", En = "'Young' lifetime")]
    [ShowIf("HasGas & starFormation")]
    [TooltipEn("How long a star stays 'young' (bright in the display).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    public float youngStarLifetime = 10f;
    [Tooltip("Fraction des étoiles jeunes qui retournent au gaz en fin de vie (vents, supernovæ)")]
    [UILabel("Fraction recyclée en gaz", En = "Fraction recycled into gas")]
    [ShowIf("HasGas & starFormation")]
    [TooltipEn("Fraction of young stars that return to gas at the end of their life (winds, supernovae).")]
    [UIPanel("Gaz", En = "Gas", ShowIf = "IsGalaxy", Single = true)]
    [Range(0f, 1f)] public float gasRecycleFraction = 0.5f;

    [Header("Galaxie dans la cavité")]
    [UILabel("Rayon du disque", En = "Disk radius")]
    [Tooltip("Rayon du disque d'étoiles (troncature du profil exponentiel).")]
    [TooltipEn("Radius of the stellar disk (truncation of the exponential profile).")]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public float diskRadius = 50f;
    [UILabel("Épaisseur du disque", En = "Disk thickness")]
    [Tooltip("Épaisseur initiale du disque d'étoiles.")]
    [TooltipEn("Initial thickness of the stellar disk.")]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public float diskThickness = 1.5f;
    [Tooltip("Dispersion de vitesse du disque, en fraction de la vitesse circulaire (utilisé seulement si diskToomreQ = 0)")]
    [UILabel("Agitation du disque", En = "Disk velocity spread")]
    [ShowIf("DiskQZero")]
    [TooltipEn("Velocity dispersion of the disk, as a fraction of the circular velocity (used only if Toomre Q = 0).")]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public float diskVelocityDispersion = 0.08f;
    [Tooltip("Paramètre de Toomre Q des étoiles : fixe l'agitation du disque selon le critère de stabilité. " +
             "1,2-1,5 = disque qui développe des bras sans s'effondrer en barre. 0 = ancien réglage (fraction fixe).")]
    [UILabel("Toomre Q", En = "Toomre Q")]
    [TooltipEn("Toomre Q parameter of the stars: sets the disk velocity spread from the stability criterion. 1.2–1.5 = a disk that develops arms without collapsing into a bar. 0 = old setting (fixed fraction).")]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public float diskToomreQ = 1.3f;
    [Tooltip("Vrai disque exponentiel tronqué au bord. Décoché = ancien tirage (trop concentré au centre, " +
             "et 20 % des étoiles empilées en anneau au bord).")]
    [UILabel("Vrai disque exponentiel", En = "True exponential disk")]
    [TooltipEn("True exponential disk truncated at the edge. Off = old sampling (too concentrated at the centre, with 20 % of the stars piled up in a ring at the edge).")]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public bool diskExponentialFix = true;
    [UILabel("Rayon de la cavité", En = "Cavity radius")]
    [Tooltip("Rayon de la cavité vide de masses négatives au départ (Janus). Doit rester sous la demi-boîte.")]
    [TooltipEn("Radius of the cavity free of negative masses at the start (Janus). Must stay below half the box.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    [ShowIf("IsGalaxy")]
    public float cavityRadius = 70f;

    [Header("Dispersions de vitesse")]
    [Tooltip("'Température' du milieu négatif : faible = grumeleux, élevé = lisse")]
    [UILabel("Agitation du milieu négatif", En = "Negative medium velocity spread")]
    [TooltipEn("'Temperature' of the negative medium: low = clumpy, high = smooth.")]
    [UIPanel("Janus", En = "Janus", Single = true)]
    [ShowIf("IsGalaxy")]
    public float negativeSigma = 12f;
    [Tooltip("Mode LargeScaleStructure : agitation initiale des masses positives")]
    [UILabel("Agitation initiale ordinaire", En = "Initial ordinary velocity spread")]
    [TooltipEn("Large-scale mode: initial velocity spread of the positive masses.")]
    [UIPanel("Univers", En = "Universe", ShowIf = "IsLss", Single = true)]
    public float positiveSigma = 0.5f;

    [Header("Rendu")]
    [UIHide]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public Mesh instanceMesh;
    [UIHide]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public Material instanceMaterial;
    [UIHide]
    [UIPanel("Galaxie", En = "Galaxy", ShowIf = "IsGalaxy", Single = true)]
    public float renderScale = 0.15f;
    [UILabel("Afficher le milieu", En = "Show the medium")]
    [Live]
    [Tooltip("Affiche les particules du milieu (rouge : masses négatives ; violet : matière noire).")]
    [TooltipEn("Displays the medium particles (red: negative masses; purple: dark matter).")]
    [UIPanel("Rendu", En = "Rendering", Single = true)]
    public bool showNegatives = true;

    [Header("Compute")]
    [UIHide]
    [UIPanel("Rendu", En = "Rendering", Single = true)]
    public ComputeShader compute;

    [Header("Diagnostics")]
    [UILabel("Rayon de confinement", En = "Confinement radius")]
    [Live]
    [ShowIf("IsGalaxy")]
    [Tooltip("Rayon dans lequel une étoile est comptée comme confinée (diagnostic).")]
    [TooltipEn("Radius within which a star counts as confined (diagnostic).")]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public float confinementRadius = 70f;
    [UILabel("Mesure tous les N pas", En = "Measure every N steps")]
    [Live]
    [ShowIf("!SelfConsistent")]
    [Tooltip("Fréquence des mesures (affichage et CSV). En expansion auto-cohérente, les mesures sont espacées en a.")]
    [TooltipEn("Measurement frequency (display and CSV). With self-consistent expansion, measurements are spaced in a.")]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public int diagnosticEverySteps = 100;
    [Tooltip("Optionnel : la caméra suit le centre de la galaxie en gardant son décalage initial")]
    [UIHide]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public Transform followCamera;

    [Header("Export CSV")]
    [UILabel("Exporter en CSV", En = "Export CSV")]
    [Live]
    [Tooltip("Écrit un fichier CSV des mesures pour chaque simulation.")]
    [TooltipEn("Writes a CSV file of the measurements for each simulation.")]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public bool exportCsv = true;
    [Tooltip("Dossier de sortie. Vide = dossier JanusRuns à la racine du projet Unity")]
    [UILabel("Dossier CSV", En = "CSV folder")]
    [TooltipEn("Output folder. Empty = JanusRuns folder next to the application (or the Unity project).")]
    [FolderPath]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public string csvFolder = "";
    [Tooltip("Séparateur ';' et virgule décimale (Excel en français). Décoché = ',' et point décimal (Python, R)")]
    [UILabel("Format Excel français", En = "French Excel format")]
    [Live]
    [TooltipEn("';' separator and decimal comma (French Excel). Off = ',' and decimal point (Python, R, English Excel).")]
    [UIPanel("Mesures et export", En = "Measurements & export", Single = true)]
    public bool csvFrenchExcel = true;

    // Clic droit sur le composant (ou menu ⋮) → « Configuration de référence »
    [ContextMenu("Configuration de référence")]
    public void ApplyReferenceConfig()
    {
        mode = InitMode.GalaxyInCavity;
        boxSize = 400f; gridSize = 256; smoothingLength = 1f;
        // Tests de convergence : 300 000 étoiles ; 480 000 particules de milieu (le milieu négatif à 240 000
        // particules, chacune ~90 fois plus lourde qu'une étoile, chauffait le disque et renforçait la barre)
        positiveCount = 300000; negativeCount = 480000;
        positiveTotalMass = 35000f; negativeTotalMass = 2500000f;
        G = 1f; dt = 0.02f; stepsPerFrame = 1;
        diskRadius = 50f; diskThickness = 1f; diskToomreQ = 1.3f; diskExponentialFix = true;
        cavityRadius = 70f; negativeSigma = 18f;
        dmScaleRadius = 0f; dmTruncationRadius = 150f;
        gasFraction = 0.15f; gasThickness = 0.4f; gasVelocityDispersion = 0.04f;
        gasSoundSpeed = 3f; gasCoolingTime = 2f; gasGridSize = 128;
        starFormation = true; starFormationTime = 80f; starFormationThreshold = 60f;
        youngStarLifetime = 10f; gasRecycleFraction = 0.7f;
        diagnosticEverySteps = 250; confinementRadius = 70f;
        batchEndTime = 100f; batchSummaryFrom = 50f;
        galaxyLightSpeedRatio = 1f;        // galaxie validée avec les lois Janus symétriques (c− = c+)
        if (JanusWeb.IsWeb)
        {
            // version web allégée : la mémoire GPU d'un navigateur est limitée (tampons de 128 Mo par défaut)
            gridSize = 128; positiveCount = 150000; negativeCount = 240000; gasGridSize = 64;
        }
        Debug.Log("Janus PM : configuration de référence appliquée.");
    }

    [Header("Campagne multi-seeds")]
    [Tooltip("Enchaîne automatiquement plusieurs simulations (seeds × modèles cochés) puis écrit un CSV " +
             "de synthèse. Tous les autres réglages restent ceux de l'inspecteur, identiques pour chaque run.")]
    [UILabel("Mode campagne (multi-seeds)", En = "Campaign mode (multi-seed)")]
    [TooltipEn("Runs several simulations in a row (seeds × selected models), then writes a summary CSV. All other settings are identical for every run.")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public bool batchMode = false;
    [Tooltip("Liste des seeds à tester, ex. 1, 2, 3, 4, 5")]
    [UILabel("Seeds", En = "Seeds")]
    [ShowIf("batchMode")]
    [TooltipEn("List of seeds to test, e.g. 1, 2, 3, 4, 5.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public int[] batchSeeds = { 1, 2, 3 };
    [Tooltip("Durée de chaque simulation (en unités de temps de la simulation)")]
    [UILabel("Durée de chaque run", En = "Duration of each run")]
    [ShowIf("batchMode & !SelfConsistent")]
    [TooltipEn("Duration of each simulation (in simulation time units).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float batchEndTime = 100f;
    [Tooltip("La synthèse moyenne les mesures entre ce temps et la fin du run")]
    [UILabel("Moyennes dès t =", En = "Averages from t =")]
    [ShowIf("batchMode & !SelfConsistent")]
    [TooltipEn("The summary averages the measurements between this time and the end of the run.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float batchSummaryFrom = 50f;
    [Tooltip("Pas de calcul par image pendant la campagne (plus = plus rapide, image moins fluide)")]
    [UILabel("Pas de calcul par image", En = "Steps per frame")]
    [Live]
    [ShowIf("batchMode")]
    [TooltipEn("Computation steps per frame during the campaign (more = faster, less smooth display).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public int batchStepsPerFrame = 10;
    [Tooltip("Décoché = pas d'affichage des particules pendant la campagne (plus rapide)")]
    [UILabel("Afficher pendant la campagne", En = "Display during the campaign")]
    [Live]
    [ShowIf("batchMode")]
    [TooltipEn("Off = no particle display during the campaign (faster).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool batchRender = true;
    [Tooltip("Modèles à tester (chacun avec toutes les seeds)")]
    [UILabel("Janus libre", En = "Janus, free")]
    [ShowIf("batchMode & IsGalaxy")]
    [TooltipEn("Models to test (each with all seeds).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runJanusLibre = true;
    [UILabel("Janus figé", En = "Janus, frozen")]
    [ShowIf("!simplifiedModels & batchMode & IsGalaxy")]
    [Tooltip("Janus avec le milieu négatif figé (témoin).")]
    [TooltipEn("Janus with the negative medium frozen (control).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runJanusFige = false;
    [UILabel("Halo NFW libre", En = "NFW halo, free")]
    [ShowIf("batchMode & IsGalaxy")]
    [Tooltip("Halo de matière noire NFW, particules libres.")]
    [TooltipEn("NFW dark-matter halo, free particles.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runNFWLibre = true;
    [UILabel("Halo NFW figé", En = "NFW halo, frozen")]
    [ShowIf("!simplifiedModels & batchMode & IsGalaxy")]
    [Tooltip("Halo NFW figé (témoin).")]
    [TooltipEn("Frozen NFW halo (control).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runNFWFige = false;
    [UILabel("Halo isotherme libre", En = "Isothermal halo, free")]
    [ShowIf("!simplifiedModels & batchMode & IsGalaxy")]
    [Tooltip("Halo isotherme à cœur, particules libres.")]
    [TooltipEn("Cored isothermal halo, free particles.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runIsothermeLibre = false;
    [UILabel("Halo isotherme figé", En = "Isothermal halo, frozen")]
    [ShowIf("!simplifiedModels & batchMode & IsGalaxy")]
    [Tooltip("Halo isotherme figé (témoin).")]
    [TooltipEn("Frozen isothermal halo (control).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runIsothermeFige = false;
    [UILabel("Sphère homogène libre", En = "Uniform sphere, free")]
    [ShowIf("!simplifiedModels & batchMode & IsGalaxy")]
    [Tooltip("Halo sphérique homogène (essais historiques).")]
    [TooltipEn("Homogeneous spherical halo (historical tests).")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool runSphereLibre = false;
    [Tooltip("Mode LargeScaleStructure : modèles à tester dans la campagne")]
    [UILabel("Janus", En = "Janus")]
    [ShowIf("batchMode & IsLss")]
    [TooltipEn("Large-scale mode: models to test in the campaign.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssRunJanus = true;
    [UILabel("Matière noire (même masse)", En = "Dark matter (same mass)")]
    [ShowIf("!simplifiedModels & batchMode & IsLss")]
    [Tooltip("La masse négative remplacée par la même masse positive.")]
    [TooltipEn("The negative mass replaced by the same positive mass.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssRunMatiereNoire = true;
    [UILabel("Positives seules", En = "Positive only")]
    [ShowIf("!simplifiedModels & batchMode & IsLss")]
    [Tooltip("Matière ordinaire seule, le milieu devient des traceurs sans masse.")]
    [TooltipEn("Ordinary matter alone; the medium becomes massless tracers.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssRunPositivesSeules = true;
    [UILabel("Matière noire réaliste", En = "Realistic dark matter")]
    [ShowIf("!simplifiedModels & batchMode & IsLss")]
    [Tooltip("Matière noire de masse lssRealisticRatio × la masse ordinaire.")]
    [TooltipEn("Dark matter with mass = ratio × the ordinary mass.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssRunMatiereNoireRealiste = true;
    [UILabel("ΛCDM (modèle standard)", En = "ΛCDM (standard model)")]
    [ShowIf("batchMode & IsLss")]
    [Tooltip("Modèle standard complet : matière noire = (Ω_m/Ω_b − 1) × masse ordinaire ≈ 5,33 ×, et constante cosmologique " +
             "(Ω_m = 0,31, Ω_Λ = 0,69 aujourd'hui). Expansion a³ ∝ sh²(3/2 H_Λ t), H₀ = 67,7 km/s/Mpc en fin de run. " +
             "C'est la vraie référence à laquelle comparer Janus.")]
    [TooltipEn("Full standard model: dark matter = (Ω_m/Ω_b − 1) × ordinary mass ≈ 5.33 ×, and a cosmological constant (Ω_m = 0.31, Ω_Λ = 0.69 today). Expansion a³ ∝ sinh²(3/2 H_Λ t), H₀ = 67.7 km/s/Mpc at the end of the run. This is the true reference to compare Janus with.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssRunLambdaCdm = true;
    [UILabel("Janus : vitesses initiales du secteur négatif", En = "Janus: initial rates of the negative sector")]
    [ShowIf("batchMode & IsLss & expansionSelfConsistent & negativeOwnScaleFactor & !NegTargetAtEnd")]
    [Tooltip("Liste de vitesses d'expansion initiales du secteur négatif (multiples de la vitesse parabolique), séparées par « ; ». " +
             "Exemple : 0,98 ; 1 ; 1,2. Chaque valeur devient un modèle Janus distinct de la campagne (LSS_Janus_v0.98…), " +
             "testé avec toutes les seeds. Vide = la seule valeur de l'onglet Janus.")]
    [TooltipEn("List of initial expansion rates of the negative sector (multiples of the parabolic rate), separated by ';'. " +
               "Example: 0.98; 1; 1.2. Each value becomes a separate Janus model of the campaign (LSS_Janus_v0.98…), " +
               "tested with all seeds. Empty = the single value of the Janus tab.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float[] lssNegRates = { };
    [UILabel("Janus : amplitudes initiales σ₈", En = "Janus: initial amplitudes σ₈")]
    [ShowIf("batchMode & IsLss & IsRealisticIC")]
    [Tooltip("Liste de σ₈ pour les modèles Janus seulement, séparés par « ; ». Exemple : 0,25 ; 0,4 ; 0,6. Combinée avec la " +
             "liste des vitesses (chaque vitesse × chaque σ₈), noms LSS_Janus_v1.2_s8_0.4… Les témoins (ΛCDM, matière noire) " +
             "gardent le σ₈ de l'onglet Univers. But : trouver l'amplitude qui donne à Janus le même contraste que ΛCDM, " +
             "puis comparer les vides à contraste égal. Vide = le σ₈ de l'onglet pour tous.")]
    [TooltipEn("List of σ₈ for the Janus models only, separated by ';'. Example: 0.25; 0.4; 0.6. Combined with the rate list (each rate × each σ₈), names LSS_Janus_v1.2_s8_0.4… Controls (ΛCDM, dark matter) keep the σ₈ of the Universe tab. Goal: find the amplitude giving Janus the same contrast as ΛCDM, then compare voids at equal contrast. Empty = the tab's σ₈ for all.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float[] lssJanusSigma8s = { };
    [UILabel("Janus : rapports c−/c+", En = "Janus: c−/c+ ratios")]
    [ShowIf("batchMode & IsLss")]
    [Tooltip("Test de sensibilité : liste de rapports des vitesses de la lumière c−/c+, séparés par « ; » (ex. 0,05 ; 0,1 ; 0,3 ; 1). " +
             "Pour chaque valeur, la masse du milieu est ajustée pour garder (c−/c+)² |M−| constant, donc la même expansion " +
             "(H₀ ≈ 67) : seule la force des couplages croisés change. Combinée aux autres listes. Noms LSS_Janus_k0.3… " +
             "Vide = la valeur de l'onglet Janus.")]
    [TooltipEn("Sensitivity test: list of speed-of-light ratios c−/c+, separated by ';' (e.g. 0.05; 0.1; 0.3; 1). For each value the medium mass is adjusted to keep (c−/c+)² |M−| constant, hence the same expansion (H₀ ≈ 67): only the strength of the cross couplings changes. Combined with the other lists. Names LSS_Janus_k0.3… Empty = the value of the Janus tab.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float[] lssJanusKappas = { };
    [UILabel("Janus : indices spectraux n_s", En = "Janus: spectral indices n_s")]
    [ShowIf("batchMode & IsLss & IsRealisticIC")]
    [Tooltip("Liste d'indices du spectre primordial pour les modèles Janus seulement, séparés par « ; » (ex. 0,965 ; 1,2 ; 1,5). " +
             "Un indice plus grand donne plus de fluctuations aux petites échelles, à σ₈ égal (pivot à 8 Mpc/h). Le spectre " +
             "primordial de Janus n'est pas fixé par la théorie : c'est un test de ce qu'il faudrait. Noms LSS_Janus_ns1.2… " +
             "Les témoins gardent l'indice de l'onglet Univers. Vide = indice de l'onglet pour tous.")]
    [TooltipEn("List of primordial spectral indices for Janus models only, separated by ';' (e.g. 0.965; 1.2; 1.5). A larger index gives more small-scale fluctuations at equal σ₈ (pivot at 8 Mpc/h). Janus's primordial spectrum is not fixed by the theory: this tests what would be needed. Names LSS_Janus_ns1.2… Controls keep the Universe tab index. Empty = the tab index for all.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float[] lssJanusNs = { };
    [UILabel("Janus : horloges T−/T+", En = "Janus: clocks T−/T+")]
    [ShowIf("batchMode & IsLss")]
    [Tooltip("Liste de rapports d'horloge T−/T+ (exploratoire, voir l'onglet Janus), séparés par « ; » (ex. 0,3 ; 1 ; 3). " +
             "Noms LSS_Janus_T3… Vide = valeur de l'onglet Janus.")]
    [TooltipEn("List of clock ratios T−/T+ (exploratory, see the Janus tab), separated by ';' (e.g. 0.3; 1; 3). Names LSS_Janus_T3… Empty = the Janus tab value.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public float[] lssJanusClocks = { };
    [UILabel("Janus : tester les deux champs initiaux négatifs", En = "Janus: test both initial negative fields")]
    [ShowIf("batchMode & IsLss & IsRealisticIC")]
    [Tooltip("Oui : chaque modèle Janus est lancé deux fois, champ initial du secteur négatif indépendant (_ind) puis " +
             "anticorrélé aux baryons (_anti). Non : la valeur de l'onglet Univers.")]
    [TooltipEn("Yes: each Janus model is run twice, with an independent (_ind) then anticorrelated (_anti) initial negative field. No: the value of the Universe tab.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssJanusBothNegFields = false;
    [UILabel("Janus : tester les deux conditions initiales", En = "Janus: test both initial conditions")]
    [ShowIf("batchMode & IsLss & IsRealisticIC")]
    [Tooltip("Oui : chaque modèle Janus est lancé deux fois, vitesses nulles (_v0) puis mode croissant linéaire (_mc). " +
             "Non : la valeur de l'onglet Univers.")]
    [TooltipEn("Yes: each Janus model is run twice, with zero velocities (_v0) then the linear growing mode (_mc). No: the value of the Universe tab.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool lssJanusBothIc = false;
    [UILabel("Test de convergence : niveaux de résolution", En = "Convergence test: resolution levels")]
    [ShowIf("batchMode & IsLss")]
    [Tooltip("Liste de niveaux séparés par « ; » (ex. 1 ; 2 ; 3). Chaque modèle coché (Janus et témoins) est lancé à chaque niveau : " +
             "1 = grille 128³, 64³ + 75³ particules ; 2 = grille 256³, 96³ + 112³ (référence) ; 3 = grille 256³, 128³ + 149³. " +
             "Les conditions initiales sont strictement les mêmes modes à tous les niveaux : le spectre initial est coupé au " +
             "nombre d'onde du niveau le plus grossier. Noms LSS_…_r1, _r2, _r3. Vide = réglages de l'onglet.")]
    [TooltipEn("List of levels separated by ';' (e.g. 1; 2; 3). Each selected model (Janus and controls) runs at every level: 1 = 128³ grid, 64³ + 75³ particles; 2 = 256³ grid, 96³ + 112³ (reference); 3 = 256³ grid, 128³ + 149³. The initial conditions are exactly the same modes at all levels: the initial spectrum is cut at the wavenumber of the coarsest level. Names LSS_…_r1, _r2, _r3. Empty = tab settings.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public int[] lssResLevels = { };
    static readonly int[] ResGrid = { 0, 128, 256, 256 };
    static readonly int[] ResSide = { 0, 64, 96, 128 };
    static int ResNegSide(int side) => Mathf.RoundToInt(side * 112f / 96f);
    int icNyqOverride = 0;                  // > 0 : coupure commune des conditions initiales (test de convergence)

    struct Body
    {
        public Vector3 pos;
        public float mass;
        public Vector3 vel;
        public int sign;
    }

    const float FixScale = 2048f;

    ComputeBuffer bodyBuffer, densBuffer, gridBuffer, forceBuffer, forceNegBuffer, argsBuffer, gasGridBuffer;
    int kClear, kDeposit, kToComplex, kFFT, kPoisson, kGradient, kKick, kDrift;
    int kGasClear, kGasDeposit, kGasUpdate;
    const float GasFix = 256f;

    // Les GasCount premières particules positives sont du gaz (tirées comme les étoiles)
    int GasCount => mode == InitMode.GalaxyInCavity ? Mathf.RoundToInt(gasFraction * positiveCount) : 0;

    bool IsLss => mode == InitMode.LargeScaleStructure;

    // Masse par particule du milieu, selon le modèle
    float NegMassParam => (IsLss ? lssModel == LssModel.Janus : !darkMatterHalo) ? negativeTotalMass / negativeCount : 0f;
    float HaloMassParam => IsLss
        ? (lssModel == LssModel.MatiereNoire ? negativeTotalMass / negativeCount
           : lssModel == LssModel.MatiereNoireRealiste ? lssRealisticRatio * positiveTotalMass / negativeCount
           : lssModel == LssModel.LambdaCDM ? (float)(LcdmDmRatio * positiveTotalMass / negativeCount) : 0f)
        : (darkMatterHalo ? haloMass / negativeCount : 0f);

    // ------------------------------------------------------------ expansion (coordonnées comobiles)
    // a(0) = 1, a(expansionDuration) = expansionFactorEnd.
    //  Einstein-de Sitter : a = (1 + t/τ)^(2/3), H = (2/3)/(τ + t).
    //  Janus (secteur positif, Petit & d'Agostini) : a = ch²(v), t = s·(v + sh(2v)/2), H = th(v) / (s·ch²(v)).
    bool SelfConsistent => IsLss && expansionSelfConsistent && expansionFactorEnd > 1f;
    bool ExpansionOn => SelfConsistent ||
                        (IsLss && expansionLaw != ExpansionLaw.Aucune && expansionFactorEnd > 1f && expansionDuration > 0f);

    // Densités moyennes (comobiles, a = 1)
    double RhoPos => positiveTotalMass / ((double)boxSize * boxSize * boxSize);
    double RhoMedium   // masse du milieu par unité de volume (valeur absolue), selon le modèle
    {
        get
        {
            double L3 = (double)boxSize * boxSize * boxSize;
            switch (lssModel)
            {
                case LssModel.Janus:
                case LssModel.MatiereNoire: return negativeTotalMass / L3;
                case LssModel.MatiereNoireRealiste: return lssRealisticRatio * positiveTotalMass / L3;
                case LssModel.LambdaCDM: return LcdmDmRatio * positiveTotalMass / L3;
                default: return 0;
            }
        }
    }
    // Janus auto-cohérent : il faut un excès de masse négative (|ρ−| > ρ+), sinon pas d'expansion accélérée
    // Secteur négatif (Petit) : couplages croisés et facteur d'échelle propre
    // c−/c+ du mode courant : chaque mode a son propre réglage
    float LightSpeedRatio => IsLss ? lightSpeedRatio : galaxyLightSpeedRatio;
    double KappaSq => System.Math.Max(1e-4, (double)LightSpeedRatio * LightSpeedRatio);
    float EffNegTotalMass => (float)(KappaSq * negativeTotalMass);      // masse négative « vue » par les positives
    bool UsesNegatives => IsLss ? lssModel == LssModel.Janus || batchMode : !darkMatterHalo || batchMode;
    bool IsJanusLss => lssModel == LssModel.Janus || batchMode;
    bool NegOwnScale => SelfConsistent && lssModel == LssModel.Janus && negativeOwnScaleFactor && !JanusPhenoOn;
    double negA, negADot, negT;            // état de ā(t) (intégré au fil des pas)
    float curDt;                            // pas de temps courant (variable si « pas de temps automatique »)

    bool UseQ0 => IsLss && expansionSelfConsistent && janusMatchQ0;
    float Q0AEnd => 1f + 1f / (2f * Mathf.Max(1e-3f, Mathf.Abs(janusQ0Target)));
    void ApplyQ0()
    {
        if (!UseQ0) return;
        expansionFactorEnd = Q0AEnd;
        lssStartRedshift = expansionFactorEnd - 1f;
    }

    // Paramètre de décélération du secteur positif : q = −a ä / ȧ²
    double Decel(double t)
    {
        if (!ExpansionOn) return double.NaN;
        if (EffectiveLaw == ExpansionLaw.EinsteinDeSitter) return 0.5;
        if (EffectiveLaw == ExpansionLaw.LambdaCDM)
        {
            double u = LcdmU(t), om = 1.0 / (System.Math.Cosh(u) * System.Math.Cosh(u));
            return 0.5 * om - (1 - om);                      // q = Ω_m/2 − Ω_Λ
        }
        double a = ScaleFactor(t);
        return a > 1 + 1e-9 ? -1.0 / (2.0 * (a - 1.0)) : double.NegativeInfinity;
    }

    bool NegTargetAtEnd => negativeScaleTarget == NegScaleTarget.ALaFin;
    double NegRhoEff => RhoMedium - RhoPos / KappaSq;                       // attraction propre − répulsion des positives

    // Vitesse d'expansion initiale de ā : lam × vitesse « parabolique » (juste suffisante pour ne pas se recontracter)
    double NegInitialRate(double r0, double lam) =>
        lam * Tau * System.Math.Sqrt(8 * System.Math.PI * G * System.Math.Abs(NegRhoEff) / (3 * r0));

    // Intègre ä = k/ā² de t0 à t1 (RK4, pas internes adaptés à la vitesse relative de ā)
    const double NegCollapse = 1e-6;          // ā en dessous : secteur effondré, on arrête d'intégrer

    static void IntegrateNeg(ref double a, ref double v, double t0, double t1, double k, double hMax)
    {
        double t = t0;
        int guard = 0;
        while (t < t1 - 1e-12)
        {
            // effondrement (ā → 0) : le pas deviendrait infinitésimal, on fige l'état au lieu de boucler sans fin
            if (a <= NegCollapse) { a = NegCollapse; v = 0; return; }
            if (++guard > 20000) return;    // sécurité absolue : jamais de blocage de l'application
            double rate = System.Math.Abs(v) / System.Math.Max(a, 1e-9) + System.Math.Sqrt(System.Math.Abs(k) / System.Math.Pow(System.Math.Max(a, 1e-9), 3));
            double h = System.Math.Min(t1 - t, System.Math.Min(hMax, 0.02 / System.Math.Max(rate, 1e-9)));
            double A(double x) => k / (System.Math.Max(x, 1e-9) * System.Math.Max(x, 1e-9));
            double a1 = a, v1 = v;
            double a2 = a1 + 0.5 * h * v1, v2 = v1 + 0.5 * h * A(a1);
            double a3 = a1 + 0.5 * h * v2, v3 = v1 + 0.5 * h * A(a2);
            double a4 = a1 + h * v3, v4 = v1 + h * A(a3);
            a = a1 + h / 6 * (v1 + 2 * v2 + 2 * v3 + v4);
            v = v1 + h / 6 * (A(a1) + 2 * A(a2) + 2 * A(a3) + A(a4));
            t += h;
            if (a < NegCollapse) { a = NegCollapse; v = 0; return; }
        }
    }

    double NegK => -4.0 / 3.0 * System.Math.PI * G * NegRhoEff * Tau * Tau;   // horloge T−/T+ : dynamique × (T−/T+)²

    // ā en fin de run (et ā minimal en cours de route) pour un départ (r0, lam)
    void NegRun(double r0, double lam, out double aEnd, out double aMin)
    {
        double a = r0, v = NegInitialRate(r0, lam), tEnd = ExpansionEndTime();
        aMin = a;
        int n = 200;
        for (int i = 0; i < n; i++)
        {
            IntegrateNeg(ref a, ref v, tEnd * i / n, tEnd * (i + 1) / n, NegK, System.Math.Max(1e-4, dt));
            if (a < aMin) aMin = a;
        }
        aEnd = a;
    }

    // Vitesse initiale (en multiple de la vitesse parabolique) qui donne ā/a = negativeScaleRatioEnd en fin de run
    string negSolveKey; double negSolveLam;
    double NegLambda()
    {
        if (!NegTargetAtEnd)
            return negativeRateFromCurvature ? CurvatureLambda(System.Math.Max(1e-4, negativeScaleRatio)) : negativeExpansionRate;
        string key = $"{negativeScaleRatioEnd}|{negativeScaleRatio}|{LightSpeedRatio}|{RhoMedium}|{RhoPos}|{G}|{ExpansionEndTime()}|{expansionFactorEnd}|{dt}";
        if (key == negSolveKey) return negSolveLam;
        double r0 = System.Math.Max(1e-4, negativeScaleRatio), target = negativeScaleRatioEnd * expansionFactorEnd;
        double lo = 0, hi = 20;
        for (int i = 0; i < 45; i++)
        {
            double mid = 0.5 * (lo + hi);
            NegRun(r0, mid, out double aEnd, out _);
            if (aEnd < target) lo = mid; else hi = mid;
        }
        negSolveKey = key; negSolveLam = 0.5 * (lo + hi);
        return negSolveLam;
    }

    double negLam;                             // vitesse initiale effectivement utilisée

    void ResetNegScale()
    {
        negT = 0;
        negA = NegOwnScale ? System.Math.Max(1e-4, negativeScaleRatio) : 1.0;
        negLam = NegOwnScale ? NegLambda() : 0;
        negADot = NegOwnScale ? NegInitialRate(negA, negLam) : 0;
    }

    // Pas de temps : ≤ dt, et ≤ 0,1 × temps dynamique du milieu négatif (densité « ressentie », facteur d'échelle propre)
    float StepDt(double t, double aNeg)
    {
        if (!IsLss || lssModel != LssModel.Janus || !adaptiveTimestep) return dt;
        double rhoFelt = System.Math.Max(RhoMedium, RhoPos / KappaSq) / (aNeg * aNeg * aNeg) * System.Math.Max(1.0, Tau * Tau);
        double tDyn = 1.0 / System.Math.Sqrt(4 * System.Math.PI * G * rhoFelt);
        return (float)System.Math.Max(1e-7, System.Math.Min(dt, 0.1 * tDyn));
    }

    // Estimation du nombre de pas (et de la durée) du run avec les réglages actuels
    // L'interface interroge l'estimation 4 fois par seconde : elle n'est recalculée que si un réglage a changé
    string estimateKey, estimateCache = "";
    public string RunEstimate
    {
        get
        {
            string key = $"{mode}|{lssModel}|{batchMode}|{expansionSelfConsistent}|{janusCalibration}|{janusMatchQ0}|{janusQ0Target}|" +
                         $"{expansionFactorEnd}|{lssStartRedshift}|{negativeTotalMass}|{positiveTotalMass}|{boxSize}|{G}|{dt}|" +
                         $"{LightSpeedRatio}|{negativeOwnScaleFactor}|{negativeScaleTarget}|{negativeScaleRatio}|{negativeScaleRatioEnd}|" +
                         $"{negativeExpansionRate}|{negativeRateFromCurvature}|{negativeScaleRatio}|{adaptiveTimestep}|{(int)(msPerStep * 10)}|{JanusLang.English}";
            if (key == estimateKey) return estimateCache;
            estimateKey = key;
            estimateCache = ComputeRunEstimate();
            return estimateCache;
        }
    }

    string ComputeRunEstimate()
    {
        {
            if (!IsLss || !SelfConsistent || (lssModel != LssModel.Janus && !batchMode)) return "";
            var savedModel = lssModel;
            float savedEnd = expansionFactorEnd, savedZ = lssStartRedshift;
            ApplyQ0();
            lssModel = LssModel.Janus;               // en campagne : estimation pour le run Janus
            try
            {
                double tEnd = ExpansionEndTime();
                double r0 = NegOwnScale ? System.Math.Max(1e-4, negativeScaleRatio) : 1.0;
                double lam = NegOwnScale ? NegLambda() : 0;
                double a = r0, v = NegOwnScale ? NegInitialRate(r0, lam) : 0, aMin = r0;
                double steps = 0, t = 0;
                int n = 400;
                for (int i = 0; i < n; i++)
                {
                    double t1 = tEnd * (i + 1) / n;
                    double aN = NegOwnScale ? a : ScaleFactor(t);
                    steps += (t1 - t) / StepDt(t, aN);
                    if (NegOwnScale) { IntegrateNeg(ref a, ref v, t, t1, NegK, System.Math.Max(1e-4, dt)); aMin = System.Math.Min(aMin, a); }
                    t = t1;
                }
                string txt = L("Estimation du run Janus : ≈ ", "Janus run estimate: ≈ ") + $"{steps:N0}" + L(" pas", " steps");
                double hEnd = HubbleKms(Hubble(tEnd));
                txt += L("\nFin du run : ", "\nEnd of run: ") + $"a = {expansionFactorEnd:G3}, q = {Decel(tEnd):G3}, H ≈ {hEnd:F0} km/s/Mpc";
                if (janusCalibration == JanusCalibration.Densites && hEnd > 0 && JanusSelfConsistentOk)
                {
                    // H² ∝ (κ²|M−| − M+) : masse négative qui donnerait H₀ = 67,7
                    double r = (KappaSq * negativeTotalMass - positiveTotalMass) / positiveTotalMass;
                    double rNeed = r * System.Math.Pow(H0ObservedKms / hEnd, 2);
                    double mNeed = positiveTotalMass * (1 + rNeed) / KappaSq;
                    txt += L("   (pour H₀ = 67,7 : masse du milieu ≈ ", "   (for H₀ = 67.7: medium mass ≈ ") + $"{mNeed:G3})";
                }
                if (msPerStep > 0) txt += $" (≈ {FormatDuration(steps * msPerStep / 1000.0)}" + L(" au rythme mesuré)", " at the measured speed)");
                if (NegOwnScale)
                {
                    txt += $"\nā/a : {r0:G3}" + L(" au départ → ", " at start → ") + $"{a / expansionFactorEnd:G3}" + L(" à la fin", " at the end") +
                           L("   (vitesse initiale du secteur négatif : ", "   (initial rate of the negative sector: ") + $"{lam:G3}" +
                           L(" × parabolique)", " × parabolic)");
                    if (lam < 1 && NegRhoEff > 0)
                        txt += L("\nSecteur négatif lié (< 1) : il finit par se recontracter, contrairement à la solution ouverte (k̄ = −1) de Petit.",
                                 "\nBound negative sector (< 1): it eventually recollapses, unlike Petit's open solution (k̄ = −1).");
                    if (aMin < 1e-3 * r0)
                        txt += L("\nIMPOSSIBLE : le secteur négatif s'effondre sur lui-même avant la fin (ā → 0).",
                                 "\nIMPOSSIBLE: the negative sector collapses onto itself before the end (ā → 0).");
                }
                return txt;
            }
            catch { return ""; }
            finally { lssModel = savedModel; expansionFactorEnd = savedEnd; lssStartRedshift = savedZ; }
        }
    }
    float msPerStep;                          // durée réelle moyenne d'un pas (mesurée pendant les runs)

    static string FormatDuration(double sec)
    {
        if (sec < 120) return $"{sec:F0} s";
        if (sec < 7200) return $"{sec / 60:F0} min";
        if (sec < 172800) return $"{sec / 3600:F1} h";
        return $"{sec / 86400:F1} jours";
    }

    // Avance ā jusqu'au temps t ; les appels se font à t croissant
    void AdvanceNegScale(double t)
    {
        if (t <= negT) return;
        IntegrateNeg(ref negA, ref negADot, negT, t, NegK, System.Math.Max(1e-4, dt));
        negT = t;
    }

    // Pas de temps trop grand pour le secteur négatif (densité physique ou couplage aux positives très élevés) ?
    string TimestepWarning()
    {
        if (!UsesNegatives || (IsLss && lssModel != LssModel.Janus)) return null;
        if (IsLss && adaptiveTimestep) return null;                       // le pas s'adapte de lui-même
        double aN = NegOwnScale ? negA : (ExpansionOn ? ScaleFactor(simTime) : 1.0);
        double rhoFelt = System.Math.Max(RhoMedium, RhoPos / KappaSq) / (aN * aN * aN);
        if (!IsLss) rhoFelt = System.Math.Max(rhoFelt, positiveTotalMass / KappaSq / System.Math.Max(1.0, diskRadius * diskRadius * diskRadius * 4.19));
        double tDyn = 1.0 / System.Math.Sqrt(4 * System.Math.PI * G * rhoFelt);
        if (dt <= 0.1 * tDyn) return null;
        return L("ATTENTION : pas de temps trop grand pour le secteur négatif", "WARNING: time step too large for the negative sector") +
               $" (dt = {dt:G3}, " + L("conseillé", "advised") + $" ≤ {0.1 * tDyn:G3}). " +
               L("Les résultats ne sont pas fiables.", "Results are not reliable.");
    }

    bool JanusSelfConsistentOk => KappaSq * RhoMedium > RhoPos;

    // Loi effectivement appliquée
    ExpansionLaw EffectiveLaw => !ExpansionOn ? ExpansionLaw.Aucune
        : !SelfConsistent ? expansionLaw
        : lssModel == LssModel.LambdaCDM || JanusPhenoOn ? ExpansionLaw.LambdaCDM
        : (lssModel == LssModel.Janus && (JanusSelfConsistentOk || janusCalibration == JanusCalibration.H0Observe)
            ? ExpansionLaw.JanusPetit : ExpansionLaw.EinsteinDeSitter);

    // Einstein-de Sitter : a = (1 + t/τ)^(2/3)
    double EdsTau()
    {
        if (!SelfConsistent) return expansionDuration / (System.Math.Pow(expansionFactorEnd, 1.5) - 1);
        // Friedmann : H₀² = 8πG ρ / 3 ; ρ = densité gravitante totale (Janus non conforme : |ρ+ − ρ−|)
        double rho = lssModel == LssModel.Janus ? System.Math.Abs(RhoPos - KappaSq * RhoMedium) : RhoPos + RhoMedium;
        double H0 = System.Math.Sqrt(8 * System.Math.PI * G * System.Math.Max(rho, 1e-30) / 3);
        return 2.0 / (3.0 * H0);
    }

    // ΛCDM (Planck) : a³ = a_fin³ (sh u / sh u_fin)², u = 3/2 H_Λ τ, H = H_Λ coth u, Ω_m(u) = 1/ch² u.
    // La fin du run (a = expansionFactorEnd) est aujourd'hui : Ω_m = 0,31, d'où ch² u_fin = 1/Ω_m.
    // Auto-cohérent : H_Λ² = (Ω_Λ/Ω_m) × (8πG/3) ρ_m / a_fin³ (ρ_m simulée) ; imposé : H_Λ tiré de la durée.
    const double LcdmOm = 0.31, LcdmOb = 0.049;
    static double LcdmDmRatio => LcdmOm / LcdmOb - 1;        // ≈ 5,33 : matière noire / matière ordinaire
    double LcdmUEnd => Acosh(1.0 / System.Math.Sqrt(LcdmOm));
    double LcdmU0 => Asinh(System.Math.Sinh(LcdmUEnd) / System.Math.Pow(expansionFactorEnd, 1.5));
    double LcdmHL()
    {
        if (SelfConsistent)
        {
            // matière « gravitante » du fond : ΛCDM simulé, ou Janus phénoménologique (fond ΛCDM imposé : Ω_m/Ω_b × ρ+)
            double C = 8 * System.Math.PI * G * (JanusPhenoOn ? RhoPos * LcdmOm / LcdmOb : RhoPos + RhoMedium) / 3;
            return System.Math.Sqrt((1 - LcdmOm) / LcdmOm * C / System.Math.Pow(expansionFactorEnd, 3));
        }
        return (LcdmUEnd - LcdmU0) / (1.5 * System.Math.Max(1e-6, expansionDuration));
    }
    double LcdmU(double t) => LcdmU0 + 1.5 * LcdmHL() * t;
    static double Asinh(double x) => System.Math.Log(x + System.Math.Sqrt(x * x + 1));
    // taux de croissance f = d ln D / d ln a ≈ Ω_m(a)^0,55 au départ (conditions initiales)
    double GrowthRateStart() => EffectiveLaw == ExpansionLaw.LambdaCDM
        ? System.Math.Pow(1.0 / System.Math.Pow(System.Math.Cosh(LcdmU0), 2), 0.55) : 1.0;

    // Durée pour atteindre expansionFactorEnd avec la loi effective
    double ExpansionEndTime()
    {
        if (EffectiveLaw == ExpansionLaw.LambdaCDM) return (LcdmUEnd - LcdmU0) / (1.5 * LcdmHL());
        if (EffectiveLaw == ExpansionLaw.EinsteinDeSitter) return EdsTau() * (System.Math.Pow(expansionFactorEnd, 1.5) - 1);
        double vEnd = Acosh(System.Math.Sqrt(expansionFactorEnd));
        return JanusS() * (vEnd + System.Math.Sinh(2 * vEnd) / 2);
    }

    double JanusS()
    {
        if (SelfConsistent && janusCalibration == JanusCalibration.H0Observe)
        {
            // H(fin) = th(v) / (s ch²(v)) = H₀ observé, exprimé en unités du code
            double vE = Acosh(System.Math.Sqrt(expansionFactorEnd));
            double h0Code = H0ObservedPerGyr * TimeUnitGyr();
            return System.Math.Tanh(vE) / (expansionFactorEnd * h0Code);
        }
        if (SelfConsistent && JanusSelfConsistentOk)
            return System.Math.Sqrt(3.0 / (8 * System.Math.PI * G * (KappaSq * RhoMedium - RhoPos)));
        double vEnd = Acosh(System.Math.Sqrt(expansionFactorEnd));
        return expansionDuration / (vEnd + System.Math.Sinh(2 * vEnd) / 2);
    }
    static double Acosh(double x) => System.Math.Log(x + System.Math.Sqrt(x * x - 1));

    double JanusV(double t)   // inverse de t = s·(v + sh(2v)/2), par dichotomie
    {
        double s = JanusS(), lo = 0, hi = 1;
        while (s * (hi + System.Math.Sinh(2 * hi) / 2) < t) hi *= 2;
        for (int i = 0; i < 60; i++) { double mid = 0.5 * (lo + hi); if (s * (mid + System.Math.Sinh(2 * mid) / 2) < t) lo = mid; else hi = mid; }
        return 0.5 * (lo + hi);
    }

    double ScaleFactor(double t)
    {
        if (!ExpansionOn) return 1;
        if (EffectiveLaw == ExpansionLaw.LambdaCDM)
            return expansionFactorEnd * System.Math.Pow(System.Math.Sinh(LcdmU(t)) / System.Math.Sinh(LcdmUEnd), 2.0 / 3.0);
        if (EffectiveLaw == ExpansionLaw.EinsteinDeSitter)
            return System.Math.Pow(1 + t / EdsTau(), 2.0 / 3.0);
        double ch = System.Math.Cosh(JanusV(t));
        return ch * ch;
    }

    double Hubble(double t)
    {
        if (!ExpansionOn) return 0;
        if (EffectiveLaw == ExpansionLaw.LambdaCDM)
            return LcdmHL() / System.Math.Tanh(LcdmU(t));
        if (EffectiveLaw == ExpansionLaw.EinsteinDeSitter)
            return (2.0 / 3.0) / (EdsTau() + t);
        double v = JanusV(t), ch = System.Math.Cosh(v);
        return System.Math.Tanh(v) / (JanusS() * ch * ch);
    }

    // Correspondance indicative en unités réelles (voir lssStartRedshift)
    //   unité de temps = √(G ρ+_code / (G_SI ρ_b,SI)), ρ_b,SI = Ω_b ρ_crit (1 + z_départ)³
    double TimeUnitGyr()
    {
        const double GSI = 6.674e-11, H0SI = 67.7e3 / 3.0857e22, OmegaB = 0.049, Gyr = 3.156e16;
        double rhoCrit = 3 * H0SI * H0SI / (8 * System.Math.PI * GSI);
        double rhoB = OmegaB * rhoCrit * System.Math.Pow(1 + lssStartRedshift, 3);
        return System.Math.Sqrt(G * RhoPos / (GSI * rhoB)) / Gyr;
    }
    double RedshiftAt(double a) => (1 + lssStartRedshift) / a - 1;
    const double H0ObservedKms = 67.7;                                      // Planck 2018
    static double H0ObservedPerGyr => H0ObservedKms / 977.79;               // 1 km/s/Mpc = 1/977,79 Gyr⁻¹
    // Taux d'expansion converti en km/s/Mpc (même correspondance d'unités que t_gyr)
    double HubbleKms(double hCode) => hCode / TimeUnitGyr() * 977.79;

    // Facteurs du pas de vitesse entre t et t + h
    void SetExpansionKick(double t, float h)
    {
        double aK = ScaleFactor(t);
        if (JanusPhenoOn)
        {
            double sK = PhenoS(aK);
            compute.SetFloat("_CrossPos", (float)(KappaSq * sK));
            compute.SetFloat("_CrossNeg", (float)(sK / KappaSq));
        }
        compute.SetFloat("_GravScale", (float)(1.0 / (aK * aK * aK)));
        compute.SetFloat("_VelDamp", (float)System.Math.Exp(-2.0 * Hubble(t) * h));
        if (NegOwnScale)
        {
            AdvanceNegScale(t);
            double hN = negADot / negA;
            compute.SetFloat("_GravScaleNeg", (float)(Tau * Tau / (negA * negA * negA)));
            compute.SetFloat("_VelDampNeg", (float)System.Math.Exp(-2.0 * hN * h));
        }
        else
        {
            compute.SetFloat("_GravScaleNeg", (float)(Tau * Tau / (aK * aK * aK)));
            compute.SetFloat("_VelDampNeg", (float)System.Math.Exp(-2.0 * Hubble(t) * h));
        }
    }
    int total, particleGroups, g8, g4;
    uint[] args = new uint[5];
    bool ready, readbackPending, lastShowNegatives;
    float simTime;
    int stepCount;
    string diagText = "";
    Vector3 galaxyCenter, smoothCenter, camOffset;
    System.IO.StreamWriter csv;
    string runId, csvPath;
    float haloMass;   // masse totale du halo de matière noire (0 en mode Janus)
    double[] extAccel; // halo réaliste : accélération du halo (fond soustrait) tabulée en r
    double extDr;

    float ExtAccel(float r)
    {
        int i = Mathf.Clamp((int)(r / extDr), 0, extAccel.Length - 1);
        return (float)extAccel[i];
    }

    // ------------------------------------------------------------ campagne : état

    class Scenario
    {
        public string name;
        public bool darkMatter;
        public HaloProfile profile;
        public bool freeze;
        public int seed;
        public LssModel lss;
        public float negRate = float.NaN;    // Janus grande échelle : vitesse initiale du secteur négatif (NaN = inchangée)
        public float sigma8 = float.NaN;     // Janus grande échelle : amplitude initiale σ₈ (NaN = valeur de l'onglet)
        public float kappa = float.NaN;      // Janus : c−/c+ (NaN = valeur de l'onglet), |M−| ajusté pour garder κ²|M−|
        public int negField = -1;            // Janus : champ initial négatif (−1 = valeur de l'onglet)
        public int janusIc = -1;             // Janus : conditions initiales (−1 = valeur de l'onglet)
        public int resLevel = 0;             // test de convergence : niveau de résolution (0 = réglages de l'onglet)
        public float ns = float.NaN;         // Janus : indice spectral (NaN = onglet)
        public float clock = float.NaN;      // Janus : horloge T−/T+ (NaN = onglet)
    }

    // Mesures accumulées pendant un run (fenêtre batchSummaryFrom → fin)
    class RunStats
    {
        public int n;
        public double A2in, A2out, A3out, thick, sigz, conf, drift, med25, med50;
        public double A2gas; public int nGasSamples;
        public double finA2gas = double.NaN, finGas = double.NaN, finYoung = double.NaN;
        public double finA2in, finA2out, finThick;
        public double[] finCurve;
        public float finT;
        // mode LargeScaleStructure : sommes (t ≥ batchSummaryFrom) et valeurs finales de chaque mesure
        public double[] lssSum, lssFin;
        public int[] lssCnt;
    }

    static readonly string[] StatNames =
        { "A2_in", "A2_out", "A3_out", "thickness_rms", "sigma_z", "confined_pct", "drift_speed",
          "medium_mass_r_half_disk", "medium_mass_r_disk", "A2_in_final", "A2_out_final", "thickness_final",
          "A2_out_gas", "A2_out_gas_final", "gas_frac_final", "young_frac_final" };

    System.Collections.Generic.List<Scenario> queue;
    System.Collections.Generic.List<RunStats> doneStats = new System.Collections.Generic.List<RunStats>();
    int runIndex = -1;
    int runToken;           // ignore les lectures GPU arrivant d'un run précédent
    bool finishing;         // dernière mesure demandée, en attente
    bool finalSampleDone;
    double nextSampleA;     // expansion auto-cohérente : prochaine mesure (en a)
    float readbackTime; int readbackStep; double readbackANeg;   // temps et pas au moment où la lecture GPU a été demandée
    RunStats stats;
    string campaignId, campaignFolder;
    float[] curveRadii;

    // ------------------------------------------------------------ init

    // ------------------------------------------------------------ pilotage (interface JanusUI)

    [UIHide]
    [Tooltip("Lance la simulation au démarrage. Décoché : attendre le bouton Lecture de l'interface.")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool autoStart = true;
    [UIHide]
    [Tooltip("Affichage texte des diagnostics par OnGUI (désactivé automatiquement par JanusUI)")]
    [UIPanel("Campagne", En = "Campaign", ShowIf = "batchMode", Single = true)]
    public bool legacyOnGUI = true;
    [System.NonSerialized] public bool paused;
    // fluidité de la vue (appelé par l'interface quand la caméra est manipulée)
    float viewInteractUntil;
    // Garde-fou GPU (Windows coupe la carte graphique si une image demande plus de ~2 s de calcul : « GPU Timeout ») :
    // le nombre de pas par image est réduit dès qu'une image devient lente, puis remonte doucement.
    int stepsCap = 1000000;
    public void NotifyViewInteraction() { viewInteractUntil = Time.unscaledTime + 0.6f; }

    public bool IsRunning => ready || preparing;
    public bool IsPreparing => preparing;
    public bool IsFinished { get; private set; }
    public string DiagnosticText =>
        IsRunning && !string.IsNullOrEmpty(launchPlanText) ? diagText + "\n\n" + launchPlanText : diagText;
    public string LastError { get; private set; } = "";
    public float SimTime => simTime;
    public int StepCount => stepCount;
    public string CampaignProgress =>
        batchMode && queue != null && runIndex >= 0 && runIndex < queue.Count
            ? $"run {runIndex + 1}/{queue.Count} — {queue[runIndex].name}, seed {seed}" : "";
    public Vector3 CameraTarget => followCamera != null ? smoothCenter : Vector3.zero;
    public Vector3 CameraOffset { get { return camOffset; } set { camOffset = value; } }

    // Conditions d'affichage utilisées par l'interface
    bool IsGalaxy => mode == InitMode.GalaxyInCavity;
    bool IsRealisticIC => lssInitialConditions == LssInitial.SpectreRealiste;
    // σ₈ du run : celui de Janus pour les modèles Janus, celui des témoins sinon
    float EffSigma8 => lssModel == LssModel.Janus ? lssSigma8Janus : lssSigma8;
    bool IsUniformSphere => darkMatterHalo && dmProfile == HaloProfile.UniformSphere;
    bool HasGas => gasFraction > 0f;
    bool DiskQZero => diskToomreQ <= 0f;

    bool camOffsetSet;
    float negRateBackup = float.NaN;           // valeur de l'onglet, rétablie après une campagne à vitesses multiples
    void RestoreNegRate()
    {
        if (!float.IsNaN(negRateBackup)) negativeExpansionRate = negRateBackup;
        negRateBackup = float.NaN;
        if (!float.IsNaN(sigma8Backup)) lssSigma8Janus = sigma8Backup;
        sigma8Backup = float.NaN;
        if (!float.IsNaN(kappaBackup)) { lightSpeedRatio = kappaBackup; negativeTotalMass = negMassBackup; }
        kappaBackup = float.NaN;
        if (negFieldBackup >= 0) lssNegField = (LssNegField)negFieldBackup;
        negFieldBackup = -1;
        if (janusIcBackup >= 0) lssJanusIc = (LssJanusIc)janusIcBackup;
        janusIcBackup = -1;
        if (resBackupGrid > 0) { gridSize = resBackupGrid; positiveCount = resBackupPos; negativeCount = resBackupNeg; }
        resBackupGrid = 0; icNyqOverride = 0;
        if (!float.IsNaN(nsBackup)) lssSpectralIndex = nsBackup;
        if (!float.IsNaN(clockBackup)) negativeClockRatio = clockBackup;
        nsBackup = float.NaN; clockBackup = float.NaN;
    }
    float kappaBackup = float.NaN, negMassBackup = float.NaN;   // c−/c+ et |M−| de l'onglet
    int negFieldBackup = -1;
    int janusIcBackup = -1;
    float sigma8Backup = float.NaN;            // σ₈ de l'onglet, rétabli après une campagne à σ₈ multiples

    void Start()
    {
        if (autoStart) StartSimulation();
    }

    // Lance une simulation (ou une campagne) avec les réglages actuels. false = réglages invalides (voir LastError).
    public bool StartSimulation()
    {
        StopSimulation();
        LastError = "";
        IsFinished = false;
        if (!SystemInfo.supportsComputeShaders)
            return Fail(L("cette plateforme ne gère pas les compute shaders. Dans un navigateur, il faut WebGPU " +
                          "(Chrome ou Edge récent, page en https).",
                          "this platform does not support compute shaders. In a browser, WebGPU is required " +
                          "(recent Chrome or Edge, page served over https)."));
        if (IsLss)
        {
            launchPlanText = LaunchPlan(out bool hBad) ?? "";
            if (hBad && !ignoreH0Guard)
            {
                Fail(L("lancement bloqué par le garde-fou H₀ (voir le plan ci-dessous ; cocher « Ignorer le garde-fou H₀ » pour forcer).",
                       "launch blocked by the H₀ guard (see the plan below; tick 'Ignore the H₀ guard' to force)."));
                diagText = L("ERREUR : ", "ERROR: ") + LastError + "\n\n" + launchPlanText;
                return false;
            }
        }
        else launchPlanText = "";
        if (Boot()) return true;
        ReleaseBuffers();
        diagText = L("ERREUR : ", "ERROR: ") + LastError;
        return false;
    }

    // Arrête et libère la simulation en cours (une campagne en cours est interrompue ; les runs terminés restent écrits)
    public void StopSimulation()
    {
        ReleaseBuffers();
        preparing = false; icTask = null; icCache = null; webIcJob = null;   // un calcul de conditions initiales en cours est abandonné
        runToken++;               // ignore les lectures GPU encore en vol
        readbackPending = false;
        finishing = false;
        paused = false;
        if (csv != null) { csv.Flush(); csv.Close(); csv = null; JanusWeb.Sync(); }
        RestoreNegRate();
    }

    // Valeurs par défaut du script (celles déclarées dans le code). Les références (shader, matériau...) sont conservées.
    public void ResetToDefaults()
    {
        var go = new GameObject("JanusDefaults");
        go.SetActive(false);                                // aucun Start / Update sur la copie
        var d = go.AddComponent<JanusPMSimulation>();
        foreach (var f in typeof(JanusPMSimulation).GetFields(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (typeof(Object).IsAssignableFrom(f.FieldType) || f.IsNotSerialized) continue;
            object v = f.GetValue(d);
            if (v is System.Array arr) v = arr.Clone();
            f.SetValue(this, v);
        }
        DestroyImmediate(go);
    }

    // Configuration de référence validée, selon le mode courant
    // Résumé de la référence appliquée (message de l'interface)
    public string ReferenceSummary
    {
        get
        {
            if (!IsLss)
                return L("Référence GALAXIE appliquée : 300 000 étoiles, 480 000 particules de milieu, c−/c+ = 1.",
                         "GALAXY reference applied: 300,000 stars, 480,000 medium particles, c−/c+ = 1.");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string v = lssReference == LssReference.Petit ? L("Janus selon Petit", "Janus per Petit")
                     : lssReference == LssReference.MeilleurSpectre ? L("Meilleur spectre", "Best spectrum")
                     : L("Historique du 15/10", "Historical 15/10");
            return L("Référence GRANDE ÉCHELLE appliquée — ", "LARGE-SCALE reference applied — ") + v + " : " +
                   $"c−/c+ = {lightSpeedRatio.ToString("0.##", ci)}, |M−| = {negativeTotalMass.ToString("0.###E0", ci)}, " +
                   $"ā/a₀ = {negativeScaleRatio.ToString("0.###", ci)}, q₀ = {janusQ0Target.ToString("0.###", ci)}, " +
                   L("Janus σ₈ = ", "Janus σ₈ = ") + lssSigma8Janus.ToString("0.###", ci) + ", ΛCDM σ₈ = " + lssSigma8.ToString("0.##", ci) +
                   L(", boîte ", ", box ") + lssBoxMpc.ToString("0", ci) + " Mpc, " + $"{positiveCount} + {negativeCount} " +
                   L("particules. Campagne : Janus + ΛCDM, listes vides. (Choix de la variante : onglet Scénario, « Référence grande échelle ».)",
                     "particles. Campaign: Janus + ΛCDM, empty lists. (Variant choice: Scenario tab, 'Large-scale reference'.)");
        }
    }

    public void ApplyReference()
    {
        if (IsLss) ApplyReferenceConfigLss(); else ApplyReferenceConfig();
    }

    [ContextMenu("Configuration de référence (grande échelle)")]
    public void ApplyReferenceConfigLss()
    {
        // Base commune (spectre réaliste 2LPT, facteur d'échelle propre, calage sur les densités, H ≈ 67 km/s/Mpc,
        // 96³ + 112³ particules, grille 256³, boîte de 400 Mpc), puis variante choisie dans lssReference (voir le switch).
        mode = InitMode.LargeScaleStructure;
        boxSize = 400f; gridSize = 256; smoothingLength = 1f;
        positiveCount = 884736; negativeCount = 1404928;  // 96³ et 112³ : réseaux complets (espacement 2,1 Mpc)
        // Masse : (c−/c+)² |M−| ≈ M+ (1 + 2|q₀|/Ω_b) ≈ 4,6 M+ pour garder H₀ = 67,7 km/s/Mpc
        positiveTotalMass = 35000f; negativeTotalMass = 16000000f;
        G = 1f; dt = 0.02f; stepsPerFrame = 1;
        lssModel = LssModel.Janus; lssMediumSigma = 0f; positiveSigma = 0f; lssRealisticRatio = 5f;   // départ froid
        expansionSelfConsistent = true; expansionFactorEnd = 5f; lssStartRedshift = 4f;   // remplacés par q₀ ci-dessous
        lssSamples = 20; lssSummaryFromA = 3f;
        // Conditions initiales réalistes (spectre ΛCDM, même champ pour tous les modèles)
        lssInitialConditions = LssInitial.SpectreRealiste; lssSigma8 = 0.81f; lssSigma8Janus = 0.3f; lssSpectralIndex = 0.965f;
        lssNegField = LssNegField.Independant; lssBoxMpc = 400f;   // 400 Mpc : statistique des amas (convergence vérifiée le 24/10)
        lss2LPT = true;
        // Secteur négatif (Petit)
        lightSpeedRatio = 0.1f;
        negativeOwnScaleFactor = true;
        negativeScaleTarget = NegScaleTarget.AuDepart;   // requis pour comparer des vitesses initiales en campagne
        negativeScaleRatio = 1f; negativeScaleRatioEnd = 0.3f; negativeExpansionRate = 1f;
        adaptiveTimestep = true;
        janusCalibration = JanusCalibration.Densites;
        janusMatchQ0 = true; janusQ0Target = -0.09f;
        // Témoins de campagne
        lssRunJanus = true; lssRunMatiereNoire = false; lssRunPositivesSeules = false; lssRunMatiereNoireRealiste = false;
        lssRunLambdaCdm = true;
        lssNegRates = new float[0]; lssJanusSigma8s = new float[0];   // listes vides : valeurs ci-dessus pour tous
        lssJanusKappas = new float[0]; lssResLevels = new int[0];
        lssJanusBothNegFields = false; lssJanusBothIc = false;
        lssJanusIc = LssJanusIc.VitessesNulles; lssJanusSigma8Today = false; janusPheno = false; negativeRateFromCurvature = false;
        negativeClockRatio = 1f; lssJanusNs = new float[0]; lssJanusClocks = new float[0];
        switch (lssReference)
        {
            case LssReference.Petit:
                // valeurs publiées les plus probables (c− ≈ 10 c+, ā/a ≈ 1/100) ; κ²|M−| = 4,6 M+ garde H₀ ≈ 67 ;
                // σ₈ = 0,04 : contraste de ΛCDM (campagne 20261024_180814)
                lightSpeedRatio = 10f; negativeTotalMass = 1600f; negativeScaleRatio = 0.01f;
                lssSigma8Janus = 0.04f; janusQ0Target = -0.09f;
                break;
            case LssReference.MeilleurSpectre:
                // départ z = 20 : forme du spectre la plus proche de ΛCDM (campagne 20261021_123506)
                lightSpeedRatio = 0.1f; negativeTotalMass = 7070000f; negativeScaleRatio = 1f;
                lssSigma8Janus = 0.1f; janusQ0Target = -0.025f;
                break;
            default:
                // configuration du 15/10/2026
                lssBoxMpc = 200f;
                break;
        }
        if (JanusWeb.IsWeb)
        {
            // version web allégée : grille 128³, 64³ particules ordinaires et 75³ de milieu (même rapport qu'en 96³ / 112³)
            gridSize = 128; positiveCount = 262144; negativeCount = 421875;
        }
        Debug.Log("Janus PM : configuration de référence (grande échelle) appliquée.");
    }

    static string L(string fr, string en) => JanusLang.L(fr, en);

    bool Fail(string msg)
    {
        LastError = msg;
        Debug.LogError("Janus PM : " + msg);
        return false;
    }

    bool Boot()
    {
        if (compute == null || instanceMesh == null || instanceMaterial == null)
            return Fail(L("assigne Compute (JanusPM.compute), Instance Mesh et Instance Material.", "assign Compute (JanusPM.compute), Instance Mesh and Instance Material."));
        string[] kernels = { "CSClearDensity", "CSDeposit", "CSDensityToComplex", "CSFFT",
                             "CSSolvePoisson", "CSGradient", "CSKick", "CSDrift",
                             "CSGasClear", "CSGasDeposit", "CSGasUpdate" };
        if (!compute.HasKernel("CSJanusV3"))
            return Fail(L("le compute shader assigné (" + compute.name + ") est une ancienne version. Assigne le dernier JanusPM.compute.",
                          "the assigned compute shader (" + compute.name + ") is an old version. Assign the latest JanusPM.compute."));
        foreach (var k in kernels)
            if (!compute.HasKernel(k))
            {
                return Fail("kernel " + k + L(" introuvable. Assigne JanusPM.compute et vérifie qu'il compile.", " not found. Assign JanusPM.compute and check that it compiles."));
            }
        if (!Mathf.IsPowerOfTwo(gridSize) || gridSize < 16 || gridSize > 256)
        {
            return Fail(L("la grille de calcul doit être une puissance de 2 entre 16 et 256.", "the computation grid must be a power of 2 between 16 and 256."));
        }
        if (mode == InitMode.GalaxyInCavity && cavityRadius >= boxSize * 0.5f)
        {
            return Fail(L("la cavité doit être plus petite que la demi-boîte.", "the cavity must be smaller than half the box."));
        }

        kClear = compute.FindKernel("CSClearDensity");
        kDeposit = compute.FindKernel("CSDeposit");
        kToComplex = compute.FindKernel("CSDensityToComplex");
        kFFT = compute.FindKernel("CSFFT");
        kPoisson = compute.FindKernel("CSSolvePoisson");
        kGradient = compute.FindKernel("CSGradient");
        kKick = compute.FindKernel("CSKick");
        kDrift = compute.FindKernel("CSDrift");
        kGasClear = compute.FindKernel("CSGasClear");
        kGasDeposit = compute.FindKernel("CSGasDeposit");
        kGasUpdate = compute.FindKernel("CSGasUpdate");
        if (!Mathf.IsPowerOfTwo(gasGridSize) || gasGridSize < 16 || gasGridSize > 256)
        {
            return Fail(L("la grille du gaz doit être une puissance de 2 entre 16 et 256.", "the gas grid must be a power of 2 between 16 and 256."));
        }

        if (positiveCount < 1 || negativeCount < 1) return Fail(L("il faut au moins une particule de chaque population.", "at least one particle of each population is needed."));
        if (followCamera != null && !camOffsetSet) { camOffset = followCamera.position; camOffsetSet = true; }
        ApplyQ0();

        if (batchMode)
        {
            negRateBackup = negativeExpansionRate;
            sigma8Backup = lssSigma8Janus;
            kappaBackup = lightSpeedRatio; negMassBackup = negativeTotalMass;
            negFieldBackup = (int)lssNegField;
            janusIcBackup = (int)lssJanusIc;
            resBackupGrid = gridSize; resBackupPos = positiveCount; resBackupNeg = negativeCount;
            nsBackup = lssSpectralIndex; clockBackup = negativeClockRatio;
            icNyqOverride = ResNyquist();
            runIndex = -1;
            doneStats.Clear();
            curveRadii = null;
            BuildQueue();
            if (queue.Count == 0)
                return Fail(L("campagne vide (aucune seed ou aucun modèle coché).", "empty campaign (no seed or no model selected)."));
            campaignId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string root = string.IsNullOrEmpty(csvFolder)
                ? JanusWeb.DefaultFolder("JanusRuns")
                : csvFolder;
            campaignFolder = System.IO.Path.Combine(root, "campagne_" + campaignId);
            System.IO.Directory.CreateDirectory(campaignFolder);
            Debug.Log($"Janus PM : campagne de {queue.Count} simulations → {campaignFolder}");
            StartNextRun();
        }
        else
        {
            if (!ValidateScenario()) return false;
            InitRun();
        }
        return true;
    }

    bool ValidateScenario()
    {
        if (IsLss && SelfConsistent && negativeOwnScaleFactor && lssModel == LssModel.Janus && !JanusPhenoOn)
        {
            var saved = lssModel; lssModel = LssModel.Janus;
            NegRun(System.Math.Max(1e-4, negativeScaleRatio), NegLambda(), out double aEndN, out double aMinN);
            lssModel = saved;
            if (aMinN < 1e-3 * negativeScaleRatio)
                return Fail(L("avec ces réglages, le secteur négatif s'effondre sur lui-même avant la fin (ā → 0). ",
                              "with these settings the negative sector collapses onto itself before the end (ā → 0). ") +
                            (NegTargetAtEnd
                                ? L("Le rapport ā/a final visé n'est atteignable qu'en recontraction. ", "The targeted final ā/a can only be reached by recollapse. ")
                                : L("La vitesse d'expansion initiale du secteur négatif est trop faible. ", "The initial expansion rate of the negative sector is too low. ")) +
                            L("Augmentez le rapport visé ou réglez la vitesse initiale à la main (« Fixé au départ »).",
                              "Increase the targeted ratio or set the initial rate by hand ('Fixed at start')."));
        }
        if (mode == InitMode.GalaxyInCavity && EffNegTotalMass <= positiveTotalMass)
            return Fail($"c−/c+ = {LightSpeedRatio:G3} : " +
                        L("la masse négative vue par la matière ordinaire", "the negative mass seen by ordinary matter") +
                        $" (|M−| × (c−/c+)² = {EffNegTotalMass:G4}) " + L("ne dépasse pas", "does not exceed") + $" M+ = {positiveTotalMass:G4} : " +
                        L("aucune cavité ne peut confiner la galaxie. Il faut |M−| > ", "no cavity can confine the galaxy. Required: |M−| > ") +
                        $"{positiveTotalMass / KappaSq:G4}.");
        if (darkMatterHalo && mode != InitMode.GalaxyInCavity)
        {
            return Fail(L("le halo de matière noire n'est disponible qu'en mode galaxie.", "the dark-matter halo is only available in galaxy mode."));
        }
        if (darkMatterHalo && dmProfile == HaloProfile.UniformSphere &&
            (dmHaloRadius <= diskRadius || dmHaloRadius >= boxSize * 0.5f))
        {
            return Fail(L("le rayon de la sphère doit être entre le rayon du disque et la demi-boîte.", "the sphere radius must lie between the disk radius and half the box."));
        }
        if (darkMatterHalo && dmProfile != HaloProfile.UniformSphere &&
            (dmTruncationRadius <= diskRadius || dmTruncationRadius >= boxSize * 0.5f))
        {
            return Fail(L("le rayon de troncature doit être entre le rayon du disque et la demi-boîte.", "the truncation radius must lie between the disk radius and half the box."));
        }
        return true;
    }

    void BuildQueue()
    {
        var models = new System.Collections.Generic.List<Scenario>();
        void Add(bool on, string name, bool dm, HaloProfile p, bool fz)
        {
            if (on) models.Add(new Scenario { name = name, darkMatter = dm, profile = p, freeze = fz });
        }
        bool legacy = !simplifiedModels;     // modèles historiques : seulement si le mode simplifié est désactivé
        Add(runJanusLibre, "Janus_libre", false, HaloProfile.NFW, false);
        Add(runJanusFige && legacy, "Janus_fige", false, HaloProfile.NFW, true);
        Add(runNFWLibre, "NFW_libre", true, HaloProfile.NFW, false);
        Add(runNFWFige && legacy, "NFW_fige", true, HaloProfile.NFW, true);
        Add(runIsothermeLibre && legacy, "Isotherme_libre", true, HaloProfile.CoredIsothermal, false);
        Add(runIsothermeFige && legacy, "Isotherme_fige", true, HaloProfile.CoredIsothermal, true);
        Add(runSphereLibre && legacy, "Sphere_libre", true, HaloProfile.UniformSphere, false);
        if (IsLss)
        {
            // Structure à grande échelle : les modèles de ce mode remplacent ceux du mode galaxie
            models.Clear();
            bool varyRates = lssNegRates != null && lssNegRates.Length > 0 && expansionSelfConsistent && negativeOwnScaleFactor && !NegTargetAtEnd;
            bool varyS8 = lssJanusSigma8s != null && lssJanusSigma8s.Length > 0 && IsRealisticIC;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (lssRunJanus)
            {
                float[] rates = varyRates ? lssNegRates : new[] { float.NaN };
                float[] s8s = varyS8 ? lssJanusSigma8s : new[] { float.NaN };
                bool varyK = lssJanusKappas != null && lssJanusKappas.Length > 0;
                float[] ks = varyK ? lssJanusKappas : new[] { float.NaN };
                int[] fields = lssJanusBothNegFields && IsRealisticIC ? new[] { 0, 1 } : new[] { -1 };
                int[] ics = lssJanusBothIc && IsRealisticIC ? new[] { 0, 1 } : new[] { -1 };
                float[] nss = lssJanusNs != null && lssJanusNs.Length > 0 && IsRealisticIC ? lssJanusNs : new[] { float.NaN };
                float[] clks = lssJanusClocks != null && lssJanusClocks.Length > 0 ? lssJanusClocks : new[] { float.NaN };
                foreach (float nsv in nss)
                    foreach (float clk in clks)
                        foreach (float r in rates)
                            foreach (float s8 in s8s)
                                foreach (float kp in ks)
                                    foreach (int nf in fields)
                                        foreach (int ic in ics)
                                            models.Add(new Scenario
                                            {
                                                name = "LSS_Janus" + (float.IsNaN(r) ? "" : "_v" + r.ToString("0.###", ci))
                                                                   + (float.IsNaN(s8) ? "" : "_s8_" + s8.ToString("0.###", ci))
                                                                   + (float.IsNaN(kp) ? "" : "_k" + kp.ToString("0.###", ci))
                                                                   + (nf < 0 ? "" : nf == 0 ? "_ind" : "_anti")
                                                                   + (ic < 0 ? "" : ic == 0 ? "_v0" : "_mc")
                                                                   + (float.IsNaN(nsv) ? "" : "_ns" + nsv.ToString("0.###", ci))
                                                                   + (float.IsNaN(clk) ? "" : "_T" + clk.ToString("0.###", ci)),
                                                lss = LssModel.Janus,
                                                negRate = r,
                                                sigma8 = s8,
                                                kappa = kp,
                                                negField = nf,
                                                janusIc = ic,
                                                ns = nsv,
                                                clock = clk
                                            });
            }
            if (lssRunMatiereNoire && legacy) models.Add(new Scenario { name = "LSS_MatiereNoire", lss = LssModel.MatiereNoire });
            if (lssRunPositivesSeules && legacy) models.Add(new Scenario { name = "LSS_PositivesSeules", lss = LssModel.PositivesSeules });
            if (lssRunMatiereNoireRealiste && legacy)
                models.Add(new Scenario { name = "LSS_MatiereNoireRealiste", lss = LssModel.MatiereNoireRealiste });
            if (lssRunLambdaCdm)
                models.Add(new Scenario { name = "LSS_LambdaCDM", lss = LssModel.LambdaCDM });
            // test de convergence : chaque modèle à chaque niveau de résolution
            var lv = new System.Collections.Generic.List<int>();
            if (lssResLevels != null) foreach (int l in lssResLevels) if (l >= 1 && l <= 3 && !lv.Contains(l)) lv.Add(l);
            if (lv.Count > 0)
            {
                var expanded = new System.Collections.Generic.List<Scenario>();
                foreach (var m in models)
                    foreach (int l in lv)
                        expanded.Add(new Scenario
                        {
                            name = m.name + "_r" + l,
                            lss = m.lss,
                            negRate = m.negRate,
                            sigma8 = m.sigma8,
                            kappa = m.kappa,
                            negField = m.negField,
                            janusIc = m.janusIc,
                            resLevel = l,
                            ns = m.ns,
                            clock = m.clock
                        });
                models = expanded;
            }
        }

        // Ordre : seed par seed, tous les modèles pour chaque seed (résultats comparables dès le début)
        queue = new System.Collections.Generic.List<Scenario>();
        if (batchSeeds == null) return;
        foreach (int s in batchSeeds)
            foreach (var m in models)
                queue.Add(new Scenario { name = m.name, darkMatter = m.darkMatter, profile = m.profile, freeze = m.freeze, seed = s, lss = m.lss, negRate = m.negRate, sigma8 = m.sigma8, kappa = m.kappa, negField = m.negField, janusIc = m.janusIc, resLevel = m.resLevel, ns = m.ns, clock = m.clock });
    }

    // Réglages d'un run de campagne (utilisé aussi par le plan de lancement)
    void ApplyScenario(Scenario sc)
    {
        seed = sc.seed;
        darkMatterHalo = sc.darkMatter;
        dmProfile = sc.profile;
        freezeNegatives = sc.freeze;
        if (IsLss) { lssModel = sc.lss; darkMatterHalo = false; freezeNegatives = false; }
        if (IsLss && !float.IsNaN(sc.negRate)) negativeExpansionRate = sc.negRate;
        else if (IsLss && !float.IsNaN(negRateBackup)) negativeExpansionRate = negRateBackup;
        if (IsLss && !float.IsNaN(sc.sigma8)) lssSigma8Janus = sc.sigma8;            // σ₈ propre à ce run Janus
        else if (IsLss && !float.IsNaN(sigma8Backup)) lssSigma8Janus = sigma8Backup;  // valeur de l'onglet
        if (IsLss && !float.IsNaN(kappaBackup))
        {
            // c−/c+ : |M−| ajusté pour garder (c−/c+)² |M−| (donc l'expansion) identique à celle de l'onglet
            float k = float.IsNaN(sc.kappa) ? kappaBackup : Mathf.Clamp(sc.kappa, 0.01f, 10f);
            lightSpeedRatio = k;
            negativeTotalMass = negMassBackup * (kappaBackup * kappaBackup) / (k * k);
        }
        if (IsLss && negFieldBackup >= 0) lssNegField = sc.negField >= 0 ? (LssNegField)sc.negField : (LssNegField)negFieldBackup;
        if (IsLss && janusIcBackup >= 0) lssJanusIc = sc.janusIc >= 0 ? (LssJanusIc)sc.janusIc : (LssJanusIc)janusIcBackup;
        if (IsLss && !float.IsNaN(nsBackup)) lssSpectralIndex = !float.IsNaN(sc.ns) ? sc.ns : nsBackup;
        if (IsLss && !float.IsNaN(clockBackup)) negativeClockRatio = !float.IsNaN(sc.clock) ? sc.clock : clockBackup;
        if (IsLss && resBackupGrid > 0)
        {
            if (sc.resLevel > 0)
            {
                gridSize = ResGrid[sc.resLevel];
                positiveCount = ResSide[sc.resLevel] * ResSide[sc.resLevel] * ResSide[sc.resLevel];
                int ns = ResNegSide(ResSide[sc.resLevel]);
                negativeCount = ns * ns * ns;
            }
            else { gridSize = resBackupGrid; positiveCount = resBackupPos; negativeCount = resBackupNeg; }
        }
    }
    int resBackupGrid = 0, resBackupPos = 0, resBackupNeg = 0;
    float nsBackup = float.NaN, clockBackup = float.NaN;
    // coupure commune : niveau le plus grossier de la liste
    int ResNyquist()
    {
        int m = 0;
        if (IsLss && lssResLevels != null)
            foreach (int l in lssResLevels) if (l >= 1 && l <= 3) m = m == 0 ? ResSide[l] : Mathf.Min(m, ResSide[l]);
        return m;
    }

    // ------------------------------------------------------------ plan de lancement et garde-fou
    // Valeurs principales de chaque modèle avant le lancement ; bad = H final prévu à plus de 20 % de 67,7 km/s/Mpc.
    [UILabel("Ignorer le garde-fou H₀", En = "Ignore the H₀ guard")]
    [ShowIf("IsLss")]
    [Tooltip("Le lancement est bloqué si le H final prévu d'un modèle s'écarte de plus de 20 % de 67,7 km/s/Mpc " +
             "(par exemple c−/c+ changé dans l'onglet sans adapter |M−|). Cocher pour lancer quand même (essais volontaires).")]
    [TooltipEn("Launch is blocked if a model's predicted final H differs by more than 20% from 67.7 km/s/Mpc (e.g. c−/c+ changed in the tab without adapting |M−|). Tick to launch anyway (deliberate tests).")]
    [UIPanel("Scénario", En = "Scenario", Single = true)]
    public bool ignoreH0Guard = false;

    public string LaunchPlan(out bool bad)
    {
        bad = false;
        if (!IsLss) return null;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        // sauvegarde de tout ce que les scénarios modifient
        var saved = (lssModel, negativeExpansionRate, lssSigma8Janus, lightSpeedRatio, negativeTotalMass, lssNegField, lssJanusIc,
                     seed, darkMatterHalo, dmProfile, freezeNegatives, expansionFactorEnd, lssStartRedshift);
        var sb = new System.Text.StringBuilder();
        try
        {
            ApplyQ0();
            var list = new System.Collections.Generic.List<Scenario>();
            if (batchMode)
            {
                negRateBackup = negativeExpansionRate; sigma8Backup = lssSigma8Janus;
                kappaBackup = lightSpeedRatio; negMassBackup = negativeTotalMass;
                negFieldBackup = (int)lssNegField; janusIcBackup = (int)lssJanusIc;
                resBackupGrid = gridSize; resBackupPos = positiveCount; resBackupNeg = negativeCount;
                nsBackup = lssSpectralIndex; clockBackup = negativeClockRatio;
                BuildQueue();
                int nSeeds = batchSeeds != null ? batchSeeds.Length : 0;
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var q in queue) if (seen.Add(q.name)) list.Add(q);
                sb.Append(L("PLAN DE CAMPAGNE : ", "CAMPAIGN PLAN: ") + $"{queue.Count} runs = {list.Count} " +
                          L("modèle(s) × ", "model(s) × ") + $"{nSeeds} seed(s)\n");
            }
            else
            {
                list.Add(new Scenario { name = "LSS_" + lssModel, lss = lssModel, seed = seed });
                sb.Append(L("PLAN DU RUN :\n", "RUN PLAN:\n"));
            }
            sb.Append(L("Boîte ", "Box ") + $"{lssBoxMpc:0} Mpc, " + L("grille ", "grid ") + $"{gridSize}³, " +
                      $"{positiveCount} + {negativeCount} " + L("particules", "particles") +
                      (SelfConsistent ? L(", départ z = ", ", start z = ") + $"{lssStartRedshift:0.##}" : "") + "\n");
            foreach (var sc in list)
            {
                if (batchMode) ApplyScenario(sc); else lssModel = sc.lss;
                double hEnd = SelfConsistent && ExpansionOn ? HubbleKms(Hubble(ExpansionEndTime())) : double.NaN;
                bool janus = lssModel == LssModel.Janus;
                sb.Append($"• {sc.name} : ");
                if (sc.resLevel > 0) sb.Append(L("grille ", "grid ") + $"{gridSize}³, {positiveCount} + {negativeCount} part., ");
                if (janus)
                {
                    sb.Append($"c−/c+ = {LightSpeedRatio.ToString("0.###", ci)}, |M−| = {negativeTotalMass.ToString("0.###E0", ci)}, " +
                              $"κ²|M−|/M+ = {(KappaSq * negativeTotalMass / System.Math.Max(1e-30, positiveTotalMass)).ToString("0.##", ci)}");
                    if (NegOwnScale) sb.Append($", ā/a₀ = {negativeScaleRatio.ToString("0.###", ci)}, λ = {NegLambda().ToString("0.###", ci)}" +
                                               (negativeRateFromCurvature ? L(" (courbure)", " (curvature)") : ""));
                    if (Tau != 1.0) sb.Append($", T−/T+ = {Tau.ToString("0.###", ci)}");
                    if (IsRealisticIC) sb.Append($", n_s = {lssSpectralIndex.ToString("0.###", ci)}");
                    if (JanusPhenoOn) sb.Append(L(", PHÉNOMÉNOLOGIQUE (bascule z = ", ", PHENOMENOLOGICAL (switch z = ") + $"{janusPhenoZ.ToString("0.##", ci)})");
                    if (IsRealisticIC) sb.Append($", σ₈ = {EffSigma8.ToString("0.###", ci)}" + (lssJanusSigma8Today ? L(" (linéaire final)", " (final linear)") : "") +
                                                 (JanusGrowingMode ? L(", mode croissant", ", growing mode") : ""));
                }
                else sb.Append(IsRealisticIC ? $"σ₈ = {EffSigma8.ToString("0.###", ci)}" : "");
                sb.Append(", H final = " + (double.IsNaN(hEnd) ? "—" : hEnd.ToString("0.0", ci)) + " km/s/Mpc");
                if (!double.IsNaN(hEnd) && System.Math.Abs(hEnd / H0ObservedKms - 1) > 0.2)
                {
                    bad = true;
                    sb.Append(L("  ← H₀ FAUX", "  ← WRONG H₀"));
                }
                sb.Append("\n");
            }
            if (bad)
                sb.Append(L("ATTENTION : au moins un modèle a un H final à plus de 20 % de 67,7 km/s/Mpc. Cause habituelle : c−/c+ " +
                            "changé dans l'onglet sans adapter |M−| (passer plutôt par la liste « Janus : rapports c−/c+ »), " +
                            "ou q₀ changé sans adapter |M−|.",
                            "WARNING: at least one model has a final H more than 20% away from 67.7 km/s/Mpc. Usual cause: c−/c+ " +
                            "changed in the tab without adapting |M−| (use the 'Janus: c−/c+ ratios' list instead), or q₀ changed " +
                            "without adapting |M−|."));
        }
        catch (System.Exception e) { sb.Append("\n(" + e.Message + ")"); }
        finally
        {
            (lssModel, negativeExpansionRate, lssSigma8Janus, lightSpeedRatio, negativeTotalMass, lssNegField, lssJanusIc,
             seed, darkMatterHalo, dmProfile, freezeNegatives, expansionFactorEnd, lssStartRedshift) = saved;
            negRateBackup = float.NaN; sigma8Backup = float.NaN; kappaBackup = float.NaN; negMassBackup = float.NaN;
            negFieldBackup = -1; janusIcBackup = -1;
            if (resBackupGrid > 0) { gridSize = resBackupGrid; positiveCount = resBackupPos; negativeCount = resBackupNeg; }
            resBackupGrid = 0;
            if (!float.IsNaN(nsBackup)) lssSpectralIndex = nsBackup;
            if (!float.IsNaN(clockBackup)) negativeClockRatio = clockBackup;
            nsBackup = float.NaN; clockBackup = float.NaN;
            queue = null;
        }
        return sb.ToString();
    }
    string launchPlanText = "";

    void StartNextRun()
    {
        while (true)
        {
            runIndex++;
            if (runIndex >= queue.Count) { FinishCampaign(); return; }
            var sc = queue[runIndex];
            ApplyScenario(sc);
            if (ValidateScenario()) break;
            Debug.LogWarning($"Janus PM : run {sc.name} seed {sc.seed} ignoré (réglages invalides).");
            doneStats.Add(null);
        }
        InitRun();
        Debug.Log($"Janus PM : run {runIndex + 1}/{queue.Count} — {queue[runIndex].name}, seed {seed}");
    }

    // Prépare une simulation complète (appelée une fois hors campagne, à chaque run en campagne)
    void InitRun()
    {
        ReleaseBuffers();
        if (csv != null) { csv.Flush(); csv.Close(); csv = null; JanusWeb.Sync(); }
        // Conditions initiales réalistes : les FFT (plusieurs secondes) sont calculées dans un fil séparé,
        // l'interface reste réactive ; InitRun est rappelé par Update quand elles sont prêtes.
        if (IsLss && IsRealisticIC && (icCache == null || icCache.key != IcKey()))
        {
            StartIcTask();
            return;
        }
        csvPath = null;
        simTime = 0f;
        stepCount = 0;
        readbackPending = false;
        finishing = false;
        finalSampleDone = false;
        nextSampleA = 1.0;
        ResetNegScale();
        galaxyCenter = smoothCenter = Vector3.zero;
        runToken++;
        stats = new RunStats();
        diagText = "";

        total = positiveCount + negativeCount;
        particleGroups = Mathf.CeilToInt(total / 256f);
        g8 = gridSize / 8; g4 = gridSize / 4;
        int cells = gridSize * gridSize * gridSize;

        bodyBuffer = new ComputeBuffer(total, sizeof(float) * 7 + sizeof(int));
        densBuffer = new ComputeBuffer(cells * 3, sizeof(uint));
        gridBuffer = new ComputeBuffer(cells, sizeof(float) * 2);
        forceBuffer = new ComputeBuffer(cells, sizeof(float) * 4);
        forceNegBuffer = new ComputeBuffer(cells, sizeof(float) * 4);
        int gasCells = gasGridSize * gasGridSize * gasGridSize;
        gasGridBuffer = new ComputeBuffer(gasCells * 4, sizeof(int));

        bodyBuffer.SetData(CreateBodies());

        compute.SetBuffer(kClear, "_DensInt", densBuffer);
        compute.SetBuffer(kDeposit, "_Bodies", bodyBuffer);
        compute.SetBuffer(kDeposit, "_DensInt", densBuffer);
        compute.SetBuffer(kToComplex, "_DensInt", densBuffer);
        compute.SetBuffer(kToComplex, "_Grid", gridBuffer);
        compute.SetBuffer(kFFT, "_Grid", gridBuffer);
        compute.SetBuffer(kPoisson, "_Grid", gridBuffer);
        compute.SetBuffer(kGradient, "_Grid", gridBuffer);
        compute.SetBuffer(kGradient, "_Force", forceBuffer);
        compute.SetBuffer(kGradient, "_ForceNeg", forceNegBuffer);
        compute.SetBuffer(kKick, "_Bodies", bodyBuffer);
        compute.SetBuffer(kKick, "_Force", forceBuffer);
        compute.SetBuffer(kKick, "_ForceNeg", forceNegBuffer);
        compute.SetBuffer(kDrift, "_Bodies", bodyBuffer);
        compute.SetBuffer(kGasClear, "_GasGrid", gasGridBuffer);
        compute.SetBuffer(kGasDeposit, "_GasGrid", gasGridBuffer);
        compute.SetBuffer(kGasDeposit, "_Bodies", bodyBuffer);
        compute.SetBuffer(kGasUpdate, "_GasGrid", gasGridBuffer);
        compute.SetBuffer(kGasUpdate, "_Bodies", bodyBuffer);

        PushParams();

        instanceMaterial.SetBuffer("_Bodies", bodyBuffer);
        instanceMaterial.SetFloat("_RenderScale", renderScale);
        InitArgs();

        // Demi-kick initial (leapfrog)
        curDt = StepDt(0.0, NegOwnScale ? negA : 1.0);
        compute.SetFloat("_Dt", curDt);
        ComputeField();
        compute.SetFloat("_KickDt", 0.5f * curDt);
        if (ExpansionOn) SetExpansionKick(0.0, 0.5f * curDt);
        compute.Dispatch(kKick, particleGroups, 1, 1);

        ready = true;
    }

    void ReleaseBuffers()
    {
        ready = false;
        bodyBuffer?.Release(); bodyBuffer = null;
        densBuffer?.Release(); densBuffer = null;
        gridBuffer?.Release(); gridBuffer = null;
        forceBuffer?.Release(); forceBuffer = null;
        forceNegBuffer?.Release(); forceNegBuffer = null;
        gasGridBuffer?.Release(); gasGridBuffer = null;
        argsBuffer?.Release(); argsBuffer = null;
    }

    void PushParams()
    {
        compute.SetInt("_Count", total);
        compute.SetInt("_N", gridSize);
        compute.SetInt("_LogN", Mathf.RoundToInt(Mathf.Log(gridSize, 2)));
        compute.SetFloat("_L", boxSize);
        compute.SetFloat("_G", G);
        compute.SetFloat("_Dt", dt);
        compute.SetFloat("_MassPos", positiveTotalMass / positiveCount);
        compute.SetFloat("_MassNeg", NegMassParam);
        compute.SetFloat("_MassHalo", HaloMassParam);
        compute.SetFloat("_GravScale", 1f);
        compute.SetFloat("_VelDamp", 1f);
        compute.SetFloat("_GravScaleNeg", 1f);
        compute.SetFloat("_VelDampNeg", 1f);
        compute.SetFloat("_CrossPos", (float)KappaSq);
        compute.SetFloat("_CrossNeg", (float)(1.0 / KappaSq));
        compute.SetFloat("_FixScale", FixScale);
        compute.SetFloat("_SmoothLen", smoothingLength);
        compute.SetInt("_FreezeNegatives", freezeNegatives ? 1 : 0);
        compute.SetInt("_NG", gasGridSize);
        compute.SetInt("_GasN", GasCount);
        compute.SetFloat("_GasFix", GasFix);
        compute.SetFloat("_GasCoolTime", Mathf.Max(gasCoolingTime, 1e-3f));
        compute.SetFloat("_GasSoundSpeed", gasSoundSpeed);
        compute.SetInt("_StarFormation", starFormation ? 1 : 0);
        compute.SetFloat("_SFTime", Mathf.Max(starFormationTime, 1e-3f));
        compute.SetFloat("_SFThreshold", starFormationThreshold);
        compute.SetFloat("_YoungLifetime", youngStarLifetime);
        compute.SetFloat("_RecycleFraction", gasRecycleFraction);
    }

    void InitArgs()
    {
        args[0] = instanceMesh.GetIndexCount(0);
        args[1] = (uint)(showNegatives ? total : positiveCount);
        args[2] = instanceMesh.GetIndexStart(0);
        args[3] = instanceMesh.GetBaseVertex(0);
        args[4] = 0;
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        lastShowNegatives = showNegatives;
    }

    // ------------------------------------------------------------ conditions initiales

    static float Gauss()
    {
        float u1 = Mathf.Max(Random.value, 1e-7f), u2 = Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }
    static Vector3 Gauss3() { return new Vector3(Gauss(), Gauss(), Gauss()); }
    static Vector3 RandomInBox(float hl)
    {
        return new Vector3(Random.Range(-hl, hl), Random.Range(-hl, hl), Random.Range(-hl, hl));
    }

    Body[] CreateBodies()
    {
        Random.InitState(seed);
        var b = new Body[total];
        float hl = boxSize * 0.5f;
        float mP = positiveTotalMass / positiveCount;
        float mN = negativeTotalMass / negativeCount;

        // Positives d'abord (permet de masquer les négatives au rendu)
        for (int i = 0; i < positiveCount; i++)
        {
            Vector3 p, v = Vector3.zero;
            if (mode == InitMode.LargeScaleStructure)
            {
                p = RandomInBox(hl);
                v = Gauss3() * positiveSigma;
            }
            else
            {
                float Rd = diskRadius / 3f;
                float r;
                if (diskExponentialFix)
                {
                    // Densité de surface Σ ∝ exp(−r/Rd) : le rayon suit une loi Gamma(2, Rd), tronquée au bord
                    do { r = -Rd * Mathf.Log(Mathf.Max(Random.value, 1e-7f) * Mathf.Max(Random.value, 1e-7f)); }
                    while (r > diskRadius);
                }
                else
                {
                    r = Mathf.Min(-Rd * Mathf.Log(1f - Random.value * 0.9999f), diskRadius);   // ancien tirage
                }
                float a = Random.value * 2f * Mathf.PI;
                p = new Vector3(r * Mathf.Cos(a), Gauss() * diskThickness, r * Mathf.Sin(a));
            }
            bool gas = i < GasCount;
            if (gas && diskThickness > 0f) p.y *= gasThickness / diskThickness;   // aucun tirage aléatoire en plus
            b[i] = new Body { pos = p, vel = v, mass = mP, sign = gas ? 3 : +1 };
        }

        for (int j = 0; j < negativeCount; j++)
        {
            Vector3 p;
            do { p = RandomInBox(hl); }
            while (mode == InitMode.GalaxyInCavity && p.magnitude < cavityRadius);

            float sig = IsLss ? lssMediumSigma : negativeSigma;
            int medSign = IsLss && lssModel != LssModel.Janus ? 2 : -1;   // témoins : milieu positif (ou traceur sans masse)
            b[positiveCount + j] = new Body { pos = p, vel = Gauss3() * sig, mass = mN, sign = medSign };
        }

        if (IsLss && IsRealisticIC) ApplyRealisticIC(b);

        // Les masses négatives ci-dessus sont tirées dans tous les cas : ainsi, pour un même seed,
        // les positions du disque et ses tirages aléatoires sont EXACTEMENT les mêmes dans tous les modèles.
        haloMass = 0f;
        extAccel = null;
        var diskRandomState = Random.state;

        // Halo réaliste : créé AVANT les vitesses du disque, pour que le disque démarre
        // en équilibre dans le champ réel de ce halo (et non dans celui de la cavité Janus).
        if (darkMatterHalo && dmProfile != HaloProfile.UniformSphere) CreateRealisticHalo(b);

        Random.state = diskRandomState;
        if (mode == InitMode.GalaxyInCavity) SetDiskVelocities(b);

        if (darkMatterHalo && dmProfile == HaloProfile.UniformSphere) CreateDarkMatterHalo(b);
        return b;
    }

    // ------------------------------------------------------------ conditions initiales réalistes (Zel'dovich)
    //
    // Champ gaussien δ de spectre P(k) = A k^n_s T²(k) (Eisenstein & Hu 1998, sans oscillations baryoniques),
    // normalisé par σ₈ × D(z_départ) (croissance ΛCDM). Déplacement ψ_k = i k δ_k / k² (δ = −∇·ψ),
    // particules sur réseau régulier : x = q + ψ(q), v = f H ψ (Einstein-de Sitter : f = 1 ; Janus au rebond : H = 0).
    // Unités du spectre : k en h/Mpc, P en (Mpc/h)³.
    const double IcH = 0.677, IcOm = 0.31, IcOb = 0.049;
    const int IcGrid = 128;
    string icInfo = "";

    static double TransferEH(double kh)   // kh en h/Mpc
    {
        double omh2 = IcOm * IcH * IcH, obh2 = IcOb * IcH * IcH, fb = IcOb / IcOm;
        double theta = 2.7255 / 2.7;
        double sound = 44.5 * System.Math.Log(9.83 / omh2) / System.Math.Sqrt(1 + 10 * System.Math.Pow(obh2, 0.75));   // Mpc
        double alpha = 1 - 0.328 * System.Math.Log(431 * omh2) * fb + 0.38 * System.Math.Log(22.3 * omh2) * fb * fb;
        double k = kh * IcH;                                                     // Mpc⁻¹
        double gam = IcOm * IcH * (alpha + (1 - alpha) / (1 + System.Math.Pow(0.43 * k * sound, 4)));
        double q = kh * theta * theta / gam;
        double L0 = System.Math.Log(2 * System.Math.E + 1.8 * q), C0 = 14.2 + 731 / (1 + 62.5 * q);
        return L0 / (L0 + C0 * q * q);
    }

    // Facteur de croissance linéaire ΛCDM D(z)/D(0) (Carroll, Press & Turner 1992)
    static double GrowthLcdm(double z)
    {
        double OL = 1 - IcOm;
        double Gf(double zz)
        {
            double a3 = System.Math.Pow(1 + zz, 3), e2 = IcOm * a3 + OL;
            double om = IcOm * a3 / e2, ol = OL / e2;
            return 2.5 * om / (System.Math.Pow(om, 4.0 / 7.0) - ol + (1 + om / 2) * (1 + ol / 70));
        }
        return Gf(z) / (Gf(0) * (1 + z));
    }

    // Constante A telle que σ(8 Mpc/h) = sigma pour P(k) = A k^ns T²(k)
    static double SpectrumAmplitude(double sigma, double ns)
    {
        double sum = 0, lnMin = System.Math.Log(1e-5), lnMax = System.Math.Log(1e3);
        int n = 4000; double dl = (lnMax - lnMin) / n;
        for (int i = 0; i < n; i++)
        {
            double k = System.Math.Exp(lnMin + (i + 0.5) * dl), x = 8 * k;
            double w = 3 * (System.Math.Sin(x) - x * System.Math.Cos(x)) / (x * x * x);
            double t = TransferEH(k);
            sum += k * k * k * System.Math.Pow(k, ns) * t * t * w * w * dl;   // k³ P W² d ln k
        }
        return sigma * sigma / (sum / (2 * System.Math.PI * System.Math.PI));
    }

    static void Fft1(double[] re, double[] im, bool inverse)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = 2 * System.Math.PI / len * (inverse ? 1 : -1);
            double wr = System.Math.Cos(ang), wi = System.Math.Sin(ang);
            int half = len >> 1;
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < half; k++)
                {
                    int u = i + k, v = u + half;
                    double xr = re[v] * cr - im[v] * ci, xi = re[v] * ci + im[v] * cr;
                    re[v] = re[u] - xr; im[v] = im[u] - xi;
                    re[u] += xr; im[u] += xi;
                    double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                }
            }
        }
    }

    // FFT 3D non normalisée, indice = x + N (y + N z)
    static void Fft3(double[] re, double[] im, int N, bool inverse)
    {
        var lr = new double[N]; var li = new double[N];
        for (int axis = 0; axis < 3; axis++)
        {
            int stride = axis == 0 ? 1 : axis == 1 ? N : N * N;
            for (int a = 0; a < N; a++)
                for (int c = 0; c < N; c++)
                {
                    int b0 = axis == 0 ? N * (a + N * c) : axis == 1 ? a + N * N * c : a + N * c;
                    for (int k = 0; k < N; k++) { lr[k] = re[b0 + k * stride]; li[k] = im[b0 + k * stride]; }
                    Fft1(lr, li, inverse);
                    for (int k = 0; k < N; k++) { re[b0 + k * stride] = lr[k]; im[b0 + k * stride] = li[k]; }
                }
        }
    }

    // Champ de déplacement ψ (3 composantes, en Mpc/h) sur une grille N³ couvrant Lh (Mpc/h)
    // i k X / k² (× factor) pour les 3 axes, en espace réel : ψ = −∇ ∇⁻² X  (δ = −∇·ψ)
    static float[][] GradInvLap(double[] re, double[] im, double[] kxs, int N, double factor)
    {
        int N3 = N * N * N;
        var psi = new float[3][];
        var wre = new double[N3]; var wim = new double[N3];
        for (int axis = 0; axis < 3; axis++)
        {
            for (int z = 0; z < N; z++)
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        int id = x + N * (y + N * z);
                        double k2 = kxs[x] * kxs[x] + kxs[y] * kxs[y] + kxs[z] * kxs[z];
                        double ka = axis == 0 ? kxs[x] : axis == 1 ? kxs[y] : kxs[z];
                        double g = k2 > 0 ? factor * ka / k2 : 0;
                        wre[id] = -g * im[id];        // i·g·(re + i im) = −g im + i g re
                        wim[id] = g * re[id];
                    }
            Fft3(wre, wim, N, true);
            var comp = new float[N3];
            for (int i = 0; i < N3; i++) comp[i] = (float)(wre[i] / N3);
            psi[axis] = comp;
        }
        return psi;
    }

    // k_a k_b / k² X en espace réel (= ∂_a ∂_b φ avec ∇²φ = X)
    static float[] Deriv2(double[] re, double[] im, double[] kxs, int N, int a, int b)
    {
        int N3 = N * N * N;
        var wre = new double[N3]; var wim = new double[N3];
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int id = x + N * (y + N * z);
                    double k2 = kxs[x] * kxs[x] + kxs[y] * kxs[y] + kxs[z] * kxs[z];
                    double ka = a == 0 ? kxs[x] : a == 1 ? kxs[y] : kxs[z];
                    double kb = b == 0 ? kxs[x] : b == 1 ? kxs[y] : kxs[z];
                    double g = k2 > 0 ? ka * kb / k2 : 0;
                    wre[id] = g * re[id]; wim[id] = g * im[id];
                }
        Fft3(wre, wim, N, true);
        var o = new float[N3];
        for (int i = 0; i < N3; i++) o[i] = (float)(wre[i] / N3);
        return o;
    }

    // Champs de déplacement (Mpc/h) sur une grille N³ couvrant Lh (Mpc/h).
    //  ψ1 : Zel'dovich, ψ1 = −∇φ1, ∇²φ1 = δ.
    //  ψ2 : 2e ordre (2LPT, Scoccimarro 1998), ψ2 = −(3/7) ∇φ2, ∇²φ2 = Σ_{i<j} (φ1,ii φ1,jj − φ1,ij²).
    static void DisplacementField(int N, double Lh, double A, double ns, double kCut, System.Random rng, bool lpt2,
                                  out float[][] psi1, out float[][] psi2, out double deltaRms)
    {
        int N3 = N * N * N;
        var dre = new double[N3]; var dim = new double[N3];
        for (int i = 0; i < N3; i++)
        {
            double u1 = System.Math.Max(rng.NextDouble(), 1e-12), u2 = rng.NextDouble();
            dre[i] = System.Math.Sqrt(-2 * System.Math.Log(u1)) * System.Math.Cos(2 * System.Math.PI * u2);
        }
        Fft3(dre, dim, N, false);
        double V = Lh * Lh * Lh, dk = 2 * System.Math.PI / Lh;
        var kxs = new double[N];
        for (int i = 0; i < N; i++) kxs[i] = (i <= N / 2 ? i : i - N) * dk;
        double pow = 0;
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int id = x + N * (y + N * z);
                    double k2 = kxs[x] * kxs[x] + kxs[y] * kxs[y] + kxs[z] * kxs[z];
                    double k = System.Math.Sqrt(k2);
                    bool nyq = x == N / 2 || y == N / 2 || z == N / 2;
                    if (k2 == 0 || k > kCut || nyq) { dre[id] = dim[id] = 0; continue; }
                    double t = TransferEH(k);
                    double f = System.Math.Sqrt(N3 * A * System.Math.Pow(k, ns) * t * t / V);
                    dre[id] *= f; dim[id] *= f;
                    pow += dre[id] * dre[id] + dim[id] * dim[id];
                }
        deltaRms = System.Math.Sqrt(pow) / N3;              // Parseval : σ² = Σ|δ_k|² / N⁶
        psi1 = GradInvLap(dre, dim, kxs, N, 1.0);
        psi2 = null;
        if (!lpt2) return;

        var pxx = Deriv2(dre, dim, kxs, N, 0, 0);
        var pyy = Deriv2(dre, dim, kxs, N, 1, 1);
        var pzz = Deriv2(dre, dim, kxs, N, 2, 2);
        var src = new double[N3];
        for (int i = 0; i < N3; i++) src[i] = (double)pxx[i] * pyy[i] + (double)pxx[i] * pzz[i] + (double)pyy[i] * pzz[i];
        pxx = pyy = pzz = null;
        var pxy = Deriv2(dre, dim, kxs, N, 0, 1);
        for (int i = 0; i < N3; i++) src[i] -= (double)pxy[i] * pxy[i];
        pxy = Deriv2(dre, dim, kxs, N, 0, 2);
        for (int i = 0; i < N3; i++) src[i] -= (double)pxy[i] * pxy[i];
        pxy = Deriv2(dre, dim, kxs, N, 1, 2);
        for (int i = 0; i < N3; i++) src[i] -= (double)pxy[i] * pxy[i];
        pxy = null;
        var sim = new double[N3];
        Fft3(src, sim, N, false);
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int id = x + N * (y + N * z);
                    double k = System.Math.Sqrt(kxs[x] * kxs[x] + kxs[y] * kxs[y] + kxs[z] * kxs[z]);
                    if (x == N / 2 || y == N / 2 || z == N / 2 || k > 2 * kCut) { src[id] = sim[id] = 0; }
                }
        // ψ2 = −(3/7) ∇φ2 = (3/7) · i k S / k²  (même opérateur que ψ1, appliqué à S)
        psi2 = GradInvLap(src, sim, kxs, N, 3.0 / 7.0);
    }

    static Vector3 SamplePsi(float[][] psi, int N, double gx, double gy, double gz)
    {
        int x0 = (int)System.Math.Floor(gx), y0 = (int)System.Math.Floor(gy), z0 = (int)System.Math.Floor(gz);
        float fx = (float)(gx - x0), fy = (float)(gy - y0), fz = (float)(gz - z0);
        Vector3 r = Vector3.zero;
        for (int c = 0; c < 8; c++)
        {
            int ox = c & 1, oy = (c >> 1) & 1, oz = (c >> 2) & 1;
            float w = (ox == 1 ? fx : 1 - fx) * (oy == 1 ? fy : 1 - fy) * (oz == 1 ? fz : 1 - fz);
            int ix = ((x0 + ox) % N + N) % N, iy = ((y0 + oy) % N + N) % N, iz = ((z0 + oz) % N + N) % N;
            int id = ix + N * (iy + N * iz);
            r += w * new Vector3(psi[0][id], psi[1][id], psi[2][id]);
        }
        return r;
    }

    // Place count particules (à partir de start) sur un réseau n³ (sous-échantillonné régulièrement si n³ > count)
    double PlaceOnLattice(Body[] b, int start, int count, float[][] psi, float[][] psi2, float sign, double Lh,
                          float velFactor, float velFactor2, float sigma, float offset)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(count, 1f / 3f) - 1e-3f));
        while ((long)n * n * n < count) n++;
        long sites = (long)n * n * n;
        float hl = boxSize * 0.5f;
        double cell = Lh / n, toCode = boxSize / Lh;
        double sum2 = 0; int placed = 0;
        for (long s = 0; s < sites && placed < count; s++)
        {
            // sélection régulière : on garde count sites parmi n³, répartis uniformément
            if ((s + 1) * count / sites == s * count / sites) continue;
            int ix = (int)(s % n), iy = (int)((s / n) % n), iz = (int)(s / ((long)n * n));
            double qx = (ix + offset) * cell, qy = (iy + offset) * cell, qz = (iz + offset) * cell;
            Vector3 d1 = sign * SamplePsi(psi, IcGrid, qx / Lh * IcGrid, qy / Lh * IcGrid, qz / Lh * IcGrid);   // Mpc/h
            // 2e ordre : quadratique en δ (facteur sign², de même signe pour un champ anticorrélé)
            Vector3 d2 = psi2 != null ? sign * sign * SamplePsi(psi2, IcGrid, qx / Lh * IcGrid, qy / Lh * IcGrid, qz / Lh * IcGrid) : Vector3.zero;
            Vector3 d = d1 + d2;
            sum2 += d.sqrMagnitude;
            Vector3 dc = d * (float)toCode;
            Vector3 p = new Vector3((float)(qx * toCode) - hl, (float)(qy * toCode) - hl, (float)(qz * toCode) - hl) + dc;
            p.x = Mathf.Repeat(p.x + hl, boxSize) - hl;
            p.y = Mathf.Repeat(p.y + hl, boxSize) - hl;
            p.z = Mathf.Repeat(p.z + hl, boxSize) - hl;
            var body = b[start + placed];
            body.pos = p;
            body.vel = d1 * (float)toCode * velFactor + d2 * (float)toCode * velFactor2 + (sigma > 0 ? Gauss3() * sigma : Vector3.zero);
            b[start + placed] = body;
            placed++;
        }
        return placed > 0 ? System.Math.Sqrt(sum2 / placed) / cell : 0;   // déplacement rms en espacements du réseau
    }

    // ---- calcul des champs initiaux en arrière-plan
    class IcFields { public string key; public float[][] psiB, psiB2, psiM, psiM2; public double dRms; }
    class IcJob { public string key; public double Lh, A, ns, kCut; public int seed; public bool lpt2, ownNeg; }
    IcFields icCache;
    System.Threading.Tasks.Task<IcFields> icTask;
    IcJob webIcJob; int webIcWait;                           // version web : calcul différé sur le fil principal
    bool preparing;

    string IcKey() => $"{lssBoxMpc}|{icNyqOverride}|{StartSigma8Pos():R}|{lssSpectralIndex}|{lssStartRedshift}|{positiveCount}|{negativeCount}|{seed}|" +
                      $"{lss2LPT}|{OwnNegField}";
    bool OwnNegField => lssModel == LssModel.Janus && lssNegField == LssNegField.Independant && !JanusGrowingMode && !JanusPhenoOn;

    IcJob MakeIcJob()
    {
        double Lh = System.Math.Max(1.0, lssBoxMpc) * IcH;
        int nB = Mathf.CeilToInt(Mathf.Pow(positiveCount, 1f / 3f) - 1e-3f);
        int nM = Mathf.CeilToInt(Mathf.Pow(negativeCount, 1f / 3f) - 1e-3f);
        return new IcJob
        {
            key = IcKey(),
            Lh = Lh,
            ns = lssSpectralIndex,
            A = SpectrumAmplitude(StartSigma8Pos(), lssSpectralIndex),
            kCut = System.Math.PI * Mathf.Min(Mathf.Min(Mathf.Min(nB, nM), IcGrid), icNyqOverride > 0 ? icNyqOverride : IcGrid) / Lh,
            seed = seed,
            lpt2 = lss2LPT,
            ownNeg = OwnNegField
        };
    }

    // Uniquement du calcul pur (aucun appel à Unity) : exécutable hors du fil principal
    static IcFields ComputeIcFields(IcJob j)
    {
        var f = new IcFields { key = j.key };
        DisplacementField(IcGrid, j.Lh, j.A, j.ns, j.kCut, new System.Random(j.seed * 104729 + 11), j.lpt2,
                          out f.psiB, out f.psiB2, out f.dRms);
        f.psiM = f.psiB; f.psiM2 = f.psiB2;
        if (j.ownNeg)
            DisplacementField(IcGrid, j.Lh, j.A, j.ns, j.kCut, new System.Random(j.seed * 7919 + 5), j.lpt2,
                              out f.psiM, out f.psiM2, out _);
        return f;
    }

    void StartIcTask()
    {
        var job = MakeIcJob();                                  // paramètres lus sur le fil principal
        preparing = true;
        if (JanusWeb.IsWeb) { webIcJob = job; webIcWait = 2; icTask = null; }   // navigateur : pas de fil de calcul
        else icTask = System.Threading.Tasks.Task.Run(() => ComputeIcFields(job));
        diagText = L("Préparation des conditions initiales (spectre réaliste", "Preparing initial conditions (realistic spectrum") +
                   (lss2LPT ? ", 2LPT" : "") + L(") : calcul des champs en cours…", "): computing the fields…") +
                   (batchMode && queue != null && runIndex >= 0 && runIndex < queue.Count
                       ? $"\n{L("CAMPAGNE", "CAMPAIGN")} : run {runIndex + 1}/{queue.Count} — {queue[runIndex].name}, seed {seed}" : "");
    }

    // ------------------------------------------------------------ théorie linéaire à deux fluides (Janus)
    // Contrastes de densité comobiles, avec exactement les équations que le calcul N corps intègre
    // (coordonnées comobiles, chaque secteur avec son propre facteur d'échelle, couplages croisés κ² et 1/κ²) :
    //   δ̈+ + 2H δ̇+ = 4πG/a³ (ρ+ δ+ − κ² ρ− δ−)
    //   δ̈− + 2H̄ δ̇− = 4πG/ā³ (ρ− δ− − ρ+ δ+ / κ²)
    // ρ± : densités comobiles moyennes (ρ− en valeur absolue). Sans pression, aucun coefficient ne dépend de k :
    // une seule intégration donne la croissance linéaire à toutes les échelles.
    class LinearGrowth
    {
        public string key;
        public bool ok;
        public double tEnd, aBarEnd;
        public double[,] R = new double[2, 4];   // (δ+, δ−) en fin de run pour δ+₀ = 1, δ−₀ = 1, δ̇+₀ = 1, δ̇−₀ = 1
        public double gamma, ratio;               // mode croissant au départ : δ̇ = γ δ pour les deux secteurs, δ− = ratio × δ+
        public double growthZeroVel;              // croissance rms de δ+ (départ → fin), vitesses nulles, champ négatif de l'onglet
        public double growthMode;                 // croissance de δ+ dans le mode croissant
        public double negEndPerPos;               // mode croissant : δ−/δ+ en fin de run
    }
    LinearGrowth linCache;

    LinearGrowth LinearJanus()
    {
        bool own = NegOwnScale;
        double lam = own ? NegLambda() : 0;
        string key = $"{RhoPos:R}|{RhoMedium:R}|{KappaSq:R}|{G}|{expansionFactorEnd}|{own}|{negativeScaleRatio}|{lam:R}|{Tau:R}|" +
                     $"{lssNegField}|{janusCalibration}|{SelfConsistent}|{ExpansionOn}|{lssModel}";
        if (linCache != null && linCache.key == key) return linCache;
        var res = new LinearGrowth { key = key };
        linCache = res;
        if (!ExpansionOn || lssModel != LssModel.Janus || JanusPhenoOn) return res;
        double tE = ExpansionEndTime();
        if (!(tE > 0) || double.IsInfinity(tE) || double.IsNaN(tE)) return res;
        res.tEnd = tE;

        double rp = RhoPos, rm = RhoMedium, k2 = KappaSq, c = 4 * System.Math.PI * G, nk = NegK, tau2 = Tau * Tau;
        double ab0 = own ? System.Math.Max(1e-4, negativeScaleRatio) : 1.0;
        double abd0 = own ? NegInitialRate(ab0, lam) : 0;
        // pas : ≤ 1/20 du temps dynamique le plus court au départ (secteur négatif), entre tE/4000 et tE/100000
        double rhoFast = System.Math.Max(rm, rp / k2) / (ab0 * ab0 * ab0);
        double tDyn = 1 / System.Math.Sqrt(c * System.Math.Max(rhoFast, rp));
        int n = (int)System.Math.Min(100000, System.Math.Max(4000, System.Math.Ceiling(tE / (0.05 * tDyn))));
        double h = tE / n;
        var aT = new double[2 * n + 1];
        var hT = new double[2 * n + 1];
        for (int i = 0; i <= 2 * n; i++) { double t = 0.5 * h * i; aT[i] = ScaleFactor(t); hT[i] = Hubble(t); }

        // état : ā, dā/dt, puis 4 jeux (δ+, δ−, δ̇+, δ̇−) partant des 4 conditions unitaires
        const int NS = 18;
        var y = new double[NS];
        y[0] = ab0; y[1] = abd0;
        for (int j = 0; j < 4; j++) y[2 + 4 * j + j] = 1;
        void Deriv(int idx, double[] st, double[] d)
        {
            double a = aT[idx], hp = hT[idx];
            double ab = own ? System.Math.Max(st[0], 1e-9) : a;
            double hb = own ? st[1] / ab : hp;
            d[0] = own ? st[1] : 0;
            d[1] = own ? nk / (ab * ab) : 0;
            double ca = c / (a * a * a), cb = c * tau2 / (ab * ab * ab);
            for (int j = 0; j < 4; j++)
            {
                int o = 2 + 4 * j;
                double dp = st[o], dm = st[o + 1], vp = st[o + 2], vm = st[o + 3];
                d[o] = vp; d[o + 1] = vm;
                d[o + 2] = -2 * hp * vp + ca * (rp * dp - k2 * rm * dm);
                d[o + 3] = -2 * hb * vm + cb * (rm * dm - rp / k2 * dp);
            }
        }
        double[] k1 = new double[NS], kk2 = new double[NS], k3 = new double[NS], k4 = new double[NS], tmp = new double[NS];
        for (int i = 0; i < n; i++)
        {
            Deriv(2 * i, y, k1);
            for (int q = 0; q < NS; q++) tmp[q] = y[q] + 0.5 * h * k1[q];
            Deriv(2 * i + 1, tmp, kk2);
            for (int q = 0; q < NS; q++) tmp[q] = y[q] + 0.5 * h * kk2[q];
            Deriv(2 * i + 1, tmp, k3);
            for (int q = 0; q < NS; q++) tmp[q] = y[q] + h * k3[q];
            Deriv(2 * i + 2, tmp, k4);
            for (int q = 0; q < NS; q++) y[q] += h / 6 * (k1[q] + 2 * kk2[q] + 2 * k3[q] + k4[q]);
            if (own && y[0] < 1e-6) return res;                   // secteur négatif effondré : pas de théorie linéaire
            if (double.IsNaN(y[2]) || double.IsInfinity(y[2])) return res;
        }
        res.aBarEnd = own ? y[0] : aT[2 * n];
        for (int j = 0; j < 4; j++) { res.R[0, j] = y[2 + 4 * j]; res.R[1, j] = y[3 + 4 * j]; }

        // Mode croissant au départ (coefficients figés) : (γ² + 2γH − m11)(γ² + 2γH̄ − m22) = m12 m21, plus grande racine γ > 0
        double a0 = aT[0], ab0c = own ? ab0 : a0, h0 = hT[0], hb0 = own ? abd0 / ab0 : h0;
        double m11 = c * rp / (a0 * a0 * a0), m12 = -c * k2 * rm / (a0 * a0 * a0);
        double m21 = -c * tau2 * rp / k2 / (ab0c * ab0c * ab0c), m22 = c * tau2 * rm / (ab0c * ab0c * ab0c);
        double F(double g) => (g * g + 2 * g * h0 - m11) * (g * g + 2 * g * hb0 - m22) - m12 * m21;
        double gHi = System.Math.Sqrt(System.Math.Abs(m11) + System.Math.Abs(m22) + System.Math.Abs(m12 * m21) / System.Math.Max(1e-30, System.Math.Abs(m11) + System.Math.Abs(m22)))
                     + 2 * (System.Math.Abs(h0) + System.Math.Abs(hb0)) + 1e-12;
        double gamma = 0;
        int nScan = 4000;
        double prev = F(gHi);
        for (int i = nScan - 1; i >= 1; i--)              // de haut en bas : première racine rencontrée = la plus grande
        {
            double g = gHi * i / nScan, f = F(g);
            if (System.Math.Sign(f) != System.Math.Sign(prev))
            {
                double lo = g, hi = gHi * (i + 1) / nScan;
                for (int it = 0; it < 80; it++) { double mid = 0.5 * (lo + hi); if (System.Math.Sign(F(mid)) == System.Math.Sign(F(lo))) lo = mid; else hi = mid; }
                gamma = 0.5 * (lo + hi);
                break;
            }
            prev = f;
        }
        if (gamma > 0 && System.Math.Abs(m12) > 0)
        {
            res.gamma = gamma;
            res.ratio = (gamma * gamma + 2 * gamma * h0 - m11) / m12;
            double gp = res.R[0, 0] + res.ratio * res.R[0, 1] + gamma * res.R[0, 2] + gamma * res.ratio * res.R[0, 3];
            double gm = res.R[1, 0] + res.ratio * res.R[1, 1] + gamma * res.R[1, 2] + gamma * res.ratio * res.R[1, 3];
            res.growthMode = System.Math.Abs(gp);
            res.negEndPerPos = System.Math.Abs(gp) > 0 ? gm / gp : double.NaN;
        }
        // vitesses nulles : champs indépendants de même amplitude (somme quadratique) ou champ anticorrélé
        res.growthZeroVel = lssNegField == LssNegField.Anticorrele
            ? System.Math.Abs(res.R[0, 0] - res.R[0, 1])
            : System.Math.Sqrt(res.R[0, 0] * res.R[0, 0] + res.R[0, 1] * res.R[0, 1]);
        res.ok = true;
        return res;
    }

    bool JanusGrowingMode
    {
        get
        {
            if (!(IsLss && lssModel == LssModel.Janus && lssJanusIc == LssJanusIc.ModeCroissant && ExpansionOn)) return false;
            var lg = LinearJanus();
            return lg.ok && lg.gamma > 0;
        }
    }

    // Amplitude σ₈ de la matière ordinaire au départ du run
    double StartSigma8Pos()
    {
        double s = EffSigma8 * GrowthLcdm(lssStartRedshift);                 // historique (et ΛCDM, exact)
        if (IsLss && lssModel == LssModel.Janus && lssJanusSigma8Today && ExpansionOn)
        {
            var lg = LinearJanus();
            double g = JanusGrowingMode ? lg.growthMode : lg.growthZeroVel;
            if (lg.ok && g > 1e-12) s = EffSigma8 / g;
        }
        return s;
    }

    // valeurs du dernier run, écrites dans le fichier des runs
    double linS8Start = double.NaN, linGrowth = double.NaN, linS8Today = double.NaN, linRatio = double.NaN, linGamma = double.NaN;

    void ApplyRealisticIC(Body[] b)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double Lh = System.Math.Max(1.0, lssBoxMpc) * IcH;                     // Mpc/h
        double s8Start = StartSigma8Pos();
        double ns = lssSpectralIndex;
        int nB = Mathf.CeilToInt(Mathf.Pow(positiveCount, 1f / 3f) - 1e-3f);
        bool janus = lssModel == LssModel.Janus && !JanusPhenoOn;   // phénoménologique : conditions initiales de ΛCDM
        IcFields fields = icCache != null && icCache.key == IcKey() ? icCache : ComputeIcFields(MakeIcJob());
        icCache = null;                                        // libère la mémoire (≈ 100 Mo)
        float[][] psiB = fields.psiB, psiB2 = fields.psiB2, psiM = fields.psiM, psiM2 = fields.psiM2;
        double dRms = fields.dRms;
        float signM = janus && lssNegField == LssNegField.Anticorrele ? -1f : 1f;
        float vPos = ExpansionOn ? (float)(GrowthRateStart() * Hubble(0.0)) : 0f;   // f·H (f = 1 en EdS, Ω_m^0,55 en ΛCDM) ; 0 au rebond de Janus
        float vMed = janus ? 0f : vPos;
        // 2e ordre : f2 ≈ 2 Ω_m^(6/11) (Bouchet et al. 1995) ; Ω_m ≈ 1 au départ
        float f2 = EffectiveLaw == ExpansionLaw.LambdaCDM
            ? (float)(2 * System.Math.Pow(1.0 / System.Math.Pow(System.Math.Cosh(LcdmU0), 2), 6.0 / 11.0)) : 2f;
        float vPos2 = ExpansionOn ? (float)(f2 * Hubble(0.0)) : 0f;
        float vMed2 = janus ? 0f : vPos2;
        LinearGrowth lin = janus && ExpansionOn ? LinearJanus() : null;
        bool growing = janus && JanusGrowingMode;
        if (growing)
        {
            // mode croissant : même champ pour les deux secteurs, δ− = ratio δ+, vitesses δ̇ = γ δ (2e ordre : 2γ)
            signM = (float)lin.ratio;
            vPos = vMed = (float)lin.gamma;
            vPos2 = vMed2 = (float)(2 * lin.gamma);
        }
        linS8Start = s8Start;
        linGrowth = lin != null && lin.ok ? (growing ? lin.growthMode : lin.growthZeroVel) : double.NaN;
        linS8Today = s8Start * linGrowth;
        linRatio = growing ? lin.ratio : double.NaN;
        linGamma = growing ? lin.gamma : double.NaN;
        double rmsB = PlaceOnLattice(b, 0, positiveCount, psiB, psiB2, 1f, Lh, vPos, vPos2, positiveSigma, 0f);
        double rmsM = PlaceOnLattice(b, positiveCount, negativeCount, psiM, psiM2, signM, Lh, vMed, vMed2, lssMediumSigma, 0.5f);
        double spacing = lssBoxMpc / nB;
        icInfo = L("Conditions initiales : spectre réaliste", "Initial conditions: realistic spectrum") +
                 (lss2LPT ? " (2LPT)" : " (Zel'dovich)") +
                 $" (σ₈ = {EffSigma8:G3}, " + L("au départ", "at start") + $" {s8Start:G3} ; n_s = {ns:G3}" +
                 (janus ? ", " + L("secteur négatif ", "negative sector ") +
                          (growing ? L("en mode croissant linéaire", "in the linear growing mode")
                           : lssNegField == LssNegField.Anticorrele ? L("anticorrélé", "anticorrelated") : L("indépendant", "independent")) : "") +
                 $")\n" + L("Boîte ", "Box ") + $"{lssBoxMpc:F0} Mpc, " + L("espacement des baryons ", "baryon spacing ") +
                 $"{spacing:F1} Mpc ; " + L("déplacement rms ", "rms displacement ") +
                 $"{rmsB:F2} / {rmsM:F2} " + L("espacement", "spacing") + L(" ; δ rms à la résolution ", " ; rms δ at resolution ") + $"{dRms:F2}";
        if (JanusPhenoOn)
            icInfo += "\n" + L("JANUS PHÉNOMÉNOLOGIQUE : expansion ΛCDM, couplage croisé × s(z) : s = ", "PHENOMENOLOGICAL JANUS: ΛCDM expansion, cross coupling × s(z): s = ") +
                      $"{PhenoSEarly:G3}" + L(" (attraction) avant z ≈ ", " (attraction) before z ≈ ") + $"{janusPhenoZ:G3}" +
                      L(", → +1 (répulsion) ensuite ; s au départ ", ", → +1 (repulsion) afterwards; s at start ") + $"{PhenoS(ScaleFactor(0.0)):G3}" +
                      L(", aujourd'hui ", ", today ") + $"{PhenoS(expansionFactorEnd):G3}";
        if (lin != null && lin.ok)
        {
            icInfo += "\n" + L("Théorie linéaire Janus : δ+ croît ×", "Janus linear theory: δ+ grows ×") + $"{lin.growthZeroVel:G3} " +
                      L("(vitesses nulles)", "(zero velocities)") +
                      (lin.gamma > 0 ? L(", ×", ", ×") + $"{lin.growthMode:G3} " + L("(mode croissant : δ−/δ+ = ", "(growing mode: δ−/δ+ = ") +
                                       $"{lin.ratio:G3}, γ = {lin.gamma:G3})" : "") +
                      L(" ; ΛCDM ×", " ; ΛCDM ×") + $"{1.0 / GrowthLcdm(lssStartRedshift):G3}" +
                      L(". σ₈ linéaire final des baryons ≈ ", ". Final linear σ₈ of baryons ≈ ") + $"{linS8Today:G3}" +
                      (growing ? L(" (mode croissant)", " (growing mode)") : "");
            if (growing && System.Math.Abs(lin.ratio) * dRms > 0.5)
                icInfo += L("\nATTENTION : secteur négatif déjà non linéaire au départ (δ− rms ≈ ", "\nWARNING: negative sector already non-linear at start (rms δ− ≈ ") +
                          $"{System.Math.Abs(lin.ratio) * dRms:G2}" + L(") : cochez « σ₈ = valeur linéaire aujourd'hui » ou baissez σ₈.",
                                                                       "): tick 'σ₈ = linear value today' or lower σ₈.");
        }
        if (dRms > 0.5)
            icInfo += L("\nATTENTION : δ rms > 0,5 à l'échelle du réseau : départ trop tardif ou amplitude trop forte pour l'approximation de Zel'dovich.",
                        "\nWARNING: rms δ > 0.5 at lattice scale: start too late or amplitude too large for the Zel'dovich approximation.");
        Debug.Log("Janus PM : " + icInfo.Replace("\n", " | ") + $" — {watch.ElapsedMilliseconds} ms");
    }

    // ------------------------------------------------------------ halo réaliste (NFW / isotherme à cœur)

    float EffectiveScaleRadius()
    {
        if (dmScaleRadius > 0f) return dmScaleRadius;
        float Rd = diskRadius / 3f;   // longueur d'échelle du disque exponentiel
        return dmProfile == HaloProfile.NFW ? 6.6f * Rd : 2f * Rd;
    }

    // Densité (non normalisée) du profil, avec coupure exponentielle au-delà de la troncature
    double ProfileDensity(double r, double a, double rt, double decay)
    {
        double x = System.Math.Max(r, 1e-3) / a;
        double rho = dmProfile == HaloProfile.NFW
            ? 1.0 / (x * (1.0 + x) * (1.0 + x))
            : 1.0 / (1.0 + x * x);
        if (r > rt)
        {
            double xt = rt / a;
            double rhoT = dmProfile == HaloProfile.NFW
                ? 1.0 / (xt * (1.0 + xt) * (1.0 + xt))
                : 1.0 / (1.0 + xt * xt);
            rho = rhoT * System.Math.Exp(-(r - rt) / decay);
        }
        return rho;
    }

    // Halo réaliste, particules positives libres.
    //  - Normalisation : à r = diskRadius, la force du halo (moyenne de la boîte soustraite, comme en Janus)
    //    est égale à celle de la cavité Janus. Même vitesse circulaire au bord du disque ;
    //    la forme de la courbe à l'intérieur dépend en revanche du profil.
    //  - Vitesses : inversion d'Eddington (distribution isotrope en équilibre) dans le potentiel
    //    halo + disque (approximation sphérique) + fond soustrait par la boîte périodique.
    void CreateRealisticHalo(Body[] b)
    {
        Random.InitState(seed * 7919 + 17);   // flux aléatoire séparé, le disque reste inchangé

        double a = EffectiveScaleRadius();
        double rt = dmTruncationRadius;
        double decay = 0.1 * rt;
        double rMaxH = System.Math.Min(rt + 5.0 * decay, 0.49 * boxSize);
        double L3 = (double)boxSize * boxSize * boxSize;
        double Rn = diskRadius;
        double VR = 4.0 / 3.0 * System.Math.PI * Rn * Rn * Rn;

        // Table radiale : masse cumulée (non normalisée)
        const int NT = 4000;
        var rr = new double[NT];
        var rhoU = new double[NT];
        var mU = new double[NT];
        for (int i = 0; i < NT; i++)
        {
            rr[i] = rMaxH * (i + 0.5) / NT;
            rhoU[i] = ProfileDensity(rr[i], a, rt, decay);
        }
        double dr = rMaxH / NT, acc = 0;
        for (int i = 0; i < NT; i++)
        {
            acc += 4.0 * System.Math.PI * rr[i] * rr[i] * rhoU[i] * dr;
            mU[i] = acc;
        }
        double mUtot = acc;
        int iR = System.Math.Min((int)(Rn / dr), NT - 1);
        double frac = mU[iR] / mUtot;   // fraction de la masse du halo à r < diskRadius

        // Normalisation (voir commentaire) : f·M − V_R (M + M+)/L³ = k R³ / G
        double k = 4.0 / 3.0 * System.Math.PI * G * (EffNegTotalMass - positiveTotalMass) / L3;
        k = System.Math.Max(k, 0.0);
        double denom = frac - VR / L3;
        if (denom <= 0)
        {
            Debug.LogError("Janus PM : halo trop étendu pour être normalisé (rayon d'échelle trop grand).");
            denom = 1e-3;
        }
        double Mh = (k * Rn * Rn * Rn / G + VR * positiveTotalMass / L3) / denom;
        haloMass = (float)Mh;
        double norm = Mh / mUtot;
        double rhoBar = (Mh + positiveTotalMass) / L3;   // fond soustrait par le solveur

        // Masse du disque à r < r (distribution sphérique réelle des étoiles)
        var mDisk = new double[NT];
        double mP = positiveTotalMass / positiveCount;
        for (int i = 0; i < positiveCount; i++)
        {
            int bin = (int)(b[i].pos.magnitude / dr);
            if (bin < NT) mDisk[bin] += mP;
        }
        for (int i = 1; i < NT; i++) mDisk[i] += mDisk[i - 1];

        // Accélération du halo seul (fond soustrait), pour les vitesses circulaires du disque
        extAccel = new double[NT];
        extDr = dr;
        for (int i = 0; i < NT; i++)
            extAccel[i] = G * (norm * mU[i] - rhoBar * 4.0 / 3.0 * System.Math.PI * rr[i] * rr[i] * rr[i])
                          / (rr[i] * rr[i]);

        var g = new double[NT];
        for (int i = 0; i < NT; i++)
        {
            double mEff = norm * mU[i] + mDisk[i] - rhoBar * 4.0 / 3.0 * System.Math.PI * rr[i] * rr[i] * rr[i];
            g[i] = G * mEff / (rr[i] * rr[i]);
        }
        // ---- Vitesses : inversion d'Eddington (fonction de distribution isotrope exacte) ----
        // Une maxwellienne locale tronquée à la vitesse de libération retire ici ~36 % de l'énergie
        // cinétique (halo tronqué peu profond) et le halo s'effondre au départ. Eddington donne une
        // distribution en équilibre par construction dans le potentiel total (halo + disque + fond).

        // Potentiel relatif Ψ(r) = ∫_r^rmax g dr'  (Ψ = 0 au bord, maximal au centre)
        var psi = new double[NT];
        double accPsi = 0;
        for (int i = NT - 1; i >= 0; i--) { accPsi += System.Math.Max(g[i], 1e-9) * dr; psi[i] = accPsi; }

        // Tableaux rangés par Ψ croissant (r décroissant)
        var P = new double[NT];
        var Rh = new double[NT];
        for (int j = 0; j < NT; j++) { P[j] = psi[NT - 1 - j]; Rh[j] = norm * rhoU[NT - 1 - j]; }
        var slope = new double[NT - 1];   // dρ/dΨ sur chaque segment
        for (int j = 0; j < NT - 1; j++)
            slope[j] = (Rh[j + 1] - Rh[j]) / System.Math.Max(P[j + 1] - P[j], 1e-12);

        // F(E) = ∫_0^E (dρ/dΨ) dΨ / √(E − Ψ), intégrée exactement segment par segment ; f(E) ∝ dF/dE
        const int NE = 2000;
        double Pmax = P[NT - 1];
        var Eg = new double[NE];
        var Fg = new double[NE];
        for (int e = 0; e < NE; e++)
        {
            double E = Pmax * e / (NE - 1);
            Eg[e] = E;
            double F = 0;
            for (int j = 0; j < NT - 1 && P[j] < E; j++)
            {
                double pb = System.Math.Min(P[j + 1], E);
                F += slope[j] * 2.0 * (System.Math.Sqrt(E - P[j]) - System.Math.Sqrt(E - pb));
            }
            Fg[e] = F;
        }
        var fE = new double[NE];
        int negF = 0;
        for (int e = 0; e < NE; e++)
        {
            int e0 = System.Math.Max(e - 1, 0), e1 = System.Math.Min(e + 1, NE - 1);
            double d = (Fg[e1] - Fg[e0]) / (Eg[e1] - Eg[e0]);
            if (d < 0) negF++;
            fE[e] = System.Math.Max(d, 0.0);   // normalisation sans importance pour le tirage
        }
        System.Func<double, double> fOf = (double en) =>
        {
            if (en <= 0) return 0.0;
            double x = en / Pmax * (NE - 1);
            int i0 = System.Math.Min((int)x, NE - 2);
            double t = x - i0;
            return fE[i0] * (1 - t) + fE[i0 + 1] * t;
        };

        // Tables de vitesse par tranche de rayon : p(v) ∝ v² f(Ψ − v²/2), v ∈ [0, √(2Ψ)]
        const int NVR = 400, NVG = 256;
        var vTop = new double[NVR];
        var cdf = new double[NVR, NVG];
        for (int bIdx = 0; bIdx < NVR; bIdx++)
        {
            double rc = rMaxH * (bIdx + 0.5) / NVR;
            int ir = System.Math.Min((int)(rc / dr), NT - 1);
            double ps = psi[ir];
            double vm = System.Math.Sqrt(2.0 * ps);
            vTop[bIdx] = vm;
            double c = 0;
            for (int iv = 0; iv < NVG; iv++)
            {
                double v = vm * iv / (NVG - 1);
                c += v * v * fOf(ps - 0.5 * v * v);
                cdf[bIdx, iv] = c;
            }
            for (int iv = 0; iv < NVG; iv++) cdf[bIdx, iv] = c > 0 ? cdf[bIdx, iv] / c : (double)iv / (NVG - 1);
        }

        // Tirage des particules
        float mH = (float)(Mh / negativeCount);
        for (int j = 0; j < negativeCount; j++)
        {
            double u = Random.value * mUtot;
            int lo = 0, hi = NT - 1;
            while (lo < hi) { int mid = (lo + hi) / 2; if (mU[mid] < u) lo = mid + 1; else hi = mid; }
            double r0 = lo > 0 ? rr[lo - 1] : 0.0;
            double m0 = lo > 0 ? mU[lo - 1] : 0.0;
            double r = r0 + (rr[lo] - r0) * (u - m0) / System.Math.Max(mU[lo] - m0, 1e-30);

            int bv = System.Math.Min((int)(r / rMaxH * NVR), NVR - 1);
            double uv = Random.value;
            int a0 = 0, a1 = NVG - 1;
            while (a0 < a1) { int mid = (a0 + a1) / 2; if (cdf[bv, mid] < uv) a0 = mid + 1; else a1 = mid; }
            double c0 = a0 > 0 ? cdf[bv, a0 - 1] : 0.0;
            double frv = (uv - c0) / System.Math.Max(cdf[bv, a0] - c0, 1e-12);
            double speed = vTop[bv] * (System.Math.Max(a0 - 1, 0) + frv) / (NVG - 1);

            b[positiveCount + j] = new Body
            {
                pos = Random.onUnitSphere * (float)r,
                vel = Random.onUnitSphere * (float)speed,
                mass = mH,
                sign = 2
            };
        }
        if (negF > NE / 10)
            Debug.LogWarning($"Janus PM : la fonction de distribution d'Eddington est négative sur {100 * negF / NE} % " +
                             "des énergies (ramenée à 0). Fréquent pour un halo à cœur autour d'un disque dense ; " +
                             "le halo reste proche de l'équilibre mais pas exactement.");

        Debug.Log($"Janus PM : halo {dmProfile}, échelle = {a:F0}, troncature = {rt:F0}, " +
                  $"M_h = {Mh:F0} ({Mh / positiveTotalMass:F1} × M+), dont {frac * 100:F0} % à r < {Rn:F0}");
    }

    // Halo de matière noire : sphère homogène de rayon R, particules positives libres.
    // Sa densité est choisie pour que la densité effective vue par le disque
    // (moyenne de la boîte soustraite) soit la même que dans la cavité Janus :
    //   ρ_h − (M_h + M+)/L³ = (|M−| − M+)/L³   =>   ρ_h = |M−| / (L³ − V_h)
    // Vitesses : équilibre de Jeans isotrope d'une sphère homogène, σ²(r) = k (R² − r²) / 2.
    void CreateDarkMatterHalo(Body[] b)
    {
        Random.InitState(seed * 7919 + 17);   // flux aléatoire séparé, le disque reste inchangé

        float R = dmHaloRadius;
        float L3 = boxSize * boxSize * boxSize;
        float Vh = 4f / 3f * Mathf.PI * R * R * R;
        float rhoH = negativeTotalMass / (L3 - Vh);
        haloMass = rhoH * Vh;
        float mH = haloMass / negativeCount;

        float k = 4f / 3f * Mathf.PI * G * (EffNegTotalMass - positiveTotalMass) / L3;
        k = Mathf.Max(k, 0f);

        for (int j = 0; j < negativeCount; j++)
        {
            Vector3 dir = Random.onUnitSphere;
            float r = R * Mathf.Pow(Random.value, 1f / 3f);
            float sigma = Mathf.Sqrt(Mathf.Max(0.5f * k * (R * R - r * r), 0f));
            b[positiveCount + j] = new Body
            {
                pos = dir * r,
                vel = Gauss3() * sigma,
                mass = mH,
                sign = 2
            };
        }
        Debug.Log($"Janus PM : halo de matière noire, M_h = {haloMass:F0} ({haloMass / positiveTotalMass:F1} × M+), R = {R:F0}");
    }

    // Vitesses circulaires = équilibre avec l'autogravité du disque + l'effet de la cavité.
    // Dans la cavité, la densité effective vue par les positives vaut (|M−| − M+)/L³
    // (moyenne soustraite), d'où un rappel harmonique k·r. Ce champ n'est PAS imposé
    // pendant la simulation : il sert seulement à démarrer près de l'équilibre.
    void SetDiskVelocities(Body[] b)
    {
        int n = positiveCount;
        var rr = new float[n];
        var idx = new int[n];
        for (int i = 0; i < n; i++) { rr[i] = b[i].pos.magnitude; idx[i] = i; }
        System.Array.Sort(rr, idx);

        float L3 = boxSize * boxSize * boxSize;
        float kCav = 4f / 3f * Mathf.PI * G * (EffNegTotalMass - positiveTotalMass) / L3;
        if (kCav <= 0f)
            Debug.LogWarning("Janus PM : |M−| ≤ M+, aucune force de confinement n'est attendue dans la cavité.");
        kCav = Mathf.Max(kCav, 0f);

        float eps = Mathf.Max(smoothingLength, boxSize / gridSize);
        float mP = positiveTotalMass / n;

        // --- Tables radiales pour le réglage par Toomre Q : vc, κ, Σ, σR et dérive asymétrique ---
        const int NR = 400;
        float rTab = Mathf.Max(rr[n - 1], 1f);
        float drT = rTab / NR;
        var vcT = new float[NR];
        var kapT = new float[NR];
        var sRT = new float[NR];
        var vphT = new float[NR];
        var sigT = new float[NR];
        bool useQ = diskToomreQ > 0f;
        if (useQ)
        {
            float Rd = diskRadius / 3f;
            float normT = diskExponentialFix ? 1f - (1f + diskRadius / Rd) * Mathf.Exp(-diskRadius / Rd) : 1f;
            float Ms = positiveTotalMass * (1f - gasFraction);   // Q stellaire : étoiles seules
            float sigma0 = Ms / (2f * Mathf.PI * Rd * Rd * normT);
            int cnt = 0;
            for (int j = 0; j < NR; j++)
            {
                float rj = drT * (j + 0.5f);
                while (cnt < n && rr[cnt] <= rj) cnt++;
                float aS = G * cnt * mP * rj / Mathf.Pow(rj * rj + eps * eps, 1.5f);
                float aE = extAccel != null ? ExtAccel(rj) : kCav * rj;
                vcT[j] = Mathf.Sqrt(Mathf.Max(rj * (aS + aE), 0f));
                sigT[j] = rj <= diskRadius ? sigma0 * Mathf.Exp(-rj / Rd) : 0f;
            }
            for (int j = 0; j < NR; j++)
            {
                float rj = drT * (j + 0.5f);
                int j0 = Mathf.Max(j - 1, 0), j1 = Mathf.Min(j + 1, NR - 1);
                float dv = (vcT[j1] - vcT[j0]) / ((j1 - j0) * drT);
                float om = vcT[j] / rj;
                kapT[j] = Mathf.Sqrt(Mathf.Max(2f * om * (om + dv), 1e-6f));
                // Toomre : σR = Q · 3,36 G Σ / κ, plafonné à 60 % de vc (centre)
                sRT[j] = Mathf.Min(diskToomreQ * 3.36f * G * sigT[j] / kapT[j], 0.6f * vcT[j]);
            }
            for (int j = 0; j < NR; j++)
            {
                // Équation de Jeans radiale : vφ² = vc² + σR² (1 − κ²/4Ω² + dln(Σ σR²)/dln R)
                float rj = drT * (j + 0.5f);
                int j0 = Mathf.Max(j - 1, 0), j1 = Mathf.Min(j + 1, NR - 1);
                float f0 = Mathf.Max(sigT[j0] * sRT[j0] * sRT[j0], 1e-12f);
                float f1 = Mathf.Max(sigT[j1] * sRT[j1] * sRT[j1], 1e-12f);
                float r0 = drT * (j0 + 0.5f), r1 = drT * (j1 + 0.5f);
                float dln = (Mathf.Log(f1) - Mathf.Log(f0)) / (Mathf.Log(r1) - Mathf.Log(r0));
                float om = vcT[j] / rj;
                float ratio = kapT[j] * kapT[j] / (4f * om * om);
                float v2 = vcT[j] * vcT[j] + sRT[j] * sRT[j] * (1f - ratio + dln);
                vphT[j] = Mathf.Sqrt(Mathf.Clamp(v2, 0.25f * vcT[j] * vcT[j], vcT[j] * vcT[j]));
            }
        }

        for (int k = 0; k < n; k++)
        {
            int i = idx[k];
            float r = Mathf.Max(rr[k], 1e-3f);
            float mEnc = (k + 1) * mP;
            float aSelf = G * mEnc * r / Mathf.Pow(r * r + eps * eps, 1.5f);
            float aExt = extAccel != null ? ExtAccel(r) : kCav * r;   // halo réaliste ou cavité Janus
            float vc = Mathf.Sqrt(Mathf.Max(r * (aSelf + aExt), 0f));

            Vector3 p = b[i].pos;
            Vector3 t = Vector3.Cross(Vector3.up, new Vector3(p.x, 0f, p.z)).normalized;

            if (useQ && i >= GasCount)
            {
                // Étoiles : dispersions radiale / azimutale / verticale cohérentes avec Q et l'équilibre vertical
                float R = Mathf.Max(Mathf.Sqrt(p.x * p.x + p.z * p.z), 1e-3f);
                int j = Mathf.Clamp((int)(R / drT), 0, NR - 1);
                float om = vcT[j] / Mathf.Max(drT * (j + 0.5f), 1e-3f);
                float sR = sRT[j];
                float sPhi = sR * kapT[j] / (2f * Mathf.Max(om, 1e-6f));
                float sZ = Mathf.Sqrt(Mathf.PI * G * sigT[j] * diskThickness);
                Vector3 eR = new Vector3(p.x, 0f, p.z) / R;
                Vector3 g3 = Gauss3();
                b[i].vel = t * (vphT[j] + g3.y * sPhi) + eR * (g3.x * sR) + Vector3.up * (g3.z * sZ);
            }
            else
            {
                float disp = i < GasCount ? gasVelocityDispersion : diskVelocityDispersion;   // gaz plus froid
                Vector3 noise = Gauss3() * (disp * vc);
                noise.y *= 0.5f;
                b[i].vel = t * vc + noise;
            }
        }
    }

    // ------------------------------------------------------------ boucle

    void ComputeField()
    {
        compute.Dispatch(kClear, g8, g8, g4);
        compute.Dispatch(kDeposit, particleGroups, 1, 1);
        compute.Dispatch(kToComplex, g8, g8, g4);
        FFT3D(false);
        compute.Dispatch(kPoisson, g8, g8, g4);
        FFT3D(true);
        compute.Dispatch(kGradient, g8, g8, g4);
    }

    void FFT3D(bool inverse)
    {
        compute.SetFloat("_FFTSign", inverse ? 1f : -1f);
        compute.SetInt("_Inverse", inverse ? 1 : 0);
        for (int axis = 0; axis < 3; axis++)
        {
            compute.SetInt("_Axis", axis);
            compute.Dispatch(kFFT, gridSize, gridSize, 1);
        }
    }

    void Update()
    {
        if (preparing && webIcJob != null)
        {
            // version web : calcul sur le fil principal, après deux images pour que le message s'affiche
            if (webIcWait-- > 0) return;
            var job = webIcJob; webIcJob = null; preparing = false;
            try { icCache = ComputeIcFields(job); }
            catch (System.Exception ex)
            {
                Fail(L("calcul des conditions initiales impossible : ", "initial conditions could not be computed: ") + ex.Message);
                diagText = L("ERREUR : ", "ERROR: ") + LastError;
                return;
            }
            InitRun();
            return;
        }
        if (preparing)
        {
            if (icTask == null || !icTask.IsCompleted) return;
            preparing = false;
            var task = icTask; icTask = null;
            if (task.IsFaulted || task.Result == null)
            {
                string msg = task.Exception != null ? task.Exception.GetBaseException().Message : "?";
                Fail(L("calcul des conditions initiales impossible : ", "initial conditions could not be computed: ") + msg);
                diagText = L("ERREUR : ", "ERROR: ") + LastError;
                return;
            }
            icCache = task.Result;
            InitRun();
            return;
        }
        if (!ready) return;

        // Campagne : fin du run → une dernière mesure, puis run suivant
        if (batchMode && finishing && !paused)
        {
            if (finalSampleDone) { EndRun(); StartNextRun(); }
            else if (!readbackPending) RequestReadback(true);   // mesure finale (ou nouvel essai)
            if (!ready) return;
        }

        if (!batchMode)
        {
            // réglages en direct (désactivés en campagne pour garder des runs identiques)
            compute.SetFloat("_G", G);
            compute.SetFloat("_Dt", dt);
            compute.SetFloat("_SmoothLen", smoothingLength);
        }

        int steps = paused ? 0 : (batchMode ? Mathf.Max(1, batchStepsPerFrame) : stepsPerFrame);
        // Vue fluide : pendant que l'utilisateur manipule la caméra (et 0,6 s après), un seul pas par image.
        // Le résultat ne change pas (mêmes pas, répartis sur plus d'images) ; hors manipulation, pleine vitesse.
        if (steps > 1 && Time.unscaledTime < viewInteractUntil) steps = 1;
        if (steps > 0)
        {
            float frame = Time.unscaledDeltaTime;
            if (frame > 0.5f) stepsCap = Mathf.Max(1, Mathf.Min(stepsCap, steps) / 2);       // image lente : moitié moins de pas
            else if (frame < 0.2f && stepsCap < steps) stepsCap++;                            // marge : on remonte d'un pas
            steps = Mathf.Min(steps, stepsCap);
        }
        int stepsDone = 0;
        for (int s = 0; s < steps && !finishing; s++)
        {
            stepsDone++;
            // expansion auto-cohérente : on attend chaque mesure (runs courts, sinon des mesures en a seraient sautées)
            if (SelfConsistent && readbackPending) break;
            // leapfrog à pas variable : drift de curDt, puis kick de (curDt + pas suivant) / 2
            compute.SetFloat("_Dt", curDt);
            compute.Dispatch(kDrift, particleGroups, 1, 1);
            ComputeField();
            double tNext = simTime + curDt;
            if (NegOwnScale) AdvanceNegScale(tNext);
            float nextDt = StepDt(tNext, NegOwnScale ? negA : (ExpansionOn ? ScaleFactor(tNext) : 1.0));
            float kick = 0.5f * (curDt + nextDt);
            compute.SetFloat("_KickDt", kick);
            if (ExpansionOn) SetExpansionKick(tNext, kick);   // a et H au temps du pas
            compute.Dispatch(kKick, particleGroups, 1, 1);

            simTime += curDt;
            curDt = nextDt;
            stepCount++;

            if (GasCount > 0)
            {
                // Gaz : vitesse moyenne locale, dissipation, formation et recyclage d'étoiles
                int gg = gasGridSize / 8;
                compute.SetFloat("_SimTime", simTime);
                compute.SetInt("_StepIndex", stepCount);
                compute.Dispatch(kGasClear, gg, gg, gasGridSize / 4);
                int gasGroups = Mathf.CeilToInt(GasCount / 256f);   // uniquement les particules de gaz possibles
                compute.Dispatch(kGasDeposit, gasGroups, 1, 1);
                compute.Dispatch(kGasUpdate, gasGroups, 1, 1);
            }

            bool endReached = SelfConsistent
                ? ScaleFactor(simTime) >= expansionFactorEnd - 1e-6
                : simTime >= batchEndTime - 0.5f * dt;
            if (batchMode && endReached)
            {
                finishing = true;
                if (!readbackPending) RequestReadback(true);
            }
            else if (SelfConsistent)
            {
                // mesures régulièrement espacées en a (les durées diffèrent beaucoup d'un modèle à l'autre)
                if (ScaleFactor(simTime) >= nextSampleA && !readbackPending)
                {
                    RequestReadback();
                    double stepA = (expansionFactorEnd - 1.0) / Mathf.Max(1, lssSamples);
                    while (nextSampleA <= ScaleFactor(simTime)) nextSampleA += stepA;
                }
            }
            else if (diagnosticEverySteps > 0 && stepCount % diagnosticEverySteps == 0 && !readbackPending)
                RequestReadback();
        }

        if (stepsDone > 0)
        {
            // le GPU travaille en différé : on mesure la durée totale d'une image rapportée aux pas calculés
            float ms = Time.unscaledDeltaTime * 1000f / stepsDone;
            msPerStep = msPerStep <= 0 ? ms : Mathf.Lerp(msPerStep, ms, 0.02f);
        }

        if (showNegatives != lastShowNegatives)
        {
            args[1] = (uint)(showNegatives ? total : positiveCount);
            argsBuffer.SetData(args);
            lastShowNegatives = showNegatives;
        }

        // Rendu : âge des étoiles jeunes
        instanceMaterial.SetFloat("_SimTime", simTime);
        instanceMaterial.SetFloat("_YoungLifetime", Mathf.Max(youngStarLifetime, 1e-3f));

        if (followCamera != null)
        {
            Vector3 d = Wrap(galaxyCenter - smoothCenter);
            smoothCenter = Wrap(smoothCenter + d * (1f - Mathf.Exp(-2f * Time.deltaTime)));
            followCamera.position = smoothCenter + camOffset;
        }

        if (!batchMode || batchRender)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.one * boxSize * 2f);
            Graphics.DrawMeshInstancedIndirect(instanceMesh, 0, instanceMaterial, bounds, argsBuffer);
        }
    }

    void RequestReadback(bool final = false)
    {
        readbackPending = true;
        readbackTime = simTime; readbackStep = stepCount;   // instant réel de la copie GPU
        readbackANeg = NegOwnScale ? negA : (ExpansionOn ? ScaleFactor(simTime) : 1.0);
        int token = runToken;
        AsyncGPUReadback.Request(bodyBuffer, req => OnReadback(req, token, final));
    }

    // ------------------------------------------------------------ diagnostics

    // Écart le plus court dans la boîte périodique
    Vector3 Wrap(Vector3 d)
    {
        d.x -= boxSize * Mathf.Round(d.x / boxSize);
        d.y -= boxSize * Mathf.Round(d.y / boxSize);
        d.z -= boxSize * Mathf.Round(d.z / boxSize);
        return d;
    }

    void OnReadback(AsyncGPUReadbackRequest req, int token, bool isFinal)
    {
        if (token != runToken) return;   // mesure d'un run précédent : ignorée
        readbackPending = false;
        if (!ready || req.hasError) return;
        NativeArray<Body> data = req.GetData<Body>();
        if (IsLss) { OnReadbackLss(data, isFinal); return; }

        const int H = 32;
        var hp = new int[H * H * H];
        var hn = new int[H * H * H];
        float hl = boxSize * 0.5f;
        float cell = boxSize / H;

        // Histogramme grossier (recouvrement + point de départ du centrage)
        int best = 0;
        for (int i = 0; i < data.Length; i++)
        {
            Body bd = data[i];
            int cx = Mathf.Clamp((int)((bd.pos.x + hl) / boxSize * H), 0, H - 1);
            int cy = Mathf.Clamp((int)((bd.pos.y + hl) / boxSize * H), 0, H - 1);
            int cz = Mathf.Clamp((int)((bd.pos.z + hl) / boxSize * H), 0, H - 1);
            int c = cx + H * (cy + H * cz);
            if (bd.sign == 1 || bd.sign >= 3) { hp[c]++; if (hp[c] > hp[best]) best = c; }   // disque : étoiles + gaz
            else hn[c]++;
        }

        double overlap = 0;
        for (int c = 0; c < hp.Length; c++) overlap += (double)hp[c] * hn[c];
        overlap *= (double)hp.Length / ((double)positiveCount * negativeCount);

        // Centre de la galaxie : sphère qui rétrécit autour du pic de densité positive
        Vector3 center = new Vector3(
            (best % H + 0.5f) * cell - hl,
            ((best / H) % H + 0.5f) * cell - hl,
            (best / (H * H) + 0.5f) * cell - hl);
        float[] radii = { 60f, 40f, 25f, 15f, 10f };
        foreach (float R in radii)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            for (int i = 0; i < positiveCount; i++)
            {
                Vector3 d = Wrap(data[i].pos - center);
                if (d.sqrMagnitude < R * R) { sum += d; n++; }
            }
            if (n > 50) center = center + sum / n;
        }
        center = Wrap(center);

        // Vitesse d'ensemble de la galaxie
        Vector3 vBulk = Vector3.zero; int nb = 0;
        for (int i = 0; i < positiveCount; i++)
        {
            Vector3 d = Wrap(data[i].pos - center);
            if (d.sqrMagnitude < diskRadius * diskRadius) { vBulk += data[i].vel; nb++; }
        }
        if (nb > 0) vBulk /= nb;

        // Mesures dans le référentiel de la galaxie
        int inside = 0;
        const int bins = 10;
        float rMax = diskRadius * 1.5f;
        var vSum = new float[bins];
        var vCnt = new int[bins];
        double h2 = 0, vz2 = 0; int nd = 0;
        // Modes de Fourier m = 2..4 dans deux anneaux : intérieur (barre) et extérieur (bras)
        var fc = new double[2, 5];
        var fs = new double[2, 5];
        var fn = new int[2];
        var fcG = new double[4];
        var fsG = new double[4];
        int fnG = 0, nGas = 0, nYoung = 0;

        // Masse du milieu (négatives ou halo) proche de la galaxie : montre s'il se contracte ou s'éloigne
        double medium25 = 0, medium50 = 0;
        float mMedium = darkMatterHalo ? haloMass / negativeCount : negativeTotalMass / negativeCount;
        for (int i = positiveCount; i < data.Length; i++)
        {
            float r = Wrap(data[i].pos - center).magnitude;
            if (r < diskRadius) { medium50 += mMedium; if (r < 0.5f * diskRadius) medium25 += mMedium; }
        }

        for (int i = 0; i < positiveCount; i++)
        {
            Vector3 d = Wrap(data[i].pos - center);
            Vector3 v = data[i].vel - vBulk;
            if (d.magnitude < confinementRadius) inside++;

            float rp = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            if (rp > 0.01f && rp < rMax)
            {
                int bin = Mathf.Min((int)(rp / rMax * bins), bins - 1);
                vSum[bin] += (d.z * v.x - d.x * v.z) / rp;
                vCnt[bin]++;
            }
            if (rp < diskRadius) { h2 += d.y * d.y; vz2 += v.y * v.y; nd++; }

            float rn = rp / diskRadius;
            int ann = rn < 0.1f ? -1 : (rn < 0.4f ? 0 : (rn < 1f ? 1 : -1));
            if (ann >= 0)
            {
                float th = Mathf.Atan2(d.z, d.x);
                for (int m = 2; m <= 4; m++)
                {
                    fc[ann, m] += System.Math.Cos(m * th);
                    fs[ann, m] += System.Math.Sin(m * th);
                }
                fn[ann]++;

                // Bras vus dans le gaz et les étoiles jeunes (traceurs des bras sur les vraies photos)
                if (ann == 1 && data[i].sign >= 3)
                {
                    for (int m = 2; m <= 3; m++)
                    {
                        fcG[m] += System.Math.Cos(m * th);
                        fsG[m] += System.Math.Sin(m * th);
                    }
                    fnG++;
                }
            }
            if (data[i].sign == 3) nGas++;
            else if (data[i].sign == 4) nYoung++;
        }
        double A2g = fnG >= 100 ? System.Math.Sqrt(fcG[2] * fcG[2] + fsG[2] * fsG[2]) / fnG : double.NaN;
        double A3g = fnG >= 100 ? System.Math.Sqrt(fcG[3] * fcG[3] + fsG[3] * fsG[3]) / fnG : double.NaN;
        double gasNow = (double)nGas / positiveCount, youngNow = (double)nYoung / positiveCount;

        // Valeurs numériques (CSV)
        double thick = nd > 0 ? System.Math.Sqrt(h2 / nd) : double.NaN;
        double sigz = nd > 0 ? System.Math.Sqrt(vz2 / nd) : double.NaN;
        var amp = new double[2, 5];
        for (int a = 0; a < 2; a++)
            for (int m = 2; m <= 4; m++)
                amp[a, m] = fn[a] >= 100
                    ? System.Math.Sqrt(fc[a, m] * fc[a, m] + fs[a, m] * fs[a, m]) / fn[a]
                    : double.NaN;
        var vCurve = new double[bins];
        for (int k = 0; k < bins; k++)
            vCurve[k] = vCnt[k] > 20 ? vSum[k] / vCnt[k] : double.NaN;
        double confinedPct = 100.0 * inside / positiveCount;

        galaxyCenter = center;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"t = {simTime:F1}   " + L("pas", "step") + $" = {stepCount}");
        string dmDesc = dmProfile == HaloProfile.UniformSphere
            ? L("sphère", "sphere") + $" R = {dmHaloRadius:F0}"
            : $"{dmProfile}, " + L("échelle", "scale") + $" {EffectiveScaleRadius():F0}";
        sb.AppendLine(L("Modèle : ", "Model: ") + (darkMatterHalo ? L("MATIÈRE NOIRE", "DARK MATTER") + $" ({dmDesc}, M = {haloMass:F0})"
                                                              : L("JANUS (masses négatives)", "JANUS (negative masses)")) +
                      (freezeNegatives ? L("   — milieu figé", "   — frozen medium") : L("   — milieu libre", "   — free medium")));
        sb.AppendLine(L("Masse du milieu à r < ", "Medium mass at r < ") + $"{0.5f * diskRadius:F0} : {medium25:F0}   " +
                      L("à r < ", "at r < ") + $"{diskRadius:F0} : {medium50:F0}");
        sb.AppendLine(L("Recouvrement étoiles/milieu : ", "Star/medium overlap: ") + $"{overlap:F3}   " +
                      L("(1 = mélangé, 0 = séparé)", "(1 = mixed, 0 = separated)"));
        if (mode == InitMode.GalaxyInCavity)
        {
            sb.AppendLine(L("Centre galaxie : ", "Galaxy centre: ") + $"({center.x:F0}, {center.y:F0}, {center.z:F0})   " +
                          L("|v| dérive = ", "|v| drift = ") + $"{vBulk.magnitude:F1}");
            sb.AppendLine(L("Positives à r < ", "Positives at r < ") + $"{confinementRadius:F0}" + L(" du centre : ", " from centre: ") +
                          $"{100f * inside / positiveCount:F1} %");
            if (nd > 0)
                sb.AppendLine(L("Épaisseur rms : ", "rms thickness: ") + $"{System.Math.Sqrt(h2 / nd):F1}   σz : {System.Math.Sqrt(vz2 / nd):F1}   (" +
                              L("départ : ", "start: ") + $"{diskThickness:F1})");
            string[] annName = { L("Intérieur (barre)", "Inner (bar)"), L("Extérieur (bras)", "Outer (arms)") };
            for (int a = 0; a < 2; a++)
            {
                if (fn[a] < 100) continue;
                var line = new System.Text.StringBuilder($"{annName[a]} A2/A3/A4 : ");
                for (int m = 2; m <= 4; m++)
                {
                    double am = System.Math.Sqrt(fc[a, m] * fc[a, m] + fs[a, m] * fs[a, m]) / fn[a];
                    line.Append(am.ToString("F3")).Append(m < 4 ? " / " : "");
                }
                line.Append("   (" + L("bruit", "noise") + $" ≈ {1.0 / System.Math.Sqrt(fn[a]):F3})");
                sb.AppendLine(line.ToString());
            }
            if (GasCount > 0)
                sb.AppendLine(L("Gaz : ", "Gas: ") + $"{gasNow * 100:F1} %   " + L("étoiles jeunes : ", "young stars: ") +
                              $"{youngNow * 100:F1} %   " + L("bras gaz+jeunes A2/A3 : ", "gas+young arms A2/A3: ") + $"{A2g:F3} / {A3g:F3}");
            sb.AppendLine(L("Courbe de rotation v(r) :", "Rotation curve v(r):"));
            for (int k = 0; k < bins; k++)
            {
                float r0 = rMax * (k + 0.5f) / bins;
                string vs = vCnt[k] > 20 ? (vSum[k] / vCnt[k]).ToString("F1") : "—";
                sb.AppendLine($"  r = {r0,5:F0}   v = {vs}");
            }
        }
        if (exportCsv)
        {
            WriteCsvRow(overlap, center, vBulk.magnitude, confinedPct, thick, sigz, amp, vCurve, rMax, medium25, medium50,
                        gasNow, youngNow, A2g, A3g);
            if (csvPath != null) sb.AppendLine("CSV : " + System.IO.Path.GetFileName(csvPath));
        }

        // Campagne : accumulation des mesures pour la synthèse
        if (batchMode && stats != null)
        {
            if (curveRadii == null)
            {
                curveRadii = new float[bins];
                for (int k = 0; k < bins; k++) curveRadii[k] = rMax * (k + 0.5f) / bins;
            }
            if (simTime >= batchSummaryFrom)
            {
                stats.n++;
                stats.A2in += amp[0, 2]; stats.A2out += amp[1, 2]; stats.A3out += amp[1, 3];
                stats.thick += thick; stats.sigz += sigz; stats.conf += confinedPct;
                stats.drift += vBulk.magnitude; stats.med25 += medium25; stats.med50 += medium50;
                if (!double.IsNaN(A2g)) { stats.A2gas += A2g; stats.nGasSamples++; }
            }
            if (isFinal)
            {
                stats.finA2in = amp[0, 2]; stats.finA2out = amp[1, 2]; stats.finThick = thick;
                stats.finA2gas = A2g; stats.finGas = gasNow; stats.finYoung = youngNow;
                stats.finCurve = (double[])vCurve.Clone();
                stats.finT = simTime;
                finalSampleDone = true;
            }
            sb.Insert(0, L("CAMPAGNE", "CAMPAIGN") + $" : run {runIndex + 1}/{queue.Count} — {queue[runIndex].name}, seed {seed}" +
                         $"   (t = {simTime:F0} / {batchEndTime:F0})\n");
        }
        string warnDt = TimestepWarning();
        if (warnDt != null) sb.Insert(0, warnDt + "\n");
        diagText = sb.ToString();
    }

    // ------------------------------------------------------------ structure à grande échelle : mesures
    //
    // Deux champs de contraste sur une grille 64³ (affectation CIC) :
    //   δb : matière ordinaire (étoiles, gaz)      δm : milieu (négatives, matière noire ou traceurs)
    // Spectres (estimateur standard, fenêtre CIC corrigée, bruit de Poisson soustrait) :
    //   Δ²(k) = k³ P(k) / 2π²   (variance du contraste par intervalle logarithmique de k)
    //   r(k)  = P_bm / sqrt(P_bb P_mm)   (+1 : les deux suivent la même structure ; −1 : anticorrélés)
    // Statistiques sur une grille 32³ : écarts-types, corrélation, fraction de vides, ségrégation.

    const int LssNG = 64;
    static readonly int[] LssEdges = { 1, 2, 3, 4, 6, 8, 11, 16, 23, 32 };   // bornes en |n| (k = 2π n / L)
    const float LssVoidDelta = -0.8f;
    // Profil empilé autour des pics du milieu : bornes des coquilles (unités de longueur)
    static readonly float[] LssProfEdges = { 0f, 8f, 16f, 25f, 35f, 50f, 70f, 100f, 140f, 200f };   // 200 = demi-boîte de référence
    const int LssMaxPeaks = 64;          // nombre maximal de pics empilés
    const float LssPeakMinDelta = 1f;    // seuil : δ_milieu > 1 sur la grille 32³
    const int LssPeakMinSep = 3;         // séparation minimale entre pics (cellules 32³)
    string[] lssNames;

    int LssBins => LssEdges.Length - 1;

    // Longueur d'onde représentative de l'intervalle b (L / moyenne géométrique des bornes)
    int LssLambda(int b) => Mathf.RoundToInt(boxSize / Mathf.Sqrt(LssEdges[b] * LssEdges[b + 1]));

    string[] LssMetricNames()
    {
        if (lssNames != null) return lssNames;
        var l = new System.Collections.Generic.List<string>
        {
            "sigma_b", "sigma_m", "corr_bm", "void_frac_b", "void_frac_m",
            "baryon_frac_in_medium_overdense", "vol_frac_medium_overdense", "max_delta_b"
        };
        for (int b = 0; b < LssBins; b++) l.Add("D2b_l" + LssLambda(b));
        for (int b = 0; b < LssBins; b++) l.Add("D2m_l" + LssLambda(b));
        for (int b = 0; b < LssBins; b++) l.Add("r_l" + LssLambda(b));
        l.Add("n_peaks"); l.Add("peak_in_baryon_void_frac");
        for (int r = 0; r < LssProfEdges.Length - 1; r++) l.Add("prof_b_r" + Mathf.RoundToInt(LssProfEdges[r + 1]));
        for (int r = 0; r < LssProfEdges.Length - 1; r++) l.Add("prof_m_r" + Mathf.RoundToInt(LssProfEdges[r + 1]));
        l.Add("void_radius"); l.Add("void_radius_mpc"); l.Add("neg_scale_ratio");
        // vides des baryons (même détecteur pour tous les modèles)
        l.Add("bvoid_count"); l.Add("bvoid_density_per_100mpc3"); l.Add("bvoid_radius"); l.Add("bvoid_radius_mpc"); l.Add("bvoid_ridge");
        for (int r = 0; r < LssProfEdges.Length - 1; r++) l.Add("bvoid_prof_b_r" + Mathf.RoundToInt(LssProfEdges[r + 1]));
        for (int r = 0; r < LssProfEdges.Length - 1; r++) l.Add("bvoid_prof_m_r" + Mathf.RoundToInt(LssProfEdges[r + 1]));
        // profils de vides empilés en r / R_v (grille 128³), crête par classe de taille
        l.Add("rv_count"); l.Add("rv_radius_mpc_median"); l.Add("rv_radius_mpc_small");
        l.Add("rv_ridge_all"); l.Add("rv_x_ridge");
        l.Add("rv_ridge_small"); l.Add("rv_x_ridge_small"); l.Add("rv_ridge_mid"); l.Add("rv_ridge_large");
        for (int q = 0; q < RvBins; q++) l.Add("rv_prof_b_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("rv_prof_m_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("rv_small_prof_b_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("rv_small_prof_m_x" + RvX(q));
        // lentille gravitationnelle des vides : densité de surface projetée de la masse « vue » par la lumière
        // (secteur positif). Sans dimension : en unités de ρ̄_réf × R_v, ρ̄_réf = (Ω_m/Ω_b) ρ̄_baryons.
        l.Add("lens_medium_weight");
        l.Add("lens_sigma_center"); l.Add("lens_sigma_center_b"); l.Add("lens_sigma_center_m");
        l.Add("lens_dsigma_min"); l.Add("lens_x_dsigma_min"); l.Add("lens_dsigma_min_hmsun_pc2");
        l.Add("lens_small_dsigma_min"); l.Add("lens_small_x_dsigma_min"); l.Add("lens_small_dsigma_min_hmsun_pc2");
        l.Add("lens_large_dsigma_min"); l.Add("lens_large_dsigma_min_hmsun_pc2");
        for (int q = 0; q < RvBins; q++) l.Add("lens_sigma_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("lens_dsigma_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("lens_small_dsigma_x" + RvX(q));
        for (int q = 0; q < RvBins; q++) l.Add("lens_large_dsigma_x" + RvX(q));
        // fonction de taille des vides (point 3) : tous les minima, sans recouvrement, R_v en Mpc/h
        // empilement à taille de vide égale (tranches de R_v en Mpc/h, comme les relevés) : profils et lentille
        for (int t = 0; t + 1 < RvbEdges.Length; t++)
        {
            string pre = RvbName(t);
            l.Add(pre + "_count"); l.Add(pre + "_rv_median_mpch");
            l.Add(pre + "_center_b"); l.Add(pre + "_center_m");
            l.Add(pre + "_ridge"); l.Add(pre + "_x_ridge");
            l.Add(pre + "_lens_dsigma_min_hmsun_pc2"); l.Add(pre + "_lens_x_min");
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_prof_b_x" + RvX(q));
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_prof_m_x" + RvX(q));
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_lens_dsigma_hmsun_pc2_x" + RvX(q));
        }
        l.AddRange(GalNames());
        l.Add("vsf_n_candidates"); l.Add("vsf_n_voids"); l.Add("vsf_density_per_100mpch3"); l.Add("vsf_volume_frac");
        l.Add("vsf_rv_median_mpch"); l.Add("vsf_rv_mean_mpch"); l.Add("vsf_rv_max_mpch");
        l.Add("vsf_n_gt10_per_100mpch3"); l.Add("vsf_n_gt20_per_100mpch3"); l.Add("vsf_n_gt30_per_100mpch3");
        for (int k = 0; k < VsfBins; k++) l.Add("vsf_count_R" + VsfLabel(k));
        for (int k = 0; k < VsfBins; k++) l.Add("vsf_dndlnR_R" + VsfLabel(k));
        // spectre en unités physiques et spectre linéaire ΛCDM (Planck, σ₈ = 0,81) au même redshift
        for (int b = 0; b < LssBins; b++) l.Add("k_hmpc_b" + (b + 1));
        for (int b = 0; b < LssBins; b++) l.Add("Pb_mpch3_b" + (b + 1));
        for (int b = 0; b < LssBins; b++) l.Add("Pm_mpch3_b" + (b + 1));
        for (int b = 0; b < LssBins; b++) l.Add("Plin_lcdm_b" + (b + 1));
        for (int b = 0; b < LssBins; b++) l.Add("Pb_over_lin_b" + (b + 1));
        // forme : (P/P_lin) divisé par sa valeur à k = 0,1 h/Mpc (indépendant de l'amplitude σ₈)
        l.Add("Pb_over_lin_at_k0.1");
        for (int b = 0; b < LssBins; b++) l.Add("Pb_shape_b" + (b + 1));
        lssNames = l.ToArray();
        return lssNames;
    }

    // FFT 1D en place (radix 2), sur des tableaux temporaires
    static void Fft1D(double[] re, double[] im, int n, bool inverse)
    {
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                double t = re[i]; re[i] = re[j]; re[j] = t;
                t = im[i]; im[i] = im[j]; im[j] = t;
            }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = (inverse ? 2.0 : -2.0) * System.Math.PI / len;
            double wr = System.Math.Cos(ang), wi = System.Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int j = 0; j < len / 2; j++)
                {
                    int a = i + j, b = a + len / 2;
                    double vr = re[b] * cr - im[b] * ci, vi = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - vr; im[b] = im[a] - vi;
                    re[a] += vr; im[a] += vi;
                    double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                }
            }
        }
    }

    // FFT 3D directe d'un champ complexe N³ (indice x + N(y + N z))
    static void Fft3D(double[] re, double[] im, int N)
    {
        var tr = new double[N]; var ti = new double[N];
        for (int axis = 0; axis < 3; axis++)
        {
            int stride = axis == 0 ? 1 : (axis == 1 ? N : N * N);
            for (int u = 0; u < N; u++)
                for (int v = 0; v < N; v++)
                {
                    int bse = axis == 0 ? N * (u + N * v) : (axis == 1 ? u + N * N * v : u + N * v);
                    for (int t = 0; t < N; t++) { tr[t] = re[bse + t * stride]; ti[t] = im[bse + t * stride]; }
                    Fft1D(tr, ti, N, false);
                    for (int t = 0; t < N; t++) { re[bse + t * stride] = tr[t]; im[bse + t * stride] = ti[t]; }
                }
        }
    }

    // Fenêtre CIC W² et facteur du bruit de Poisson replié, pour une composante n du mode
    static void CicTerms(int q, int N, ref double w2, ref double c)
    {
        double s = System.Math.PI * q / N;
        double sn = System.Math.Sin(s);
        double sinc = q == 0 ? 1.0 : sn / s;
        w2 *= sinc * sinc * sinc * sinc;
        c *= 1.0 - 2.0 / 3.0 * sn * sn;
    }

    void OnReadbackLss(NativeArray<Body> data, bool isFinal)
    {
        int N = LssNG, N3 = N * N * N;
        var cb = new double[N3];   // comptes CIC, matière ordinaire
        var cm = new double[N3];   // comptes CIC, milieu
        long nb = 0, nm = 0;

        for (int i = 0; i < data.Length; i++)
        {
            Body bd = data[i];
            bool medium = bd.sign < 0 || bd.sign == 2;
            var arr = medium ? cm : cb;
            if (medium) nm++; else nb++;
            float gx = (bd.pos.x / boxSize + 0.5f) * N - 0.5f;
            float gy = (bd.pos.y / boxSize + 0.5f) * N - 0.5f;
            float gz = (bd.pos.z / boxSize + 0.5f) * N - 0.5f;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy), z0 = Mathf.FloorToInt(gz);
            float fx = gx - x0, fy = gy - y0, fz = gz - z0;
            for (int k = 0; k < 8; k++)
            {
                int ox = k & 1, oy = (k >> 1) & 1, oz = (k >> 2) & 1;
                float w = (ox == 1 ? fx : 1 - fx) * (oy == 1 ? fy : 1 - fy) * (oz == 1 ? fz : 1 - fz);
                int ix = ((x0 + ox) % N + N) % N, iy = ((y0 + oy) % N + N) % N, iz = ((z0 + oz) % N + N) % N;
                arr[ix + N * (iy + N * iz)] += w;
            }
        }
        if (nb == 0 || nm == 0) { diagText = L("LSS : population vide, mesure impossible", "LSS: empty population, no measurement possible"); return; }

        // ---- statistiques sur la grille 32³ (cellules de 2×2×2)
        int M = N / 2, M3 = M * M * M;
        var db = new double[M3]; var dm = new double[M3];
        double mb = nb / (double)M3, mm = nm / (double)M3;
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int j = x / 2 + M * (y / 2 + M * (z / 2)), i = x + N * (y + N * z);
                    db[j] += cb[i]; dm[j] += cm[i];
                }
        double sb2 = 0, sm2 = 0, sbm = 0, maxB = double.MinValue;
        int voidB = 0, voidM = 0, overM = 0; double bInOver = 0;
        for (int j = 0; j < M3; j++)
        {
            double inB = db[j];
            db[j] = db[j] / mb - 1.0; dm[j] = dm[j] / mm - 1.0;
            sb2 += db[j] * db[j]; sm2 += dm[j] * dm[j]; sbm += db[j] * dm[j];
            if (db[j] < LssVoidDelta) voidB++;
            if (dm[j] < LssVoidDelta) voidM++;
            if (dm[j] > 0) { overM++; bInOver += inB; }
            if (db[j] > maxB) maxB = db[j];
        }
        double sigB = System.Math.Sqrt(sb2 / M3), sigM = System.Math.Sqrt(sm2 / M3);
        double corr = sigB > 0 && sigM > 0 ? sbm / M3 / (sigB * sigM) : double.NaN;

        // ---- spectres : une seule FFT complexe pour les deux champs réels (δb + i δm)
        double mean64b = nb / (double)N3, mean64m = nm / (double)N3;
        var re = new double[N3]; var im = new double[N3];
        for (int i = 0; i < N3; i++) { re[i] = cb[i] / mean64b - 1.0; im[i] = cm[i] / mean64m - 1.0; }
        Fft3D(re, im, N);

        double V = (double)boxSize * boxSize * boxSize;
        double norm = V / ((double)N3 * N3);
        double shotB = V / nb, shotM = V / nm;
        int B = LssBins;
        var pbb = new double[B]; var pmm = new double[B]; var pbm = new double[B];
        var ksum = new double[B]; var cnt = new int[B];
        int nMax = LssEdges[B];
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int nx = x > N / 2 ? x - N : x, ny = y > N / 2 ? y - N : y, nz = z > N / 2 ? z - N : z;
                    double nn = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (nn < LssEdges[0] || nn >= nMax) continue;
                    int b = 0; while (b < B - 1 && nn >= LssEdges[b + 1]) b++;

                    int i = x + N * (y + N * z);
                    int i2 = ((N - x) % N) + N * (((N - y) % N) + N * ((N - z) % N));   // mode −k
                    // séparation : δb_k = (F_k + F*_−k)/2,  δm_k = (F_k − F*_−k)/(2i)
                    double br = 0.5 * (re[i] + re[i2]), bi = 0.5 * (im[i] - im[i2]);
                    double mr = 0.5 * (im[i] + im[i2]), mi = -0.5 * (re[i] - re[i2]);

                    // fenêtre CIC et bruit de Poisson replié (Jing 2005)
                    double w2 = 1, c = 1;
                    CicTerms(nx, N, ref w2, ref c); CicTerms(ny, N, ref w2, ref c); CicTerms(nz, N, ref w2, ref c);
                    pbb[b] += ((br * br + bi * bi) * norm - shotB * c) / w2;
                    pmm[b] += ((mr * mr + mi * mi) * norm - shotM * c) / w2;
                    pbm[b] += (br * mr + bi * mi) * norm / w2;
                    ksum[b] += nn; cnt[b]++;
                }

        // ---- profil empilé autour des pics du milieu
        //   Pics : maxima locaux de δm (32³), δm > seuil, les plus forts d'abord, séparés d'au moins LssPeakMinSep.
        //   Centre affiné sur la cellule 64³ la plus dense du bloc 2×2×2.
        //   Profil : contraste moyen de chaque population dans des coquilles autour des centres.
        var cand = new System.Collections.Generic.List<int>();
        for (int z = 0; z < M; z++)
            for (int y = 0; y < M; y++)
                for (int x = 0; x < M; x++)
                {
                    int j = x + M * (y + M * z);
                    double v0 = dm[j];
                    if (v0 <= LssPeakMinDelta) continue;
                    bool isMax = true;
                    for (int dz = -1; dz <= 1 && isMax; dz++)
                        for (int dy = -1; dy <= 1 && isMax; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;
                                int jn = (x + dx + M) % M + M * ((y + dy + M) % M + M * ((z + dz + M) % M));
                                if (dm[jn] > v0) { isMax = false; break; }
                            }
                    if (isMax) cand.Add(j);
                }
        cand.Sort((p, q) => dm[q].CompareTo(dm[p]));
        var peaks = new System.Collections.Generic.List<int>();
        foreach (int j in cand)
        {
            if (peaks.Count >= LssMaxPeaks) break;
            int x = j % M, y = (j / M) % M, z = j / (M * M);
            bool farEnough = true;
            foreach (int pk in peaks)
            {
                int px = pk % M, py = (pk / M) % M, pz = pk / (M * M);
                int ex = System.Math.Abs(x - px); ex = System.Math.Min(ex, M - ex);
                int ey = System.Math.Abs(y - py); ey = System.Math.Min(ey, M - ey);
                int ez = System.Math.Abs(z - pz); ez = System.Math.Min(ez, M - ez);
                if (ex * ex + ey * ey + ez * ez < LssPeakMinSep * LssPeakMinSep) { farEnough = false; break; }
            }
            if (farEnough) peaks.Add(j);
        }

        int NP = LssProfEdges.Length - 1;
        var profB = new double[NP]; var profM = new double[NP]; var profN = new double[NP];
        float h64 = boxSize / N;
        int R = Mathf.CeilToInt(LssProfEdges[NP] / h64);
        int inVoid = 0;
        foreach (int pk in peaks)
        {
            int px = pk % M, py = (pk / M) % M, pz = pk / (M * M);
            int cx = 2 * px, cy = 2 * py, cz = 2 * pz; double bestC = -1;
            for (int o = 0; o < 8; o++)
            {
                int ix = 2 * px + (o & 1), iy = 2 * py + ((o >> 1) & 1), iz = 2 * pz + ((o >> 2) & 1);
                double cv = cm[ix + N * (iy + N * iz)];
                if (cv > bestC) { bestC = cv; cx = ix; cy = iy; cz = iz; }
            }
            double cenB = 0; int cenN = 0;
            for (int dz = -R; dz <= R; dz++)
                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        float dist = h64 * Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist >= LssProfEdges[NP]) continue;
                        int r = 0; while (r < NP - 1 && dist >= LssProfEdges[r + 1]) r++;
                        int i = (cx + dx + N) % N + N * ((cy + dy + N) % N + N * ((cz + dz + N) % N));
                        profB[r] += cb[i]; profM[r] += cm[i]; profN[r]++;
                        if (dist < 12.5f) { cenB += cb[i]; cenN++; }   // centre : sphère de 12,5 (une cellule 32³)
                    }
            if (cenN > 0 && cenB / (cenN * mean64b) - 1.0 < LssVoidDelta) inVoid++;
        }
        var pB = new double[NP]; var pM = new double[NP];
        for (int r = 0; r < NP; r++)
        {
            pB[r] = profN[r] > 0 ? profB[r] / (profN[r] * mean64b) - 1.0 : double.NaN;
            pM[r] = profN[r] > 0 ? profM[r] / (profN[r] * mean64m) - 1.0 : double.NaN;
        }

        var vals = new System.Collections.Generic.List<double>
        {
            sigB, sigM, corr, voidB / (double)M3, voidM / (double)M3,
            bInOver / nb, overM / (double)M3, maxB
        };
        var d2b = new double[B]; var d2m = new double[B]; var rk = new double[B];
        for (int b = 0; b < B; b++)
        {
            if (cnt[b] == 0) { d2b[b] = d2m[b] = rk[b] = double.NaN; continue; }
            double kk = 2.0 * System.Math.PI / boxSize * ksum[b] / cnt[b];
            double Pb = pbb[b] / cnt[b], Pm = pmm[b] / cnt[b], Px = pbm[b] / cnt[b];
            double f = kk * kk * kk / (2.0 * System.Math.PI * System.Math.PI);
            d2b[b] = Pb * f; d2m[b] = Pm * f;
            rk[b] = Pb > 0 && Pm > 0 ? Px / System.Math.Sqrt(Pb * Pm) : double.NaN;
        }
        vals.AddRange(d2b); vals.AddRange(d2m); vals.AddRange(rk);
        vals.Add(peaks.Count); vals.Add(peaks.Count > 0 ? inVoid / (double)peaks.Count : double.NaN);
        vals.AddRange(pB); vals.AddRange(pM);

        double aNow = ExpansionOn ? ScaleFactor(readbackTime) : 1.0;
        // Rayon des vides : rayon où le profil empilé des baryons autour des pics du milieu remonte à δ = −0,5
        // (interpolation linéaire entre les milieux des coquilles). NaN si le centre n'est pas un vide.
        double voidR = VoidRadius(pB);
        double mpcPerUnit = lssBoxMpc > 0f ? lssBoxMpc / boxSize : double.NaN;
        double negRatio = NegOwnScale ? readbackANeg / aNow : double.NaN;
        vals.Add(voidR); vals.Add(voidR * mpcPerUnit); vals.Add(negRatio);

        // ---- vides des baryons : même détecteur pour tous les modèles
        //   Centres : minima locaux de δb (32³) sous LssVoidDelta, les plus profonds d'abord, séparés d'au moins
        //   LssPeakMinSep cellules. Profil empilé des deux populations autour des (au plus LssMaxPeaks) plus profonds.
        //   Rayon : δb empilé remonte à −0,5. Crête : maximum de δb empilé au-delà de ce rayon (paroi compensatrice).
        var vcand = new System.Collections.Generic.List<int>();
        for (int z = 0; z < M; z++)
            for (int y = 0; y < M; y++)
                for (int x = 0; x < M; x++)
                {
                    int j = x + M * (y + M * z);
                    double v0 = db[j];
                    if (v0 >= LssVoidDelta) continue;
                    bool isMin = true;
                    for (int dz = -1; dz <= 1 && isMin; dz++)
                        for (int dy = -1; dy <= 1 && isMin; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;
                                int jn = (x + dx + M) % M + M * ((y + dy + M) % M + M * ((z + dz + M) % M));
                                if (db[jn] < v0) { isMin = false; break; }
                            }
                    if (isMin) vcand.Add(j);
                }
        vcand.Sort((p, q) => db[p].CompareTo(db[q]));
        var voids = new System.Collections.Generic.List<int>();
        foreach (int j in vcand)
        {
            int x = j % M, y = (j / M) % M, z = j / (M * M);
            bool far = true;
            foreach (int vk in voids)
            {
                int px = vk % M, py = (vk / M) % M, pz = vk / (M * M);
                int ex = System.Math.Abs(x - px); ex = System.Math.Min(ex, M - ex);
                int ey = System.Math.Abs(y - py); ey = System.Math.Min(ey, M - ey);
                int ez = System.Math.Abs(z - pz); ez = System.Math.Min(ez, M - ez);
                if (ex * ex + ey * ey + ez * ez < LssPeakMinSep * LssPeakMinSep) { far = false; break; }
            }
            if (far) voids.Add(j);     // tous comptés ; seuls les LssMaxPeaks plus profonds sont empilés
        }
        var vB = new double[NP]; var vM = new double[NP]; var vN = new double[NP];
        for (int iv = 0; iv < voids.Count && iv < LssMaxPeaks; iv++)
        {
            int vk = voids[iv];
            int px = vk % M, py = (vk / M) % M, pz = vk / (M * M);
            int cx = 2 * px, cy = 2 * py, cz = 2 * pz; double bestC = double.MaxValue;
            for (int o = 0; o < 8; o++)
            {
                int ix = 2 * px + (o & 1), iy = 2 * py + ((o >> 1) & 1), iz = 2 * pz + ((o >> 2) & 1);
                double cv = cb[ix + N * (iy + N * iz)];
                if (cv < bestC) { bestC = cv; cx = ix; cy = iy; cz = iz; }
            }
            for (int dz = -R; dz <= R; dz++)
                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        float dist = h64 * Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist >= LssProfEdges[NP]) continue;
                        int r = 0; while (r < NP - 1 && dist >= LssProfEdges[r + 1]) r++;
                        int i = (cx + dx + N) % N + N * ((cy + dy + N) % N + N * ((cz + dz + N) % N));
                        vB[r] += cb[i]; vM[r] += cm[i]; vN[r]++;
                    }
        }
        var pVB = new double[NP]; var pVM = new double[NP];
        for (int r = 0; r < NP; r++)
        {
            pVB[r] = vN[r] > 0 ? vB[r] / (vN[r] * mean64b) - 1.0 : double.NaN;
            pVM[r] = vN[r] > 0 ? vM[r] / (vN[r] * mean64m) - 1.0 : double.NaN;
        }
        double bvR = VoidRadius(pVB);
        double ridge = double.NaN;
        if (!double.IsNaN(bvR))
            for (int r = 0; r < NP; r++)
                if (0.5 * (LssProfEdges[r] + LssProfEdges[r + 1]) > bvR && !double.IsNaN(pVB[r]))
                    ridge = double.IsNaN(ridge) ? pVB[r] : System.Math.Max(ridge, pVB[r]);
        double vol100 = lssBoxMpc > 0f ? System.Math.Pow(lssBoxMpc / 100.0, 3) : double.NaN;
        vals.Add(voids.Count); vals.Add(voids.Count / vol100); vals.Add(bvR); vals.Add(bvR * mpcPerUnit); vals.Add(ridge);
        vals.AddRange(pVB); vals.AddRange(pVM);

        // ---- profils de vides empilés en r / R_v (méthode des relevés : Hamaus et al. 2014)
        //   Grille 128³. R_v de chaque vide : rayon où le contraste CUMULÉ des baryons Δ(<r) remonte à −0,5.
        //   Empilement en x = r / R_v ; crête = maximum de δ_ord pour 1 ≤ x < 3, pour tous les vides puis
        //   séparément pour les petits et les grands (séparés par la médiane de R_v). Dans ΛCDM, les petits vides
        //   ont une crête nette (vides « dans un nuage »), les grands n'en ont presque pas.
        int N2 = RvGrid, N23 = N2 * N2 * N2;
        var fb = new float[N23]; var fm = new float[N23];
        for (int i = 0; i < data.Length; i++)
        {
            Body bd = data[i];
            bool medium = bd.sign < 0 || bd.sign == 2;
            var arr = medium ? fm : fb;
            float gx = (bd.pos.x / boxSize + 0.5f) * N2 - 0.5f;
            float gy = (bd.pos.y / boxSize + 0.5f) * N2 - 0.5f;
            float gz = (bd.pos.z / boxSize + 0.5f) * N2 - 0.5f;
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy), z0 = Mathf.FloorToInt(gz);
            float fx = gx - x0, fy = gy - y0, fz = gz - z0;
            for (int k = 0; k < 8; k++)
            {
                int ox = k & 1, oy = (k >> 1) & 1, oz = (k >> 2) & 1;
                float w = (ox == 1 ? fx : 1 - fx) * (oy == 1 ? fy : 1 - fy) * (oz == 1 ? fz : 1 - fz);
                int ix = ((x0 + ox) % N2 + N2) % N2, iy = ((y0 + oy) % N2 + N2) % N2, iz = ((z0 + oz) % N2 + N2) % N2;
                arr[ix + N2 * (iy + N2 * iz)] += w;
            }
        }
        double mean2b = nb / (double)N23, mean2m = nm / (double)N23;
        double h2 = boxSize / (double)N2;                                           // cellule fine (unités du code)
        double h2Mpc = h2 * mpcPerUnit;
        int nR = (int)System.Math.Min(N2 / 2 - 1, System.Math.Ceiling(RvMaxMpc / System.Math.Max(h2Mpc, 1e-6)));
        // Détection dédiée (indépendante de celle sur 32³) : minima locaux du contraste des baryons sur 64³,
        // lissé sur 3×3×3 cellules (≈ 14 Mpc pour 300 Mpc, limite le bruit de Poisson), avec δ_lissé < −0,5.
        // Seuil modéré pour garder les petits vides (ceux qui ont une crête). Au plus RvMaxVoids, choisis
        // régulièrement dans l'ordre de profondeur pour couvrir toutes les tailles.
        var dsm = new double[N3];
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    double acc = 0;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                acc += cb[(x + dx + N) % N + N * ((y + dy + N) % N + N * ((z + dz + N) % N))];
                    dsm[x + N * (y + N * z)] = acc / (27.0 * mean64b) - 1.0;
                }
        // Départage des plateaux : dans un vide parfaitement vide, δ_lissé = −1 partout et chaque cellule serait un
        // « minimum ». On retranche 10⁻⁴ × la distance (en cellules) à la cellule occupée la plus proche, de sorte que
        // le minimum retenu soit le centre du plateau (point le plus éloigné de toute matière). Sans effet ailleurs.
        {
            var dist = new int[N3];
            var bfsQ = new System.Collections.Generic.Queue<int>();
            for (int i = 0; i < N3; i++)
            {
                if (cb[i] > 0) { dist[i] = 0; bfsQ.Enqueue(i); } else dist[i] = int.MaxValue;
            }
            while (bfsQ.Count > 0)
            {
                int cur = bfsQ.Dequeue();
                int cx0 = cur % N, cy0 = (cur / N) % N, cz0 = cur / (N * N);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nb2 = (cx0 + dx + N) % N + N * ((cy0 + dy + N) % N + N * ((cz0 + dz + N) % N));
                            if (dist[nb2] == int.MaxValue) { dist[nb2] = dist[cur] + 1; bfsQ.Enqueue(nb2); }
                        }
            }
            for (int i = 0; i < N3; i++) if (dist[i] != int.MaxValue) dsm[i] -= 1e-4 * dist[i];
        }
        var rcand = new System.Collections.Generic.List<int>();
        for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int j0 = x + N * (y + N * z);
                    double v0 = dsm[j0];
                    if (v0 >= RvDetectDelta) continue;
                    bool isMin = true;
                    for (int dz = -1; dz <= 1 && isMin; dz++)
                        for (int dy = -1; dy <= 1 && isMin; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;
                                if (dsm[(x + dx + N) % N + N * ((y + dy + N) % N + N * ((z + dz + N) % N))] < v0) { isMin = false; break; }
                            }
                    if (isMin) rcand.Add(j0);
                }
        rcand.Sort((p, q) => dsm[p].CompareTo(dsm[q]));
        int scale = N2 / N;                                                          // cellules fines par cellule 64³
        // ---- catalogue de vides commun (empilement, lentille, fonction de taille)
        //   Tous les minima détectés : R_v par la règle Δ_ord cumulé = −0,5 (coquilles de la grille 128³), puis
        //   suppression des recouvrements, du plus grand au plus petit : un vide est rejeté s'il chevauche un vide
        //   plus grand déjà retenu (distance des centres < R₁ + R₂, méthode des « vides sphériques », Padilla 2005).
        //   Sans cela, un grand vide parfaitement vide (Janus) fournissait des centaines de minima décentrés,
        //   comptés comme autant de petits vides et sur-représentés dans l'empilement.
        BuildShellOffsets(N2 / 2 - 1);                                             // coquilles jusqu'à la demi-boîte (préfixes)
        var vsfR = new System.Collections.Generic.List<double>();
        var vsfC = new System.Collections.Generic.List<Vector3>();
        var vsfI = new System.Collections.Generic.List<int>();
        foreach (int vk in rcand)
        {
            double cxf = scale * (vk % N) + 0.5 * scale, cyf = scale * ((vk / N) % N) + 0.5 * scale, czf = scale * (vk / (N * N)) + 0.5 * scale;
            int ic = (int)cxf, jc = (int)cyf, kc = (int)czf;
            double cb3 = 0, cn3 = 0, prevD3 = double.NaN, rv3 = double.NaN;
            for (int r = 0; r < nR; r++)
            {
                for (int o = shellStart[r]; o < shellStart[r + 1]; o++)
                {
                    int i = ((ic + shellDx[o]) % N2 + N2) % N2 + N2 * (((jc + shellDy[o]) % N2 + N2) % N2 + N2 * (((kc + shellDz[o]) % N2 + N2) % N2));
                    cb3 += fb[i]; cn3++;
                }
                double D = cn3 > 0 ? cb3 / (cn3 * mean2b) - 1 : double.NaN;
                if (r == 0 && D >= -0.5) break;
                if (D >= -0.5) { double f = (-0.5 - prevD3) / (D - prevD3); rv3 = (r + f) * h2; break; }
                prevD3 = D;
            }
            if (double.IsNaN(rv3) || rv3 < 2 * h2) continue;
            vsfR.Add(rv3); vsfC.Add(new Vector3((float)cxf, (float)cyf, (float)czf)); vsfI.Add(vk);
        }
        var order = new System.Collections.Generic.List<int>();
        for (int v = 0; v < vsfR.Count; v++) order.Add(v);
        order.Sort((p, q) => vsfR[q].CompareTo(vsfR[p]));
        var keptR = new System.Collections.Generic.List<double>();
        var keptC = new System.Collections.Generic.List<Vector3>();
        var keptI = new System.Collections.Generic.List<int>();
        foreach (int v in order)
        {
            bool inside = false;
            for (int u = 0; u < keptR.Count && !inside; u++)
            {
                double ddx = System.Math.Abs(vsfC[v].x - keptC[u].x); ddx = System.Math.Min(ddx, N2 - ddx);
                double ddy = System.Math.Abs(vsfC[v].y - keptC[u].y); ddy = System.Math.Min(ddy, N2 - ddy);
                double ddz = System.Math.Abs(vsfC[v].z - keptC[u].z); ddz = System.Math.Min(ddz, N2 - ddz);
                double dd = System.Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz) * h2;     // unités du code
                if (dd < keptR[u] + vsfR[v]) inside = true;
            }
            if (!inside) { keptR.Add(vsfR[v]); keptC.Add(vsfC[v]); keptI.Add(vsfI[v]); }
        }
        // empilement : le catalogue (déjà trié du plus grand au plus petit), au plus RvMaxVoids choisis régulièrement
        var rsel = new System.Collections.Generic.List<int>();
        var rselR = new System.Collections.Generic.List<double>();
        if (keptI.Count <= RvMaxVoids) { rsel.AddRange(keptI); rselR.AddRange(keptR); }
        else for (int t = 0; t < RvMaxVoids; t++)
            {
                int jt = (int)((long)t * keptI.Count / RvMaxVoids);
                rsel.Add(keptI[jt]); rselR.Add(keptR[jt]);
            }
        // profils jusqu'à 3 R_v pour chaque vide (et non plus 80 Mpc) : nécessaire pour la crête des grands vides
        int nRS = N2 / 2 - 1;

        var rvR = new System.Collections.Generic.List<double>();                     // R_v (unités du code)
        var rvB = new System.Collections.Generic.List<double[]>();
        var rvM = new System.Collections.Generic.List<double[]>();
        var rvN = new System.Collections.Generic.List<double[]>();
        for (int iv = 0; iv < rsel.Count; iv++)
        {
            int vk = rsel[iv];
            int lim = System.Math.Min(nRS, (int)System.Math.Ceiling(3.05 * rselR[iv] / h2) + 1);
            double cxf = scale * (vk % N) + 0.5 * scale, cyf = scale * ((vk / N) % N) + 0.5 * scale, czf = scale * (vk / (N * N)) + 0.5 * scale;
            var sbv = new double[lim]; var smv = new double[lim]; var snv = new double[lim];
            int ic = (int)cxf, jc = (int)cyf, kc = (int)czf;
            for (int dz = -lim; dz <= lim; dz++)
                for (int dy = -lim; dy <= lim; dy++)
                    for (int dx = -lim; dx <= lim; dx++)
                    {
                        double ddx = ic + dx + 0.5 - cxf, ddy = jc + dy + 0.5 - cyf, ddz = kc + dz + 0.5 - czf;
                        double dist = System.Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);   // en cellules fines
                        int r = (int)dist;
                        if (r >= lim) continue;
                        int i = ((ic + dx) % N2 + N2) % N2 + N2 * (((jc + dy) % N2 + N2) % N2 + N2 * (((kc + dz) % N2 + N2) % N2));
                        sbv[r] += fb[i]; smv[r] += fm[i]; snv[r]++;
                    }
            // R_v : contraste cumulé des baryons Δ(<r) remonte à −0,5
            double cb2 = 0, cn2 = 0, prevD = double.NaN, rv = double.NaN;
            for (int r = 0; r < lim; r++)
            {
                cb2 += sbv[r]; cn2 += snv[r];
                double D = cn2 > 0 ? cb2 / (cn2 * mean2b) - 1 : double.NaN;
                if (r == 0 && D >= -0.5) break;                                      // pas un vide à cette résolution
                if (D >= -0.5) { double f = (-0.5 - prevD) / (D - prevD); rv = (r + f) * h2; break; }   // bord extérieur des coquilles
                prevD = D;
            }
            if (double.IsNaN(rv) || rv < 2 * h2) continue;                            // trop petit pour être résolu
            rvR.Add(rv); rvB.Add(sbv); rvM.Add(smv); rvN.Add(snv);
        }
        // classes de taille : tiers inférieur, médian, supérieur de R_v
        double rvMedian = double.NaN, rvT1 = double.NaN, rvT2 = double.NaN, rvSmallMed = double.NaN;
        if (rvR.Count > 0)
        {
            var sorted = new System.Collections.Generic.List<double>(rvR); sorted.Sort();
            rvMedian = sorted[sorted.Count / 2];
            rvT1 = sorted[sorted.Count / 3]; rvT2 = sorted[(2 * sorted.Count) / 3];
            rvSmallMed = sorted[sorted.Count / 6];
        }
        double[] StackRv(int cls, out double[] outM)                                // cls : 0 = tous, 1 = petits, 2 = moyens, 3 = grands
        {
            var sB = new double[RvBins]; var sM = new double[RvBins]; var sN = new double[RvBins];
            for (int v = 0; v < rvR.Count; v++)
            {
                if (cls == 1 && rvR[v] >= rvT1) continue;
                if (cls == 2 && (rvR[v] < rvT1 || rvR[v] >= rvT2)) continue;
                if (cls == 3 && rvR[v] < rvT2) continue;
                for (int r = 0; r < rvB[v].Length; r++)
                {
                    double xr = (r + 0.5) * h2 / rvR[v];
                    int q = (int)(xr / RvDx);
                    if (q >= RvBins) break;
                    sB[q] += rvB[v][r]; sM[q] += rvM[v][r]; sN[q] += rvN[v][r];
                }
            }
            var pb = new double[RvBins]; outM = new double[RvBins];
            for (int q = 0; q < RvBins; q++)
            {
                pb[q] = sN[q] > 0 ? sB[q] / (sN[q] * mean2b) - 1 : double.NaN;
                outM[q] = sN[q] > 0 ? sM[q] / (sN[q] * mean2m) - 1 : double.NaN;
            }
            return pb;
        }
        double RidgeOf(double[] pb, out double xAt)
        {
            double best = double.NaN; xAt = double.NaN;
            for (int q = 0; q < RvBins; q++)
            {
                double xc = (q + 0.5) * RvDx;
                if (xc < 1.0 || double.IsNaN(pb[q])) continue;
                if (double.IsNaN(best) || pb[q] > best) { best = pb[q]; xAt = xc; }
            }
            return best;
        }
        var rvAllB = StackRv(0, out double[] rvAllM);
        var rvSmallB = StackRv(1, out double[] rvSmallM);
        var rvMidB = StackRv(2, out _);
        var rvLargeB = StackRv(3, out double[] rvLargeM);
        double ridgeAll = RidgeOf(rvAllB, out double xRidge);
        double ridgeSmall = RidgeOf(rvSmallB, out double xRidgeSmall);
        double ridgeMid = RidgeOf(rvMidB, out _), ridgeLarge = RidgeOf(rvLargeB, out _);
        vals.Add(rvR.Count); vals.Add(rvMedian * mpcPerUnit); vals.Add(rvSmallMed * mpcPerUnit);
        vals.Add(ridgeAll); vals.Add(xRidge);
        vals.Add(ridgeSmall); vals.Add(xRidgeSmall); vals.Add(ridgeMid); vals.Add(ridgeLarge);
        vals.AddRange(rvAllB); vals.AddRange(rvAllM);
        vals.AddRange(rvSmallB); vals.AddRange(rvSmallM);

        // ---- lentille gravitationnelle des vides (point 2)
        //   Source vue par la lumière du secteur positif = celle qui accélère les particules positives :
        //     Janus  : ρ_ord − (c−/c+)² |ρ−|   (les masses négatives dévient la lumière en sens inverse)
        //     témoins : ρ_ord + ρ_MN ;  positives seules : ρ_ord.
        //   Contraste effectif δ_eff = (ρ̄_ord δ_ord + w ρ̄_ord δ_mil) / ρ̄_réf, w = poids du milieu, ρ̄_réf = (Ω_m/Ω_b) ρ̄_ord
        //   (même normalisation pour tous les modèles, qui ont les mêmes baryons). Projection d'Abel exacte des
        //   coquilles empilées (profil constant par coquille), tronquée à 3 R_v :
        //     Σ̂(X) = Σ_q δ_q · 2 [√(x_{q+1}² − X²) − √(max(x_q, X)² − X²)]       (unités ρ̄_réf R_v)
        //     ΔΣ̂(X) = Σ̂(< X) − Σ̂(X)   (ce que mesurent les relevés de lentille faible : négatif pour un vide)
        //   Conversion : ΔΣ [h M☉/pc²] = ΔΣ̂ × Ω_m ρ_crit × R_v [Mpc/h] = ΔΣ̂ × 0,0860 × R_v [Mpc/h].
        double wMed = LensMediumWeight();
        double refFac = LcdmOb / LcdmOm;                       // ρ̄_ord / ρ̄_réf
        double rvHMed = rvMedian * mpcPerUnit * LcdmH, rvHSmall = rvSmallMed * mpcPerUnit * LcdmH;
        double rvHLarge = double.NaN;
        if (rvR.Count > 0)
        {
            var srt = new System.Collections.Generic.List<double>(rvR); srt.Sort();
            rvHLarge = srt[System.Math.Min(srt.Count - 1, (5 * srt.Count) / 6)] * mpcPerUnit * LcdmH;
        }
        double[] EffProfile(double[] pb, double[] pm, double wb, double wm)
        {
            var d = new double[RvBins];
            for (int q = 0; q < RvBins; q++)
            {
                double vb = pb[q], vm = pm != null ? pm[q] : 0;
                if (double.IsNaN(vb)) vb = 0;                  // coquille vide (rare) : contraste nul
                if (double.IsNaN(vm)) vm = 0;
                d[q] = (wb * vb + wm * vm) * refFac;
            }
            return d;
        }
        double SigmaAt(double[] d, double X)
        {
            double sum = 0;
            for (int q = 0; q < RvBins; q++)
            {
                double x0 = q * RvDx, x1 = (q + 1) * RvDx;
                if (x1 <= X) continue;
                double lo = System.Math.Max(x0, X);
                sum += d[q] * 2.0 * (System.Math.Sqrt(x1 * x1 - X * X) - System.Math.Sqrt(lo * lo - X * X));
            }
            return sum;
        }
        void LensProfiles(double[] d, out double[] sig, out double[] dsig)
        {
            sig = new double[RvBins]; dsig = new double[RvBins];
            const int sub = 20;                                // intégration de Σ̄(<X) : 20 pas par classe
            double acc = 0; double dX = RvDx / sub;
            for (int q = 0; q < RvBins; q++)
            {
                for (int s2 = 0; s2 < sub; s2++)
                {
                    double Xm = (q * sub + s2 + 0.5) * dX;
                    acc += SigmaAt(d, Xm) * Xm * dX;
                }
                double X = (q + 1) * RvDx;
                double s0 = SigmaAt(d, X);
                sig[q] = s0;
                dsig[q] = 2.0 * acc / (X * X) - s0;
            }
        }
        double MinOf(double[] v, out double xAt)
        {
            double m = double.NaN; xAt = double.NaN;
            for (int q = 0; q < RvBins; q++)
                if (!double.IsNaN(v[q]) && (double.IsNaN(m) || v[q] < m)) { m = v[q]; xAt = (q + 1) * RvDx; }
            return m;
        }
        double[] lensSig, lensDsig, lensSmallDsig, lensLargeDsig;
        double sigC = double.NaN, sigCb = double.NaN, sigCm = double.NaN;
        if (rvR.Count > 0)
        {
            var dAll = EffProfile(rvAllB, rvAllM, 1.0, wMed);
            LensProfiles(dAll, out lensSig, out lensDsig);
            LensProfiles(EffProfile(rvSmallB, rvSmallM, 1.0, wMed), out _, out lensSmallDsig);
            LensProfiles(EffProfile(rvLargeB, rvLargeM, 1.0, wMed), out _, out lensLargeDsig);
            sigC = SigmaAt(dAll, 0.0);
            sigCb = SigmaAt(EffProfile(rvAllB, null, 1.0, 0.0), 0.0);
            sigCm = SigmaAt(EffProfile(new double[RvBins], rvAllM, 0.0, wMed), 0.0);
        }
        else
        {
            lensSig = new double[RvBins]; lensDsig = new double[RvBins];
            lensSmallDsig = new double[RvBins]; lensLargeDsig = new double[RvBins];
            for (int q = 0; q < RvBins; q++) lensSig[q] = lensDsig[q] = lensSmallDsig[q] = lensLargeDsig[q] = double.NaN;
        }
        double dsMin = MinOf(lensDsig, out double xDsMin);
        double dsMinS = MinOf(lensSmallDsig, out double xDsMinS);
        double dsMinL = MinOf(lensLargeDsig, out _);
        vals.Add(wMed);
        vals.Add(sigC); vals.Add(sigCb); vals.Add(sigCm);
        vals.Add(dsMin); vals.Add(xDsMin); vals.Add(dsMin * LensPhysFactor * rvHMed);
        vals.Add(dsMinS); vals.Add(xDsMinS); vals.Add(dsMinS * LensPhysFactor * rvHSmall);
        vals.Add(dsMinL); vals.Add(dsMinL * LensPhysFactor * rvHLarge);
        vals.AddRange(lensSig); vals.AddRange(lensDsig); vals.AddRange(lensSmallDsig); vals.AddRange(lensLargeDsig);

        // ---- empilement à taille de vide égale : tranches fixes de R_v en Mpc/h (mêmes pour tous les modèles)
        //   Permet de comparer Janus et ΛCDM à R_v égal et de superposer les profils aux mesures publiées
        //   (profils de densité et ΔΣ des relevés, présentés par tranches de rayon). ΔΣ converti avec le R_v médian
        //   de la tranche ; profils δ_ord, δ_mil en x = r/R_v.
        double toMpchV = mpcPerUnit * LcdmH;
        var rvbLines = new System.Text.StringBuilder();
        for (int t = 0; t + 1 < RvbEdges.Length; t++)
        {
            double lo = RvbEdges[t], hi = RvbEdges[t + 1];
            var sB = new double[RvBins]; var sM = new double[RvBins]; var sN = new double[RvBins];
            var rs = new System.Collections.Generic.List<double>();
            for (int v = 0; v < rvR.Count; v++)
            {
                double rm = rvR[v] * toMpchV;
                if (double.IsNaN(rm) || rm < lo || rm >= hi) continue;
                rs.Add(rm);
                for (int r = 0; r < rvB[v].Length; r++)
                {
                    double xr = (r + 0.5) * h2 / rvR[v];
                    int q = (int)(xr / RvDx);
                    if (q >= RvBins) break;
                    sB[q] += rvB[v][r]; sM[q] += rvM[v][r]; sN[q] += rvN[v][r];
                }
            }
            var pbT = new double[RvBins]; var pmT = new double[RvBins];
            for (int q = 0; q < RvBins; q++)
            {
                pbT[q] = sN[q] > 0 ? sB[q] / (sN[q] * mean2b) - 1 : double.NaN;
                pmT[q] = sN[q] > 0 && mean2m > 0 ? sM[q] / (sN[q] * mean2m) - 1 : double.NaN;
            }
            rs.Sort();
            double rmedT = rs.Count > 0 ? rs[rs.Count / 2] : double.NaN;
            double ridgeT = double.NaN, xRidgeT = double.NaN, dsMinT = double.NaN, xDsT = double.NaN;
            var dsPhys = new double[RvBins];
            for (int q = 0; q < RvBins; q++) dsPhys[q] = double.NaN;
            if (rs.Count > 0)
            {
                ridgeT = RidgeOf(pbT, out xRidgeT);
                LensProfiles(EffProfile(pbT, pmT, 1.0, wMed), out _, out double[] dsT);
                for (int q = 0; q < RvBins; q++) dsPhys[q] = dsT[q] * LensPhysFactor * rmedT;
                dsMinT = MinOf(dsPhys, out xDsT);
            }
            vals.Add(rs.Count); vals.Add(rmedT);
            vals.Add(pbT[0]); vals.Add(pmT[0]);
            vals.Add(ridgeT); vals.Add(xRidgeT);
            vals.Add(dsMinT); vals.Add(xDsT);
            vals.AddRange(pbT); vals.AddRange(pmT); vals.AddRange(dsPhys);
            rvbLines.Append($"  [{lo:F0}–{hi:F0}] n={rs.Count}" +
                            (rs.Count > 0 ? L(" crête ", " ridge ") + $"{ridgeT:+0.00;-0.00} ΔΣmin {dsMinT:+0.00;-0.00}" : ""));
        }

        // ---- galaxies traceuses et vides « comme les relevés » (BOSS, DES)
        //   Groupes friends-of-friends des particules ordinaires (longueur de liaison 0,2 × séparation moyenne,
        //   au moins GalMinMembers particules), les plus massifs gardés jusqu'à la densité de BOSS
        //   (GalDensity ≈ 3·10⁻⁴ (h/Mpc)³) : même nombre de traceurs pour tous les modèles. Vides sphériques dans
        //   ces traceurs : centres = maxima de la distance à la galaxie la plus proche, R_v = rayon où le contraste
        //   cumulé des galaxies atteint GalVoidDelta (−0,8), sans recouvrement. Profils (galaxies, matière ordinaire,
        //   milieu) et lentille ΔΣ par tranches de R_v en Mpc/h. Calculé à la mesure finale seulement (campagne).
        var galNames = GalNames();
        bool doGal = isFinal || !batchMode;
        var gv = new System.Collections.Generic.List<double>();
        string galLine = "";
        if (doGal)
        {
            var tracers = FindGalaxyTracers(data, out int nGroups, out int minMem, out double targetN, out double pMass,
                                            out System.Collections.Generic.List<Vector3> clCenters);
            int nT = tracers.Count;
            double volMpchG = System.Math.Pow(lssBoxMpc * LcdmH, 3);
            double per100G = volMpchG > 0 ? 1e6 / volMpchG : double.NaN;
            gv.Add(pMass); gv.Add(nGroups); gv.Add(targetN); gv.Add(nT); gv.Add(minMem);
            gv.Add(volMpchG > 0 ? nT / volMpchG : double.NaN);
            // grille fine des traceurs (plus proche cellule)
            var ft = new float[N23];
            foreach (var tp in tracers)
            {
                int ix = Mathf.Clamp(Mathf.FloorToInt((tp.x / boxSize + 0.5f) * N2), 0, N2 - 1);
                int iy = Mathf.Clamp(Mathf.FloorToInt((tp.y / boxSize + 0.5f) * N2), 0, N2 - 1);
                int iz = Mathf.Clamp(Mathf.FloorToInt((tp.z / boxSize + 0.5f) * N2), 0, N2 - 1);
                ft[ix + N2 * (iy + N2 * iz)] += 1f;
            }
            double meanT = nT / (double)N23;
            int nRSg = N2 / 2 - 1;
            // distance de chaque cellule 64³ à la galaxie la plus proche (propagation du plus proche voisin)
            var near = new int[N3]; var dNear = new double[N3];
            for (int i = 0; i < N3; i++) { near[i] = -1; dNear[i] = double.MaxValue; }
            double hc = boxSize / (double)N;
            double DistTo(int cell, int t)
            {
                double cxp = ((cell % N) + 0.5) * hc - 0.5 * boxSize, cyp = (((cell / N) % N) + 0.5) * hc - 0.5 * boxSize, czp = ((cell / (N * N)) + 0.5) * hc - 0.5 * boxSize;
                double ddx = System.Math.Abs(cxp - tracers[t].x); ddx = System.Math.Min(ddx, boxSize - ddx);
                double ddy = System.Math.Abs(cyp - tracers[t].y); ddy = System.Math.Min(ddy, boxSize - ddy);
                double ddz = System.Math.Abs(czp - tracers[t].z); ddz = System.Math.Min(ddz, boxSize - ddz);
                return System.Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
            }
            var gq = new System.Collections.Generic.Queue<int>();
            for (int t = 0; t < nT; t++)
            {
                int ix = Mathf.Clamp(Mathf.FloorToInt((tracers[t].x / boxSize + 0.5f) * N), 0, N - 1);
                int iy = Mathf.Clamp(Mathf.FloorToInt((tracers[t].y / boxSize + 0.5f) * N), 0, N - 1);
                int iz = Mathf.Clamp(Mathf.FloorToInt((tracers[t].z / boxSize + 0.5f) * N), 0, N - 1);
                int ci = ix + N * (iy + N * iz);
                double dd = DistTo(ci, t);
                if (dd < dNear[ci]) { if (near[ci] < 0) gq.Enqueue(ci); near[ci] = t; dNear[ci] = dd; }
            }
            while (gq.Count > 0)
            {
                int cur = gq.Dequeue();
                int t = near[cur];
                int cx0 = cur % N, cy0 = (cur / N) % N, cz0 = cur / (N * N);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0 && dz == 0) continue;
                            int nb2 = (cx0 + dx + N) % N + N * ((cy0 + dy + N) % N + N * ((cz0 + dz + N) % N));
                            if (near[nb2] == t) continue;
                            double dd = DistTo(nb2, t);
                            if (dd < dNear[nb2] - 1e-9) { near[nb2] = t; dNear[nb2] = dd; gq.Enqueue(nb2); }
                        }
            }
            // candidats : maxima locaux de la distance (vide le plus grand possible autour du point)
            var gcand = new System.Collections.Generic.List<int>();
            if (nT >= GalMinTracers)
                for (int z = 0; z < N; z++)
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            int j0 = x + N * (y + N * z);
                            double v0 = dNear[j0];
                            if (v0 < 1.5 * hc || v0 == double.MaxValue) continue;
                            bool isMax = true;
                            for (int dz = -1; dz <= 1 && isMax; dz++)
                                for (int dy = -1; dy <= 1 && isMax; dy++)
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        if (dx == 0 && dy == 0 && dz == 0) continue;
                                        int jn = (x + dx + N) % N + N * ((y + dy + N) % N + N * ((z + dz + N) % N));
                                        double vn = dNear[jn];
                                        if (vn > v0 || (vn == v0 && jn < j0)) { isMax = false; break; }
                                    }
                            if (isMax) gcand.Add(j0);
                        }
            // R_v de chaque candidat : contraste cumulé des galaxies = GalVoidDelta
            var gR = new System.Collections.Generic.List<double>(); var gI = new System.Collections.Generic.List<int>();
            foreach (int vk in gcand)
            {
                double cxf = scale * (vk % N) + 0.5 * scale, cyf = scale * ((vk / N) % N) + 0.5 * scale, czf = scale * (vk / (N * N)) + 0.5 * scale;
                int ic = (int)cxf, jc = (int)cyf, kc = (int)czf;
                double ct = 0, cn = 0, prevDt = double.NaN, rvg = double.NaN;
                for (int r = 0; r < nRSg; r++)
                {
                    for (int o = shellStart[r]; o < shellStart[r + 1]; o++)
                    {
                        int i = ((ic + shellDx[o]) % N2 + N2) % N2 + N2 * (((jc + shellDy[o]) % N2 + N2) % N2 + N2 * (((kc + shellDz[o]) % N2 + N2) % N2));
                        ct += ft[i]; cn++;
                    }
                    double D = ct / (cn * meanT) - 1;
                    if (r == 0 && D >= GalVoidDelta) break;
                    if (D >= GalVoidDelta) { double f = (GalVoidDelta - prevDt) / (D - prevDt); rvg = (r + f) * h2; break; }
                    prevDt = D;
                }
                if (double.IsNaN(rvg) || rvg < 2 * h2) continue;
                gR.Add(rvg); gI.Add(vk);
            }
            var gOrd = new System.Collections.Generic.List<int>();
            for (int v = 0; v < gR.Count; v++) gOrd.Add(v);
            gOrd.Sort((p, q) => gR[q].CompareTo(gR[p]));
            var gkR = new System.Collections.Generic.List<double>(); var gkI = new System.Collections.Generic.List<int>();
            foreach (int v in gOrd)
            {
                int vk = gI[v];
                double ax = vk % N, ay = (vk / N) % N, az = vk / (N * N);
                bool overl = false;
                for (int u = 0; u < gkR.Count && !overl; u++)
                {
                    int uk = gkI[u];
                    double ddx = System.Math.Abs(ax - uk % N); ddx = System.Math.Min(ddx, N - ddx);
                    double ddy = System.Math.Abs(ay - (uk / N) % N); ddy = System.Math.Min(ddy, N - ddy);
                    double ddz = System.Math.Abs(az - uk / (N * N)); ddz = System.Math.Min(ddz, N - ddz);
                    if (System.Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz) * hc < gkR[u] + gR[v]) overl = true;
                }
                if (!overl) { gkR.Add(gR[v]); gkI.Add(vk); }
            }
            // fonction de taille
            var gCount = new double[VsfBins];
            double gVol = 0; int g20 = 0, g30 = 0, g40 = 0;
            var gRm = new System.Collections.Generic.List<double>();
            foreach (double rc in gkR)
            {
                double rm = rc * mpcPerUnit * LcdmH;
                gRm.Add(rm); gVol += 4.0 / 3.0 * System.Math.PI * rm * rm * rm;
                if (rm > 20) g20++; if (rm > 30) g30++; if (rm > 40) g40++;
                int k = (int)System.Math.Floor(System.Math.Log(rm / VsfRmin) / VsfDlnR);
                if (k >= 0 && k < VsfBins) gCount[k]++;
            }
            gRm.Sort();
            gv.Add(gcand.Count); gv.Add(gkR.Count); gv.Add(gkR.Count * per100G);
            gv.Add(volMpchG > 0 ? gVol / volMpchG : double.NaN);
            gv.Add(gRm.Count > 0 ? gRm[gRm.Count / 2] : double.NaN);
            gv.Add(gRm.Count > 0 ? gRm[gRm.Count - 1] : double.NaN);
            gv.Add(g20 * per100G); gv.Add(g30 * per100G); gv.Add(g40 * per100G);
            gv.AddRange(gCount);
            for (int k = 0; k < VsfBins; k++) gv.Add(volMpchG > 0 ? gCount[k] / (volMpchG * VsfDlnR) : double.NaN);
            // profils empilés par tranche de R_v (coquilles jusqu'à 3 R_v, plafonnées à la demi-boîte)
            int nTr = GvEdges.Length - 1;
            var tB = new double[nTr][]; var tM = new double[nTr][]; var tT = new double[nTr][]; var tN = new double[nTr][];
            var tR = new System.Collections.Generic.List<double>[nTr];
            for (int t = 0; t < nTr; t++)
            {
                tB[t] = new double[RvBins]; tM[t] = new double[RvBins]; tT[t] = new double[RvBins]; tN[t] = new double[RvBins];
                tR[t] = new System.Collections.Generic.List<double>();
            }
            for (int v = 0; v < gkR.Count; v++)
            {
                double rm = gkR[v] * mpcPerUnit * LcdmH;
                int t = -1;
                for (int b2 = 0; b2 < nTr; b2++) if (rm >= GvEdges[b2] && rm < GvEdges[b2 + 1]) t = b2;
                if (t < 0) continue;
                tR[t].Add(rm);
                int vk = gkI[v];
                double cxf = scale * (vk % N) + 0.5 * scale, cyf = scale * ((vk / N) % N) + 0.5 * scale, czf = scale * (vk / (N * N)) + 0.5 * scale;
                int ic = (int)cxf, jc = (int)cyf, kc = (int)czf;
                int lim = System.Math.Min(nRSg, (int)System.Math.Ceiling(3.05 * gkR[v] / h2) + 1);
                for (int r = 0; r < lim; r++)
                {
                    int q = (int)((r + 0.5) * h2 / gkR[v] / RvDx);
                    if (q >= RvBins) break;
                    for (int o = shellStart[r]; o < shellStart[r + 1]; o++)
                    {
                        int i = ((ic + shellDx[o]) % N2 + N2) % N2 + N2 * (((jc + shellDy[o]) % N2 + N2) % N2 + N2 * (((kc + shellDz[o]) % N2 + N2) % N2));
                        tB[t][q] += fb[i]; tM[t][q] += fm[i]; tT[t][q] += ft[i]; tN[t][q]++;
                    }
                }
            }
            var galSb = new System.Text.StringBuilder();
            for (int t = 0; t < nTr; t++)
            {
                var pbG = new double[RvBins]; var pmG = new double[RvBins]; var ptG = new double[RvBins];
                for (int q = 0; q < RvBins; q++)
                {
                    bool okq = tN[t][q] > 0;
                    pbG[q] = okq ? tB[t][q] / (tN[t][q] * mean2b) - 1 : double.NaN;
                    pmG[q] = okq && mean2m > 0 ? tM[t][q] / (tN[t][q] * mean2m) - 1 : double.NaN;
                    ptG[q] = okq && meanT > 0 ? tT[t][q] / (tN[t][q] * meanT) - 1 : double.NaN;
                }
                tR[t].Sort();
                double rmed = tR[t].Count > 0 ? tR[t][tR[t].Count / 2] : double.NaN;
                double rgT = double.NaN, xgT = double.NaN, rgB = double.NaN, dsMn = double.NaN, xDs = double.NaN;
                var dsPh = new double[RvBins];
                for (int q = 0; q < RvBins; q++) dsPh[q] = double.NaN;
                if (tR[t].Count > 0)
                {
                    rgT = RidgeOf(ptG, out xgT);
                    rgB = RidgeOf(pbG, out _);
                    LensProfiles(EffProfile(pbG, pmG, 1.0, wMed), out _, out double[] dsG);
                    for (int q = 0; q < RvBins; q++) dsPh[q] = dsG[q] * LensPhysFactor * rmed;
                    dsMn = MinOf(dsPh, out xDs);
                }
                gv.Add(tR[t].Count); gv.Add(rmed);
                gv.Add(ptG[0]); gv.Add(pbG[0]); gv.Add(pmG[0]);
                gv.Add(rgT); gv.Add(xgT); gv.Add(rgB);
                gv.Add(dsMn); gv.Add(xDs);
                gv.AddRange(ptG); gv.AddRange(pbG); gv.AddRange(pmG); gv.AddRange(dsPh);
                galSb.Append($"  [{GvEdges[t]:F0}–{GvEdges[t + 1]:F0}] n={tR[t].Count}" +
                             (tR[t].Count > 0 ? L(" crête gal ", " gal ridge ") + $"{rgT:+0.00;-0.00} ΔΣmin {dsMn:+0.00;-0.00}" : ""));
            }
            gv.AddRange(ClusterMassFunction(data, clCenters, pMass, out string clLine));
            galSb.Append("\n" + clLine);
            galLine = L("Galaxies traceuses : ", "Tracer galaxies: ") + $"{nT} / {targetN:F0}" + L(" visées (groupes ≥ ", " targeted (groups ≥ ") +
                      $"{GalMinMembers} : {nGroups}, " + L("plus petit retenu ", "smallest kept ") + $"{minMem} part. × {pMass:E1} M☉)\n" +
                      L("Vides de galaxies : ", "Galaxy voids: ") + $"{gkR.Count}" + L(", R_v médian ", ", median R_v ") +
                      (gRm.Count > 0 ? $"{gRm[gRm.Count / 2]:F1} Mpc/h" : "—") + galSb.ToString();
        }
        while (gv.Count < galNames.Count) gv.Add(double.NaN);   // mesures intermédiaires : colonnes vides
        if (gv.Count > galNames.Count) gv.RemoveRange(galNames.Count, gv.Count - galNames.Count);
        vals.AddRange(gv);

        // ---- fonction de taille des vides (point 3) : catalogue construit plus haut (keptR, sans recouvrement)
        double toMpch = mpcPerUnit * LcdmH;
        double volMpch = System.Math.Pow(lssBoxMpc * LcdmH, 3);                          // (Mpc/h)³
        double per100 = volMpch > 0 ? 1e6 / volMpch : double.NaN;                      // → par (100 Mpc/h)³
        var vsfCount = new double[VsfBins];
        double volSum = 0, rSum = 0, rMax = double.NaN; int n10 = 0, n20 = 0, n30 = 0;
        var rKept = new System.Collections.Generic.List<double>();
        foreach (double rcode in keptR)
        {
            double rm = rcode * toMpch;
            rKept.Add(rm); rSum += rm;
            if (double.IsNaN(rMax) || rm > rMax) rMax = rm;
            volSum += 4.0 / 3.0 * System.Math.PI * rm * rm * rm;
            if (rm > 10) n10++; if (rm > 20) n20++; if (rm > 30) n30++;
            int k = (int)System.Math.Floor(System.Math.Log(rm / VsfRmin) / VsfDlnR);
            if (k >= 0 && k < VsfBins) vsfCount[k]++;
        }
        rKept.Sort();
        var vsfDn = new double[VsfBins];
        for (int k = 0; k < VsfBins; k++) vsfDn[k] = volMpch > 0 ? vsfCount[k] / (volMpch * VsfDlnR) : double.NaN;
        vals.Add(rcand.Count); vals.Add(keptR.Count); vals.Add(keptR.Count * per100);
        vals.Add(volMpch > 0 ? volSum / volMpch : double.NaN);
        vals.Add(rKept.Count > 0 ? rKept[rKept.Count / 2] : double.NaN);
        vals.Add(rKept.Count > 0 ? rSum / rKept.Count : double.NaN);
        vals.Add(rMax);
        vals.Add(n10 * per100); vals.Add(n20 * per100); vals.Add(n30 * per100);
        vals.AddRange(vsfCount); vals.AddRange(vsfDn);

        // ---- spectre en unités physiques : k en h/Mpc, P en (Mpc/h)³ ; référence linéaire ΛCDM au même redshift
        double sH = lssBoxMpc > 0f ? lssBoxMpc * IcH / boxSize : double.NaN;        // (Mpc/h) par unité de longueur
        if (double.IsNaN(lcdmAmp1)) lcdmAmp1 = SpectrumAmplitude(1.0, 0.965);
        double zMeas = RedshiftAt(aNow);
        double dz0 = GrowthLcdm(System.Math.Max(0.0, zMeas));
        double ampNow = lcdmAmp1 * System.Math.Pow(0.81 * dz0, 2);
        var kph = new double[B]; var pbph = new double[B]; var pmph = new double[B]; var plin = new double[B]; var prat = new double[B];
        for (int b = 0; b < B; b++)
        {
            if (cnt[b] == 0 || double.IsNaN(sH)) { kph[b] = pbph[b] = pmph[b] = plin[b] = prat[b] = double.NaN; continue; }
            double kc = 2.0 * System.Math.PI / boxSize * ksum[b] / cnt[b];
            kph[b] = kc / sH;
            pbph[b] = pbb[b] / cnt[b] * sH * sH * sH;
            pmph[b] = pmm[b] / cnt[b] * sH * sH * sH;
            double t = TransferEH(kph[b]);
            plin[b] = ampNow * System.Math.Pow(kph[b], 0.965) * t * t;
            prat[b] = plin[b] > 0 ? pbph[b] / plin[b] : double.NaN;
        }
        vals.AddRange(kph); vals.AddRange(pbph); vals.AddRange(pmph); vals.AddRange(plin); vals.AddRange(prat);
        // P/P_lin interpolé (en log k) à k = 0,1 h/Mpc, puis forme normalisée
        double ratio01 = double.NaN;
        for (int b = 0; b + 1 < B; b++)
            if (!double.IsNaN(kph[b]) && !double.IsNaN(kph[b + 1]) && kph[b] <= 0.1 && kph[b + 1] >= 0.1 &&
                prat[b] > 0 && prat[b + 1] > 0)
            {
                double u = System.Math.Log(0.1 / kph[b]) / System.Math.Log(kph[b + 1] / kph[b]);
                ratio01 = System.Math.Exp((1 - u) * System.Math.Log(prat[b]) + u * System.Math.Log(prat[b + 1]));
            }
        vals.Add(ratio01);
        var pshape = new double[B];
        for (int b = 0; b < B; b++) pshape[b] = ratio01 > 0 ? prat[b] / ratio01 : double.NaN;
        vals.AddRange(pshape);
        double hNow = ExpansionOn ? Hubble(readbackTime) : 0.0;
        double tGyr = readbackTime * TimeUnitGyr();
        double zNow = RedshiftAt(aNow);

        // ---- affichage
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(L("STRUCTURE À GRANDE ÉCHELLE — modèle ", "LARGE-SCALE STRUCTURE — model ") + $"{lssModel}, expansion {EffectiveLaw}" +
                      (SelfConsistent ? L(" (auto-cohérente)", " (self-consistent)") : L(" (imposée)", " (imposed)")));
        sb.AppendLine($"t = {readbackTime:F2} (≈ {tGyr:F2} Gyr)   " + L("pas", "step") + $" {readbackStep}   a = {aNow:F3} / {expansionFactorEnd:F1}" +
                      $"   z ≈ {zNow:F2}   H = {hNow:F4} (≈ {HubbleKms(hNow):F0} km/s/Mpc)");
        if (IsRealisticIC && !string.IsNullOrEmpty(icInfo)) sb.AppendLine(icInfo);
        if (lssModel == LssModel.Janus && (LightSpeedRatio != 1f || NegOwnScale))
            sb.AppendLine(L("Secteur négatif : ", "Negative sector: ") + $"c−/c+ = {LightSpeedRatio:G3}   ā = {readbackANeg:G4}" +
                          (NegOwnScale ? $"   (ā/a = {readbackANeg / aNow:G3})" : L("   (même facteur d'échelle)", "   (same scale factor)")));
        if (SelfConsistent && lssModel == LssModel.Janus && !JanusSelfConsistentOk && janusCalibration == JanusCalibration.Densites)
            sb.AppendLine(L("ATTENTION : (c−/c+)²|ρ−| ≤ ρ+, loi de Janus impossible, Einstein-de Sitter utilisé",
                            "WARNING: (c−/c+)²|ρ−| ≤ ρ+, Janus law impossible, Einstein–de Sitter used"));
        sb.AppendLine($"σ(δ) 32³ : " + L("ordinaire", "ordinary") + $" {sigB:F3}   " + L("milieu", "medium") + $" {sigM:F3}   " +
                      L("corrélation", "correlation") + $" {corr:+0.00;-0.00}");
        sb.AppendLine(L("Vides", "Voids") + $" (δ < {LssVoidDelta}) : " + L("ordinaire", "ordinary") + $" {100.0 * voidB / M3:F1} %   " +
                      L("milieu", "medium") + $" {100.0 * voidM / M3:F1} %");
        sb.AppendLine(L("Matière ordinaire dans les surdensités du milieu : ", "Ordinary matter in medium overdensities: ") +
                      $"{100.0 * bInOver / nb:F1} % (volume {100.0 * overM / M3:F1} %)   δ max {maxB:F1}");
        sb.AppendLine(L("λ        Δ²_ord     Δ²_milieu    r(k)", "λ        Δ²_ord     Δ²_medium    r(k)"));
        for (int b = 0; b < B; b++)
            sb.AppendLine($"{LssLambda(b),4}   {d2b[b],9:E2}   {d2m[b],9:E2}   {rk[b],6:+0.00;-0.00}");
        sb.AppendLine(L("Profil autour de ", "Profile around ") + $"{peaks.Count}" + L(" pics du milieu ", " medium peaks ") +
                      $"({(peaks.Count > 0 ? 100.0 * inVoid / peaks.Count : 0):F0} %" +
                      L(" au centre d'un vide ordinaire) :", " at the centre of an ordinary-matter void):"));
        sb.Append("r <   ");
        for (int r = 0; r < NP; r++) sb.Append($"{LssProfEdges[r + 1],7:F0}");
        sb.Append("\nδ_ord ");
        for (int r = 0; r < NP; r++) sb.Append($"{pB[r],7:F2}");
        sb.Append(L("\nδ_mil ", "\nδ_med "));
        for (int r = 0; r < NP; r++) sb.Append($"{pM[r],7:F1}");
        sb.AppendLine();
        sb.AppendLine(L("Rayon des vides (δ_ord = −0,5) : ", "Void radius (δ_ord = −0.5): ") +
                      (double.IsNaN(voidR) ? "—" : $"{voidR:F1} ≈ {voidR * mpcPerUnit:F1} Mpc") +
                      (NegOwnScale ? $"   ā/a = {negRatio:G3}" : ""));
        sb.AppendLine(L("Vides des baryons (minima δ_ord < ", "Baryon voids (minima δ_ord < ") + $"{LssVoidDelta}) : {voids.Count}" +
                      $" ({voids.Count / vol100:F2} / (100 Mpc)³)   " + L("rayon ", "radius ") +
                      (double.IsNaN(bvR) ? "—" : $"{bvR:F1} ≈ {bvR * mpcPerUnit:F1} Mpc") +
                      L("   crête ", "   ridge ") + (double.IsNaN(ridge) ? "—" : $"{ridge:+0.00;-0.00}"));
        sb.AppendLine(L("Vides en r/R_v : ", "Voids in r/R_v: ") + $"{rvR.Count}" + L(" vides, R_v médian ", " voids, median R_v ") +
                      (double.IsNaN(rvMedian) ? "—" : $"{rvMedian * mpcPerUnit:F1} Mpc") +
                      L("   crête : tous ", "   ridge: all ") + (double.IsNaN(ridgeAll) ? "—" : $"{ridgeAll:+0.00;-0.00} (x = {xRidge:F1})") +
                      L(", petits ", ", small ") + (double.IsNaN(ridgeSmall) ? "—" : $"{ridgeSmall:+0.00;-0.00}") +
                      L(", moyens ", ", medium ") + (double.IsNaN(ridgeMid) ? "—" : $"{ridgeMid:+0.00;-0.00}") +
                      L(", grands ", ", large ") + (double.IsNaN(ridgeLarge) ? "—" : $"{ridgeLarge:+0.00;-0.00}"));
        sb.AppendLine(L("Lentille des vides (ΔΣ min, unités ρ̄_réf R_v) : tous ", "Void lensing (min ΔΣ, units ρ̄_ref R_v): all ") +
                      (double.IsNaN(dsMin) ? "—" : $"{dsMin:+0.000;-0.000} (x = {xDsMin:F1}, ≈ {dsMin * LensPhysFactor * rvHMed:+0.00;-0.00} h M☉/pc²)") +
                      L(", petits ", ", small ") + (double.IsNaN(dsMinS) ? "—" : $"{dsMinS:+0.000;-0.000}") +
                      L(", grands ", ", large ") + (double.IsNaN(dsMinL) ? "—" : $"{dsMinL:+0.000;-0.000}") +
                      L("   Σ centre : ord ", "   Σ centre: ord ") + $"{sigCb:+0.00;-0.00}" + L(", milieu ", ", medium ") + $"{sigCm:+0.00;-0.00}" +
                      $" (w = {wMed:G3})");
        if (galLine.Length > 0) sb.AppendLine(galLine);
        sb.AppendLine(L("Vides par tranche de R_v (Mpc/h, ΔΣ en h M☉/pc²) :", "Voids by R_v bin (Mpc/h, ΔΣ in h M☉/pc²):") + rvbLines.ToString());
        sb.Append(L("Fonction de taille des vides : ", "Void size function: ") + $"{keptR.Count}" +
                  L(" vides sans recouvrement (", " non-overlapping voids (") + $"{rcand.Count}" + L(" minima), R_v médian ", " minima), median R_v ") +
                  (rKept.Count > 0 ? $"{rKept[rKept.Count / 2]:F1} Mpc/h" : "—") + L(", volume ", ", volume ") +
                  $"{100.0 * (volMpch > 0 ? volSum / volMpch : 0):F0} %\n   N par R_v (Mpc/h) : ");
        for (int k = 0; k < VsfBins; k++) sb.Append($"{VsfLabel(k)}:{vsfCount[k]:F0}  ");
        sb.AppendLine();
        sb.Append(L("Forme P/P_lin normalisée à k = 0,1 : ", "P/P_lin shape normalised at k = 0.1: "));
        for (int b = 0; b < B; b++) if (!double.IsNaN(pshape[b])) sb.Append($"k={kph[b]:F2}:{pshape[b]:F2}  ");
        sb.AppendLine();
        sb.Append(L("δ_ord ", "δ_ord "));
        for (int r = 0; r < NP; r++) sb.Append($"{pVB[r],7:F2}");
        sb.Append(L("\nδ_mil ", "\nδ_med "));
        for (int r = 0; r < NP; r++) sb.Append($"{pVM[r],7:F2}");
        sb.AppendLine();

        if (exportCsv)
        {
            WriteLssRow(aNow, hNow, vals);
            if (csvPath != null) sb.AppendLine("CSV : " + System.IO.Path.GetFileName(csvPath));
        }

        // ---- campagne
        if (batchMode && stats != null)
        {
            int nv = vals.Count;
            if (stats.lssSum == null) { stats.lssSum = new double[nv]; stats.lssCnt = new int[nv]; }
            if (SelfConsistent ? aNow >= lssSummaryFromA : readbackTime >= batchSummaryFrom)
            {
                stats.n++;
                for (int q = 0; q < nv; q++)
                    if (!double.IsNaN(vals[q])) { stats.lssSum[q] += vals[q]; stats.lssCnt[q]++; }
            }
            if (isFinal)
            {
                stats.lssFin = vals.ToArray();
                stats.finT = readbackTime;
                finalSampleDone = true;
            }
            sb.Insert(0, L("CAMPAGNE", "CAMPAIGN") + $" : run {runIndex + 1}/{queue.Count} — {queue[runIndex].name}, seed {seed}" +
                         (SelfConsistent ? $"   (a = {aNow:F2} / {expansionFactorEnd:F1})\n"
                                         : $"   (t = {readbackTime:F0} / {batchEndTime:F0})\n"));
        }
        string warnDt = TimestepWarning();
        if (warnDt != null) sb.Insert(0, warnDt + "\n");
        diagText = sb.ToString();
    }

    // Profils de vides en r / R_v : 15 intervalles de 0,2 R_v jusqu'à 3 R_v
    const int RvBins = 15; const double RvDx = 0.2;
    // ---- galaxies traceuses (friends-of-friends sur la matière ordinaire)
    const float GalLinkB = 0.2f;          // longueur de liaison / séparation moyenne
    const int GalMinMembers = 20;         // taille minimale d'un groupe
    const double GalDensity = 3e-4;       // densité visée (h/Mpc)³ ≈ BOSS CMASS
    const int GalMinTracers = 50;         // en dessous, pas de vides de galaxies
    const double GalVoidDelta = -0.8;     // seuil des vides sphériques (contraste cumulé des galaxies)
    static readonly double[] GvEdges = { 10, 20, 40, 80 };   // tranches de R_v (Mpc/h), comme DES / BOSS
    static string GvName(int t) => "gv" + GvEdges[t].ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                                   "_" + GvEdges[t + 1].ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    static System.Collections.Generic.List<string> GalNames()
    {
        var l = new System.Collections.Generic.List<string>
        {
            "gal_particle_mass_msun", "gal_n_groups", "gal_target", "gal_n_tracers", "gal_min_members_kept", "gal_density_hmpc3",
            "gv_n_candidates", "gv_n_voids", "gv_density_per_100mpch3", "gv_volume_frac", "gv_rv_median_mpch", "gv_rv_max_mpch",
            "gv_n_gt20_per_100mpch3", "gv_n_gt30_per_100mpch3", "gv_n_gt40_per_100mpch3"
        };
        for (int k = 0; k < VsfBins; k++) l.Add("gv_count_R" + VsfLabel(k));
        for (int k = 0; k < VsfBins; k++) l.Add("gv_dndlnR_R" + VsfLabel(k));
        for (int t = 0; t + 1 < GvEdges.Length; t++)
        {
            string pre = GvName(t);
            l.Add(pre + "_count"); l.Add(pre + "_rv_median_mpch");
            l.Add(pre + "_center_gal"); l.Add(pre + "_center_b"); l.Add(pre + "_center_m");
            l.Add(pre + "_ridge_gal"); l.Add(pre + "_x_ridge_gal"); l.Add(pre + "_ridge_b");
            l.Add(pre + "_lens_dsigma_min_hmsun_pc2"); l.Add(pre + "_lens_x_min");
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_prof_gal_x" + RvX(q));
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_prof_b_x" + RvX(q));
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_prof_m_x" + RvX(q));
            for (int q = 0; q < RvBins; q++) l.Add(pre + "_lens_dsigma_hmsun_pc2_x" + RvX(q));
        }
        // fonction de masse des amas (M200m, masse gravitationnelle effective et masse ordinaire), en h⁻¹ M☉ et (h/Mpc)³
        l.Add("cl_n_candidates"); l.Add("cl_n_halos"); l.Add("cl_log_m200_max_hmsun");
        l.Add("cl_ratio_eff_over_b_median_gt1e14"); l.Add("cl_r200_mpc_median_gt1e14");
        foreach (double t in ClThrEff) l.Add("cl_ngt_eff_logM" + ClLab(t) + "_hmpc3");
        foreach (double t in ClThrB) l.Add("cl_ngt_b_logM" + ClLab(t) + "_hmpc3");
        for (int k = 0; k < ClBins; k++) l.Add("cl_dndlog10M_eff_logM" + ClLab(ClDexMin + k * ClDex) + "_" + ClLab(ClDexMin + (k + 1) * ClDex));
        // fraction de baryons de l'excès de masse δM_ord(<r) / δM_eff(<r) des halos ≥ 10¹³ h⁻¹ M☉ (médiane) : à R200m et à rayon fixe
        l.Add("cl_fb_n_halos_gt1e13"); l.Add("cl_fb_r200_median_gt1e13");
        foreach (double rf in ClFbRadii) l.Add("cl_fb_r" + rf.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "mpc_median_gt1e13");
        // même fraction, mais en projection (cylindre de ±ClLosHalfMpc le long de la ligne de visée, comme une lentille)
        foreach (double rf in ClLensRadii) l.Add("cl_fbproj_r" + rf.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "mpc_median_gt1e13");
        // densité du milieu (masses négatives, ou matière noire des témoins) autour des halos ≥ 10¹² : ρ_m(coquille) / ρ̄_m
        l.Add("cl_negprof_n_halos_gt1e12");
        for (int q = 0; q + 1 < ClNegEdges.Length; q++)
            l.Add("cl_negprof_r" + ClNegEdges[q].ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "_" +
                  ClNegEdges[q + 1].ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "mpc_median_gt1e12");
        return l;
    }
    // Groupes friends-of-friends des particules ordinaires ; renvoie les centres de masse des plus massifs,
    // en nombre GalDensity × volume (en (Mpc/h)³), ou tous les groupes s'il y en a moins.
    System.Collections.Generic.List<Vector3> FindGalaxyTracers(NativeArray<Body> data, out int nGroups, out int minMembers,
                                                               out double target, out double particleMass,
                                                               out System.Collections.Generic.List<Vector3> clCenters)
    {
        var res = new System.Collections.Generic.List<Vector3>();
        clCenters = new System.Collections.Generic.List<Vector3>();
        int nbar = 0;
        for (int i = 0; i < data.Length; i++) { int sg = data[i].sign; if (!(sg < 0 || sg == 2)) nbar++; }
        double volMpc = (double)lssBoxMpc * lssBoxMpc * lssBoxMpc;
        particleMass = nbar > 0 ? LcdmOb * 2.775e11 * LcdmH * LcdmH * volMpc / nbar : double.NaN;
        target = GalDensity * System.Math.Pow(lssBoxMpc * LcdmH, 3);
        nGroups = 0; minMembers = 0;
        if (nbar < 2) return res;
        var px = new float[nbar]; var py = new float[nbar]; var pz = new float[nbar];
        int c0 = 0;
        for (int i = 0; i < data.Length; i++)
        {
            int sg = data[i].sign; if (sg < 0 || sg == 2) continue;
            px[c0] = data[i].pos.x; py[c0] = data[i].pos.y; pz[c0] = data[i].pos.z; c0++;
        }
        float Lb = boxSize;
        double link = GalLinkB * Lb / System.Math.Pow(nbar, 1.0 / 3.0);
        int G = System.Math.Max(3, (int)System.Math.Floor(Lb / link));
        float cellS = Lb / G;
        var keys = new long[nbar]; var idx = new int[nbar];
        for (int i = 0; i < nbar; i++)
        {
            int ix = ((int)System.Math.Floor((px[i] + 0.5f * Lb) / cellS) % G + G) % G;
            int iy = ((int)System.Math.Floor((py[i] + 0.5f * Lb) / cellS) % G + G) % G;
            int iz = ((int)System.Math.Floor((pz[i] + 0.5f * Lb) / cellS) % G + G) % G;
            keys[i] = ((long)iz * G + iy) * G + ix; idx[i] = i;
        }
        System.Array.Sort(keys, idx);
        var startOf = new System.Collections.Generic.Dictionary<long, int>();
        for (int s0 = 0; s0 < nbar; s0++) if (s0 == 0 || keys[s0] != keys[s0 - 1]) startOf[keys[s0]] = s0;
        var parent = new int[nbar];
        for (int i = 0; i < nbar; i++) parent[i] = i;
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        double link2 = link * link, halfL = 0.5 * Lb;
        for (int s0 = 0; s0 < nbar; s0++)
        {
            long k0 = keys[s0];
            int i = idx[s0];
            int cx = (int)(k0 % G), cy = (int)((k0 / G) % G), cz = (int)(k0 / ((long)G * G));
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        long kn = ((long)((cz + dz + G) % G) * G + (cy + dy + G) % G) * G + (cx + dx + G) % G;
                        if (kn < k0) continue;                                   // chaque paire de cellules une seule fois
                        if (!startOf.TryGetValue(kn, out int st)) continue;
                        for (int s1 = (kn == k0 ? s0 + 1 : st); s1 < nbar && keys[s1] == kn; s1++)
                        {
                            int j = idx[s1];
                            double ddx = px[j] - px[i]; if (ddx > halfL) ddx -= Lb; else if (ddx < -halfL) ddx += Lb;
                            double ddy = py[j] - py[i]; if (ddy > halfL) ddy -= Lb; else if (ddy < -halfL) ddy += Lb;
                            double ddz = pz[j] - pz[i]; if (ddz > halfL) ddz -= Lb; else if (ddz < -halfL) ddz += Lb;
                            if (ddx * ddx + ddy * ddy + ddz * ddz > link2) continue;
                            int ra = Find(i), rb = Find(j);
                            if (ra != rb) parent[ra] = rb;
                        }
                    }
        }
        // taille des groupes, centre de masse (déplié autour du premier membre)
        var size = new int[nbar]; var refI = new int[nbar];
        var sx = new double[nbar]; var sy = new double[nbar]; var sz = new double[nbar];
        for (int i = 0; i < nbar; i++) refI[i] = -1;
        for (int i = 0; i < nbar; i++)
        {
            int r0 = Find(i);
            if (refI[r0] < 0) refI[r0] = i;
            int f0 = refI[r0];
            double ddx = px[i] - px[f0]; if (ddx > halfL) ddx -= Lb; else if (ddx < -halfL) ddx += Lb;
            double ddy = py[i] - py[f0]; if (ddy > halfL) ddy -= Lb; else if (ddy < -halfL) ddy += Lb;
            double ddz = pz[i] - pz[f0]; if (ddz > halfL) ddz -= Lb; else if (ddz < -halfL) ddz += Lb;
            size[r0]++; sx[r0] += ddx; sy[r0] += ddy; sz[r0] += ddz;
        }
        var groups = new System.Collections.Generic.List<int>();
        for (int i = 0; i < nbar; i++) if (parent[i] == i && size[i] >= System.Math.Min(GalMinMembers, ClMinMembers)) groups.Add(i);
        groups.Sort((a, b) => size[b].CompareTo(size[a]));
        Vector3 CenterOf(int r0)
        {
            int f0 = refI[r0];
            double cxg = px[f0] + sx[r0] / size[r0], cyg = py[f0] + sy[r0] / size[r0], czg = pz[f0] + sz[r0] / size[r0];
            cxg -= Lb * System.Math.Floor((cxg + halfL) / Lb); cyg -= Lb * System.Math.Floor((cyg + halfL) / Lb); czg -= Lb * System.Math.Floor((czg + halfL) / Lb);
            return new Vector3((float)cxg, (float)cyg, (float)czg);
        }
        nGroups = 0;
        foreach (int r0 in groups) if (size[r0] >= GalMinMembers) nGroups++;
        int take = System.Math.Min(nGroups, (int)System.Math.Round(target));
        for (int g = 0; g < take; g++)
        {
            res.Add(CenterOf(groups[g]));
            minMembers = size[groups[g]];
        }
        // candidats amas (groupes d'au moins ClMinMembers particules), du plus gros au plus petit
        for (int g = 0; g < groups.Count && clCenters.Count < ClMaxCandidates; g++)
            if (size[groups[g]] >= ClMinMembers) clCenters.Add(CenterOf(groups[g]));
        return res;
    }

    // ---- fonction de masse des amas
    //   Centres = groupes friends-of-friends d'au moins ClMinMembers particules ordinaires. Autour de chacun, masse
    //   cumulée dans des sphères (particules, 60 coquilles jusqu'à 5 Mpc) ; R200m = plus grand rayon où la densité
    //   moyenne effective atteint 200 ρ̄_réf, ρ̄_réf = (Ω_m/Ω_b) ρ̄_ord (densité moyenne de matière de ΛCDM, mêmes baryons).
    //   Densité effective = celle qui attire la matière ordinaire (et dévie la lumière) :
    //     ρ_eff = ρ̄_réf + (ρ_ord − ρ̄_ord) + w (ρ_mil − ρ̄_mil),  w = −κ²|M−|/M+ (Janus), 5,33 (ΛCDM), 0 (positives seules)
    //   Pour ΛCDM, ρ_eff = ρ_ord + ρ_MN exactement. Pour Janus, un déficit de masse négative autour d'un amas compte comme
    //   une masse positive en plus : c'est la masse que mesureraient la lentille faible ou l'équilibre du gaz.
    //   Doublons retirés (un centre dans le R200m d'un amas plus massif déjà retenu).
    const int ClMinMembers = 10;
    const int ClMaxCandidates = 30000;
    const double ClRmaxMpc = 5.0;
    static readonly double[] ClFbRadii = { 1, 2, 3, 5 };     // Mpc : rayons fixes de la fraction de baryons
    static readonly double[] ClLensRadii = { 1, 2 };         // Mpc : rayons des cylindres de lentille projetée
    static readonly double[] ClNegEdges = { 0, 1, 2, 3, 5 };  // Mpc : coquilles du profil de densité du milieu
    const double ClLosHalfMpc = 50.0;                         // Mpc : demi-profondeur de la ligne de visée
    const int ClShells = 60;
    static readonly double[] ClThrEff = { 13.0, 13.5, 14.0, 14.5, 15.0 };   // log10 M200m,eff [h⁻¹ M☉]
    static readonly double[] ClThrB = { 12.0, 12.5, 13.0, 13.5, 14.0 };     // log10 M_ord(< R200m) [h⁻¹ M☉]
    const double ClDexMin = 13.0, ClDex = 0.25; const int ClBins = 10;
    static string ClLab(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    System.Collections.Generic.List<double> ClusterMassFunction(NativeArray<Body> data, System.Collections.Generic.List<Vector3> centers,
                                                                double pMass, out string line)
    {
        var res = new System.Collections.Generic.List<double>();
        int nb = 0, nm = 0;
        for (int i = 0; i < data.Length; i++) { int sg = data[i].sign; if (sg < 0 || sg == 2) nm++; else nb++; }
        var bpos = new Vector3[nb]; var mpos = new Vector3[nm];
        { int ib = 0, im = 0; for (int i = 0; i < data.Length; i++) { int sg = data[i].sign; if (sg < 0 || sg == 2) mpos[im++] = data[i].pos; else bpos[ib++] = data[i].pos; } }
        double Lb = boxSize, halfL = 0.5 * Lb;
        double mpcU = lssBoxMpc > 0f ? lssBoxMpc / (double)boxSize : 1.0;
        double rMax = ClRmaxMpc / mpcU, dr = rMax / ClShells;
        int G = System.Math.Max(3, (int)System.Math.Floor(Lb / rMax));
        double cs = Lb / G;
        int CellOf(Vector3 p)
        {
            int ix = ((int)System.Math.Floor((p.x + halfL) / cs) % G + G) % G;
            int iy = ((int)System.Math.Floor((p.y + halfL) / cs) % G + G) % G;
            int iz = ((int)System.Math.Floor((p.z + halfL) / cs) % G + G) % G;
            return ix + G * (iy + G * iz);
        }
        void BuildCells(Vector3[] pts, out int[] start, out int[] order)
        {
            int nc = G * G * G;
            start = new int[nc + 1]; order = new int[pts.Length];
            var cell = new int[pts.Length];
            for (int i = 0; i < pts.Length; i++) { cell[i] = CellOf(pts[i]); start[cell[i] + 1]++; }
            for (int c = 0; c < nc; c++) start[c + 1] += start[c];
            var fill = new int[nc];
            for (int i = 0; i < pts.Length; i++) { int c = cell[i]; order[start[c] + fill[c]++] = i; }
        }
        BuildCells(bpos, out int[] bStart, out int[] bOrder);
        BuildCells(mpos, out int[] mStart, out int[] mOrder);
        double V = Lb * Lb * Lb;
        double nbBar = nb / V, nmBar = nm / V;
        double wc = nm > 0 ? LensMediumWeight() * nb / (double)nm : 0.0;    // poids d'une particule du milieu, en particules ordinaires
        double refBar = nbBar * LcdmOm / LcdmOb;
        void Shells(Vector3 c, Vector3[] pts, int[] start, int[] order, double[] h)
        {
            int cx0 = ((int)System.Math.Floor((c.x + halfL) / cs) % G + G) % G;
            int cy0 = ((int)System.Math.Floor((c.y + halfL) / cs) % G + G) % G;
            int cz0 = ((int)System.Math.Floor((c.z + halfL) / cs) % G + G) % G;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int cc = (cx0 + dx + G) % G + G * ((cy0 + dy + G) % G + G * ((cz0 + dz + G) % G));
                        for (int o = start[cc]; o < start[cc + 1]; o++)
                        {
                            Vector3 p = pts[order[o]];
                            double ddx = p.x - c.x; if (ddx > halfL) ddx -= Lb; else if (ddx < -halfL) ddx += Lb;
                            double ddy = p.y - c.y; if (ddy > halfL) ddy -= Lb; else if (ddy < -halfL) ddy += Lb;
                            double ddz = p.z - c.z; if (ddz > halfL) ddz -= Lb; else if (ddz < -halfL) ddz += Lb;
                            double d = System.Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
                            if (d >= rMax) continue;
                            h[(int)(d / dr)]++;
                        }
                    }
        }
        var hMass = new System.Collections.Generic.List<double>();   // M200m,eff [particules ordinaires]
        var hB = new System.Collections.Generic.List<double>();      // M_ord(< R200m)
        var hR = new System.Collections.Generic.List<double>();      // R200m (unités du code)
        var hC = new System.Collections.Generic.List<Vector3>();
        var hF = new System.Collections.Generic.List<double[]>();   // fraction de baryons : [R200m, ClFbRadii…]
        var hN = new System.Collections.Generic.List<double[]>();   // ρ_milieu / moyenne dans les coquilles ClNegEdges
        var hb = new double[ClShells]; var hm = new double[ClShells];
        var fbK = new int[ClFbRadii.Length];
        for (int q = 0; q < ClFbRadii.Length; q++)
            fbK[q] = System.Math.Max(0, System.Math.Min(ClShells - 1, (int)System.Math.Round(ClFbRadii[q] / mpcU / dr) - 1));
        foreach (Vector3 c in centers)
        {
            System.Array.Clear(hb, 0, ClShells); System.Array.Clear(hm, 0, ClShells);
            Shells(c, bpos, bStart, bOrder, hb);
            Shells(c, mpos, mStart, mOrder, hm);
            double cb = 0, cm = 0, mE = double.NaN, mBk = double.NaN, rK = double.NaN;
            bool found = false;
            var fb = new double[1 + ClFbRadii.Length];
            for (int k = 0; k < ClShells; k++)
            {
                cb += hb[k]; cm += hm[k];
                double r = (k + 1) * dr, vr = 4.0 / 3.0 * System.Math.PI * r * r * r;
                double meff = refBar * vr + (cb - nbBar * vr) + wc * (cm - nmBar * vr);
                if (!found)
                {
                    if (meff >= 200.0 * refBar * vr) { mE = meff; mBk = cb; rK = r; }
                    else if (!double.IsNaN(mE)) found = true;             // première descente sous 200 ρ̄ : R200m trouvé
                }
                // fraction de baryons de l'excès de masse (fond retiré, comme en lentille) : δM_ord / δM_eff
                double exB = cb - nbBar * vr, exE = meff - refBar * vr;
                for (int q = 0; q < ClFbRadii.Length; q++)
                    if (k == fbK[q]) fb[1 + q] = exE > 0 ? exB / exE : double.NaN;
                if (!found && rK == r) fb[0] = exE > 0 ? exB / exE : double.NaN;
            }
            if (double.IsNaN(mE)) continue;
            var np = new double[ClNegEdges.Length - 1];
            for (int q = 0; q + 1 < ClNegEdges.Length; q++)
            {
                double r0 = ClNegEdges[q] / mpcU, r1 = ClNegEdges[q + 1] / mpcU, cnt = 0;
                for (int k = 0; k < ClShells; k++) { double rc = (k + 0.5) * dr; if (rc >= r0 && rc < r1) cnt += hm[k]; }
                double vol = 4.0 / 3.0 * System.Math.PI * (r1 * r1 * r1 - r0 * r0 * r0);
                np[q] = nmBar > 0 ? cnt / (nmBar * vol) : double.NaN;
            }
            hMass.Add(mE); hB.Add(mBk); hR.Add(rK); hC.Add(c); hF.Add(fb); hN.Add(np);
        }
        // doublons : du plus massif au moins massif, rejet d'un centre dans le R200m d'un amas retenu
        var ord = new System.Collections.Generic.List<int>();
        for (int i = 0; i < hMass.Count; i++) ord.Add(i);
        ord.Sort((p, q) => hMass[q].CompareTo(hMass[p]));
        var keepM = new System.Collections.Generic.List<double>(); var keepB = new System.Collections.Generic.List<double>();
        var keepR = new System.Collections.Generic.List<double>(); var keepC = new System.Collections.Generic.List<Vector3>();
        var keepF = new System.Collections.Generic.List<double[]>();
        var keepN = new System.Collections.Generic.List<double[]>();
        foreach (int i in ord)
        {
            bool dup = false;
            for (int u = 0; u < keepC.Count && !dup; u++)
            {
                double ddx = System.Math.Abs(hC[i].x - keepC[u].x); ddx = System.Math.Min(ddx, Lb - ddx);
                double ddy = System.Math.Abs(hC[i].y - keepC[u].y); ddy = System.Math.Min(ddy, Lb - ddy);
                double ddz = System.Math.Abs(hC[i].z - keepC[u].z); ddz = System.Math.Min(ddz, Lb - ddz);
                if (ddx * ddx + ddy * ddy + ddz * ddz < keepR[u] * keepR[u]) dup = true;
            }
            if (!dup) { keepM.Add(hMass[i]); keepB.Add(hB[i]); keepR.Add(hR[i]); keepC.Add(hC[i]); keepF.Add(hF[i]); keepN.Add(hN[i]); }
        }
        double toH = pMass * LcdmH;                                         // particules ordinaires → h⁻¹ M☉
        double volH = System.Math.Pow(lssBoxMpc * LcdmH, 3);                 // (Mpc/h)³
        var ratios = new System.Collections.Generic.List<double>(); var r14 = new System.Collections.Generic.List<double>();
        double mMax = double.NaN;
        for (int i = 0; i < keepM.Count; i++)
        {
            double m = keepM[i] * toH;
            if (double.IsNaN(mMax) || m > mMax) mMax = m;
            if (m >= 1e14) { if (keepB[i] > 0) ratios.Add(keepM[i] / keepB[i]); r14.Add(keepR[i] * mpcU); }
        }
        ratios.Sort(); r14.Sort();
        res.Add(centers.Count); res.Add(keepM.Count);
        res.Add(double.IsNaN(mMax) ? double.NaN : System.Math.Log10(mMax));
        res.Add(ratios.Count > 0 ? ratios[ratios.Count / 2] : double.NaN);
        res.Add(r14.Count > 0 ? r14[r14.Count / 2] : double.NaN);
        foreach (double t in ClThrEff)
        {
            int n = 0; double thr = System.Math.Pow(10, t);
            foreach (double m in keepM) if (m * toH >= thr) n++;
            res.Add(volH > 0 ? n / volH : double.NaN);
        }
        foreach (double t in ClThrB)
        {
            int n = 0; double thr = System.Math.Pow(10, t);
            foreach (double m in keepB) if (m * toH >= thr) n++;
            res.Add(volH > 0 ? n / volH : double.NaN);
        }
        var bins = new double[ClBins];
        foreach (double m in keepM)
        {
            double lm = System.Math.Log10(System.Math.Max(1e-30, m * toH));
            int k = (int)System.Math.Floor((lm - ClDexMin) / ClDex);
            if (k >= 0 && k < ClBins) bins[k]++;
        }
        for (int k = 0; k < ClBins; k++) res.Add(volH > 0 ? bins[k] / (volH * ClDex) : double.NaN);
        // fraction de baryons des halos ≥ 10¹³ h⁻¹ M☉ (médianes)
        int nFb = 0;
        var fbLists = new System.Collections.Generic.List<double>[1 + ClFbRadii.Length];
        for (int q = 0; q < fbLists.Length; q++) fbLists[q] = new System.Collections.Generic.List<double>();
        for (int i = 0; i < keepM.Count; i++)
        {
            if (keepM[i] * toH < 1e13) continue;
            nFb++;
            for (int q = 0; q < fbLists.Length; q++) if (!double.IsNaN(keepF[i][q])) fbLists[q].Add(keepF[i][q]);
        }
        res.Add(nFb);
        var fbMed = new double[fbLists.Length];
        for (int q = 0; q < fbLists.Length; q++)
        {
            fbLists[q].Sort();
            fbMed[q] = fbLists[q].Count > 0 ? fbLists[q][fbLists[q].Count / 2] : double.NaN;
            res.Add(fbMed[q]);
        }
        // lentille projetée : part des baryons dans l'excès de masse effective d'un cylindre (fond retiré),
        // ligne de visée selon z sur ±ClLosHalfMpc : inclut ce qui est devant et derrière l'amas (vide de masse négative…)
        double losH = System.Math.Min(ClLosHalfMpc / mpcU, 0.5 * Lb);
        int nzc = (int)System.Math.Ceiling(losH / cs);
        bool allZ = 2 * nzc + 1 >= G;
        var projLists = new System.Collections.Generic.List<double>[ClLensRadii.Length];
        for (int q = 0; q < projLists.Length; q++) projLists[q] = new System.Collections.Generic.List<double>();
        var pcb = new double[ClLensRadii.Length]; var pcm = new double[ClLensRadii.Length];
        void Cyl(Vector3 c, Vector3[] pts, int[] start, int[] order, double[] acc)
        {
            int cx0 = ((int)System.Math.Floor((c.x + halfL) / cs) % G + G) % G;
            int cy0 = ((int)System.Math.Floor((c.y + halfL) / cs) % G + G) % G;
            int cz0 = ((int)System.Math.Floor((c.z + halfL) / cs) % G + G) % G;
            int z0 = allZ ? 0 : -nzc, z1 = allZ ? G - 1 : nzc;
            for (int dz = z0; dz <= z1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int iz = allZ ? dz : (cz0 + dz + G) % G;
                        int cc = (cx0 + dx + G) % G + G * ((cy0 + dy + G) % G + G * iz);
                        for (int o = start[cc]; o < start[cc + 1]; o++)
                        {
                            Vector3 p = pts[order[o]];
                            double ddz = p.z - c.z; if (ddz > halfL) ddz -= Lb; else if (ddz < -halfL) ddz += Lb;
                            if (System.Math.Abs(ddz) >= losH) continue;
                            double ddx = p.x - c.x; if (ddx > halfL) ddx -= Lb; else if (ddx < -halfL) ddx += Lb;
                            double ddy = p.y - c.y; if (ddy > halfL) ddy -= Lb; else if (ddy < -halfL) ddy += Lb;
                            double r2 = ddx * ddx + ddy * ddy;
                            for (int q = 0; q < ClLensRadii.Length; q++)
                            {
                                double rq = ClLensRadii[q] / mpcU;
                                if (r2 < rq * rq) acc[q]++;
                            }
                        }
                    }
        }
        for (int i = 0; i < keepM.Count; i++)
        {
            if (keepM[i] * toH < 1e13) continue;
            System.Array.Clear(pcb, 0, pcb.Length); System.Array.Clear(pcm, 0, pcm.Length);
            Cyl(keepC[i], bpos, bStart, bOrder, pcb);
            Cyl(keepC[i], mpos, mStart, mOrder, pcm);
            for (int q = 0; q < ClLensRadii.Length; q++)
            {
                double rq = ClLensRadii[q] / mpcU, vol = System.Math.PI * rq * rq * 2 * losH;
                double exB = pcb[q] - nbBar * vol, exE = exB + wc * (pcm[q] - nmBar * vol);
                if (exE > 0) projLists[q].Add(exB / exE);
            }
        }
        var projMed = new double[ClLensRadii.Length];
        for (int q = 0; q < ClLensRadii.Length; q++)
        {
            projLists[q].Sort();
            projMed[q] = projLists[q].Count > 0 ? projLists[q][projLists[q].Count / 2] : double.NaN;
            res.Add(projMed[q]);
        }
        // profil du milieu autour des halos ≥ 10¹² h⁻¹ M☉ (médianes) : le milieu est-il plus dense ou moins dense que la moyenne ?
        int nNeg = 0;
        var negLists = new System.Collections.Generic.List<double>[ClNegEdges.Length - 1];
        for (int q = 0; q < negLists.Length; q++) negLists[q] = new System.Collections.Generic.List<double>();
        for (int i = 0; i < keepM.Count; i++)
        {
            if (keepM[i] * toH < 1e12) continue;
            nNeg++;
            for (int q = 0; q < negLists.Length; q++) if (!double.IsNaN(keepN[i][q])) negLists[q].Add(keepN[i][q]);
        }
        res.Add(nNeg);
        var negMed = new double[negLists.Length];
        for (int q = 0; q < negLists.Length; q++)
        {
            negLists[q].Sort();
            negMed[q] = negLists[q].Count > 0 ? negLists[q][negLists[q].Count / 2] : double.NaN;
            res.Add(negMed[q]);
        }
        int n14 = 0; foreach (double m in keepM) if (m * toH >= 1e14) n14++;
        line = L("Amas : ", "Clusters: ") + $"{keepM.Count}" + L(" halos (", " halos (") + $"{centers.Count}" +
               L(" candidats), M200m ≥ 10¹⁴ h⁻¹ M☉ : ", " candidates), M200m ≥ 10¹⁴ h⁻¹ M☉: ") + $"{n14}" +
               L(", M_eff / M_ord médian ", ", median M_eff / M_ord ") + (ratios.Count > 0 ? $"{ratios[ratios.Count / 2]:F2}" : "—") +
               (nFb > 0 ? L(" ; fraction de baryons (≥ 10¹³) à R200m ", " ; baryon fraction (≥ 10¹³) at R200m ") + $"{fbMed[0]:F2}" +
                          L(", à 2 Mpc ", ", at 2 Mpc ") + $"{fbMed[2]:F2}" + L(", à 5 Mpc ", ", at 5 Mpc ") + $"{fbMed[4]:F2}" +
                          L(" ; en projection (lentille, ±50 Mpc) à 1 Mpc ", " ; projected (lensing, ±50 Mpc) at 1 Mpc ") + $"{projMed[0]:F2}" +
                          L(", à 2 Mpc ", ", at 2 Mpc ") + $"{projMed[1]:F2}" : "") +
               (nNeg > 0 ? L("\nMilieu autour des halos ≥ 10¹² (ρ / ρ̄) : 0–1 Mpc ", "\nMedium around halos ≥ 10¹² (ρ / ρ̄): 0–1 Mpc ") +
                           $"{negMed[0]:F2}, 1–2 : {negMed[1]:F2}, 2–3 : {negMed[2]:F2}, 3–5 : {negMed[3]:F2}" : "");
        return res;
    }

    // tranches fixes de R_v (Mpc/h) pour l'empilement à taille égale
    static readonly double[] RvbEdges = { 5, 10, 20, 40 };
    static string RvbName(int t) => "rvb" + RvbEdges[t].ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                                    "_" + RvbEdges[t + 1].ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    // fonction de taille des vides : 9 classes en ln R_v, de 4 à 90,5 Mpc/h (facteur √2)
    const int VsfBins = 9; const double VsfRmin = 4.0;
    static readonly double VsfDlnR = 0.5 * System.Math.Log(2.0);
    static string VsfLabel(int k)
    {
        double lo = VsfRmin * System.Math.Exp(k * VsfDlnR), hi = lo * System.Math.Exp(VsfDlnR);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return lo.ToString("0.0", ci) + "_" + hi.ToString("0.0", ci);
    }
    // décalages des cellules fines groupés par coquille entière (distance au centre décalé de ½ cellule)
    int[] shellDx, shellDy, shellDz, shellStart; int shellNR = -1;
    void BuildShellOffsets(int nR)
    {
        if (shellNR == nR) return;
        var lists = new System.Collections.Generic.List<int>[nR];
        for (int r = 0; r < nR; r++) lists[r] = new System.Collections.Generic.List<int>();
        for (int dz = -nR; dz <= nR; dz++)
            for (int dy = -nR; dy <= nR; dy++)
                for (int dx = -nR; dx <= nR; dx++)
                {
                    double d = System.Math.Sqrt((dx + 0.5) * (dx + 0.5) + (dy + 0.5) * (dy + 0.5) + (dz + 0.5) * (dz + 0.5));
                    int r = (int)d;
                    if (r < nR) lists[r].Add(((dx + 512) << 20) | ((dy + 512) << 10) | (dz + 512));
                }
        int total = 0; foreach (var li in lists) total += li.Count;
        shellDx = new int[total]; shellDy = new int[total]; shellDz = new int[total]; shellStart = new int[nR + 1];
        int o = 0;
        for (int r = 0; r < nR; r++)
        {
            shellStart[r] = o;
            foreach (int c in lists[r])
            {
                shellDx[o] = ((c >> 20) & 1023) - 512; shellDy[o] = ((c >> 10) & 1023) - 512; shellDz[o] = (c & 1023) - 512;
                o++;
            }
        }
        shellStart[nR] = o;
        shellNR = nR;
    }
    const double LcdmH = 0.677;                       // h (Planck) pour les conversions en Mpc/h
    const double LensPhysFactor = 0.31 * 2.775e11 / 1e12;   // Ω_m ρ_crit en h M☉/pc² par (Mpc/h) ≈ 0,086
    // Poids du milieu dans la source de lentille, relatif à la masse ordinaire : ρ_eff = ρ_ord (δ_ord + w δ_mil) / …
    double LensMediumWeight()
    {
        double mp = System.Math.Max(1e-30, (double)positiveTotalMass);
        switch (lssModel)
        {
            case LssModel.Janus:
                return -KappaSq * negativeTotalMass / mp     // masses négatives : signe opposé
                                                       * (JanusPhenoOn ? PhenoS(ScaleFactor(simTime)) : 1.0);
            case LssModel.MatiereNoire: return negativeTotalMass / mp;
            case LssModel.MatiereNoireRealiste: return lssRealisticRatio;
            case LssModel.LambdaCDM: return LcdmDmRatio;
            default: return 0.0;                                 // positives seules : traceurs sans masse
        }
    }
    const int RvGrid = 128;            // grille fine des profils de vides
    const double RvMaxMpc = 80;        // rayon maximal exploré autour de chaque vide (Mpc)
    const int RvMaxVoids = 1024;       // nombre maximal de vides empilés (catalogue sans recouvrement)
    const double RvDetectDelta = -0.5; // seuil de détection (contraste lissé sur 3³ cellules 64³)
    static string RvX(int q) => ((q + 1) * RvDx).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    double lcdmAmp1 = double.NaN;      // A pour σ₈ = 1, n_s = 0,965 (spectre de référence, mis en cache)

    // Rayon où le profil des baryons franchit δ = −0,5 en remontant depuis le centre du vide
    static double VoidRadius(double[] pB)
    {
        const double thr = -0.5;
        int n = pB.Length;
        if (n == 0 || double.IsNaN(pB[0]) || pB[0] >= thr) return double.NaN;
        for (int r = 1; r < n; r++)
        {
            if (double.IsNaN(pB[r])) return double.NaN;
            if (pB[r] >= thr)
            {
                double r0 = 0.5 * (LssProfEdges[r] + LssProfEdges[r + 1]);
                double rPrev = 0.5 * (LssProfEdges[r - 1] + LssProfEdges[r]);
                double f = (thr - pB[r - 1]) / (pB[r] - pB[r - 1]);
                return rPrev + f * (r0 - rPrev);
            }
        }
        return double.NaN;
    }

    void WriteLssRow(double aNow, double hNow, System.Collections.Generic.List<double> vals)
    {
        double zNow = RedshiftAt(aNow);
        double tGyr = readbackTime * TimeUnitGyr();
        try
        {
            if (csv == null)
            {
                string folder = batchMode && campaignFolder != null ? campaignFolder
                    : string.IsNullOrEmpty(csvFolder)
                        ? JanusWeb.DefaultFolder("JanusRuns")
                        : csvFolder;
                System.IO.Directory.CreateDirectory(folder);
                runId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_LSS_" + lssModel + "_" + EffectiveLaw +
                        (SelfConsistent ? "_autocoherent" : "_impose") + "_seed" + seed;
                csvPath = System.IO.Path.Combine(folder, "janus_" + runId + ".csv");
                csv = new System.IO.StreamWriter(csvPath, false, new System.Text.UTF8Encoding(true)); JanusWeb.Register(csvPath);
                var cols = new System.Collections.Generic.List<string>
                {
                    "run_id", "lss_model", "expansion_law", "expansion_self_consistent", "expansion_factor_end",
                    "expansion_duration", "expansion_end_time", "start_redshift", "time_unit_gyr", "seed",
                    "box_size", "grid_size", "smoothing_length", "positive_count", "negative_count",
                    "positive_total_mass", "negative_total_mass", "G", "dt", "positive_sigma", "lss_medium_sigma",
                    "light_speed_ratio", "neg_own_scale_factor", "neg_scale_ratio_init", "neg_expansion_rate",
                    "t", "step", "a", "H", "z", "t_gyr", "H_kms_Mpc", "a_neg", "q"
                };
                cols.AddRange(LssMetricNames());
                csv.WriteLine(string.Join(Sep.ToString(), cols));
                Debug.Log("Janus PM : export CSV vers " + csvPath);
            }
            var f = new System.Collections.Generic.List<string>
            {
                runId, lssModel.ToString(), EffectiveLaw.ToString(), SelfConsistent ? "1" : "0", Num(expansionFactorEnd),
                Num(expansionDuration), Num(ExpansionOn ? ExpansionEndTime() : double.NaN), Num(lssStartRedshift),
                Num(TimeUnitGyr()),
                seed.ToString(), Num(boxSize), gridSize.ToString(), Num(smoothingLength),
                positiveCount.ToString(), negativeCount.ToString(), Num(positiveTotalMass), Num(negativeTotalMass),
                Num(G), Num(dt), Num(positiveSigma), Num(lssMediumSigma),
                Num(LightSpeedRatio), NegOwnScale ? "1" : "0", Num(negativeScaleRatio), Num(negativeExpansionRate),
                Num(readbackTime), readbackStep.ToString(), Num(aNow), Num(hNow), Num(zNow), Num(tGyr), Num(HubbleKms(hNow)),
                Num(readbackANeg), Num(Decel(readbackTime))
            };
            foreach (double v in vals) f.Add(Num(v));
            csv.WriteLine(string.Join(Sep.ToString(), f));
            csv.Flush();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Janus PM : écriture CSV impossible (" + e.Message + "). Export désactivé.");
            exportCsv = false;
        }
    }

    // Noms et valeurs des mesures de synthèse selon le mode
    string[] ActiveStatNames()
    {
        if (!IsLss) return StatNames;
        var m = LssMetricNames();
        var l = new System.Collections.Generic.List<string>();
        foreach (var s in m) l.Add(s + "_avg");
        foreach (var s in m) l.Add(s + "_final");
        return l.ToArray();
    }

    double[] ActiveStatValues(RunStats s)
    {
        if (!IsLss) return StatValues(s);
        int nv = LssMetricNames().Length;
        var v = new double[2 * nv];
        for (int q = 0; q < nv; q++)
        {
            v[q] = s.lssSum != null && s.lssCnt[q] > 0 ? s.lssSum[q] / s.lssCnt[q] : double.NaN;
            v[nv + q] = s.lssFin != null && q < s.lssFin.Length ? s.lssFin[q] : double.NaN;
        }
        return v;
    }

    // ------------------------------------------------------------ export CSV

    char Sep { get { return csvFrenchExcel ? ';' : ','; } }

    string Num(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return "";
        string s = x.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
        return csvFrenchExcel ? s.Replace('.', ',') : s;
    }

    void OpenCsv(float rMax, int bins)
    {
        string folder = batchMode && campaignFolder != null ? campaignFolder
            : string.IsNullOrEmpty(csvFolder)
                ? JanusWeb.DefaultFolder("JanusRuns")
                : csvFolder;
        System.IO.Directory.CreateDirectory(folder);

        runId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + mode +
                (darkMatterHalo ? "_matiereNoire" + dmProfile : "_janus") +
                (freezeNegatives ? "_fige" : "_libre") + "_seed" + seed;
        csvPath = System.IO.Path.Combine(folder, "janus_" + runId + ".csv");
        csv = new System.IO.StreamWriter(csvPath, false, new System.Text.UTF8Encoding(true)); JanusWeb.Register(csvPath);

        var cols = new System.Collections.Generic.List<string>
        {
            // paramètres
            "run_id", "mode", "freeze_negatives", "dark_matter", "dm_profile", "dm_halo_radius",
            "dm_scale_radius", "dm_truncation_radius", "halo_mass", "seed", "box_size", "grid_size", "smoothing_length",
            "positive_count", "negative_count", "positive_total_mass", "negative_total_mass",
            "G", "dt", "disk_radius", "disk_thickness_init", "disk_velocity_dispersion",
            "cavity_radius", "negative_sigma", "positive_sigma", "light_speed_ratio",
            // mesures
            "t", "step", "overlap", "center_x", "center_y", "center_z", "drift_speed",
            "confined_pct", "thickness_rms", "sigma_z",
            "A2_in", "A3_in", "A4_in", "A2_out", "A3_out", "A4_out",
            "medium_mass_r_half_disk", "medium_mass_r_disk",
            "gas_fraction_init", "gas_frac", "young_frac", "A2_out_gas", "A3_out_gas"
        };
        for (int k = 0; k < bins; k++)
            cols.Add("v_r" + Mathf.RoundToInt(rMax * (k + 0.5f) / bins));

        csv.WriteLine(string.Join(Sep.ToString(), cols));
        csv.Flush();
        Debug.Log("Janus PM : export CSV vers " + csvPath);
    }

    void WriteCsvRow(double overlap, Vector3 center, double drift, double confinedPct,
                     double thick, double sigz, double[,] amp, double[] vCurve, float rMax,
                     double medium25, double medium50,
                     double gasNow, double youngNow, double A2g, double A3g)
    {
        try
        {
            if (csv == null) OpenCsv(rMax, vCurve.Length);

            var f = new System.Collections.Generic.List<string>
            {
                runId, mode.ToString(), freezeNegatives ? "1" : "0",
                darkMatterHalo ? "1" : "0", darkMatterHalo ? dmProfile.ToString() : "",
                darkMatterHalo && dmProfile == HaloProfile.UniformSphere ? Num(dmHaloRadius) : "",
                darkMatterHalo && dmProfile != HaloProfile.UniformSphere ? Num(EffectiveScaleRadius()) : "",
                darkMatterHalo && dmProfile != HaloProfile.UniformSphere ? Num(dmTruncationRadius) : "",
                Num(haloMass),
                seed.ToString(),
                Num(boxSize), gridSize.ToString(), Num(smoothingLength),
                positiveCount.ToString(), negativeCount.ToString(),
                Num(positiveTotalMass), Num(negativeTotalMass),
                Num(G), Num(dt), Num(diskRadius), Num(diskThickness), Num(diskVelocityDispersion),
                Num(cavityRadius), Num(negativeSigma), Num(positiveSigma), Num(LightSpeedRatio),

                Num(simTime), stepCount.ToString(), Num(overlap),
                Num(center.x), Num(center.y), Num(center.z), Num(drift),
                Num(confinedPct), Num(thick), Num(sigz),
                Num(amp[0, 2]), Num(amp[0, 3]), Num(amp[0, 4]),
                Num(amp[1, 2]), Num(amp[1, 3]), Num(amp[1, 4]),
                Num(medium25), Num(medium50),
                Num(gasFraction), Num(gasNow), Num(youngNow), Num(A2g), Num(A3g)
            };
            foreach (double v in vCurve) f.Add(Num(v));

            csv.WriteLine(string.Join(Sep.ToString(), f));
            csv.Flush();   // les données sont conservées même si Unity plante
        }
        catch (System.Exception e)
        {
            Debug.LogError("Janus PM : écriture CSV impossible (" + e.Message + "). Export désactivé.");
            exportCsv = false;
        }
    }

    // ------------------------------------------------------------ campagne : synthèse

    double[] StatValues(RunStats s)
    {
        double n = System.Math.Max(s.n, 1);
        return new double[]
        {
            s.n > 0 ? s.A2in / n : double.NaN, s.n > 0 ? s.A2out / n : double.NaN, s.n > 0 ? s.A3out / n : double.NaN,
            s.n > 0 ? s.thick / n : double.NaN, s.n > 0 ? s.sigz / n : double.NaN, s.n > 0 ? s.conf / n : double.NaN,
            s.n > 0 ? s.drift / n : double.NaN, s.n > 0 ? s.med25 / n : double.NaN, s.n > 0 ? s.med50 / n : double.NaN,
            s.finA2in, s.finA2out, s.finThick,
            s.nGasSamples > 0 ? s.A2gas / s.nGasSamples : double.NaN, s.finA2gas, s.finGas, s.finYoung
        };
    }

    string RunsPath => System.IO.Path.Combine(campaignFolder, "campagne_" + campaignId + "_runs.csv");
    string SynthPath => System.IO.Path.Combine(campaignFolder, "campagne_" + campaignId + "_synthese.csv");

    // Fin d'un run : ligne ajoutée immédiatement au fichier des runs (rien n'est perdu si Unity s'arrête)
    void EndRun()
    {
        if (csv != null) { csv.Flush(); csv.Close(); csv = null; JanusWeb.Sync(); }
        var sc = queue[runIndex];
        doneStats.Add(stats);
        try
        {
            bool newFile = !System.IO.File.Exists(RunsPath);
            JanusWeb.Register(RunsPath);
            using (var w = new System.IO.StreamWriter(RunsPath, true, new System.Text.UTF8Encoding(true)))
            {
                string S = Sep.ToString();
                if (newFile)
                {
                    var h = new System.Collections.Generic.List<string>
                    {
                        "campaign_id", "run", "model", "seed", "dark_matter", "dm_profile", "freeze", "dm_scale_radius",
                        "halo_mass", "grid_size", "smoothing_length", "gas_fraction", "positive_count", "negative_count",
                        "positive_total_mass", "negative_total_mass", "dt", "end_time", "summary_from", "n_samples",
                        "box_size", "cavity_radius", "negative_sigma", "disk_radius", "disk_thickness",
                        "disk_toomre_q", "disk_exponential_fix", "disk_velocity_dispersion",
                        "gas_thickness", "gas_velocity_dispersion", "gas_sound_speed", "gas_cooling_time",
                        "gas_grid_size", "star_formation", "sf_time", "sf_threshold", "young_lifetime",
                        "gas_recycle_fraction", "dm_truncation_radius",
                        "lss_model", "expansion_law", "expansion_factor_end", "expansion_duration", "lss_medium_sigma",
                        "expansion_self_consistent", "expansion_end_time", "final_a", "final_t_gyr", "start_redshift",
                        "janus_calibration", "final_H_kms_Mpc",
                        "light_speed_ratio", "neg_own_scale_factor", "neg_scale_ratio_init", "neg_expansion_rate", "box_size_mpc",
                        "initial_conditions", "sigma8", "spectral_index", "neg_initial_field",
                        "janus_ic", "janus_sigma8_today", "sigma8_start_pos", "lin_growth_pos", "lin_sigma8_final_pos",
                        "lin_mode_neg_over_pos", "lin_mode_rate", "janus_pheno", "pheno_switch_z", "pheno_sharpness", "pheno_s_early", "neg_clock_ratio"
                    };
                    foreach (var nm in ActiveStatNames()) h.Add(nm);
                    if (curveRadii != null) foreach (var r in curveRadii) h.Add("v_r" + Mathf.RoundToInt(r) + "_final");
                    w.WriteLine(string.Join(S, h));
                }
                var f = new System.Collections.Generic.List<string>
                {
                    campaignId, (runIndex + 1).ToString(), sc.name, sc.seed.ToString(),
                    sc.darkMatter ? "1" : "0", sc.darkMatter ? sc.profile.ToString() : "", sc.freeze ? "1" : "0",
                    sc.darkMatter && sc.profile != HaloProfile.UniformSphere ? Num(EffectiveScaleRadius()) : "",
                    Num(haloMass), gridSize.ToString(), Num(smoothingLength), Num(gasFraction),
                    positiveCount.ToString(), negativeCount.ToString(),
                    Num(positiveTotalMass), Num(negativeTotalMass), Num(dt), Num(stats.finT), Num(batchSummaryFrom),
                    stats.n.ToString(),
                    Num(boxSize), Num(cavityRadius), Num(negativeSigma), Num(diskRadius), Num(diskThickness),
                    Num(diskToomreQ), diskExponentialFix ? "1" : "0", Num(diskVelocityDispersion),
                    Num(gasThickness), Num(gasVelocityDispersion), Num(gasSoundSpeed), Num(gasCoolingTime),
                    gasGridSize.ToString(), starFormation ? "1" : "0", Num(starFormationTime), Num(starFormationThreshold),
                    Num(youngStarLifetime), Num(gasRecycleFraction),
                    sc.darkMatter && sc.profile != HaloProfile.UniformSphere ? Num(dmTruncationRadius) : "",
                    IsLss ? sc.lss.ToString() : "", IsLss ? EffectiveLaw.ToString() : "",
                    IsLss ? Num(expansionFactorEnd) : "", IsLss ? Num(expansionDuration) : "", IsLss ? Num(lssMediumSigma) : "",
                    IsLss ? (SelfConsistent ? "1" : "0") : "", IsLss && ExpansionOn ? Num(ExpansionEndTime()) : "",
                    IsLss ? Num(ScaleFactor(stats.finT)) : "", IsLss ? Num(stats.finT * TimeUnitGyr()) : "",
                    IsLss ? Num(lssStartRedshift) : "",
                    IsLss ? janusCalibration.ToString() : "",
                    IsLss && ExpansionOn ? Num(HubbleKms(Hubble(stats.finT))) : "",
                    Num(LightSpeedRatio), IsLss ? (NegOwnScale ? "1" : "0") : "", IsLss ? Num(negativeScaleRatio) : "",
                    IsLss ? Num(NegOwnScale ? NegLambda() : negativeExpansionRate) : "", IsLss ? Num(lssBoxMpc) : "",
                    IsLss ? lssInitialConditions.ToString() : "", IsLss && IsRealisticIC ? Num(EffSigma8) : "",
                    IsLss && IsRealisticIC ? Num(lssSpectralIndex) : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? lssNegField.ToString() : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? lssJanusIc.ToString() : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? (lssJanusSigma8Today ? "1" : "0") : "",
                    IsLss && IsRealisticIC ? Num(linS8Start) : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? Num(linGrowth) : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? Num(linS8Today) : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? Num(linRatio) : "",
                    IsLss && IsRealisticIC && sc.lss == LssModel.Janus ? Num(linGamma) : "",
                    IsLss && sc.lss == LssModel.Janus ? (JanusPhenoOn ? "1" : "0") : "",
                    IsLss && JanusPhenoOn ? Num(janusPhenoZ) : "", IsLss && JanusPhenoOn ? Num(janusPhenoSharpness) : "",
                    IsLss && JanusPhenoOn ? Num(PhenoSEarly) : "",
                    IsLss && sc.lss == LssModel.Janus ? Num(Tau) : ""
                };
                foreach (var v in ActiveStatValues(stats)) f.Add(Num(v));
                if (curveRadii != null)
                    for (int k = 0; k < curveRadii.Length; k++)
                        f.Add(stats.finCurve != null && k < stats.finCurve.Length ? Num(stats.finCurve[k]) : "");
                w.WriteLine(string.Join(S, f));
            }
        }
        catch (System.Exception e) { Debug.LogError("Janus PM : écriture du fichier des runs impossible : " + e.Message); }
        WriteSynthesis(false);
    }

    // Moyenne et écart-type entre seeds, pour chaque modèle (réécrit après chaque run)
    void WriteSynthesis(bool complete)
    {
        try
        {
            var order = new System.Collections.Generic.List<string>();
            var byModel = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<RunStats>>();
            for (int i = 0; i < doneStats.Count; i++)
            {
                if (doneStats[i] == null) continue;
                string m = queue[i].name;
                if (!byModel.ContainsKey(m)) { byModel[m] = new System.Collections.Generic.List<RunStats>(); order.Add(m); }
                byModel[m].Add(doneStats[i]);
            }
            string S = Sep.ToString();
            JanusWeb.Register(SynthPath);
            using (var w = new System.IO.StreamWriter(SynthPath, false, new System.Text.UTF8Encoding(true)))
            {
                var h = new System.Collections.Generic.List<string> { "model", "n_seeds", "campagne_complete" };
                string[] names = ActiveStatNames();
                foreach (var nm in names) { h.Add(nm + "_mean"); h.Add(nm + "_std"); }
                if (curveRadii != null) foreach (var r in curveRadii) h.Add("v_r" + Mathf.RoundToInt(r) + "_final_mean");
                w.WriteLine(string.Join(S, h));

                foreach (var m in order)
                {
                    var list = byModel[m];
                    var f = new System.Collections.Generic.List<string> { m, list.Count.ToString(), complete ? "1" : "0" };
                    for (int q = 0; q < names.Length; q++)
                    {
                        double sum = 0, sum2 = 0; int cnt = 0;
                        foreach (var st in list)
                        {
                            double v = ActiveStatValues(st)[q];
                            if (double.IsNaN(v)) continue;
                            sum += v; sum2 += v * v; cnt++;
                        }
                        double mean = cnt > 0 ? sum / cnt : double.NaN;
                        double sd = cnt > 1 ? System.Math.Sqrt(System.Math.Max((sum2 - cnt * mean * mean) / (cnt - 1), 0)) : double.NaN;
                        f.Add(Num(mean)); f.Add(Num(sd));
                    }
                    if (curveRadii != null)
                        for (int k = 0; k < curveRadii.Length; k++)
                        {
                            double sum = 0; int cnt = 0;
                            foreach (var st in list)
                                if (st.finCurve != null && k < st.finCurve.Length && !double.IsNaN(st.finCurve[k]))
                                { sum += st.finCurve[k]; cnt++; }
                            f.Add(cnt > 0 ? Num(sum / cnt) : "");
                        }
                    w.WriteLine(string.Join(S, f));
                }
            }
        }
        catch (System.Exception e) { Debug.LogError("Janus PM : écriture de la synthèse impossible : " + e.Message); }
    }

    void FinishCampaign()
    {
        WriteSynthesis(true);
        ReleaseBuffers();
        IsFinished = true;
        RestoreNegRate();
        diagText = L("CAMPAGNE TERMINÉE : ", "CAMPAIGN FINISHED: ") + $"{doneStats.Count} runs\n" +
                   L("Synthèse : ", "Summary: ") + SynthPath + "\n" + L("Détail par run : ", "Per-run details: ") + RunsPath;
        Debug.Log("Janus PM : campagne terminée. Synthèse : " + SynthPath);
    }

    void OnGUI()
    {
        if (!legacyOnGUI) return;
        GUI.Label(new Rect(10, 10, 560, 640), diagText);
    }

    void OnDestroy()
    {
        ready = false;
        if (csv != null) { csv.Flush(); csv.Close(); csv = null; JanusWeb.Sync(); }
        ReleaseBuffers();
    }
}