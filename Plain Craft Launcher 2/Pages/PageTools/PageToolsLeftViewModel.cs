using PCL.Core.App;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>工具页面左侧导航栏的视图模型。</summary>
public class PageToolsLeftViewModel : ViewModelBase
{
    public bool IsHiddenPage(bool isGameLinkChecked, bool isLogChecked, bool isTestChecked, bool isForceShow)
    {
        var hide = Config.Preference.Hide;
        var isHiddenPage = isGameLinkChecked && hide.ToolsGameLink || isLogChecked && hide.ToolsLog ||
                           isTestChecked && hide.ToolsTest;
        return isForceShow ? false : isHiddenPage;
    }

    public FormMain.PageSubType ResolveInitialPageId()
    {
        var hide = Config.Preference.Hide;
        // 默认落在「百宝箱」：它只是若干输入框与按钮，初始化开销远小于日志页，
        // 不会在进入工具页的第一帧就卡住界面。
        if (!hide.ToolsTest) return FormMain.PageSubType.ToolsTest;
        if (!hide.ToolsGameLink) return FormMain.PageSubType.ToolsGameLink;
        if (!hide.ToolsLog) return FormMain.PageSubType.ToolsLog;
        return FormMain.PageSubType.ToolsTest;
    }
}
