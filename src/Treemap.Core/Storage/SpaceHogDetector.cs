using System.Text.RegularExpressions;

namespace Treemap.Core.Storage;

/// <summary>A known category of space consumer, with every place it was found in the scan.</summary>
public sealed record SpaceHog(
    string RuleId,
    string Title,
    string Explanation,
    string Advice,
    long SizeBytes,
    IReadOnlyList<FolderNode> Nodes);

/// <summary>
/// Spots the usual suspects when a Windows drive is full (hibernation file, WinSxS, node_modules, virtual disks...)
/// in an already scanned tree. It only describes: nothing here deletes or changes anything.
/// </summary>
public static partial class SpaceHogDetector
{
    private sealed record Rule(
        string Id,
        string Title,
        string Explanation,
        string Advice,
        Func<FolderNode, bool> Matches);

    private static readonly Rule[] Rules =
    [
        FileRule(
            "hibernation",
            "Fichier d'hibernation",
            "hiberfil.sys : copie de la mémoire écrite au disque pour la veille prolongée et le démarrage rapide.",
            "Se supprime en désactivant l'hibernation (invite de commandes administrateur : powercfg /h off). Cela désactive aussi le démarrage rapide.",
            "hiberfil.sys"),
        FileRule(
            "pagefile",
            "Fichier d'échange (mémoire virtuelle)",
            "pagefile.sys : extension de la RAM sur le disque, gérée par Windows.",
            "Nécessaire à Windows : ne pas supprimer à la main. La taille se règle dans Paramètres système avancés > Performances > Mémoire virtuelle.",
            "pagefile.sys"),
        FileRule(
            "swapfile",
            "Fichier d'échange des applications",
            "swapfile.sys : espace d'échange utilisé par les applications modernes.",
            "Géré par Windows, ne pas le supprimer à la main.",
            "swapfile.sys"),
        FileRule(
            "memory-dump",
            "Vidage mémoire",
            "MEMORY.DMP : image de la mémoire enregistrée lors d'un plantage de Windows.",
            "Peut être supprimé via Nettoyage de disque (fichiers de vidage mémoire) s'il ne sert pas à diagnostiquer un plantage.",
            "MEMORY.DMP"),
        new Rule(
            "virtual-disk",
            "Disques virtuels",
            "Fichiers .vhdx / .vhd / .vmdk / .vdi : disques de WSL, Docker, Hyper-V ou machines virtuelles.",
            "Ils grossissent mais ne rétrécissent pas tout seuls : supprimer des données à l'intérieur ne libère pas toujours de la place. Il faut compacter le disque (WSL : wsl --manage <distro> --set-sparse true, ou réglages de Docker Desktop).",
            n => n.Kind == NodeKind.File && HasAnyExtension(n.Name, ".vhdx", ".vhd", ".vmdk", ".vdi")),
        FolderRule(
            "node-modules",
            "Dossiers node_modules",
            "Dépendances JavaScript téléchargées par npm, une copie par projet.",
            "Entièrement recréables avec npm install : supprimer ceux des projets que tu n'ouvres plus libère de la place sans rien perdre.",
            "node_modules"),
        FolderRule(
            "winsxs",
            "Magasin de composants Windows (WinSxS)",
            "Versions des composants système, utilisées pour les mises à jour et la réparation de Windows.",
            "Ne jamais supprimer à la main. DISM /Online /Cleanup-Image /StartComponentCleanup le nettoie en sécurité. Une partie de la taille affichée est partagée par des liens physiques et comptée plusieurs fois.",
            "WinSxS"),
        FolderRule(
            "windows-old",
            "Ancienne installation de Windows",
            "Windows.old : sauvegarde de la version précédente après une mise à niveau.",
            "Se supprime via Nettoyage de disque > Nettoyer les fichiers système (Installation(s) précédente(s) de Windows). Windows le retire aussi seul après quelques semaines.",
            "Windows.old"),
        FolderRule(
            "recycle-bin",
            "Corbeille",
            "Fichiers supprimés mais pas encore vidés.",
            "Vider la corbeille libère cet espace.",
            "$Recycle.Bin"),
        new Rule(
            "windows-update-cache",
            "Cache de Windows Update",
            "SoftwareDistribution\\Download : mises à jour téléchargées.",
            "Se vide via Nettoyage de disque > Nettoyer les fichiers système (Fichiers de mise à jour Windows).",
            n => n.IsFolder && PathEndsWith(n, @"\SoftwareDistribution\Download")),
        new Rule(
            "temp",
            "Fichiers temporaires",
            "Dossiers Temp de Windows et de ton profil.",
            "Généralement supprimables sans risque une fois les applications fermées (Paramètres > Stockage > Fichiers temporaires).",
            n => n.IsFolder && (PathEndsWith(n, @"\AppData\Local\Temp") || PathEndsWith(n, @"\Windows\Temp"))),
        new Rule(
            "nuget-cache",
            "Cache de paquets NuGet",
            "Paquets .NET téléchargés, partagés entre tes projets.",
            "Recréé au besoin : dotnet nuget locals all --clear.",
            n => n.IsFolder && PathEndsWith(n, @"\.nuget\packages")),
        new Rule(
            "games",
            "Bibliothèques de jeux",
            "Jeux installés (Steam, Epic Games, Xbox).",
            "Désinstalle depuis le lanceur les jeux que tu ne lances plus : ils pèsent souvent 50 à 150 Go chacun.",
            n => n.IsFolder && (PathEndsWith(n, @"\steamapps\common")
                || n.Name.Equals("Epic Games", StringComparison.OrdinalIgnoreCase)
                || n.Name.Equals("XboxGames", StringComparison.OrdinalIgnoreCase))),
        new Rule(
            "downloads",
            "Téléchargements",
            "Dossier Téléchargements de ton profil.",
            "Trie ou supprime les installeurs et archives déjà utilisés.",
            n => n.IsFolder && DownloadsPattern().IsMatch(n.FullPath)),
    ];

