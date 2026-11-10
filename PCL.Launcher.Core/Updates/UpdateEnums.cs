namespace PCL;

public static class UpdateEnums
{
    public enum UpdateType
    {
        Silent = 0,
        PromptOnly = 1,
        DownloadAndPrompt = 2,
        UpdateNow = 3
    }

    public enum VersionStatus
    {
        Latest,
        NotLatest,
        Unknown,

        /// <summary>仓库还没有发布任何版本，此时无法判断新旧，也不应视为故障。</summary>
        NoRelease
    }
}