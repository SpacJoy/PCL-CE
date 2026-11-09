using System;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>工具页日志子页面的视图模型。</summary>
public partial class PageToolsLogViewModel : ViewModelBase
{
    /// <summary>日志文件选择框的标题。</summary>
    public string FileCardTitle => Lang.Text("Tools.Log.File.Title");

    /// <summary>当前可展示的最大日志行数。</summary>
    public ulong GetMaxLogCount()
    {
        var maxLog = (ulong)Config.System.MaxGameLog;
        switch (maxLog)
        {
            case <= 5UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 10m + 50m);
                break;
            }
            case <= 13UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 50m - 150m);
                break;
            }
            case <= 28UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 100m - 800m);
                break;
            }
            default:
            {
                maxLog = ulong.MaxValue;
                break;
            }
        }

        return maxLog;
    }

    /// <summary>获取最大行数滑条的提示文本。</summary>
    public static object GetMaxLogHintText(object value)
    {
        return (int)value switch
        {
            <= 5 => ((int)value * 10 + 50).ToString(),
            <= 13 => ((int)value * 50 - 150).ToString(),
            <= 28 => ((int)value * 100 - 800).ToString(),
            _ => Lang.Text("LogPage.MaxLines.Unlimited")
        };
    }
}
