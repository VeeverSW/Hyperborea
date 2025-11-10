using Dalamud.Interface.Components;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods.TerritorySelection;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyperborea.Gui;
public unsafe class EditorWindow : Window
{
    Dictionary<string, HashSet<uint>> BgToTerritoryType = [];
    internal uint SelectedTerritory = 0;
    uint TerrID => SelectedTerritory == 0 ? Svc.ClientState.TerritoryType : SelectedTerritory;
    public EditorWindow() : base("Hyperborea 区域编辑器")
    {
        EzConfigGui.WindowSystem.AddWindow(this);
        foreach(var x in Svc.Data.GetExcelSheet<TerritoryType>())
        {
            var bg = ((TerritoryType?)x).GetBG();
            if (!bg.IsNullOrEmpty())
            {
                if(!BgToTerritoryType.TryGetValue(bg, out var list))
                {
                    list = [];
                    BgToTerritoryType[bg] = list;
                }
                list.Add(x.RowId);
            }
        }
    }

    public override void Draw()
    {
        var cur = ImGui.GetCursorPos();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetCursorPosX(ImGuiEx.GetWindowContentRegionWidth() - ImGui.CalcTextSize("\uf0c7").X);
        if(Utils.TryGetZoneInfo(ExcelTerritoryHelper.GetBG(TerrID), out _, out var isOverriden1))
        {
            if (isOverriden1)
            {
                ImGuiEx.Text(EColor.YellowBright, $"\uf0c7");
                ImGui.PopFont();
                ImGuiEx.Tooltip("区域数据已从你设置的文件中加载");
            }
            else
            {
                ImGuiEx.Text(EColor.GreenBright, $"\uf0c7");
                ImGui.PopFont();
                ImGuiEx.Tooltip("区域数据已从游戏文件中加载");
            }
        }
        else
        {
            ImGuiEx.Text(EColor.RedBright, $"\uf0c7");
            ImGui.PopFont();
            ImGuiEx.Tooltip("未找到适用于此区域的配置");
        }
        ImGui.SetCursorPos(cur);
        var shares = BgToTerritoryType.TryGetValue(ExcelTerritoryHelper.GetBG(TerrID), out var set) ? set : [];
        ImGuiEx.TextWrapped($"正在编辑: {ExcelTerritoryHelper.GetName(TerrID, true)}");
        if(shares.Count > 1)
        {
            ImGuiComponents.HelpMarker($"共享编辑数据的区域: \n{shares.Where(z => z != TerrID).Select(z => ExcelTerritoryHelper.GetName(z, true)).Print("\n")}");
        }
        if (ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf002, "浏览区域列表"))
        {
            new TerritorySelector(SelectedTerritory, (_, x) =>
            {
                SelectedTerritory = x;
            });
        }
        ImGui.SameLine();
        if(ImGuiComponents.IconButtonWithText((FontAwesomeIcon)0xf276, "编辑当前区域"))
        {
            SelectedTerritory = 0;
        }

