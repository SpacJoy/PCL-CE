using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;

namespace PCL;

/// <summary>
/// 工具页的日志子页面：展示当前（或最近一次）游戏的日志，并允许切换到其他日志文件。
/// </summary>
public partial class PageToolsLog : IPageView
{
    private const int PollIntervalMs = 500;
    private const int MaxReadBytesPerTick = 256 * 1024;
    private const long InitialTailBytes = 1024 * 1024;

    private readonly PageToolsLogViewModel _vm = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly List<LogSource> _sources = [];

    private bool _refreshed;
    private bool _suppressSelection;
    private bool _userPicked;

    private string? _currentPath;
    private Encoding _encoding = Encoding.UTF8;
    private int _bomLength;
    private long _readOffset;
    private byte[] _pending = [];
    private GameLogLevel _lastLevel = GameLogLevel.Info;
    private int _tickCount;
    private readonly uint[] _counts = new uint[5];
    private Run[]? _levelRuns;
    private string _runningSignature = "";

    public PageToolsLog()
    {
        InitializeComponent();
        DataContext = _vm;
        InitCardTitle();
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollIntervalMs) };
        _pollTimer.Tick += PollTimer_Tick;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += PageToolsLog_Unloaded;
    }

    public PageToolsLogViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => RefreshOnNavigate();

    public void OnNavigatedFrom() => _pollTimer.Stop();

    private void PageToolsLog_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
        _pollTimer.Stop();
    }

    private static readonly string[] LevelKeys = ["Debug", "Info", "Warn", "Error", "Fatal"];

    private static readonly string[] LevelBrushes =
    [
        "ColorBrushDebug", "ColorBrushInfo", "ColorBrushWarn", "ColorBrushError", "ColorBrushFatal"
    ];

    /// <summary>构建卡片标题：标题文字 + 各级别日志数量（与实时日志页面一致）。</summary>
    private void InitCardTitle()
    {
        PanLogCard.Inlines.Clear();
        var title = new Run();
        title.TrySetLocalizedValue("Text", "Tools.Log.Title");
        PanLogCard.Inlines.Add(title);

        var runs = new Run[LevelKeys.Length];
        for (var i = 0; i < LevelKeys.Length; i++)
        {
            PanLogCard.Inlines.Add(new Run(" | "));
            var brushKey = LevelKeys[i] == "Info" && ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : LevelBrushes[i];
            var run = new Run { Foreground = Application.Current.Resources[brushKey] as Brush ?? Brushes.White };
            PanLogCard.Inlines.Add(run);
            runs[i] = run;
        }

        _levelRuns = runs;
        RefreshCounters();
    }

    private void RefreshCounters()
    {
        if (_levelRuns is null)
            return;
        for (var i = 0; i < _levelRuns.Length; i++)
            _levelRuns[i].Text = $"{_counts[i]} {Lang.Text($"LogPage.Level.{LevelKeys[i]}")}";
    }

    private void RefreshOnNavigate()
    {
        if (!_refreshed)
        {
            _refreshed = true;
            BuildSources();
        }

        // OnNavigatedFrom 会停掉轮询，返回页面时必须重新启动
        if (!_pollTimer.IsEnabled)
            _pollTimer.Start();
    }

    protected override void RefreshLocalization()
    {
        base.RefreshLocalization();
        InitCardTitle();
        BuildSources();
    }

    #region 日志来源

    private sealed class LogSource(string title, string filePath, bool isRunning)
    {
        public string Title { get; } = title;
        public string FilePath { get; } = filePath;
        public bool IsRunning { get; } = isRunning;
    }

    /// <summary>
    ///     构建可选的日志文件列表：运行中的游戏 → 最近一次游戏的实例日志 → 崩溃报告。
    /// </summary>
    private void BuildSources()
    {
        var previous = _currentPath;
        // 用户手动挑选的文件（可能是实例目录之外的日志）在刷新列表后仍要保留
        var picked = _userPicked
            ? _sources.FirstOrDefault(s => string.Equals(s.FilePath, _currentPath, StringComparison.OrdinalIgnoreCase))
            : null;
        _sources.Clear();

        // 1. 运行中的游戏
        var running = GetRunningWatchers();
        _runningSignature = string.Join('|', running.Select(w => w.version.Name));
        foreach (var watcher in running)
            AddSource(Lang.Text("Tools.Log.Source.Running", watcher.version.Name), GetLatestLogPath(watcher), true);

        // 2. 最近一次游戏的实例日志
        var recent = FindRecentInstance();
        if (recent is not null)
        {
            var logsFolder = Path.Combine(recent.PathIndie, "logs");
            foreach (var file in EnumerateFiles(logsFolder, "*.log").Take(8))
                AddSource(FormatFileTitle(file), file, false);
            var crashFolder = Path.Combine(recent.PathIndie, "crash-reports");
            foreach (var file in EnumerateFiles(crashFolder, "*.txt").Take(5))
                AddSource(Lang.Text("Tools.Log.Source.Crash", Path.GetFileName(file)), file, false);
        }

        // 3. 当前选中实例的日志（兜底，便于手动切换）
        var selected = InstanceList.McMcInstanceSelected;
        if (selected is not null && !string.Equals(selected.Name, recent?.Name, StringComparison.Ordinal))
        {
            foreach (var file in EnumerateFiles(Path.Combine(selected.PathIndie, "logs"), "*.log").Take(5))
                AddSource(FormatFileTitle(file), file, false);
        }

        // 用户手动挑选的文件优先保留在列表最前面（已在自动列表里、或文件已被删除就不重复插入）
        if (picked is not null && File.Exists(picked.FilePath) &&
            !_sources.Any(s => string.Equals(s.FilePath, picked.FilePath, StringComparison.OrdinalIgnoreCase)))
            _sources.Insert(0, picked);

        FillCombo(previous);
    }

    private void AddSource(string title, string filePath, bool isRunning)
    {
        if (!File.Exists(filePath))
            return;
        if (_sources.Any(s => string.Equals(s.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            return;
        _sources.Add(new LogSource(title, filePath, isRunning));
    }

    private static IEnumerable<string> EnumerateFiles(string folder, string pattern)
    {
        try
        {
            if (!Directory.Exists(folder))
                return [];
            return Directory.GetFiles(folder, pattern)
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string FormatFileTitle(string file)
    {
        try
        {
            return $"{Path.GetFileName(file)} - {Lang.Date(File.GetLastWriteTime(file), "G")}";
        }
        catch (Exception)
        {
            return Path.GetFileName(file);
        }
    }

    private static McInstance? FindRecentInstance()
    {
        // 优先取最后一次启动的实例（watcher 按启动顺序注册，关闭后仍保留在列表里）
        var lastWatcher = GameWatcher.mcWatcherList.LastOrDefault();
        if (lastWatcher?.version is not null)
            return lastWatcher.version;

        var name = States.Game.SelectedInstance;
        if (string.IsNullOrWhiteSpace(name))
            return InstanceList.McMcInstanceSelected;
        return InstanceList.mcInstanceList.Values
                   .SelectMany(list => list)
                   .FirstOrDefault(instance => string.Equals(instance.Name, name, StringComparison.Ordinal))
               ?? InstanceList.McMcInstanceSelected;
    }

    private void FillCombo(string? previousPath)
    {
        _suppressSelection = true;
        try
        {
            ComboLogFile.Items.Clear();
            foreach (var source in _sources)
                ComboLogFile.Items.Add(new MyComboBoxItem { Content = source.Title, Tag = source });

            // 保持原有选择；若用户未手动选择过，则优先切到运行中的游戏日志
            LogSource? target = null;
            if (_userPicked && previousPath is not null)
                target = _sources.FirstOrDefault(s => string.Equals(s.FilePath, previousPath, StringComparison.OrdinalIgnoreCase));
            target ??= _sources.FirstOrDefault(s => s.IsRunning) ?? _sources.FirstOrDefault();

            ComboLogFile.SelectedIndex = target is null ? -1 : _sources.IndexOf(target);
        }
        finally
        {
            _suppressSelection = false;
        }

        var selected = ComboLogFile.SelectedIndex >= 0 ? _sources[ComboLogFile.SelectedIndex] : null;
        if (selected is null)
        {
            // 没有可选来源时不要反复清空文档
            if (_currentPath is not null)
                OpenSource(null);
            LabEmpty.Visibility = Visibility.Visible;
            return;
        }

        // 路径未变时只刷新空状态，避免重复重读整份日志造成闪烁
        if (_currentPath is not null &&
            string.Equals(selected.FilePath, _currentPath, StringComparison.OrdinalIgnoreCase))
        {
            LabEmpty.Visibility = Visibility.Collapsed;
            return;
        }

        OpenSource(selected);
    }

    private void ComboLogFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
            return;
        var index = ComboLogFile.SelectedIndex;
        if (index < 0 || index >= _sources.Count)
            return;
        _userPicked = true;
        OpenSource(_sources[index]);
    }

    #endregion

    #region 文件读取

    private void OpenSource(LogSource? source)
    {
        _currentPath = source?.FilePath;
        _pending = [];
        _readOffset = 0;
        _bomLength = 0;
        _lastLevel = GameLogLevel.Info;
        _tickCount = 0;
        Array.Clear(_counts);
        RefreshCounters();
        ClearLog();
        LabEmpty.Visibility = source is null ? Visibility.Visible : Visibility.Collapsed;
        if (source is null)
            return;

        try
        {
            using var stream = new FileStream(source.FilePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            var head = new byte[(int)Math.Min(4L, length)];
            if (head.Length > 0)
                stream.ReadExactly(head);
            _encoding = DetectEncoding(head, out _bomLength);
            if (_bomLength == 0)
                _encoding = ProbeEncoding(stream, length);
            _readOffset = Math.Min(_bomLength, length);

            // 文件过大时只读取末尾部分，避免一次载入过多内容
            if (length - _readOffset > InitialTailBytes)
            {
                _readOffset = length - InitialTailBytes;
                stream.Seek(_readOffset, SeekOrigin.Begin);
                var probe = new byte[4096];
                var read = stream.Read(probe, 0, probe.Length);
                var index = Array.IndexOf(probe, (byte)'\n', 0, read);
                _readOffset += index >= 0 ? index + 1 : read;
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "打开日志文件失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Log.Error.Load"));
        }

        // 初次载入时连续读取若干块，避免尾部内容逐帧填充
        for (var i = 0; i < InitialTailBytes / MaxReadBytesPerTick + 1; i++)
        {
            if (!ReadChunk())
                break;
        }
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        PollCurrentFile();
        // 每 3 秒检查一次日志来源是否有变化（新游戏启动 / 游戏关闭 / 日志文件刚生成）
        _tickCount++;
        if (_tickCount % 6 != 0)
            return;
        var running = GetRunningWatchers();
        var signature = string.Join('|', running.Select(w => w.version.Name));
        var missing = running.Any(w => !_sources.Any(s => s.IsRunning &&
                                                         string.Equals(s.FilePath, GetLatestLogPath(w),
                                                             StringComparison.OrdinalIgnoreCase)));
        if (_sources.Count == 0 || missing || !string.Equals(signature, _runningSignature, StringComparison.Ordinal))
            BuildSources();
    }

    private static List<GameWatcher.Watcher> GetRunningWatchers()
    {
        try
        {
            return GameWatcher.mcWatcherList
                .Where(w => w.State is GameWatcher.Watcher.MinecraftState.Loading or GameWatcher.Watcher.MinecraftState.Running)
                .ToList();
        }
        catch (InvalidOperationException)
        {
            // 列表正被启动线程修改，等下一轮
            return [];
        }
    }

    private static string GetLatestLogPath(GameWatcher.Watcher watcher)
    {
        return Path.Combine(watcher.version.PathIndie, "logs", "latest.log");
    }

    private void PollCurrentFile() => ReadChunk();

    /// <summary>读取一块新增内容；返回 <see langword="false" /> 表示当前已无更多内容。</summary>
    private bool ReadChunk()
    {
        if (_currentPath is null)
            return false;
        try
        {
            if (!File.Exists(_currentPath))
            {
                LabEmpty.Visibility = Visibility.Visible;
                return false;
            }

            using var stream = new FileStream(_currentPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _readOffset)
            {
                // 文件被截断或轮转
                _readOffset = _bomLength;
                _pending = [];
                _lastLevel = GameLogLevel.Info;
                Array.Clear(_counts);
                RefreshCounters();
                ClearLog();
            }

            LabEmpty.Visibility = Visibility.Collapsed;
            if (stream.Length <= _readOffset)
                return false;
            stream.Seek(_readOffset, SeekOrigin.Begin);
            var buffer = new byte[MaxReadBytesPerTick];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return false;
            _readOffset += read;
            AppendBytes(buffer.AsSpan(0, read));
            return read == MaxReadBytesPerTick;
        }
        catch (IOException)
        {
            // 文件正被占用或暂时不可读，等下一轮
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void AppendBytes(ReadOnlySpan<byte> span)
    {
        byte[] data;
        if (_pending.Length == 0)
        {
            data = span.ToArray();
        }
        else
        {
            data = new byte[_pending.Length + span.Length];
            _pending.CopyTo(data, 0);
            span.CopyTo(data.AsSpan(_pending.Length));
        }

        var lastNewLine = data.AsSpan().LastIndexOf((byte)'\n');
        if (lastNewLine < 0)
        {
            // 没有完整的行，缓存到下一次（防止无限增长）
            _pending = data.Length > 1024 * 1024 ? [] : data;
            return;
        }

        _pending = data[(lastNewLine + 1)..];
        var text = _encoding.GetString(data, 0, lastNewLine);
        if (text.Length == 0)
            return;

        var lines = text.Split('\n');
        var maxLog = _vm.GetMaxLogCount();
        var start = 0;
        if (maxLog != ulong.MaxValue && (ulong)lines.Length > maxLog)
            start = lines.Length - (int)Math.Min(maxLog, (ulong)lines.Length);

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
                continue;
            AppendLine(line);
        }

        RefreshCounters();
        if (CheckAutoScroll.Checked == true)
            PanBack.ScrollToEnd();
    }

    private const int MaxLineLength = 8192;

    private void AppendLine(string line)
    {
        if (line.Length > MaxLineLength)
            line = string.Concat(line.AsSpan(0, MaxLineLength), " …");
        if (!IsLogContinuationLine(line))
            _lastLevel = GetLevel(line, _lastLevel);
        _counts[(int)_lastLevel]++;
        var document = PanLog.Document;
        document.Blocks.Add(new Paragraph(new Run(line))
        {
            Foreground = GetColor(_lastLevel), Margin = new Thickness(0)
        });
        var maxLog = _vm.GetMaxLogCount();
        while ((ulong)document.Blocks.Count > maxLog && document.Blocks.FirstBlock is not null)
            document.Blocks.Remove(document.Blocks.FirstBlock);
    }

    private void ClearLog()
    {
        var document = new FlowDocument { PagePadding = new Thickness(0) };
        PanLog.Document = document;
    }

    #endregion

    #region 解析与着色

    private static bool IsLogContinuationLine(string line)
    {
        return line.StartsWithF("\tat ") || line.StartsWithF("Caused by: ") || line.StartsWithF("\t... ");
    }

    private static GameLogLevel GetLevel(string line, GameLogLevel lastLevel)
    {
        var separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        var levelPrefix = separatorIndex >= 0 ? line[..separatorIndex] : line;
        if (levelPrefix.ContainsF("FATAL")) return GameLogLevel.Fatal;
        if (levelPrefix.ContainsF("ERROR")) return GameLogLevel.Error;
        if (levelPrefix.ContainsF("WARN")) return GameLogLevel.Warn;
        if (levelPrefix.ContainsF("INFO")) return GameLogLevel.Info;
        if (levelPrefix.ContainsF("DEBUG")) return GameLogLevel.Debug;
        if (line.StartsWithF("Exception in thread \"")) return GameLogLevel.Error;
        if (lastLevel >= GameLogLevel.Warn &&
            (line.ContainsF("Exception") || line.ContainsF("Realms authentication error with message ") ||
             line.StartsWithF("\tat ")))
            return lastLevel;
        return GameLogLevel.Info;
    }

    private static SolidColorBrush GetColor(GameLogLevel level)
    {
        var name = level switch
        {
            GameLogLevel.Debug => "ColorBrushDebug",
            GameLogLevel.Warn => "ColorBrushWarn",
            GameLogLevel.Error => "ColorBrushError",
            GameLogLevel.Fatal => "ColorBrushFatal",
            _ => ThemeManager.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"
        };
        return Application.Current.Resources[name] as SolidColorBrush ?? Brushes.White;
    }

    /// <summary>
    ///     无 BOM 时先按 UTF-8 严格解码试读一段内容，失败则认为是 GBK 系编码。<br/>
    ///     Minecraft 的日志默认是 UTF-8，但部分旧版本或第三方启动环境会写成 GBK。
    /// </summary>
    private static Encoding ProbeEncoding(FileStream stream, long length)
    {
        var probeLength = (int)Math.Min(64 * 1024L, length);
        if (probeLength <= 0)
            return new UTF8Encoding(false);
        try
        {
            var buffer = new byte[probeLength];
            var position = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            var read = stream.Read(buffer, 0, probeLength);
            stream.Seek(position, SeekOrigin.Begin);
            if (read <= 0)
                return new UTF8Encoding(false);
            new UTF8Encoding(false, true).GetString(buffer, 0, read);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            return Encodings.GB18030;
        }
        catch (Exception)
        {
            return new UTF8Encoding(false);
        }
    }

    private static Encoding DetectEncoding(byte[] head, out int bomLength)
    {
        bomLength = 0;
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            bomLength = 3;
            return new UTF8Encoding(false);
        }

        if (head.Length >= 4 && head[0] == 0xFF && head[1] == 0xFE && head[2] == 0x00 && head[3] == 0x00)
        {
            bomLength = 4;
            return new UTF32Encoding(false, false);
        }

        if (head.Length >= 4 && head[0] == 0x00 && head[1] == 0x00 && head[2] == 0xFE && head[3] == 0xFF)
        {
            bomLength = 4;
            return new UTF32Encoding(true, false);
        }

        if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            bomLength = 2;
            return new UnicodeEncoding(false, false);
        }

        if (head.Length >= 2 && head[0] == 0xFE && head[1] == 0xFF)
        {
            bomLength = 2;
            return new UnicodeEncoding(true, false);
        }

        return new UTF8Encoding(false);
    }

    #endregion

    #region 操作

    private void BtnRefresh_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BuildSources();
    }

    private void BtnOpenFolder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_currentPath is null)
            return;
        var folder = Path.GetDirectoryName(_currentPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Basics.OpenPath(folder);
    }

    private void BtnBrowse_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var path = SystemDialogs.SelectFile(Lang.Text("Tools.Log.Browse.Filter"), Lang.Text("Tools.Log.Browse.Title"));
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        _userPicked = true;
        var existing = _sources.FirstOrDefault(s => string.Equals(s.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            // 已经在该列表里，直接切过去
            _suppressSelection = true;
            try
            {
                ComboLogFile.SelectedIndex = _sources.IndexOf(existing);
            }
            finally
            {
                _suppressSelection = false;
            }

            if (!string.Equals(_currentPath, existing.FilePath, StringComparison.OrdinalIgnoreCase))
                OpenSource(existing);
            return;
        }

        var source = new LogSource(FormatFileTitle(path), path, false);
        _sources.Insert(0, source);
        _suppressSelection = true;
        try
        {
            ComboLogFile.Items.Insert(0, new MyComboBoxItem { Content = source.Title, Tag = source });
            ComboLogFile.SelectedIndex = 0;
        }
        finally
        {
            _suppressSelection = false;
        }

        OpenSource(source);
    }

    #endregion
}