    /// <summary>Returns the categories found in the tree, largest first. Matched folders are not searched further.</summary>
    public static IReadOnlyList<SpaceHog> Find(FolderNode root)
    {
        var found = new Dictionary<string, List<FolderNode>>();
        Visit(root, found);

        return Rules
            .Where(r => found.ContainsKey(r.Id))
            .Select(r =>
            {
                var nodes = found[r.Id].OrderByDescending(n => n.SizeBytes).ToList();
                return new SpaceHog(r.Id, r.Title, r.Explanation, r.Advice, nodes.Sum(n => n.SizeBytes), nodes);
            })
            .OrderByDescending(h => h.SizeBytes)
            .ToList();
    }

    private static void Visit(FolderNode node, Dictionary<string, List<FolderNode>> found)
    {
        if (node.Kind == NodeKind.OtherFiles)
        {
            return;
        }

        foreach (var rule in Rules)
        {
            if (!rule.Matches(node))
            {
                continue;
            }

            if (!found.TryGetValue(rule.Id, out var list))
            {
                found[rule.Id] = list = [];
            }

            list.Add(node);
            return;
        }

        foreach (var child in node.Children)
        {
            Visit(child, found);
        }
    }

    private static Rule FileRule(string id, string title, string explanation, string advice, string fileName) =>
        new(id, title, explanation, advice,
            n => n.Kind == NodeKind.File && n.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private static Rule FolderRule(string id, string title, string explanation, string advice, string folderName) =>
        new(id, title, explanation, advice,
            n => n.IsFolder && n.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));

    private static bool HasAnyExtension(string name, params string[] extensions) =>
        extensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static bool PathEndsWith(FolderNode node, string suffix) =>
        node.FullPath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[A-Za-z]:\\Users\\[^\\]+\\Downloads$", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadsPattern();
}
