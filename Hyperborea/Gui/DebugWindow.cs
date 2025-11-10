using Dalamud.Memory;
using ECommons.Configuration;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Opcodes;
using ECommons.SimpleGui;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using Hyperborea.Services.OpcodeUpdaterService;
using Lumina.Excel.Sheets;
using System.Windows.Forms;
using OpcodeUpdater = Hyperborea.Services.OpcodeUpdaterService.OpcodeUpdater;

namespace Hyperborea.Gui;
public unsafe class DebugWindow: Window
{
    public DebugWindow() : base("Hyperborea Debug 窗口")
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    int i1, i2, i3, i4;
    uint i5, i6, i7;

    public override void Draw()
    {
        ImGuiEx.EzTabBar("Tabs", [
            ("Opcodes", DrawOpcodes, null, true),
            ("Debug", DrawDebug, null, true)
            ]);
    }

    void DrawOpcodes()
    {
        ImGui.Checkbox("禁用自动更新 opcode", ref C.ManualOpcodeManagement);
        ImGuiEx.HelpMarker($"When enabled, Hyperborea will not make any attempts to update opcodes and you will have to edit them manually every game update. You do not have to enable this checkbox in order to edit opcodes one time. ");

        ImGuiEx.TextWrapped($"""
                输入 ZoneDown opcodes.
                寻找方法: 前往旅馆, 输入 /xldata 找到Network Monitor, 什么都不要做等待一会儿. 看Direction那一列，你将会发现有两个ZoneDown 的 opcode，并且每隔一段时间重复一次，在右侧的OpCode那一列复制过来即可
                """);
        EditOpcodes("##zoneDown", ref C.OpcodesZoneDown);
        ImGui.Separator();
        ImGui.Checkbox("禁用 ZoneUp 自动检测", ref C.DisableZoneUpAutoDetect);
        if(C.DisableZoneUpAutoDetect)
        {
            ImGui.Indent();
            ImGuiEx.TextWrapped($"""
                输入 ZoneDown opcode.
                寻找方法: 前往旅馆, 输入 /xldata 找到Network Monitor, 什么都不要做等待一会儿. 看Direction那一列，你将会发现有一个ZoneUp 的 opcode，并且每隔一段时间重复一次，在右侧的OpCode那一列复制过来即可
                """);
            EditOpcodes("##zoneUp", ref C.OpcodesZoneUp);
            ImGui.Unindent();
        }
        if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "应用"))
        {
            OpcodeUpdater.Save();
        }

        ImGui.Unindent();
    }

    void EditOpcodes(string id, ref uint[] opcodes)
    {
        var str = opcodes.Print(",");
        List<uint> newOpcodes = [];
        if(ImGui.InputText(id, ref str))
        {
            foreach(var x in str.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if(uint.TryParse(x, out var result))
                {
                    newOpcodes.Add(result);
                }
            }
            opcodes = newOpcodes.ToArray();
        }
    }

    void DrawZoneEditor()
    {
        var TerrID = Svc.ClientState.TerritoryType;
    }

    public override void OnClose()
    {
        P.SaveZoneData();
    }

    int[] ints = new int[100];
    uint[] d = new uint[100];
    long[] longs = new long[100];
    Dictionary<int, List<ushort>> cachedMapEffects = new();

    void DrawDebug()
    {

        {
            var l = LayoutWorld.Instance()->ActiveLayout;
            if(l != null)
            {
                ImGuiEx.Text($"{l->FestivalStatus:X8}");
                ImGuiEx.Text($"{l->ActiveFestivals[0]}");
                ImGuiEx.Text($"{l->ActiveFestivals[1]}");
                ImGuiEx.Text($"{l->ActiveFestivals[2]}");
                ImGuiEx.Text($"{l->ActiveFestivals[3]}");
                ImGui.InputInt("fest 0", ref ints[0]);
                ImGui.InputInt("fest 1", ref ints[1]);
                ImGui.InputInt("fest 2", ref ints[2]);
                ImGui.InputInt("fest 3", ref ints[3]);
                if (ImGui.Button("Set festivals"))
                {
                    var s = stackalloc uint[] { (uint)ints[0], (uint)ints[1], (uint)ints[2], (uint)ints[3] };
                    l->SetActiveFestivals((FFXIVClientStructs.FFXIV.Client.Game.GameMain.Festival*)s);
                }
            }
        }

        ImGui.Checkbox($"Bypass all restrictions", ref P.Bypass);
        if(ImGui.Button("Fill phases based on supported weather"))
        {
            if (P.Weathers.TryGetValue(Svc.ClientState.TerritoryType, out var weathers))
            {
                if (!(P.ZoneData.Data.TryGetValue(Utils.GetLayout(), out var level)))
                {
                    level = new()
                    {
                        Name = ExcelTerritoryHelper.GetName(Svc.ClientState.TerritoryType)
                    };
                    P.ZoneData.Data[Utils.GetLayout()] = level;
                }
                var i = 0u;
                level.Phases = [];
                foreach (var x in weathers)
                {
                    level.Phases.Add(new() { Weather = x, Name = $"Phase {++i}" });
                }
                P.SaveZoneData();
                Notify.Info($"Success");
            }
            else
            {
                Notify.Error($"Failure");
            }
        }
        if(ImGui.CollapsingHeader("Map effect"))
        {
            ImGuiEx.TextCopy($"Module: {Utils.GetMapEffectModule()}");
            ImGuiEx.TextCopy($"Address: {(((nint)EventFramework.Instance()) + 344):X16}");
            ImGui.InputInt("1", ref i1);
            ImGui.InputInt("2", ref i2);
            ImGui.InputInt("3", ref i3);
            if (ImGui.Button("Do"))
            {
                MapEffect.Delegate(Utils.GetMapEffectModule(), (uint)i1, (ushort)i2, (ushort)i3);
            }
            if (ImGui.Button("Do 1-i1"))
            {
                for (int i = 1; i <= i1; i++)
                {
                    MapEffect.Delegate(Utils.GetMapEffectModule(), (uint)i, (ushort)i2, (ushort)i3);
                }
            }

            ImGui.Separator();
            ImGuiEx.TextV("当前地图 MapEffect:");
            if (ImGui.Button("读取当前地图所有 MapEffect"))
            {
                try
                {
                    cachedMapEffects = P.MapEffectDumper.DumpMapEffects();
                    if (cachedMapEffects.Count > 0)
                    {
                        Notify.Success($"已读取 {cachedMapEffects.Count} 个 MapEffect Slot");
                    }
                    else
                    {
                        Notify.Warning("当前地图没有可用的 MapEffect 或签名扫描失败，请查看日志");
                    }
                }
                catch (Exception ex)
                {
                    Notify.Error($"读取失败: {ex.Message}");
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("复制到剪贴板"))
            {
                if (cachedMapEffects.Count > 0)
                {
                    var lines = cachedMapEffects.Select(kvp =>
                        $"{kvp.Key:X2}: {string.Join(", ", kvp.Value.Select(f => f.ToString("X4")))}");
                    var text = string.Join("\n", lines);
                    ImGui.SetClipboardText(text);
                    Notify.Success("已复制到剪贴板");
                }
                else
                {
                    Notify.Warning("没有可用的 MapEffect 数据，请先点击读取按钮");
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("清空缓存"))
            {
                cachedMapEffects.Clear();
                Notify.Info("已清空 MapEffect 缓存");
            }

            ImGui.BeginChild("MapEffectList", new Vector2(0, 300), true);

            if (cachedMapEffects.Count == 0)
            {
                ImGuiEx.TextWrapped("暂无数据。点击上方按钮读取当前地图的 MapEffect");
            }
            else
            {
                foreach (var kvp in cachedMapEffects.OrderBy(x => x.Key))
                {
                    var index = kvp.Key;
                    var flags = kvp.Value;
                    var flagsDisplay = string.Join(", ", flags.Select(f => f.ToString("X4")));

                    if (ImGui.TreeNode($"{index:X2}: {flagsDisplay}##mapeffect{index}"))
                    {
                        ImGui.Indent();

                        ImGuiEx.Text($"共 {flags.Count} 个可用的 Flag");

                        foreach (var flag in flags)
                        {
                            if (ImGui.Selectable($"Flag: {flag:X4}##mapeffectflag{index}_{flag}"))
                            {
                                i1 = index;
                                i2 = flag;
                                i3 = 0;
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text($"点击填入参数到上方输入框");
                                ImGui.Text($"Index: {index} (0x{index:X2})");
                                ImGui.Text($"Flag: {flag} (0x{flag:X4})");
                                ImGui.EndTooltip();
                            }
                        }

                        ImGui.Unindent();
                        ImGui.TreePop();
                    }
                }
            }

            ImGui.EndChild();
        }

        if (ImGui.CollapsingHeader("Weather"))
        {
            ImGui.InputInt("weather", ref i4);
            if(ImGui.Button("Set weather"))
            {
                var e = EnvManager.Instance();
                e->ActiveWeather = (byte)i4;
                e->TransitionTime = 0.5f;
            }
            var s = (int)*P.Memory.ActiveScene;
            if(ImGui.InputInt("scene", ref s))
            {
                *P.Memory.ActiveScene = (byte)s;
            }
        }

        if (ImGui.CollapsingHeader("monitor hook"))
        {
            if (ImGui.Button("Enable hook")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Enable();
            if (ImGui.Button("Pause hook")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Pause();
            if (ImGui.Button("Disable hook")) P.Memory.PacketDispatcher_OnReceivePacketMonitorHook.Disable();
        }

        if (ImGui.CollapsingHeader("Story"))
        {/*
            foreach (var x in Svc.Data.GetExcelSheet<Story>())
            {
                ImGuiEx.Text($"{x.RowId} {ExcelTerritoryHelper.GetName(x.LayerSetTerritoryType0?.Value?.RowId ?? 0, true)}:");
                for (int i = 0; i < x.LayerSet0.Length; i++)
                {
                    ImGuiEx.Text($"  LayerSet0: {i} = {x.LayerSet0[i]}");
                }
            }*/
        }

        if (ImGui.CollapsingHeader("Story1"))
        {/*
            foreach (var x in Svc.Data.GetExcelSheet<Story>())
            {
                ImGuiEx.Text($"{x.RowId} {ExcelTerritoryHelper.GetName(x.LayerSetTerritoryType1?.Value?.RowId ?? 0, true)}:");
                for (int i = 0; i < x.LayerSet1.Length; i++)
                {
                    ImGuiEx.Text($"  LayerSet1: {i} = {x.LayerSet1[i]}");
                }
            }*/
        }

    }
}
