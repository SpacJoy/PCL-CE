using LocalCompFile = PCL.LocalComp.LocalCompFile;

namespace PCL;

/// <summary>
///     拖入 / 安装模组、资源包之前的重复检测。
///     仅靠文件名判断会漏掉真正的重复：同一个 Mod 换了版本号就是另一个文件名
///     （<c>spacejoy-mod-0.19.0.jar</c> 与 <c>spacejoy-mod-0.18.0.jar</c>），
///     并排放着会让游戏同时加载两份。这里按 Mod 自述的 ID 判断是不是同一个东西。
/// </summary>
public static class CompDuplicateCheck
{
    /// <summary>已存在于目标目录中的同类组件。</summary>
    public sealed record ExistingItem
    {
        /// <summary>磁盘上的完整路径。</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>文件名（含扩展名）。</summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>Mod 自述 ID；资源包等没有该概念时为 null。</summary>
        public string? ModId { get; init; }

        /// <summary>Mod 自述版本号。</summary>
        public string? Version { get; init; }

        /// <summary>该文件是否已被禁用（<c>.disabled</c>）。</summary>
        public bool IsDisabled { get; init; }
    }

    /// <summary>一个待安装文件与它所冲突的已存在组件。</summary>
    public sealed record Conflict
    {
        public string SourcePath { get; init; } = string.Empty;
        public string SourceFileName { get; init; } = string.Empty;
        public List<ExistingItem> Existing { get; init; } = [];
    }

    /// <summary>
    ///     查找待安装文件与目标目录中已有组件的重复项。
    ///     判定顺序：Mod ID 相同 → 同名文件 → 同名资源包（去扩展名后完全一致）。
    /// </summary>
    public static List<Conflict> FindConflicts(
        IEnumerable<string> sourceFiles,
        string targetFolder,
        Comp.CompType compType)
    {
        var conflicts = new List<Conflict>();
        if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
            return conflicts;

        var existing = ScanExisting(targetFolder, compType);
        if (existing.Count == 0)
            return conflicts;

        foreach (var sourcePath in sourceFiles)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                continue;

            var sourceName = Path.GetFileName(sourcePath);
            var sourceModId = compType == Comp.CompType.Mod ? TryReadModId(sourcePath) : null;
            var sourceStem = Path.GetFileNameWithoutExtension(NormalizeStem(sourceName));

            var matched = existing
                .Where(item => IsSameItem(item, sourceName, sourceModId, sourceStem, compType))
                .ToList();

            if (matched.Count == 0)
                continue;

            conflicts.Add(new Conflict
            {
                SourcePath = sourcePath,
                SourceFileName = sourceName,
                Existing = matched
            });
        }

        return conflicts;
    }

    private static bool IsSameItem(
        ExistingItem item,
        string sourceFileName,
        string? sourceModId,
        string sourceStem,
        Comp.CompType compType)
    {
        // 1. Mod ID 相同即视为同一个模组，与版本号、文件名无关。
        if (!string.IsNullOrWhiteSpace(sourceModId)
            && !string.IsNullOrWhiteSpace(item.ModId)
            && string.Equals(sourceModId, item.ModId, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. 同名文件（含 .disabled 的情况）。
        if (string.Equals(
                NormalizeStem(item.FileName),
                NormalizeStem(sourceFileName),
                StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. 资源包 / 光影包没有 Mod ID，退回同名比较。
        if (compType is Comp.CompType.ResourcePack or Comp.CompType.Shader)
            return string.Equals(
                Path.GetFileNameWithoutExtension(NormalizeStem(item.FileName)),
                sourceStem,
                StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static List<ExistingItem> ScanExisting(string folder, Comp.CompType compType)
    {
        var result = new List<ExistingItem>();
        foreach (var path in Directory.GetFiles(folder))
        {
            var name = Path.GetFileName(path);
            if (!IsRelevantFile(name, compType))
                continue;

            string? modId = null;
            string? version = null;
            if (compType == Comp.CompType.Mod)
                (modId, version) = TryReadModInfo(path);

            result.Add(new ExistingItem
            {
                Path = path,
                FileName = name,
                ModId = modId,
                Version = version,
                IsDisabled = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            });
        }

        return result;
    }

    private static bool IsRelevantFile(string fileName, Comp.CompType compType)
    {
        return compType switch
        {
            Comp.CompType.Mod => LocalCompFile.IsModFile(fileName),
            Comp.CompType.ResourcePack or Comp.CompType.Shader =>
                fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase),
            Comp.CompType.Schematic =>
                fileName.EndsWith(".litematic", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".nbt", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".schem", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>去掉 <c>.disabled</c> / <c>.old</c> 后再比较，避免把已禁用的同名模组当成不同文件。</summary>
    private static string NormalizeStem(string fileName)
    {
        return fileName.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".old", "", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadModId(string path)
    {
        return TryReadModInfo(path).ModId;
    }

    private static (string? ModId, string? Version) TryReadModInfo(string path)
    {
        try
        {
            var localFile = new LocalCompFile(path);
            localFile.Load();
            return (localFile.ModId, localFile.Version);
        }
        catch (Exception ex)
        {
            // 读不出元数据的文件不参与 Mod ID 比对，退回文件名判断。
            LauncherCommon.Log(ex, $"读取组件信息失败：{path}", LauncherLogLevel.Developer);
            return (null, null);
        }
    }
}
