using Dalamud.Interface.Components;
using ECommons.SimpleGui;
using Lumina.Excel.Sheets;

namespace Hyperborea.Gui;
public class SettingsWindow : Window
{
    public SettingsWindow() : base("Hyperborea 设置")
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        if (ImGuiGroup.BeginGroupBox("一般设置"))
        {
            ImGuiEx.Text($"坐骑:");
            ImGuiEx.SetNextItemFullWidth(-10);
            if (ImGui.BeginCombo($"##mount", Utils.GetMountName(C.CurrentMount) ?? "选择坐骑..."))
            {
                ImGui.SetNextItemWidth(150f);
                ImGui.InputTextWithHint("##search", "搜索", ref UI.MountFilter, 50);
                if (ImGui.Selectable("无坐骑"))
                {
                    C.CurrentMount = 0;
                }
                foreach (var x in Svc.Data.GetExcelSheet<Mount>())
                {
                    var name = Utils.GetMountName(x.RowId);
                    if (!name.IsNullOrEmpty())
                    {
                        if (UI.MountFilter.IsNullOrEmpty() || name.Contains(UI.MountFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (ImGui.Selectable(name))
                            {
                                C.CurrentMount = x.RowId;
                            }
                        }
                    }
                }
                ImGui.EndCombo();
            }
            ImGuiGroup.EndGroupBox();
        }

        if (ImGuiGroup.BeginGroupBox("危险区域", EColor.RedBright.ToUint()))
        {
            if (P.Enabled) ImGui.BeginDisabled();
            ImGui.Checkbox("禁用区域锁", ref C.DisableInnCheck);
            ImGuiComponents.HelpMarker("移除了 Hyperborea 只能在旅馆房间使用的限制, 若在公共区域中使用可能会让数据包过滤出现故障，会造成风险");
            if (P.Enabled)
            {
                ImGui.EndDisabled();
                ImGuiEx.TextWrapped(EColor.RedBright, "在插件启用时无法修改");
            }
            ImGuiGroup.EndGroupBox();
        }

        
    }
}
