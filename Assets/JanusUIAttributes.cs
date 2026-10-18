// JanusUIAttributes.cs
// Attributs lus par JanusUI pour construire l'interface automatiquement.
//
// Ajouter un paramètre à l'interface = déclarer un champ public dans JanusPMSimulation
// (ou JanusVideoRecorder). Il apparaît tout seul :
//   - dans l'onglet du dernier [UIPanel] déclaré au-dessus de lui ;
//   - avec le libellé [UILabel] (sinon le nom du champ) ;
//   - avec l'aide [Tooltip] affichée au survol et à la sélection ;
//   - avec un curseur si [Range] est présent.
// Optionnel :
//   [ShowIf("condition")]  n'affiche le champ que si la condition est vraie ;
//   [Live]                 modifiable pendant la simulation (sinon : modifiable à l'arrêt seulement) ;
//   [UIHide]               jamais affiché ;
//   [UILabel("…", En = "…")], [UIPanel("…", En = "…")], [TooltipEn("…")], [EnName("…")] : textes anglais ;
//   [FolderPath]           champ texte de dossier, avec un explorateur de dossiers.
//
// Syntaxe des conditions : noms de membres booléens (champ ou propriété, publics ou non) de l'objet,
// combinés avec ! (non), & (et), | (ou). Exemples : "IsGalaxy", "IsLss & !expansionSelfConsistent",
// "darkMatterHalo & IsProfiledHalo".
using System;

[AttributeUsage(AttributeTargets.Field)]
public class UIPanelAttribute : Attribute
{
    public readonly string title;
    public string En;          // titre en anglais
    public string ShowIf;      // condition d'affichage de l'onglet entier
    public bool Single;        // true = ne concerne que ce champ (les suivants restent dans l'onglet précédent)
    public UIPanelAttribute(string title) { this.title = title; }
}

[AttributeUsage(AttributeTargets.Field)]
public class UILabelAttribute : Attribute
{
    public readonly string label;
    public string En;          // libellé en anglais
    public UILabelAttribute(string label) { this.label = label; }
}

[AttributeUsage(AttributeTargets.Field)]
public class ShowIfAttribute : Attribute
{
    public readonly string condition;
    public ShowIfAttribute(string condition) { this.condition = condition; }
}

[AttributeUsage(AttributeTargets.Field)]
public class LiveAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public class UIHideAttribute : Attribute { }

// Aide en anglais (l'aide française est le [Tooltip] habituel, aussi affiché dans l'inspecteur Unity)
[AttributeUsage(AttributeTargets.Field)]
public class TooltipEnAttribute : Attribute
{
    public readonly string text;
    public TooltipEnAttribute(string text) { this.text = text; }
}

// Nom anglais d'une valeur d'énumération (le nom français est [InspectorName])
[AttributeUsage(AttributeTargets.Field)]
public class EnNameAttribute : Attribute
{
    public readonly string name;
    public EnNameAttribute(string name) { this.name = name; }
}

// Champ texte contenant un dossier : l'interface ajoute un bouton « Parcourir… »
[AttributeUsage(AttributeTargets.Field)]
public class FolderPathAttribute : Attribute { }

// Langue de l'interface (choisie dans JanusUI, mémorisée entre deux lancements)
public static class JanusLang
{
    public static bool English;
    public static string L(string fr, string en) => English ? en : fr;
}