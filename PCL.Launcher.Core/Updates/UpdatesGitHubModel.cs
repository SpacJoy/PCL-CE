using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

/// <summary>
///     直接基于 GitHub Releases 的更新源。
///     版本号取 Release 的 tag，下载地址取匹配当前架构的发布资源，校验值取该资源的 digest 或由发布者写在正文中的 sha256。
/// </summary>
/// <summary>
///     表示更新源里还没有任何可用的发布版本。
///     与网络故障区分开，界面可以据此提示“暂无可更新版本”而不是报网络错误。
/// </summary>
public class NoReleaseAvailableException(string message) : Exception(message);

public class UpdatesGitHubModel : IUpdateSource
{
    private readonly string _owner;

    private readonly string _repo;

    /// <summary>缓存的 Release 列表，避免同一次检查里重复请求。</summary>
    private JsonArray _releaseCache;

    public UpdatesGitHubModel(string owner, string repo, string name = "GitHub")
    {
        _owner = owner;
        _repo = repo;
        SourceName = name;
    }

    public string SourceName { get; set; }

    public bool IsAvailable()
    {
        return !string.IsNullOrWhiteSpace(_owner) && !string.IsNullOrWhiteSpace(_repo);
    }

    public bool RefreshCache()
    {
        _releaseCache = FetchReleases();
        return true;
    }

    public VersionDataModel GetLatestVersion(UpdateChannel channel, UpdateArch arch)
    {
        var release = SelectRelease(channel);
        if (release is null)
            throw new NoReleaseAvailableException("仓库中还没有发布任何版本");

        var asset = SelectAsset(release, arch);
        if (asset is null)
            throw new NoReleaseAvailableException($"最新版本没有适配 {arch} 架构的下载资源");

        return new VersionDataModel
        {
            Source = SourceName,
            VersionName = NormalizeVersionName(release["tag_name"]?.GetValue<string>()),
            VersionCode = 0,
            Sha256 = GetAssetSha256(asset, release),
            Changelog = release["body"]?.GetValue<string>() ?? string.Empty
        };
    }

    public bool IsLatest(
        UpdateChannel channel,
        UpdateArch arch,
        SemVer currentVersion,
        int currentVersionCode)
    {
        var latest = GetLatestVersion(channel, arch);
        // tag 可能带前缀（v1.2.3 或 1.0.0+abcdef），统一清洗后再比较。
        if (!SemVer.TryParse(NormalizeVersionName(latest.VersionName), out var remoteVersion))
            return true; // 解析不了就当作已是最新，避免误报更新
        return currentVersion >= remoteVersion;
    }

    /// <summary>
    ///     GitHub 更新源没有公告系统，这里返回空列表而不是抛异常：
    ///     启动流程会无条件拉取公告，抛出异常会在每次启动时弹出错误提示。
    /// </summary>
    public VersionAnnouncementDataModel GetAnnouncementList()
    {
        return new VersionAnnouncementDataModel { Content = [] };
    }

    public List<Loading.LoaderBase> GetDownloadLoader(
        UpdateChannel channel,
        UpdateArch arch,
        string output)
    {
        return
        [
            new Loading.LoaderTask<int, List<DownloadFile>>(
                Lang.Text("Update.Task.GetDownloadInfo"),
                load =>
                {
                    var release = SelectRelease(channel)
                                  ?? throw new Exception("未找到可用的 Release");
                    var asset = SelectAsset(release, arch)
                                ?? throw new Exception("Release 中没有匹配当前架构的下载资源");
                    var url = asset["browser_download_url"]?.GetValue<string>()
                              ?? throw new Exception("下载资源缺少下载地址");
                    load.output = [new DownloadFile([url], output)];
                }),
            new LoaderDownload(Lang.Text("Update.Task.DownloadUpdateFile"), [])
        ];
    }

    #region GitHub 数据获取

