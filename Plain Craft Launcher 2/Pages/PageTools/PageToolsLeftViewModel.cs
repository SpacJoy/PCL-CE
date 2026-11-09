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
        if (!hide.ToolsLog) return FormMain.PageSubType.ToolsLog;
        if (!hide.ToolsGameLink) return FormMain.PageSubType.ToolsGameLink;
        if (!hide.ToolsTest) return FormMain.PageSubType.ToolsTest;
        return FormMain.PageSubType.ToolsLog;
    }
}
