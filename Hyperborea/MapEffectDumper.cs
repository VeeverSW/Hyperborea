using Dalamud.Memory;
using ECommons.DalamudServices;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hyperborea;

public unsafe class MapEffectDumper
{
    // 使用了阿洛的 EnvironmentEffectDebugModule 参考
    private nint GetMapEffectSlotHandlePtr;
    
    // FFXIVClientStructs/FFXIV/Client/Game/InstanceContent/ContentDirector.cs
    private const int MapEffectListOffset = 0xCE8;

    public MapEffectDumper()
    {
        try
        {
            // 下层 MapEffect 函数中的 v5 = sub_14070D2E0(a1, *(unsigned int *)(v4 + 12LL * a2), 0LL);
            if (!Svc.SigScanner.TryScanText("E9 ?? ?? ?? ?? 8B 43 ?? 45 33 C0 8B D0", out GetMapEffectSlotHandlePtr))
            {
                PluginLog.Warning("[MapEffectDumper] GetMapEffectSlotHandle signature not found");
                // 尝试备用签名
                if (!Svc.SigScanner.TryScanText("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 8B F2 48 8B F9 0F B6 D1", out GetMapEffectSlotHandlePtr))
                {
                    PluginLog.Warning("[MapEffectDumper] GetMapEffectSlotHandle alternative signature also not found");
                }
                else
                {
                    PluginLog.Information($"[MapEffectDumper] GetMapEffectSlotHandle found at {GetMapEffectSlotHandlePtr:X16} (alternative sig)");
                }
            }
            else
            {
                PluginLog.Information($"[MapEffectDumper] GetMapEffectSlotHandle found at {GetMapEffectSlotHandlePtr:X16}");
            }

            if (GetMapEffectSlotHandlePtr != nint.Zero)
            {
                PluginLog.Information($"[MapEffectDumper] Initialized successfully (using direct memory read)");
            }
            else
            {
                PluginLog.Warning($"[MapEffectDumper] Initialization incomplete - GetMapEffectSlotHandle: {GetMapEffectSlotHandlePtr:X16}");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"[MapEffectDumper] Initialization failed: {ex.Message}");
        }
    }

    private nint GetMapEffectSlotHandle(uint slotLayoutId)
    {
        if (GetMapEffectSlotHandlePtr == nint.Zero) return nint.Zero;

        try
        {
            // 调用游戏函数: GetMapEffectSlotHandle(6, slotLayoutId, 0)
            var getHandle = EzDelegate.Get<GetMapEffectSlotHandleDelegate>(GetMapEffectSlotHandlePtr);
            return getHandle(6, (int)slotLayoutId, 0);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"[MapEffectDumper] GetMapEffectSlotHandle failed: {ex.Message}");
            return nint.Zero;
        }
    }


    public Dictionary<int, List<ushort>> DumpMapEffects()
    {
        var results = new Dictionary<int, List<ushort>>();

        try
        {
            var contentDirectorPtr = (nint)EventFramework.Instance() + 0x158;
            contentDirectorPtr = MemoryHelper.Read<nint>(contentDirectorPtr);

            if (contentDirectorPtr == nint.Zero)
            {
                PluginLog.Debug("[MapEffectDumper] 当前地图不存在 ContentDirector");
                return results;
            }

            var mapEffectListPtr = MemoryHelper.Read<nint>(contentDirectorPtr + MapEffectListOffset);
            if (mapEffectListPtr == nint.Zero)
            {
                PluginLog.Debug("[MapEffectDumper] MapEffectList 指针为空");
                return results;
            }
            
            ushort count = MemoryHelper.Read<ushort>(mapEffectListPtr + 0x602);
            int max = Math.Min((int)count, 128);

            PluginLog.Debug($"[MapEffectDumper] 开始读取，共 {count} 个 Slot");

            for (int slotIdx = 0; slotIdx < max; slotIdx++)
            {
                var slotResults = new List<ushort>();
                var slotPtr = mapEffectListPtr + slotIdx * 12;
                var slotLayoutId = MemoryHelper.Read<uint>(slotPtr + 0x0);

                if (slotLayoutId == 0)
                {
                    continue;
                }
                
                if (GetMapEffectSlotHandlePtr != nint.Zero)
                {
                    var slotHandle = GetMapEffectSlotHandle(slotLayoutId);
                    if (slotHandle != nint.Zero)
                    {
                        var ptr1 = MemoryHelper.Read<ulong>(slotHandle + 0x90);
                        var ptr2 = MemoryHelper.Read<ulong>(slotHandle + 0x98);

                        if (ptr1 != ptr2)
                        {
                            for (int bit = 0; bit < 16; bit++)
                            {
                                var flagByte = MemoryHelper.Read<byte>(slotHandle + bit + 0x14C);
                                if (flagByte != 0)
                                {
                                    slotResults.Add((ushort)(1 << bit));
                                }
                            }
                        }
                    }
                }

                if (slotResults.Any())
                {
                    results[slotIdx] = slotResults;
                    PluginLog.Debug($"[MapEffectDumper] {slotIdx:X2}: {string.Join(", ", slotResults.Select(i => $"{i:X4}"))}");
                }
            }

            PluginLog.Information($"[MapEffectDumper] 读取完成，共找到 {results.Count} 个有效 Slot");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"[MapEffectDumper] DumpMapEffects 失败: {ex}");
        }

        return results;
    }

    // 委托定义
    private delegate nint GetMapEffectSlotHandleDelegate(byte a1, int slotLayoutId, int a3);
}