        var bg = ExcelTerritoryHelper.GetBG(TerrID);
        if (bg.IsNullOrEmpty())
        {
            ImGuiEx.Text($"此区域不受支持");
        }
        else
        {
            if (Utils.TryGetZoneInfo(bg, out var info, out var isOverriden))
            {
                var overrideSpawn = info.Spawn != null;
                if (ImGui.Checkbox("自定义出生点", ref overrideSpawn))
                {
                    info.Spawn = overrideSpawn ? new() : null;
                }
                if (overrideSpawn)
                {
                    UI.CoordBlock("X:", ref info.Spawn.X);
                    ImGui.SameLine();
                    UI.CoordBlock("Y:", ref info.Spawn.Y);
                    ImGui.SameLine();
                    UI.CoordBlock("Z:", ref info.Spawn.Z);
                    ImGui.SameLine();
                    if (ImGuiEx.IconButton("\uf3c5")) info.Spawn = Player.Object.Position.ToPoint3();
                    ImGuiEx.Tooltip("设置为玩家当前所在位置");
                }
                ImGui.Separator();
                ImGuiEx.TextV("阶段:");
                ImGui.SameLine();
                if (ImGuiEx.IconButton(FontAwesome.Plus))
                {
                    info.Phases.Add(new());
                }
                ImGuiEx.Tooltip($"创建一个新阶段");
                foreach (var p in info.Phases)
                {
                    ImGui.PushID(p.GUID);
                    if (ImGui.CollapsingHeader($"{p.Name}###phase"))
                    {
                        ImGuiEx.TextV($"名字:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        ImGui.InputText($"##Name", ref p.Name, 20);
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesome.Trash) && ImGuiEx.Ctrl)
                        {
                            new TickScheduler(() => info.Phases.RemoveAll(z => z.GUID == p.GUID));
                        }
                        ImGuiEx.Tooltip("长按 CTRL 点此处删除该阶段");
                        ImGuiEx.TextV($"天气:");
                        ImGui.SameLine();
                        if (ImGui.BeginCombo("##Weather", $"{Utils.GetWeatherName(p.Weather)}"))
                        {
                            foreach (var x in (uint[])[0, .. P.Weathers[TerrID]])
                            {
                                if (ImGui.Selectable($"{x} - {Utils.GetWeatherName(x)}"))
                                {
                                    if (P.Enabled && Svc.ClientState.TerritoryType == TerrID && Utils.GetPhase(Svc.ClientState.TerritoryType) == p)
                                    {
                                        EnvManager.Instance()->ActiveWeather = (byte)x;
                                        EnvManager.Instance()->TransitionTime = 0.5f;
                                    }
                                    p.Weather = x;
                                }
                            }
                            ImGui.EndCombo();
                        }
                        ImGuiEx.TextV($"地图效果(MapEffects):");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesome.Plus))
                        {
                            p.MapEffects.Add(new());
                        }
                        ImGuiEx.Tooltip("添加地图效果");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesomeIcon.Copy))
                        {
                            Copy(P.YamlFactory.Serialize(p.MapEffects, true));
                        }
                        ImGuiEx.Tooltip("复制此阶段添加的所有地图效果");
                        ImGui.SameLine();
                        if (ImGuiEx.IconButton(FontAwesomeIcon.Paste))
                        {
                            Safe(() => p.MapEffects = P.YamlFactory.Deserialize<List<MapEffectInfo>>(Paste()));
                        }
                        ImGuiEx.Tooltip("粘贴并覆盖此阶段的所有地图效果");
                        foreach (var x in p.MapEffects)
                        {
                            ImGui.PushID(x.GUID);
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a1", ref x.a1);
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a2", ref x.a2);
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(100f);
                            ImGui.InputInt($"##a3", ref x.a3);
                            ImGui.SameLine();
                            if (ImGui.Button("删除"))
                            {
                                new TickScheduler(() => p.MapEffects.RemoveAll(z => z.GUID == x.GUID));
                            }
                            ImGui.PopID();
                        }
                    }
                    ImGui.PopID();
                }
                if (ImGui.Button("保存"))
                {
                    Utils.CreateZoneInfoOverride(bg, info.JSONClone(), true);
                    P.SaveZoneData();
                }
                if(isOverriden)
                {
                    if(ImGui.Button("重置"))
                    {
                        Utils.LoadBuiltInZoneData();
                        new TickScheduler(() =>
                        {
                            P.ZoneData.Data.Remove(bg);
                            P.SaveZoneData();
                        });
                    }
                }
            }
            else
            {
                ImGuiEx.Text($"此区域无数据");
                if (ImGui.Button("创建"))
                {
                    Utils.CreateZoneInfoOverride(bg, new()
                    {
                        Name = ExcelTerritoryHelper.GetName(TerrID),
                    });
                }
            }
        }
    }
}