    private JsonArray FetchReleases()
    {
        var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases?per_page=30";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.WithHeader("Accept", "application/vnd.github+json");
        using var response = request.SendAsync().GetAwaiter().GetResult();
        var node = LauncherCommon.GetJson(response.AsString());
        return node as JsonArray ?? [];
    }

    /// <summary>
    ///     按更新通道挑选 Release：稳定通道取正式版，测试通道优先取预发布版。
    ///     全程跳过草稿。
    /// </summary>
    private JsonObject SelectRelease(UpdateChannel channel)
    {
        _releaseCache ??= FetchReleases();
        var candidates = _releaseCache
            .OfType<JsonObject>()
            .Where(release => release["draft"]?.GetValue<bool>() != true)
            .ToList();
        if (candidates.Count == 0)
            return null;

        if (channel == UpdateChannel.Beta)
        {
            var prerelease = candidates.FirstOrDefault(release =>
                release["prerelease"]?.GetValue<bool>() == true);
            if (prerelease is not null)
                return prerelease;
        }

        return candidates.FirstOrDefault(release =>
            release["prerelease"]?.GetValue<bool>() != true);
    }

    /// <summary>在 Release 的资源列表里挑出匹配当前架构的可执行包。</summary>
    private static JsonObject SelectAsset(JsonObject release, UpdateArch arch)
    {
        var assets = release["assets"] as JsonArray;
        if (assets is null || assets.Count == 0)
            return null;

        // 只考虑能直接替换启动器的包，签名、校验和、源码包一律排除。
        var objects = assets
            .OfType<JsonObject>()
            .Where(IsExecutableAsset)
            .ToList();
        if (objects.Count == 0)
            return null;

        var archKeyword = arch == UpdateArch.Arm64 ? "arm64" : "x64";
        var matched = objects.FirstOrDefault(asset =>
            NameOf(asset).Contains(archKeyword, StringComparison.OrdinalIgnoreCase));
        matched ??= objects.FirstOrDefault(asset =>
            NameOf(asset).Contains(arch == UpdateArch.Arm64 ? "aarch64" : "x86_64",
                StringComparison.OrdinalIgnoreCase));

        // 只剩下一个包时直接用它，多数个人仓库就是这么发的
        return matched ?? (objects.Count == 1 ? objects[0] : null);
    }

    /// <summary>判断资源是否是可直接替换启动器的程序包。</summary>
    private static bool IsExecutableAsset(JsonObject asset)
    {
        var name = NameOf(asset);
        if (name.Length == 0)
            return false;
        if (name.EndsWith(".sigstore.json", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains("source", StringComparison.OrdinalIgnoreCase))
            return false;
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string NameOf(JsonObject asset)
    {
        return asset["name"]?.GetValue<string>() ?? string.Empty;
    }

    /// <summary>
    ///     取校验值：优先用 GitHub 计算好的 digest（形如 sha256:xxx），
    ///     否则从 Release 正文里找该资源的 sha256 声明。
    /// </summary>
    private static string GetAssetSha256(JsonObject asset, JsonObject release)
    {
        var digest = asset["digest"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(digest))
            return digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest["sha256:".Length..]
                : digest;

        var body = release["body"]?.GetValue<string>();
        var name = NameOf(asset);
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(name))
            return null;

        // 在正文中查找 "<文件名> ... sha256: <64位十六进制>" 之类的一行
        foreach (var line in body.Split('\n'))
        {
            if (!line.Contains(name, StringComparison.OrdinalIgnoreCase))
                continue;
            var match = Regex.Match(line, "\b[0-9a-fA-F]{64}\b");
            if (match.Success)
                return match.Value.ToLowerInvariant();
        }

        return null;
    }

    /// <summary>把 tag 清洗成可解析的版本号：去掉前缀 v，只保留版本主体。</summary>
    private static string NormalizeVersionName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return tag;
        return tag.TrimStart('v', 'V');
    }

    #endregion
}
