using AmongUs.Data;
using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using TOHE.Modules;
using TOHE.Modules.ChatManager;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using TOHE.Roles.Core;
using static TOHE.Translator;
using TOHE.Patches;
using TOHE.Roles.Core.AssignManager;


namespace TOHE;

public static class Utils
{
    private static readonly DateTime timeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long TimeStamp => (long)(DateTime.Now.ToUniversalTime() - timeStampStartTime).TotalSeconds;
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - timeStampStartTime).TotalSeconds;
    
    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"Error: {text} - triggering critical error", "Anti-black");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            
            _ = new LateTask(() =>
            {
                Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame"));
            }, 3f, "Anti-Black Msg SendInGame Error During Loading");
            
            _ = new LateTask(() =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                RPC.ForceEndGame(CustomWinner.Error);
            }, 5.5f, "Anti-Black End Game As Critical Error");
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout, SendOption.Reliable);
            writer.Write(text);
            writer.EndMessage();
            if (Options.EndWhenPlayerBug.GetBool())
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd"));
                }, 3f, "Anti-Black Msg SendInGame Non-Host Modded Has Error During Loading");
            }
            else
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd"));
                }, 3f, "Anti-Black Msg SendInGame Host Reject Force End");
                
                _ = new LateTask(() =>
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                    Logger.Fatal($"Error: {text} - Disconnected from the game due critical error", "Anti-black");
                }, 8f, "Anti-Black Exit Game Due Critical Error");
            }
        }
    }
    public static ClientData GetClientById(int id)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Id == id);
            return client;
        }
        catch
        {
            return null;
        }
    }

    public static bool AnySabotageIsActive()
        => IsActive(SystemTypes.Electrical)
           || IsActive(SystemTypes.Comms)
           || IsActive(SystemTypes.MushroomMixupSabotage)
           || IsActive(SystemTypes.Laboratory)
           || IsActive(SystemTypes.LifeSupp)
           || IsActive(SystemTypes.Reactor)
           || IsActive(SystemTypes.HeliSabotage);

    public static bool IsActive(SystemTypes type)
    {
        if (GameStates.IsHideNSeek) return false;

        // if ShipStatus not have current SystemTypes, return false
        if (!ShipStatus.Instance.Systems.ContainsKey(type))
        {
            return false;
        }

        int mapId = GetActiveMapId();
        /*
            The Skeld    = 0
            MIRA HQ      = 1
            Polus        = 2
            Dleks        = 3
            The Airship  = 4
            The Fungle   = 5
        */

        //Logger.Info($"{type}", "SystemTypes");

        switch (type)
        {
            case SystemTypes.Electrical:
                {
                    if (mapId == 5) return false; // if The Fungle return false
                    var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                    return SwitchSystem != null && SwitchSystem.IsActive;
                }
            case SystemTypes.Reactor:
                {
                    if (mapId == 2) return false; // if Polus return false
                    else
                    {
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType != null && ReactorSystemType.IsActive;
                    }
                }
            case SystemTypes.Laboratory:
                {
                    if (mapId != 2) return false; // Only Polus
                    var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                    return ReactorSystemType != null && ReactorSystemType.IsActive;
                }
            case SystemTypes.LifeSupp:
                {
                    if (mapId is 2 or 4 or 5) return false; // Only Skeld & Dleks & Mira HQ
                    var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                    return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                }
            case SystemTypes.HeliSabotage:
                {
                    if (mapId != 4) return false; // Only Airhip
                    var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                    return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                }
            case SystemTypes.Comms:
                {
                    if (mapId is 1 or 5) // Only Mira HQ & The Fungle
                    {
                        var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                        return HqHudSystemType != null && HqHudSystemType.IsActive;
                    }
                    else
                    {
                        var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                        return HudOverrideSystemType != null && HudOverrideSystemType.IsActive;
                    }
                }
            case SystemTypes.MushroomMixupSabotage:
                {
                    if (mapId != 5) return false; // Only The Fungle
                    var MushroomMixupSabotageSystem = ShipStatus.Instance.Systems[type].TryCast<MushroomMixupSabotageSystem>();
                    return MushroomMixupSabotageSystem != null && MushroomMixupSabotageSystem.IsActive;
                }
            default:
                return false;
        }
    }
    public static SystemTypes GetCriticalSabotageSystemType() => GetActiveMapName() switch
    {
        MapNames.Polus => SystemTypes.Laboratory,
        MapNames.Airship => SystemTypes.HeliSabotage,
        _ => SystemTypes.Reactor,
    };

    public static MapNames GetActiveMapName() => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
    public static byte GetActiveMapId() => GameOptionsManager.Instance.CurrentGameOptions.MapId;

    public static void SetVision(this IGameOptions opt, bool HasImpVision)
    {
        if (HasImpVision)
        {
            opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);
            }
            return;
        }
        else
        {
            opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
            }
            return;
        }
    }
    //誰かが死亡したときのメソッド
    public static void SetVisionV2(this IGameOptions opt)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod));
        if (IsActive(SystemTypes.Electrical))
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
        }
        return;
    }

    public static void TargetDies(PlayerControl killer, PlayerControl target)
    {
        if (!target.Data.IsDead || GameStates.IsMeeting) return;

        foreach (var seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(killer, target, seer))
            {
                seer.KillFlash();
                continue;
            }
        }

        if (target.Is(CustomRoles.Cyber))
        {
            Cyber.AfterCyberDeadTask(target, false);
        }
    }
    public static bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer)
    {
        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;

        // Global Kill Flash
        if (target.GetRoleClass().GlobalKillFlashCheck(killer, target, seer)) return true;

        // if seer is alive
        if (seer.IsAlive())
        {
            // Kill Flash as killer
            if (seer.GetRoleClass().KillFlashCheck(killer, target, seer)) return true;
        }
        return false;
    }
    public static void KillFlash(this PlayerControl player)
    {
        // Kill flash (blackout flash + reactor flash)
        bool ReactorCheck = IsActive(GetCriticalSabotageSystemType());

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; // Prolong blackout during reactor for vanilla

        //Start
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; //Set black out for player
        if (player.AmOwner)
        {
            FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
        }
        else if (player.IsModClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.GetClientId());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else if (!ReactorCheck) player.ReactorFlash(0f); //Reactor flash for vanilla
        player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; //Remove black out for player
            player.MarkDirtySettings();
        }, Options.KillFlashDuration.GetFloat(), "Remove Kill Flash");
    }
    public static void BlackOut(this IGameOptions opt, bool IsBlackOut)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        if (IsBlackOut)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
        }
        return;
    }
    public static string GetRoleTitle(this CustomRoles role)
    {
        string ColorName = ColorString(GetRoleColor(role), GetString($"{role}"));
        
        string chance = GetRoleMode(role);
        if (role.IsAdditionRole() && !role.IsEnable()) chance = ColorString(Color.red, "(OFF)");
        
        return $"{ColorName} {chance}";
    }
    public static string GetInfoLong(this CustomRoles role) 
    {
        var InfoLong = GetString($"{role}" + "InfoLong");
        var CustomName = GetString($"{role}");
        var ColorName = ColorString(GetRoleColor(role).ShadeColor(0.25f), CustomName);
        
        Translator.GetActualRoleName(role, out var RealRole);

        return InfoLong.Replace(RealRole, $"{ColorName}");
    }
    public static string GetDisplayRoleAndSubName(byte seerId, byte targetId, bool notShowAddOns = false)
    {
        var TextData = GetRoleAndSubText(seerId, targetId, notShowAddOns);
        return ColorString(TextData.Item2, TextData.Item1);
    }
    public static string GetRoleName(CustomRoles role, bool forUser = true)
    {
        return GetRoleString(Enum.GetName(typeof(CustomRoles), role), forUser);
    }
    public static string GetRoleMode(CustomRoles role, bool parentheses = true)
    {
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Length > 1)
            return string.Empty;

        string mode = GetChance(role.GetMode());
        if (role is CustomRoles.Lovers) mode = GetChance(Options.LoverSpawnChances.GetInt());
        else if (role.IsAdditionRole() && Options.CustomAdtRoleSpawnRate.ContainsKey(role))
        {
            mode = GetChance(Options.CustomAdtRoleSpawnRate[role].GetFloat());
            
        }
        
        return parentheses ? $"({mode})" : mode;
    }
    public static string GetChance(float percent)
    {
        return percent switch 
        {
            0 => "<color=#444444>0%</color>",
            5 => "<color=#EE5015>5%</color>",
            10 => "<color=#EC6817>10%</color>",
            15 => "<color=#EC7B17>15%</color>",
            20 => "<color=#EC8E17>20%</color>",
            25 => "<color=#EC9817>25%</color>",
            30 => "<color=#ECAF17>30%</color>",
            35 => "<color=#ECC217>35%</color>",
            40 => "<color=#ECD217>40%</color>",
            45 => "<color=#ECE217>45%</color>",
            50 => "<color=#DFEC17>50%</color>",
            55 => "<color=#DCEC17>55%</color>",
            60 => "<color=#C9EC17>60%</color>",
            65 => "<color=#BFEC17>65%</color>",
            70 => "<color=#ABEC17>70%</color>",
            75 => "<color=#92EC17>75%</color>",
            80 => "<color=#92EC17>80%</color>",
            85 => "<color=#7BEC17>85%</color>",
            90 => "<color=#6EEC17>90%</color>",
            95 => "<color=#5EEC17>95%</color>",
            100 => "<color=#51EC17>100%</color>",
            _ => $"<color=#4287f5>{percent}%</color>"
        };
    }
    public static string GetDeathReason(PlayerState.DeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(PlayerState.DeathReason), status));
    }
    public static Color GetRoleColor(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }
    public static Color GetTeamColor(PlayerControl player)
    {
        string hexColor = string.Empty;
        var Team = player.GetCustomRole().GetCustomRoleTeam();

        switch (Team)
        {
            case Custom_Team.Crewmate:
                hexColor = "#8cffff";
                break;
            case Custom_Team.Impostor:
                hexColor = "#ff1919";
                break;
            case Custom_Team.Neutral:
                hexColor = "#7f8c8d";
                break;
        }

        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }
    public static string GetRoleColorCode(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = "#ffffff";
        return hexColor;
    }
    public static (string, Color) GetRoleAndSubText(byte seerId, byte targetId, bool notShowAddOns = false)
    {
        string RoleText = "Invalid Role";
        Color RoleColor = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        var targetMainRole = Main.PlayerStates[targetId].MainRole;
        var targetSubRoles = Main.PlayerStates[targetId].SubRoles;
        
        // If a player is possessed by the Dollmaster swap each other's role and add-ons for display for every other client other than Dollmaster and target.
        if (DollMaster.IsControllingPlayer)
        {
            if (!(DollMaster.DollMasterTarget == null || DollMaster.controllingTarget == null))
            {
                if (seerId != DollMaster.DollMasterTarget.PlayerId && targetId == DollMaster.DollMasterTarget.PlayerId)
                    targetSubRoles = Main.PlayerStates[DollMaster.controllingTarget.PlayerId].SubRoles;
                else if (seerId != DollMaster.controllingTarget.PlayerId && targetId == DollMaster.controllingTarget.PlayerId)
                    targetSubRoles = Main.PlayerStates[DollMaster.DollMasterTarget.PlayerId].SubRoles;
            }
        }

        RoleText = GetRoleName(targetMainRole);
        RoleColor = GetRoleColor(targetMainRole);

        try
        {
            if (targetSubRoles.Any())
            {
                var seer = GetPlayerById(seerId);
                var target = GetPlayerById(targetId);

                if (seer == null || target == null) return (RoleText, RoleColor);

                // if player last imp
                if (LastImpostor.currentId == targetId)
                    RoleText = GetRoleString("Last-") + RoleText;

                if (Options.NameDisplayAddons.GetBool() && !notShowAddOns)
                {
                    var seerPlatform = seer.GetClient()?.PlatformData.Platform;
                    var addBracketsToAddons = Options.AddBracketsToAddons.GetBool();

                    static bool Checkif(string str) {

                        string[] strings = ["*Prefix", "INVALID"];
                        return strings.Any(str.Contains); 
                    }
                    static string Getname(string str) => !Checkif(GetString($"Prefix.{str}")) ? GetString($"Prefix.{str}") : GetString($"{str}");

                    // if the player is playing on a console platform
                    if (seerPlatform is Platforms.Playstation or Platforms.Xbox or Platforms.Switch)
                    {
                        // By default, censorship is enabled on consoles
                        // Need to set add-ons colors without endings "</color>"


                        // colored role
                        RoleText = ColorStringWithoutEnding(GetRoleColor(targetMainRole), RoleText);

                        // colored add-ons
                        foreach (var subRole in targetSubRoles.Where(subRole => subRole.ShouldBeDisplayed() && seer.ShowSubRoleTarget(target, subRole)).ToArray())
                            RoleText = ColorStringWithoutEnding(GetRoleColor(subRole), addBracketsToAddons ? $"({Getname($"{subRole}")}) " : $"{Getname($"{subRole}")} ") + RoleText;
                    }
                    // default
                    else
                    {
                        foreach (var subRole in targetSubRoles.Where(subRole => subRole.ShouldBeDisplayed() && seer.ShowSubRoleTarget(target, subRole)).ToArray())
                            RoleText = ColorString(GetRoleColor(subRole), addBracketsToAddons ? $"({Getname($"{subRole}")}) " : $"{Getname($"{subRole}")} ") + RoleText;
                    }
                }

                foreach (var subRole in targetSubRoles.ToArray())
                {
                    if (seer.ShowSubRoleTarget(target, subRole))
                        switch (subRole)
                        {
                            case CustomRoles.Madmate:
                            case CustomRoles.Recruit:
                            case CustomRoles.Charmed:
                            case CustomRoles.Soulless:
                            case CustomRoles.Infected:
                            case CustomRoles.Contagious:
                            case CustomRoles.Admired:
                                RoleColor = GetRoleColor(subRole);
                                RoleText = GetRoleString($"{subRole}-") + RoleText;
                                break;

                        }
                }
            }

            return (RoleText, RoleColor);
        }
        catch
        {
            return (RoleText, RoleColor);
        }
    }
    public static string GetKillCountText(byte playerId, bool ffa = false)
    {
        int count = Main.PlayerStates.Count(x => x.Value.GetRealKiller() == playerId);
        if (count < 1 && !ffa) return "";
        return ColorString(new Color32(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), count));
    }
    public static string GetVitalText(byte playerId, bool RealKillerColor = false)
    {
        var state = Main.PlayerStates[playerId];
        string deathReason = state.IsDead ? state.deathReason == PlayerState.DeathReason.etc && state.Disconnected ? GetString("Disconnected") : GetString("DeathReason." + state.deathReason) : GetString("Alive");
        if (RealKillerColor)
        {
            var KillerId = state.GetRealKiller();
            Color color = KillerId != byte.MaxValue ? GetRoleColor(Main.PlayerStates[KillerId].MainRole) : GetRoleColor(CustomRoles.Doctor);
            if (state.deathReason == PlayerState.DeathReason.etc && state.Disconnected) color = new Color(255, 255, 255, 50);
            deathReason = ColorString(color, deathReason);
        }
        return deathReason;
    }


    public static bool HasTasks(NetworkedPlayerInfo playerData, bool ForRecompute = true)
    {
        if (GameStates.IsLobby) return false;

        //Tasks may be null, in which case no task is assumed
        if (playerData == null) return false;
        if (playerData.Tasks == null) return false;
        if (playerData.Role == null) return false;

        var hasTasks = true;
        if (!Main.PlayerStates.TryGetValue(playerData.PlayerId, out var States))
        {
            return false;
        }

        if (playerData.Disconnected) return false;
        if (playerData.Role.IsImpostor)
            hasTasks = false; //Tasks are determined based on CustomRole

        if (Options.CurrentGameMode == CustomGameMode.FFA) return false;
        if (playerData.IsDead && Options.GhostIgnoreTasks.GetBool()) hasTasks = false;

        if (GameStates.IsHideNSeek) return hasTasks;

        var role = States.MainRole;

        if (States.RoleClass != null && States.RoleClass.HasTasks(playerData, role, ForRecompute) == false)
            hasTasks = false;

        switch (role)
        {
            case CustomRoles.GM:
                hasTasks = false;
                break;
            default:
                // player based on an impostor not should have tasks
                if (States.RoleClass.ThisRoleBase is CustomRoles.Impostor or CustomRoles.Shapeshifter)
                    hasTasks = false;
                break;
        }

        foreach (var subRole in States.SubRoles.ToArray())
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Recruit:
                case CustomRoles.Egoist:
                case CustomRoles.Infected:
                case CustomRoles.EvilSpirit:
                case CustomRoles.Contagious:
                case CustomRoles.Soulless:
                case CustomRoles.Rascal:
                    //Lovers don't count the task as a win
                    hasTasks &= !ForRecompute;
                    break;
                case CustomRoles.Mundane:
                    if (!hasTasks) hasTasks = !ForRecompute;
                    break;

            }

        if (CopyCat.NoHaveTask(playerData.PlayerId)) hasTasks = false;
        if (Main.TasklessCrewmate.Contains(playerData.PlayerId)) hasTasks = false;

        return hasTasks;
    }

    public static string GetProgressText(PlayerControl pc)
    {
        try
        {
            if (!Main.playerVersion.ContainsKey(AmongUsClient.Instance.HostId)) return string.Empty;
            var taskState = pc.GetPlayerTaskState();
            var Comms = false;
            if (taskState.hasTasks)
            {
                if (IsActive(SystemTypes.Comms)) Comms = true;
                if (Camouflager.AbilityActivated) Comms = true;
            }
            return GetProgressText(pc.PlayerId, Comms);
        }
        catch (Exception error)
        {
            ThrowException(error);
            Logger.Error($"PlayerId: {pc.PlayerId}, Role: {Main.PlayerStates[pc.PlayerId].MainRole}", "GetProgressText(PlayerControl pc)");
            return "Error1";
        }
    }
    public static string GetProgressText(byte playerId, bool comms = false)
    {
        try
        {
            if (!Main.playerVersion.ContainsKey(AmongUsClient.Instance.HostId)) return string.Empty;
            var ProgressText = new StringBuilder();
            var role = Main.PlayerStates[playerId].MainRole;
            
            if (Options.CurrentGameMode == CustomGameMode.FFA && role == CustomRoles.Killer)
            {
                ProgressText.Append(FFAManager.GetDisplayScore(playerId));
            }
            else
            {
                ProgressText.Append(playerId.GetRoleClassById()?.GetProgressText(playerId, comms));

                if (ProgressText.Length == 0)
                {
                    var taskState = Main.PlayerStates?[playerId].TaskState;
                    if (taskState.hasTasks)
                    {
                        Color TextColor;
                        var info = GetPlayerInfoById(playerId);
                        var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(role).ShadeColor(0.5f);
                        var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white;

                        if (Workhorse.IsThisRole(playerId))
                            NonCompleteColor = Workhorse.RoleColor;

                        var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
                        if (Main.PlayerStates.TryGetValue(playerId, out var ps) && ps.MainRole == CustomRoles.Crewpostor)
                            NormalColor = Color.red;

                        TextColor = comms ? Color.gray : NormalColor;
                        string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";
                        ProgressText.Append(ColorString(TextColor, $" ({Completed}/{taskState.AllTasksCount})"));
                    }
                }
                else
                {
                    ProgressText.Insert(0, " ");
                }
            }
            return ProgressText.ToString();
        }
        catch (Exception error)
        {
            ThrowException(error);
            Logger.Error($"PlayerId: {playerId}, Role: {Main.PlayerStates[playerId].MainRole}", "GetProgressText(byte playerId, bool comms = false)");
            return "Error2";
        }
    }
    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool()) { SendMessage(GetString("DisableDevicesInfo"), PlayerId); }
        if (Options.SyncButtonMode.GetBool()) { SendMessage(GetString("SyncButtonModeInfo"), PlayerId); }
        if (Options.SabotageTimeControl.GetBool()) { SendMessage(GetString("SabotageTimeControlInfo"), PlayerId); }
        if (Options.RandomMapsMode.GetBool()) { SendMessage(GetString("RandomMapsModeInfo"), PlayerId); }
        if (Main.EnableGM.Value) { SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId); }
        
        foreach (var role in CustomRolesHelper.AllRoles)
        {
            if (role.IsEnable() && !role.IsVanilla()) SendMessage(GetRoleName(role) + GetRoleMode(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
        }

        if (Options.NoGameEnd.GetBool()) { SendMessage(GetString("NoGameEndInfo"), PlayerId); }
    }
    public static void ShowActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        var sb = new StringBuilder();
        sb.Append(" ★ " + GetString("TabGroup.SystemSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.SystemSettings && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append("\n\n ★ " + GetString("TabGroup.ModSettings"));
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Tab is TabGroup.ModSettings && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            sb.Append($"\n{opt.GetName(true)}: {opt.GetString()}");
            //ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        SendMessage(sb.ToString(), PlayerId);
    }
    
    public static void ShowAllActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }
        var sb = new StringBuilder();

        sb.Append(GetString("Settings")).Append(':');
        foreach (var role in Options.CustomRoleCounts.Keys.ToArray())
        {
            if (!role.IsEnable()) continue;
            string mode = GetChance(role.GetMode());
            sb.Append($"\n【{GetRoleName(role)}:{mode} ×{role.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id > 59999 && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            if (opt.Name is "KillFlashDuration" or "RoleAssigningAlgorithm")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }

        SendMessage(sb.ToString(), PlayerId);
    }
    public static void CopyCurrentSettings()
    {
        var sb = new StringBuilder();
        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
        {
            ClipboardHelper.PutClipboardString(GetString("Message.HideGameSettings"));
            return;
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Roles")}】━━━━━━━━━━━━");
        foreach (var role in Options.CustomRoleCounts.Keys.ToArray())
        {
            if (!role.IsEnable()) continue;
            string mode = GetChance(role.GetMode());
            sb.Append($"\n【{GetRoleName(role)}:{mode} ×{role.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id > 59999 && !x.IsHiddenOn(Options.CurrentGameMode)).ToArray())
        {
            if (opt.Name == "KillFlashDuration")
                sb.Append($"\n【{opt.GetName(true)}: {opt.GetString()}】\n");
            else
                sb.Append($"\n【{opt.GetName(true)}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        ClipboardHelper.PutClipboardString(sb.ToString());
    }
    public static void ShowActiveRoles(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }

        List<string> impsb = [];
        List<string> neutralsb = [];
        List<string> crewsb = [];
        List<string> addonsb = [];

        foreach (var role in CustomRolesHelper.AllRoles)
        {
            string mode = GetChance(role.GetMode());
            if (role.IsEnable())
            {
                if (role is CustomRoles.Lovers) mode = GetChance(Options.LoverSpawnChances.GetInt());
                else if (role.IsAdditionRole() && Options.CustomAdtRoleSpawnRate.ContainsKey(role))
                {
                    mode = GetChance(Options.CustomAdtRoleSpawnRate[role].GetFloat());

                }
                var roleDisplay = $"{GetRoleName(role)}: {mode} x{role.GetCount()}";
                if (role.IsAdditionRole()) addonsb.Add(roleDisplay);
                else if (role.IsCrewmate()) crewsb.Add(roleDisplay);
                else if (role.IsImpostor() || role.IsMadmate()) impsb.Add(roleDisplay);
                else if (role.IsNeutral()) neutralsb.Add(roleDisplay);
            }
        }

        impsb.Sort();
        crewsb.Sort();
        neutralsb.Sort();
        addonsb.Sort();
        
        SendMessage(string.Join("\n", impsb), PlayerId, ColorString(GetRoleColor(CustomRoles.Impostor), GetString("ImpostorRoles")), ShouldSplit: true);
        SendMessage(string.Join("\n", crewsb), PlayerId, ColorString(GetRoleColor(CustomRoles.Crewmate), GetString("CrewmateRoles")), ShouldSplit: true);
        SendMessage(string.Join("\n", neutralsb), PlayerId, GetString("NeutralRoles"), ShouldSplit: true);
        SendMessage(string.Join("\n", addonsb), PlayerId, GetString("AddonRoles"), ShouldSplit: true);
    }
    public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool command = false)
    {
        if (Options.HideGameSettings.GetBool()) return;

        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }).ToArray())
        {
            if (command)
            {
                sb.Append("\n\n");
                command = false;
            }

            if (opt.Value.Name == "Maximum") continue; //Maximumの項目は飛ばす
            if (opt.Value.Name == "DisableSkeldDevices" && !GameStates.SkeldIsActive && !GameStates.DleksIsActive) continue;
            if (opt.Value.Name == "DisableMiraHQDevices" && !GameStates.MiraHQIsActive) continue;
            if (opt.Value.Name == "DisablePolusDevices" && !GameStates.PolusIsActive) continue;
            if (opt.Value.Name == "DisableAirshipDevices" && !GameStates.AirshipIsActive) continue;
            if (opt.Value.Name == "PolusReactorTimeLimit" && !GameStates.PolusIsActive) continue;
            if (opt.Value.Name == "AirshipReactorTimeLimit" && !GameStates.AirshipIsActive) continue;
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }
            sb.Append($"{opt.Value.GetName(true)}: {opt.Value.GetString()}\n");
            if (opt.Value.GetBool()) ShowChildrenSettings(opt.Value, ref sb, deep + 1);
        }
    }
    public static void ShowLastRoles(byte PlayerId = byte.MaxValue)
    {
        if (AmongUsClient.Instance.IsGameStarted)
        {
            SendMessage(GetString("CantUse.lastroles"), PlayerId);
            return;
        }

        var sb = new StringBuilder();

        sb.Append($"<#ffffff>{GetString("RoleSummaryText")}</color>");

        List<byte> cloneRoles = new(Main.PlayerStates.Keys);
        foreach (byte id in Main.winnerList.ToArray())
        {
            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;
            sb.Append($"\n<#c4aa02>★</color> ").Append(EndGamePatch.SummaryText[id]/*.RemoveHtmlTags()*/);
            cloneRoles.Remove(id);
        }
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA:
                List<(int, byte)> listFFA = [];
                foreach (byte id in cloneRoles.ToArray())
                {
                    listFFA.Add((FFAManager.GetRankOfScore(id), id));
                }
                listFFA.Sort();
                foreach ((int, byte) id in listFFA.ToArray())
                {
                    sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id.Item2]);
                }
                break;
            default: // Normal game
                foreach (byte id in cloneRoles.ToArray())
                {
                    if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>"))
                        continue;
                    sb.Append($"\n　 ").Append(EndGamePatch.SummaryText[id]);
                    
                }
                break;
        }
        string lr = sb.ToString();
        try{
            if (lr.Length > 2024 && (!GetPlayerById(PlayerId).IsModClient()))
            {
                lr = lr.Replace("<color=", "<");
                lr.SplitMessage().Do(x => SendMessage("\n", PlayerId, $"<size=75%>" + x + "</size>")); //Since it will always capture a newline, there's more than enough space to put this in
            }
            else
            {
                SendMessage("\n", PlayerId,  "<size=75%>" + lr + "</size>");
            }
        }
        catch (Exception err)
        {
            Logger.Warn($"Error after try split the msg {lr} at: {err}", "Utils.ShowLastRoles..LastRoles");
        }
    }
    public static void ShowKillLog(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.killlog"), PlayerId);
            return;
        }
        if (EndGamePatch.KillLog != "") 
        {
            string kl = EndGamePatch.KillLog;
            kl = Options.OldKillLog.GetBool() ? kl.RemoveHtmlTags() : kl.Replace("<color=", "<");
            var tytul = !Options.OldKillLog.GetBool() ? ColorString(new Color32(102, 16, 16, 255),  "《 " + GetString("KillLog") + " 》") : "";
            SendSpesificMessage(kl, PlayerId, tytul);
        }
    }
    public static void ShowLastResult(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }
        var sb = new StringBuilder();
        if (SetEverythingUpPatch.LastWinsText != "") sb.Append($"{GetString("LastResult")}: {SetEverythingUpPatch.LastWinsText}");
        if (SetEverythingUpPatch.LastWinsReason != "") sb.Append($"\n{GetString("LastEndReason")}: {SetEverythingUpPatch.LastWinsReason}");
        if (sb.Length > 0 && Options.CurrentGameMode != CustomGameMode.FFA) SendMessage(sb.ToString(), PlayerId);
    }
    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = Main.PlayerStates[id].SubRoles;
        if (SubRoles.Count == 0 && intro == false) return "";
        var sb = new StringBuilder();

        if (summary)
            sb.Append(' ');

        foreach (var role in SubRoles.ToArray())
        {
            if (role is CustomRoles.NotAssigned or
                        CustomRoles.LastImpostor) continue;
            if (summary && role is CustomRoles.Madmate or CustomRoles.Charmed or CustomRoles.Recruit or CustomRoles.Admired or CustomRoles.Infected or CustomRoles.Contagious or CustomRoles.Soulless) continue;

            var RoleColor = GetRoleColor(role);
            var RoleText = disableColor ? GetRoleName(role) : ColorString(RoleColor, GetRoleName(role));
            
            if (summary)
                sb.Append($"{ColorString(RoleColor, "(")}{RoleText}{ColorString(RoleColor, ")")}");
            else
                sb.Append($"{ColorString(Color.white, " + ")}{RoleText}");
        }

        return sb.ToString();
    }

    public static string GetRegionName(IRegionInfo region = null)
    {
        region ??= ServerManager.Instance.CurrentRegion;

        string name = region.Name;

        if (AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
        {
            name = "Local Games";
            return name;
        }

        if (region.PingServer.EndsWith("among.us", StringComparison.Ordinal))
        {
            // Official server
            if (name == "North America") name = "NA";
            else if (name == "Europe") name = "EU";
            else if (name == "Asia") name = "AS";

            return name;
        }

        var Ip = region.Servers.FirstOrDefault()?.Ip ?? string.Empty;

        if (Ip.Contains("aumods.us", StringComparison.Ordinal)
            || Ip.Contains("duikbo.at", StringComparison.Ordinal))
        {
            // Official Modded Server
            if (Ip.Contains("au-eu")) name = "MEU";
            else if (Ip.Contains("au-as")) name = "MAS";
            else if (Ip.Contains("www.")) name = "MNA";

            return name;
        }

        if (name.Contains("nikocat233", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Replace("nikocat233", "Niko233", StringComparison.OrdinalIgnoreCase);
        }

        return name;
    }
    // From EHR by Gurge44
    public static void ThrowException(Exception ex, [CallerFilePath] string fileName = "", [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string callerMemberName = "")
    {
        try
        {
            StackTrace st = new(1, true);
            StackFrame[] stFrames = st.GetFrames();

            StackFrame firstFrame = stFrames.FirstOrDefault();

            var sb = new StringBuilder();
            sb.Append($" Exception: {ex.Message}\n      thrown by {ex.Source}\n      at {ex.TargetSite}\n      in {fileName} at line {lineNumber} in {callerMemberName}\n------ Stack Trace ------");

            bool skip = true;
            foreach (StackFrame sf in stFrames)
            {
                if (skip)
                {
                    skip = false;
                    continue;
                }

                var callerMethod = sf.GetMethod();

                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;

                sb.Append($"\n      at {callerClassName}.{callerMethodName}");
            }

            sb.Append("\n------ End of Stack Trace ------");

            Logger.Error(sb.ToString(), firstFrame?.GetMethod()?.ToString(), multiLine: true);
        }
        catch
        {
        }
    }
    public static byte MsgToColor(string text, bool isHost = false)
    {
        text = text.ToLowerInvariant();
        text = text.Replace("色", string.Empty);
        int color;
        try { color = int.Parse(text); } catch { color = -1; }
        switch (text)
        {
            case "0":
            case "红":
            case "紅":
            case "red":
            case "Red":
            case "vermelho":
            case "Vermelho":
            case "крас":
            case "Крас":
            case "красн":
            case "Красн":
            case "красный":
            case "Красный":
                color = 0; break;
            case "1":
            case "蓝":
            case "藍":
            case "深蓝":
            case "blue":
            case "Blue":
            case "azul":
            case "Azul":
            case "син":
            case "Син":
            case "синий":
            case "Синий":
                color = 1; break;
            case "2":
            case "绿":
            case "綠":
            case "深绿":
            case "green":
            case "Green":
            case "verde-escuro":
            case "Verde-Escuro":
            case "Зел":
            case "зел":
            case "Зелёный":
            case "Зеленый":
            case "зелёный":
            case "зеленый":
                color = 2; break;
            case "3":
            case "粉红":
            case "pink":
            case "Pink":
            case "rosa":
            case "Rosa":
            case "Роз":
            case "роз":
            case "Розовый":
            case "розовый":
                color = 3; break;
            case "4":
            case "橘":
            case "orange":
            case "Orange":
            case "laranja":
            case "Laranja":
            case "оранж":
            case "Оранж":
            case "оранжевый":
            case "Оранжевый":
                color = 4; break;
            case "5":
            case "黄":
            case "黃":
            case "yellow":
            case "Yellow":
            case "amarelo":
            case "Amarelo":
            case "Жёлт":
            case "Желт":
            case "жёлт":
            case "желт":
            case "Жёлтый":
            case "Желтый":
            case "жёлтый":
            case "желтый":
                color = 5; break;
            case "6":
            case "黑":
            case "black":
            case "Black":
            case "preto":
            case "Preto":
            case "Чёрн":
            case "Черн":
            case "Чёрный":
            case "Черный":
            case "чёрный":
            case "черный":
                color = 6; break;
            case "7":
            case "白":
            case "white":
            case "White":
            case "branco":
            case "Branco":
            case "Белый":
            case "белый":
                color = 7; break;
            case "8":
            case "紫":
            case "purple":
            case "Purple":
            case "roxo":
            case "Roxo":
            case "Фиол":
            case "фиол":
            case "Фиолетовый":
            case "фиолетовый":
                color = 8; break;
            case "9":
            case "棕":
            case "brown":
            case "Brown":
            case "marrom":
            case "Marrom":
            case "Корич":
            case "корич":
            case "Коричневый":
            case "коричевый":
                color = 9; break;
            case "10":
            case "青":
            case "cyan":
            case "Cyan":
            case "ciano":
            case "Ciano":
            case "Голуб":
            case "голуб":
            case "Голубой":
            case "голубой":
                color = 10; break;
            case "11":
            case "黄绿":
            case "黃綠":
            case "浅绿":
            case "lime":
            case "Lime":
            case "verde-claro":
            case "Verde-Claro":
            case "Лайм":
            case "лайм":
            case "Лаймовый":
            case "лаймовый":
                color = 11; break;
            case "12":
            case "红褐":
            case "紅褐":
            case "深红":
            case "maroon":
            case "Maroon":
            case "bordô":
            case "Bordô":
            case "vinho":
            case "Vinho":
            case "Борд":
            case "борд":
            case "Бордовый":
            case "бордовый":
                color = 12; break;
            case "13":
            case "玫红":
            case "玫紅":
            case "浅粉":
            case "rose":
            case "Rose":
            case "rosa-claro":
            case "Rosa-Claro":
            case "Светло роз":
            case "светло роз":
            case "Светло розовый":
            case "светло розовый":
            case "Сирень":
            case "сирень":
            case "Сиреневый":
            case "сиреневый":
                color = 13; break;
            case "14":
            case "焦黄":
            case "焦黃":
            case "淡黄":
            case "banana":
            case "Banana":
            case "Банан":
            case "банан":
            case "Банановый":
            case "банановый":
                color = 14; break;
            case "15":
            case "灰":
            case "gray":
            case "Gray":
            case "cinza":
            case "Cinza":
            case "grey":
            case "Grey":
            case "Сер":
            case "сер":
            case "Серый":
            case "серый":
                color = 15; break;
            case "16":
            case "茶":
            case "tan":
            case "Tan":
            case "bege":
            case "Bege":
            case "Загар":
            case "загар":
            case "Загаровый":
            case "загаровый":
                color = 16; break;
            case "17":
            case "珊瑚":
            case "coral":
            case "Coral":
            case "salmão":
            case "Salmão":
            case "Корал":
            case "корал":
            case "Коралл":
            case "коралл":
            case "Коралловый":
            case "коралловый":
                color = 17; break;
                
            case "18": case "隐藏": case "?": color = 18; break;
        }
        return !isHost && color == 18 ? byte.MaxValue : color is < 0 or > 18 ? byte.MaxValue : Convert.ToByte(color);
    }

    public static void ShowHelpToClient(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            + $"\n ○ /death {GetString("Command.death")}"
            + $"\n ○ /icons {GetString("Command.iconinfo")}"
            , ID);
    }
    public static void ShowHelp(byte ID)
    {
        SendMessage(
            GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            + $"\n  ○ /icons {GetString("Command.iconinfo")}"
            + $"\n  ○ /death {GetString("Command.death")}"
            + "\n\n" + GetString("CommandHostList")
            + $"\n  ○ /s {GetString("Command.say")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /poll {GetString("Command.Poll")}"
            + $"\n  ○ /xf {GetString("Command.solvecover")}"
            + $"\n  ○ /mw {GetString("Command.mw")}"
            + $"\n  ○ /kill {GetString("Command.kill")}"
            + $"\n  ○ /exe {GetString("Command.exe")}"
            + $"\n  ○ /level {GetString("Command.level")}"
            + $"\n  ○ /id {GetString("Command.idlist")}"
            + $"\n  ○ /qq {GetString("Command.qq")}"
            + $"\n  ○ /dump {GetString("Command.dump")}"
        //    + $"\n  ○ /iconhelp {GetString("Command.iconhelp")}"
            , ID);
    }
    public static string[] SplitMessage(this string LongMsg)
    {
        List<string> result = [];
        var lines = LongMsg.Split('\n');
        var shortenedtext = string.Empty;

        foreach (var line in lines)
        {

            if (shortenedtext.Length + line.Length < 1200)
            {
                shortenedtext += line + "\n";
                continue;
            }

            if (shortenedtext.Length >= 1200) result.AddRange(shortenedtext.Chunk(1200).Select(x => new string(x)));
            else result.Add(shortenedtext);
            shortenedtext = line + "\n";

        }

        if (shortenedtext.Length > 0) result.Add(shortenedtext);

        return [.. result];
    }
    private static string TryRemove(this string text) => text.Length >= 1200 ? text.Remove(0, 1200) : string.Empty;
    
    
    public static void SendSpesificMessage(string text, byte sendTo = byte.MaxValue, string title = "") 
    {
        // Always splits it, this is incase you want to very heavily modify msg and use the splitmsg functionality.
        bool isfirst = true;
        if (text.Length > 1200 && !(Utils.GetPlayerById(sendTo).IsModClient()))
        {
            foreach(var txt in text.SplitMessage())
            {
                var titleW = isfirst ? title : "<alpha=#00>.";
                var m = Regex.Replace(txt, "^<voffset=[-]?\\d+em>", ""); // replaces the first instance of voffset, if any.
                m += $"<voffset=-1.3em><alpha=#00>.</voffset>"; // fix text clipping OOB
                if (m.IndexOf("\n") <= 4) m = m[(m.IndexOf("\n")+1)..m.Length];
                SendMessage(m, sendTo, titleW);
                isfirst = false;
            }
        }
        else 
        {
            text += $"<voffset=-1.3em><alpha=#00>.</voffset>";
            if (text.IndexOf("\n") <= 4) text = text[(text.IndexOf("\n") + 1)..text.Length];
            SendMessage(text, sendTo, title);
        }


    }
    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool logforChatManager = false, bool noReplay = false, bool ShouldSplit = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        try
        {
            if (ShouldSplit && text.Length > 1200 && (!GetPlayerById(sendTo).IsModClient()))
            {
                text.SplitMessage().Do(x => SendMessage(x, sendTo, title));
                return;
            }
            //else if (text.Length > 1200 && (!GetPlayerById(sendTo).IsModClient()))
            //{
            //    text = text.RemoveHtmlTagsIfNeccessary();
            //}
        }
        catch (Exception exx)
        {
            Logger.Warn($"Error after try split the msg {text} at: {exx}", "Utils.SendMessage.SplitMessage");
        }

        // set noReplay to false when you want to send previous sys msg or do not want to add a sys msg in the history
        if (!noReplay && GameStates.IsInGame) ChatManager.AddSystemChatHistory(sendTo, text);

        if (title == "") title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";

        if (!logforChatManager)
            ChatManager.AddToHostMessage(text.RemoveHtmlTagsTemplate());

        Main.MessagesToSend.Add((text.RemoveHtmlTagsTemplate(), sendTo, title));
    }
    public static bool IsPlayerModerator(string friendCode)
    {
        if (friendCode == "") return false;
        var friendCodesFilePath = @"./TOHE-DATA/Moderators.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode));
    }
    public static bool IsPlayerVIP(string friendCode)
    {
        if (friendCode == "") return false;
        var friendCodesFilePath = @"./TOHE-DATA/VIP-List.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode));
    }
    public static bool CheckColorHex(string ColorCode)
    {
        Regex regex = new("^[0-9A-Fa-f]{6}$");
        if (!regex.IsMatch(ColorCode)) return false;
        return true;
    }
    public static bool CheckGradientCode(string ColorCode)
    {
        Regex regex = new(@"^[0-9A-Fa-f]{6}\s[0-9A-Fa-f]{6}$");
        if (!regex.IsMatch(ColorCode)) return false;
        return true;
    }
    public static string GradientColorText(string startColorHex, string endColorHex, string text)
    {
        if (startColorHex.Length != 6 || endColorHex.Length != 6)
        {
            Logger.Error("Invalid color hex code. Hex code should be 6 characters long (without #) (e.g., FFFFFF).", "GradientColorText");
            //throw new ArgumentException("Invalid color hex code. Hex code should be 6 characters long (e.g., FFFFFF).");
            return text;
        }

        Color startColor = HexToColor(startColorHex);
        Color endColor = HexToColor(endColorHex);

        int textLength = text.Length;
        float stepR = (endColor.r - startColor.r) / textLength;
        float stepG = (endColor.g - startColor.g) / textLength;
        float stepB = (endColor.b - startColor.b) / textLength;
        float stepA = (endColor.a - startColor.a) / textLength;

        string gradientText = "";

        for (int i = 0; i < textLength; i++)
        {
            float r = startColor.r + (stepR * i);
            float g = startColor.g + (stepG * i);
            float b = startColor.b + (stepB * i);
            float a = startColor.a + (stepA * i);


            string colorHex = ColorToHex(new Color(r, g, b, a));
            //Logger.Msg(colorHex, "color");
            gradientText += $"<color=#{colorHex}>{text[i]}</color>";
        }

        return gradientText;
    }

    private static Color HexToColor(string hex)
    {
        _ = ColorUtility.TryParseHtmlString("#" + hex, out var color);
        return color;
    }

    private static string ColorToHex(Color color)
    {
        Color32 color32 = (Color32)color;
        return $"{color32.r:X2}{color32.g:X2}{color32.b:X2}{color32.a:X2}";
    }
    public static void ApplySuffix(PlayerControl player)
    {
        // Only host
        if (!AmongUsClient.Instance.AmHost || player == null) return;
        // Check invalid color
        if (player.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= player.Data.DefaultOutfit.ColorId) return;

        // Hide all tags
        if (Options.HideAllTagsAndText.GetBool())
        {
            SetRealName();
            return;
        }

        if (!(player.AmOwner || player.FriendCode.GetDevUser().HasTag()))
        {
            if (!IsPlayerModerator(player.FriendCode) && !IsPlayerVIP(player.FriendCode))
            {
                SetRealName();
                return;
            }
        }

        void SetRealName()
        {
            string realName = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var namePlayer) ? namePlayer : "";
            if (GameStates.IsLobby && realName != player.name && player.CurrentOutfitType == PlayerOutfitType.Default)
                player.RpcSetName(realName);
        }

        string name = Main.AllPlayerNames.TryGetValue(player.PlayerId, out var n) ? n : "";
        if (Main.HostRealName != "" && player.AmOwner) name = Main.HostRealName;
        if (name == "") return;
        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (Options.FormatNameMode.GetInt() == 1 && Main.HostRealName == "") name = Palette.GetColorName(player.Data.DefaultOutfit.ColorId);
        }
        else
        {
            if (!GameStates.IsLobby) return;
            if (player.AmOwner)
            {
                if (!player.IsModClient()) return;
                {
                    if (GameStates.IsOnlineGame || GameStates.IsLocalGame)
                    {
                        name = Options.HideHostText.GetBool() ? $"<color={GetString("NameColor")}>{name}</color>"
                                                              : $"<color={GetString("HostColor")}>{GetString("HostText")}</color><color={GetString("IconColor")}>{GetString("Icon")}</color><color={GetString("NameColor")}>{name}</color>";
                    }


                    //name = $"<color=#902efd>{GetString("HostText")}</color><color=#4bf4ff>♥</color>" + name;
                }
                if (Options.CurrentGameMode == CustomGameMode.FFA)
                    name = $"<color=#00ffff><size=1.7>{GetString("ModeFFA")}</size></color>\r\n" + name;
            }
            var modtag = "";
            if (Options.ApplyVipList.GetValue() == 1 && player.FriendCode != PlayerControl.LocalPlayer.FriendCode)
            {
                if (IsPlayerVIP(player.FriendCode))
                {
                    string colorFilePath = @$"./TOHE-DATA/Tags/VIP_TAGS/{player.FriendCode}.txt";
                    //static color
                    if (!Options.GradientTagsOpt.GetBool())
                    { 
                        string startColorCode = "ffff00";
                        if (File.Exists(colorFilePath))
                        {
                            string ColorCode = File.ReadAllText(colorFilePath);
                            _ = ColorCode.Trim();
                            if (CheckColorHex(ColorCode)) startColorCode = ColorCode;
                        }
                        //"ffff00"
                        modtag = $"<color=#{startColorCode}>{GetString("VipTag")}</color>";
                        }
                    else //gradient color
                    {
                        string startColorCode = "ffff00";
                        string endColorCode = "ffff00";
                        string ColorCode = "";
                        if (File.Exists(colorFilePath))
                        {
                            ColorCode = File.ReadAllText(colorFilePath);
                            if (ColorCode.Split(" ").Length == 2)
                            {
                                startColorCode = ColorCode.Split(" ")[0];
                                endColorCode = ColorCode.Split(" ")[1];
                            }
                        }
                        if (!CheckGradientCode(ColorCode))
                        {
                            startColorCode = "ffff00";
                            endColorCode = "ffff00";
                        }
                        //"33ccff", "ff99cc"
                        if (startColorCode == endColorCode) modtag = $"<color=#{startColorCode}>{GetString("VipTag")}</color>";

                        else modtag = GradientColorText(startColorCode, endColorCode, GetString("VipTag"));
                    }
                }
            }
            if (Options.ApplyModeratorList.GetValue() == 1 && player.FriendCode != PlayerControl.LocalPlayer.FriendCode)
            {
                if (IsPlayerModerator(player.FriendCode))
                {
                    string colorFilePath = @$"./TOHE-DATA/Tags/MOD_TAGS/{player.FriendCode}.txt";
                    //static color
                    if (!Options.GradientTagsOpt.GetBool())
                    { 
                        string startColorCode = "8bbee0";
                        if (File.Exists(colorFilePath))
                        {
                            string ColorCode = File.ReadAllText(colorFilePath);
                            _ = ColorCode.Trim();
                            if (CheckColorHex(ColorCode)) startColorCode = ColorCode;
                        }
                        //"33ccff", "ff99cc"
                        modtag = $"<color=#{startColorCode}>{GetString("ModTag")}</color>";
                    }
                    else //gradient color
                    {
                        string startColorCode = "8bbee0";
                        string endColorCode = "8bbee0";
                        string ColorCode = "";
                        if (File.Exists(colorFilePath))
                        {
                            ColorCode = File.ReadAllText(colorFilePath);
                            if (ColorCode.Split(" ").Length == 2)
                            {
                                startColorCode = ColorCode.Split(" ")[0];
                                endColorCode = ColorCode.Split(" ")[1];
                            }
                        }
                        if (!CheckGradientCode(ColorCode))
                        {
                            startColorCode = "8bbee0";
                            endColorCode = "8bbee0";
                        }
                        //"33ccff", "ff99cc"
                        if (startColorCode == endColorCode) modtag = $"<color=#{startColorCode}>{GetString("ModTag")}</color>";

                        else modtag = GradientColorText(startColorCode, endColorCode, GetString("ModTag"));
                    }
                }
            }
            if (player.AmOwner)
            {
                name = Options.GetSuffixMode() switch
                {
                    SuffixModes.TOHE => name += $"\r\n<color={Main.ModColor}>TOHE v{Main.PluginDisplayVersion}</color>",
                    SuffixModes.Streaming => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Streaming")}</color></size>",
                    SuffixModes.Recording => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.Recording")}</color></size>",
                    SuffixModes.RoomHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixMode.RoomHost")}</color></size>",
                    SuffixModes.OriginalName => name += $"\r\n<size=1.7><color={Main.ModColor}>{DataManager.player.Customization.Name}</color></size>",
                    SuffixModes.DoNotKillMe => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.DoNotKillMe")}</color></size>",
                    SuffixModes.NoAndroidPlz => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.NoAndroidPlz")}</color></size>",
                    SuffixModes.AutoHost => name += $"\r\n<size=1.7><color={Main.ModColor}>{GetString("SuffixModeText.AutoHost")}</color></size>",
                    _ => name
                };
            }

            if (!name.Contains($"\r\r") && player.FriendCode.GetDevUser().HasTag() && (player.AmOwner || player.IsModClient()))
            {
                name = player.FriendCode.GetDevUser().GetTag() + "<size=1.5>" + modtag + "</size>" + name;
            }
            else name = modtag + name;
        }
        if (name != player.name && player.CurrentOutfitType == PlayerOutfitType.Default)
            player.RpcSetName(name);
    }
    public static bool CheckCamoflague(this PlayerControl PC) => Camouflage.IsCamouflage || Camouflager.AbilityActivated || Utils.IsActive(SystemTypes.MushroomMixupSabotage) 
        || (Main.CheckShapeshift.TryGetValue(PC.PlayerId, out bool isShapeshifitng) && isShapeshifitng);
    public static PlayerControl GetPlayerById(int PlayerId)
    {
        return Main.AllPlayerControls.FirstOrDefault(pc => pc.PlayerId == PlayerId);
    }
    public static List<PlayerControl> GetPlayerListByIds(this IEnumerable<byte> PlayerIdList)
    {
        var PlayerList = PlayerIdList?.ToList().Select(x => GetPlayerById(x)).ToList();

        return PlayerList != null && PlayerList.Any() ? PlayerList : null;
    }
    public static List<PlayerControl> GetPlayerListByRole(this CustomRoles role)
        => GetPlayerListByIds(Main.PlayerStates.Values.Where(x => x.MainRole == role).Select(r => r.PlayerId));
    
    public static IEnumerable<t> GetRoleBasesByType <t>() where t : RoleBase
    {
        try
        {
            var cache = Main.PlayerStates.Values.Where(x => x.RoleClass != null);

            if (cache.Any())
            {
                var Get = cache.Select(x => x.RoleClass);
                return Get.OfType<t>().Any() ? Get.OfType<t>() : null;
            }
        }
        catch (Exception exx)
        {
            Logger.Exception(exx, "Utils.GetRoleBasesByType");
        }
        return null;
    }

    public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == PlayerId);
    private static readonly StringBuilder SelfSuffix = new();
    private static readonly StringBuilder SelfMark = new(20);
    private static readonly StringBuilder TargetSuffix = new();
    private static readonly StringBuilder TargetMark = new(20);
    public static async void NotifyRoles(PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool isForMeeting = false, bool NoCache = false, bool ForceLoop = true, bool CamouflageIsForMeeting = false, bool MushroomMixupIsActive = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Main.AllPlayerControls == null) return;
        if (GameStates.IsHideNSeek) return;

        //Do not update NotifyRoles during meetings
        if (GameStates.IsMeeting && !GameEndCheckerForNormal.ShowAllRolesWhenGameEnd) return;

        //var caller = new System.Diagnostics.StackFrame(1, false);
        //var callerMethod = caller.GetMethod();
        //string callerMethodName = callerMethod.Name;
        //string callerClassName = callerMethod.DeclaringType.FullName;
        //Logger.Info($" Was called from: {callerClassName}.{callerMethodName}", "NotifyRoles");

        await DoNotifyRoles(SpecifySeer, SpecifyTarget, isForMeeting, NoCache, ForceLoop, CamouflageIsForMeeting, MushroomMixupIsActive);
    }
    public static Task DoNotifyRoles(PlayerControl SpecifySeer = null, PlayerControl SpecifyTarget = null, bool isForMeeting = false, bool NoCache = false, bool ForceLoop = true, bool CamouflageIsForMeeting = false, bool MushroomMixupIsActive = false)
    {
        if (!AmongUsClient.Instance.AmHost) return Task.CompletedTask;
        if (Main.AllPlayerControls == null) return Task.CompletedTask;
        if (GameStates.IsHideNSeek) return Task.CompletedTask;

        //Do not update NotifyRoles during meetings
        if (GameStates.IsMeeting && !GameEndCheckerForNormal.ShowAllRolesWhenGameEnd) return Task.CompletedTask;

        //var logger = Logger.Handler("DoNotifyRoles");

        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        PlayerControl[] seerList = SpecifySeer != null 
            ? ([SpecifySeer]) 
            : Main.AllPlayerControls;

        PlayerControl[] targetList = SpecifyTarget != null
            ? ([SpecifyTarget])
            : Main.AllPlayerControls;

        if (!MushroomMixupIsActive)
        {
            MushroomMixupIsActive = IsActive(SystemTypes.MushroomMixupSabotage);
        }

        Logger.Info($" START - Count Seers: {seerList.Length} & Count Target: {targetList.Length}", "DoNotifyRoles");

        //seer: player who updates the nickname/role/mark
        //target: seer updates nickname/role/mark of other targets
        foreach (var seer in seerList)
        {
            // Do nothing when the seer is not present in the game
            if (seer == null || seer.Data.Disconnected) continue;
            
            // Only non-modded players
            if (seer.IsModClient()) continue;

            // During intro scene to set team name and role info for non-modded clients and skip the rest.
            // Note: When Neutral is based on the Crewmate role then it is impossible to display the info for it
            // If not a Desync Role remove team display
            if (SetUpRoleTextPatch.IsInIntro)
            {
                //Get role info font size based on the length of the role info
                static int GetInfoSize(string RoleInfo)
                {
                    RoleInfo = Regex.Replace(RoleInfo, "<[^>]*>", "");
                    RoleInfo = Regex.Replace(RoleInfo, "{[^}]*}", "");

                    var BaseFontSize = 200;
                    int BaseFontSizeMin = 100;

                    BaseFontSize -= 3 * RoleInfo.Length;
                    if (BaseFontSize < BaseFontSizeMin)
                        BaseFontSize = BaseFontSizeMin;
                    return BaseFontSize;
                }

                string IconText = "<color=#ffffff>|</color>";
                string Font = "<font=\"VCR SDF\" material=\"VCR Black Outline\">";
                string SelfTeamName = $"<size=450%>{IconText} {Font}{ColorString(GetTeamColor(seer), $"{seer.GetCustomRole().GetCustomRoleTeam()}")}</font> {IconText}</size><size=900%>\n \n</size>\r\n";
                string SelfRoleName = $"<size=185%>{Font}{ColorString(seer.GetRoleColor(), GetRoleName(seer.GetCustomRole()))}</font></size>";
                string SelfSubRolesName = string.Empty;
                string SeerRealName = seer.GetRealName();
                string SelfName = ColorString(seer.GetRoleColor(), SeerRealName);
                string RoleInfo = $"<size=25%>\n</size><size={GetInfoSize(seer.GetRoleInfo())}%>{Font}{ColorString(seer.GetRoleColor(), seer.GetRoleInfo())}</font></size>";
                string RoleNameUp = "<size=1350%>\n\n</size>";

                if (!seer.GetCustomRole().IsDesyncRole())
                {
                    SelfTeamName = string.Empty;
                    RoleNameUp = "<size=565%>\n</size>";
                    RoleInfo = $"<size=50%>\n</size><size={GetInfoSize(seer.GetRoleInfo())}%>{Font}{ColorString(seer.GetRoleColor(), seer.GetRoleInfo())}</font></size>";
                }

                // Format addons
                bool isFirstSub = true;
                foreach (var subRole in seer.GetCustomSubRoles().ToArray())
                {
                    if (isFirstSub)
                    {
                        SelfSubRolesName += $"<size=150%>\n</size=><size=125%>{Font}{ColorString(GetRoleColor(subRole), GetString($"{subRole}"))}</font></size>";
                        RoleNameUp += "\n";
                    }
                    else
                        SelfSubRolesName += $"<size=125%> {Font}{ColorString(Color.white, "+")} {ColorString(GetRoleColor(subRole), GetString($"{subRole}"))}</font></size=>";
                    isFirstSub = false;
                }

                SelfName = $"{SelfTeamName}{SelfRoleName}{SelfSubRolesName}\r\n{RoleInfo}{RoleNameUp}";

                // Privately sent name.
                seer.RpcSetNamePrivate(SelfName, seer);
                continue;
            }
            
            // Size of player roles
            string fontSize = isForMeeting ? "1.6" : "1.8";
            if (isForMeeting && (seer.GetClient().PlatformData.Platform is Platforms.Playstation or Platforms.Xbox or Platforms.Switch)) fontSize = "70%";

            //logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":START");

            var seerRole = seer.GetCustomRole();
            var seerRoleClass = seer.GetRoleClass();

            // Hide player names in during Mushroom Mixup if seer is alive and desync impostor
            if (!CamouflageIsForMeeting && MushroomMixupIsActive && seer.IsAlive() && !seer.Is(Custom_Team.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
            {
                seer.RpcSetNamePrivate("<size=0%>", force: NoCache);
            }
            else
            {
                // Clear marker after name seer
                SelfMark.Clear();

                // ====== Add SelfMark for seer ======
                SelfMark.Append(seerRoleClass?.GetMark(seer, seer, isForMeeting: isForMeeting));
                SelfMark.Append(CustomRoleManager.GetMarkOthers(seer, seer, isForMeeting: isForMeeting));

                if (seer.Is(CustomRoles.Lovers))
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♥"));

                if (seer.Is(CustomRoles.Cyber) && Cyber.CyberKnown.GetBool())
                    SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Cyber), "★"));


                // ====== Add SelfSuffix for seer ======

                SelfSuffix.Clear();

                SelfSuffix.Append(seerRoleClass?.GetLowerText(seer, seer, isForMeeting: isForMeeting));
                SelfSuffix.Append(CustomRoleManager.GetLowerTextOthers(seer, seer, isForMeeting: isForMeeting));

                if (Radar.IsEnable)
                    SelfSuffix.Append(Radar.GetPlayerArrow(seer, isForMeeting: isForMeeting));

                SelfSuffix.Append(seerRoleClass?.GetSuffix(seer, seer, isForMeeting: isForMeeting));
                SelfSuffix.Append(CustomRoleManager.GetSuffixOthers(seer, seer, isForMeeting: isForMeeting));


                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        SelfSuffix.Append(FFAManager.GetPlayerArrow(seer));
                        break;
                }


                // ====== Get SeerRealName ======

                string SeerRealName = seer.GetRealName(isForMeeting);

                // ====== Combine SelfRoleName, SelfTaskText, SelfName, SelfDeathReason for seer ======
                string SelfTaskText = GetProgressText(seer);

                string SelfRoleName = $"<size={fontSize}>{seer.GetDisplayRoleAndSubName(seer, false)}{SelfTaskText}</size>";
                string SelfDeathReason = seer.KnowDeathReason(seer) ? $" ({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})" : string.Empty;
                string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                bool IsDisplayInfo = false;
                if (MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool() && !isForMeeting && Options.CurrentGameMode != CustomGameMode.FFA)
                {
                    IsDisplayInfo = true;
                    var SeerRoleInfo = seer.GetRoleInfo();
                    string RoleText = string.Empty;
                    string Font = "<font=\"VCR SDF\" material=\"VCR Black Outline\">";

                    if (seerRole.IsImpostor()) { RoleText = ColorString(GetTeamColor(seer), GetString("TeamImpostor")); }
                    else if (seerRole.IsCrewmate()) { RoleText = ColorString(GetTeamColor(seer), GetString("TeamCrewmate")); }
                    else if (seerRole.IsNeutral()) { RoleText = ColorString(GetTeamColor(seer), GetString("TeamNeutral")); }
                    else if (seerRole.IsMadmate()) { RoleText = ColorString(GetTeamColor(seer), GetString("TeamMadmate")); }

                    SelfName = $"{SelfName}<size=600%>\n \n</size><size=150%>{Font}{ColorString(seer.GetRoleColor(), RoleText)}</size>\n<size=75%>{ColorString(seer.GetRoleColor(), seer.GetRoleInfo())}</size></font>\n";
                }

                if (NameNotifyManager.GetNameNotify(seer, out var name))
                {
                    SelfName = name;
                }

                if (Pelican.HasEnabled && Pelican.IsEaten(seer.PlayerId))
                    SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";

                if (CustomRoles.Deathpact.HasEnabled() && Deathpact.IsInActiveDeathpact(seer))
                    SelfName = Deathpact.GetDeathpactString(seer);

                // Devourer
                if (CustomRoles.Devourer.HasEnabled())
                {
                    bool playerDevoured = Devourer.HideNameOfTheDevoured(seer.PlayerId);
                    if (playerDevoured && !CamouflageIsForMeeting)
                        SelfName = GetString("DevouredName");
                }

                // Dollmaster, Prevent seeing self in mushroom cloud
                if (CustomRoles.DollMaster.HasEnabled() && seerRole != CustomRoles.DollMaster)
                {
                    if (DollMaster.IsDoll(seer.PlayerId))
                        SelfName = "<size=10000%><color=#000000>■</color></size>";
                }

                // Camouflage
                if (!CamouflageIsForMeeting && Camouflage.IsCamouflage)
                    SelfName = $"<size=0%>{SelfName}</size>";

                if (!Regex.IsMatch(SelfName, seer.GetRealName()))
                    IsDisplayInfo = false;

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        FFAManager.GetNameNotify(seer, ref SelfName);
                        SelfName = $"<size={fontSize}>{SelfTaskText}</size>\r\n{SelfName}";
                        break;
                    default:
                        if (!IsDisplayInfo)
                            SelfName = SelfRoleName + "\r\n" + SelfName;
                        else
                            SelfName = "<size=425%>\n \n</size>" + SelfRoleName + "\r\n" + SelfName;
                        break;
                }
                SelfName += SelfSuffix.Length == 0 ? string.Empty : "\r\n " + SelfSuffix.ToString();

                if (!isForMeeting) SelfName += "\r\n";

                seer.RpcSetNamePrivate(SelfName, force: NoCache);
            }

            // Start run loop for target only when condition is "true"
            if (ForceLoop && (seer.Data.IsDead || !seer.IsAlive()
                || seerList.Length == 1
                || targetList.Length == 1
                || MushroomMixupIsActive
                || NoCache
                || ForceLoop))
            {
                foreach (var realTarget in targetList)
                {
                    // if the target is the seer itself, do nothing
                    if (realTarget.PlayerId == seer.PlayerId) continue;

                    var target = realTarget;

                    if (seer != target && seer != DollMaster.DollMasterTarget)
                        target = DollMaster.SwapPlayerInfo(realTarget); // If a player is possessed by the Dollmaster swap each other's controllers.

                    //logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":START");

                    // Hide player names in during Mushroom Mixup if seer is alive and desync impostor
                    if (!CamouflageIsForMeeting && MushroomMixupIsActive && target.IsAlive() && !seer.Is(Custom_Team.Impostor) && Main.ResetCamPlayerList.Contains(seer.PlayerId))
                    {
                        realTarget.RpcSetNamePrivate("<size=0%>", seer, force: NoCache);
                    }
                    else
                    {
                        // ====== Add TargetMark for target ======

                        TargetMark.Clear();

                        TargetMark.Append(seerRoleClass?.GetMark(seer, target, isForMeeting));
                        TargetMark.Append(CustomRoleManager.GetMarkOthers(seer, target, isForMeeting));

                        if (seer.Is(Custom_Team.Impostor) && target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished)
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★"));

                        if (target.Is(CustomRoles.Cyber) && Cyber.CyberKnown.GetBool())
                            TargetMark.Append(ColorString(GetRoleColor(CustomRoles.Cyber), "★"));

                        if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                        {
                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }
                        else if (seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                        {
                            TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }

                        // ====== Seer know target role ======

                        bool KnowRoleTarget = ExtendedPlayerControl.KnowRoleTarget(seer, target, true);
                        
                        string TargetRoleText = KnowRoleTarget
                                ? $"<size={fontSize}>{seer.GetDisplayRoleAndSubName(target, false)}{GetProgressText(target)}</size>\r\n" : "";

                        if (seer.IsAlive() && Overseer.IsRevealedPlayer(seer, target) && target.Is(CustomRoles.Trickster))
                        {
                            TargetRoleText = Overseer.GetRandomRole(seer.PlayerId); // Random trickster role
                            TargetRoleText += TaskState.GetTaskState(); // Random task count for revealed trickster
                        }

                        // ====== Target player name ======

                        string TargetPlayerName = target.GetRealName(isForMeeting);

                        var tempNameText = seer.GetRoleClass()?.NotifyPlayerName(seer, target, TargetPlayerName, isForMeeting);
                        if (tempNameText != string.Empty)
                            TargetPlayerName = tempNameText;

                        // ========= Only During Meeting =========
                        if (isForMeeting)
                        {
                            // Guesser Mode is On ID
                            if (Options.GuesserMode.GetBool())
                            {
                                // seer & target is alive
                                if (seer.IsAlive() && target.IsAlive())
                                {
                                    var GetTragetId = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;

                                    //Crewmates
                                    if (Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.Inspector) && !seer.Is(CustomRoles.Lookout) && !seer.Is(CustomRoles.Swapper))
                                        TargetPlayerName = GetTragetId;

                                    else if (seer.Is(CustomRoles.NiceGuesser) && !Options.CrewmatesCanGuess.GetBool())
                                        TargetPlayerName = GetTragetId;



                                    //Impostors
                                    if (Options.ImpostorsCanGuess.GetBool() && (seer.GetCustomRole().IsImpostor() || seer.GetCustomRole().IsMadmate()) && !seer.Is(CustomRoles.Councillor) && !seer.Is(CustomRoles.Nemesis))
                                        TargetPlayerName = GetTragetId;

                                    else if (seer.Is(CustomRoles.EvilGuesser) && !Options.ImpostorsCanGuess.GetBool())
                                        TargetPlayerName = GetTragetId;



                                    // Neutrals
                                    if (Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                                        TargetPlayerName = GetTragetId;

                                    if (Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                                        TargetPlayerName = GetTragetId;
                                }
                            }
                            else // Guesser Mode is Off ID
                            {
                                if (seer.IsAlive() && target.IsAlive())
                                {
                                    if (seer.Is(CustomRoles.NiceGuesser) || seer.Is(CustomRoles.EvilGuesser) ||
                                        (seer.Is(CustomRoles.Guesser) && !seer.Is(CustomRoles.Inspector) && !seer.Is(CustomRoles.Swapper) && !seer.Is(CustomRoles.Lookout)))
                                        TargetPlayerName = ColorString(GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + TargetPlayerName;
                                }
                            }
                        }

                        TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                        // ====== Add TargetSuffix for target (TargetSuffix visible ​​only to the seer) ======
                        TargetSuffix.Clear();

                        TargetSuffix.Append(CustomRoleManager.GetLowerTextOthers(seer, target, isForMeeting: isForMeeting));

                        TargetSuffix.Append(seerRoleClass?.GetSuffix(seer, target, isForMeeting: isForMeeting));
                        TargetSuffix.Append(CustomRoleManager.GetSuffixOthers(seer, target, isForMeeting: isForMeeting));

                        if (TargetSuffix.Length > 0)
                        {
                            TargetSuffix.Insert(0, "\r\n");
                        }

                        // ====== Target Death Reason for target (Death Reason visible ​​only to the seer) ======
                        string TargetDeathReason = seer.KnowDeathReason(target) 
                            ? $" ({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})" : string.Empty;

                        // Devourer
                        if (CustomRoles.Devourer.HasEnabled())
                        {
                            bool targetDevoured = Devourer.HideNameOfTheDevoured(target.PlayerId);
                            if (targetDevoured && !CamouflageIsForMeeting)
                                TargetPlayerName = GetString("DevouredName");
                        }

                        // Camouflage
                        if (!CamouflageIsForMeeting && Camouflage.IsCamouflage)
                            TargetPlayerName = $"<size=0%>{TargetPlayerName}</size>";

                        // Target Name
                        string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}{TargetSuffix}";
                        //TargetName += TargetSuffix.ToString() == "" ? "" : ("\r\n" + TargetSuffix.ToString());

                        realTarget.RpcSetNamePrivate(TargetName, seer, force: NoCache);
                    }
                }
            }
        }
        //Logger.Info($" Loop for Targets: {}", "DoNotifyRoles", force: true);
        Logger.Info($" END", "DoNotifyRoles");
        return Task.CompletedTask;
    }
    public static void MarkEveryoneDirtySettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
    }
    public static void SyncAllSettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
        GameOptionsSender.SendAllGameOptions();
    }
    public static bool DeathReasonIsEnable(this PlayerState.DeathReason reason, bool checkbanned = false)
    {
        static bool BannedReason(PlayerState.DeathReason rso)
        {
            return rso is PlayerState.DeathReason.Overtired 
                or PlayerState.DeathReason.etc
                or PlayerState.DeathReason.Vote 
                or PlayerState.DeathReason.Gambled;
        }

        return checkbanned ? !BannedReason(reason) : reason switch
        {
            PlayerState.DeathReason.Eaten => (CustomRoles.Pelican.IsEnable()),
            PlayerState.DeathReason.Spell => (CustomRoles.Witch.IsEnable()),
            PlayerState.DeathReason.Hex => (CustomRoles.HexMaster.IsEnable()),
            PlayerState.DeathReason.Curse => (CustomRoles.CursedWolf.IsEnable()),
            PlayerState.DeathReason.Jinx => (CustomRoles.Jinx.IsEnable()),
            PlayerState.DeathReason.Shattered => (CustomRoles.Fragile.IsEnable()),
            PlayerState.DeathReason.Bite => (CustomRoles.Vampire.IsEnable()),
            PlayerState.DeathReason.Poison => (CustomRoles.Poisoner.IsEnable()),
            PlayerState.DeathReason.Bombed => (CustomRoles.Bomber.IsEnable() || CustomRoles.Burst.IsEnable()
                                || CustomRoles.Trapster.IsEnable() || CustomRoles.Fireworker.IsEnable() || CustomRoles.Bastion.IsEnable()),
            PlayerState.DeathReason.Misfire => (CustomRoles.ChiefOfPolice.IsEnable() || CustomRoles.Sheriff.IsEnable()
                                || CustomRoles.Reverie.IsEnable() || CustomRoles.Sheriff.IsEnable() || CustomRoles.Fireworker.IsEnable()
                                || CustomRoles.Hater.IsEnable() || CustomRoles.Pursuer.IsEnable() || CustomRoles.Romantic.IsEnable()),
            PlayerState.DeathReason.Torched => (CustomRoles.Arsonist.IsEnable()),
            PlayerState.DeathReason.Sniped => (CustomRoles.Sniper.IsEnable()),
            PlayerState.DeathReason.Revenge => (CustomRoles.Avanger.IsEnable() || CustomRoles.Retributionist.IsEnable()
                                || CustomRoles.Nemesis.IsEnable() || CustomRoles.Randomizer.IsEnable()),
            PlayerState.DeathReason.Quantization => (CustomRoles.Lightning.IsEnable()),
            //PlayerState.DeathReason.Overtired => (CustomRoles.Workaholic.IsEnable()),
            PlayerState.DeathReason.Ashamed => (CustomRoles.Workaholic.IsEnable()),
            PlayerState.DeathReason.PissedOff => (CustomRoles.Pestilence.IsEnable() || CustomRoles.Provocateur.IsEnable()),
            PlayerState.DeathReason.Dismembered => (CustomRoles.Butcher.IsEnable()),
            PlayerState.DeathReason.LossOfHead => (CustomRoles.Hangman.IsEnable()),
            PlayerState.DeathReason.Trialed => (CustomRoles.Judge.IsEnable() || CustomRoles.Councillor.IsEnable()),
            PlayerState.DeathReason.Infected => (CustomRoles.Infectious.IsEnable()),
            PlayerState.DeathReason.Hack => (CustomRoles.Glitch.IsEnable()),
            PlayerState.DeathReason.Pirate => (CustomRoles.Pirate.IsEnable()),
            PlayerState.DeathReason.Shrouded => (CustomRoles.Shroud.IsEnable()),
            PlayerState.DeathReason.Mauled => (CustomRoles.Werewolf.IsEnable()),
            PlayerState.DeathReason.Suicide => (CustomRoles.Unlucky.IsEnable() || CustomRoles.Ghoul.IsEnable()
                                || CustomRoles.Terrorist.IsEnable() || CustomRoles.Dictator.IsEnable()
                                || CustomRoles.Addict.IsEnable() || CustomRoles.Mercenary.IsEnable()
                                || CustomRoles.Mastermind.IsEnable() || CustomRoles.Deathpact.IsEnable()),
            PlayerState.DeathReason.FollowingSuicide => (CustomRoles.Lovers.IsEnable()),
            PlayerState.DeathReason.Execution => (CustomRoles.Jailer.IsEnable()),
            PlayerState.DeathReason.Fall => Options.LadderDeath.GetBool(),
            PlayerState.DeathReason.Sacrifice => (CustomRoles.Bodyguard.IsEnable() || CustomRoles.Revolutionist.IsEnable()
                                || CustomRoles.Hater.IsEnable()),
            PlayerState.DeathReason.Drained => CustomRoles.Puppeteer.IsEnable(),
            PlayerState.DeathReason.Trap => CustomRoles.Trapster.IsEnable(),
            PlayerState.DeathReason.Targeted => CustomRoles.Kamikaze.IsEnable(),
            PlayerState.DeathReason.Retribution => CustomRoles.Instigator.IsEnable(),
            PlayerState.DeathReason.WrongAnswer => CustomRoles.Quizmaster.IsEnable(),
            var Breason when BannedReason(Breason) => false,
            PlayerState.DeathReason.Slice => CustomRoles.Hawk.IsEnable(),
            PlayerState.DeathReason.BloodLet => CustomRoles.Bloodmoon.IsEnable(),
            PlayerState.DeathReason.Kill => true,
            _ => true,
        };
    }
    public static HashSet<Action<bool>> LateExileTask = [];
    public static void AfterMeetingTasks()
    {
        ChatManager.ClearLastSysMsg();

        if (Diseased.IsEnable) Diseased.AfterMeetingTasks();
        if (Antidote.IsEnable) Antidote.AfterMeetingTasks();

        AntiBlackout.AfterMeetingTasks();

        foreach (var playerState in Main.PlayerStates.Values.ToArray())
        {
            playerState.RoleClass?.AfterMeetingTasks();
        }

        //Set kill timer
        foreach (var player in Main.AllAlivePlayerControls)
        {
            player.SetKillTimer();
        }

        if (LateExileTask.Any())
        {
            LateExileTask.Do(t => t.Invoke(true));
            LateExileTask.Clear();
        }


        if (Statue.IsEnable) Statue.AfterMeetingTasks();
        if (Burst.IsEnable) Burst.AfterMeetingTasks();

        if (CustomRoles.CopyCat.HasEnabled()) CopyCat.UnAfterMeetingTasks(); // All crew hast to be before this
        

        if (Options.AirshipVariableElectrical.GetBool())
            AirshipElectricalDoors.Initialize();

        DoorsReset.ResetDoors();

        // Empty Deden bug support Empty vent after meeting
        var ventilationSystem = ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType) ? systemType.TryCast<VentilationSystem>() : null;
        if (ventilationSystem != null)
        {
            ventilationSystem.PlayersInsideVents.Clear();
            ventilationSystem.IsDirty = true;
        }
    }
    public static void ChangeInt(ref int ChangeTo, int input, int max)
    {
        var tmp = ChangeTo * 10;
        tmp += input;
        ChangeTo = Math.Clamp(tmp, 0, max);
    }
    public static void CountAlivePlayers(bool sendLog = false, bool checkGameEnd = true)
    {
        int AliveImpostorCount = Main.AllAlivePlayerControls.Count(pc => pc.Is(Custom_Team.Impostor));
        if (Main.AliveImpostorCount != AliveImpostorCount)
        {
            Logger.Info("Number Impostor left: " + AliveImpostorCount, "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            if (Options.CurrentGameMode != CustomGameMode.FFA)
            { 
                foreach (var countTypes in EnumHelper.GetAllValues<CountTypes>())
                {
                    var playersCount = PlayersCount(countTypes);
                    if (playersCount == 0) continue;
                    sb.Append($"{countTypes}:{AlivePlayersCount(countTypes)}/{playersCount}, ");
                }
            }
            sb.Append($"All:{AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");

            if (AmongUsClient.Instance.AmHost && checkGameEnd)
                GameEndCheckerForNormal.Prefix();
        }
    }
    public static string GetVoteName(byte num)
    {
        string name = "invalid";
        var player = GetPlayerById(num);
        if (num < 15 && player != null) name = player?.GetNameWithRole();
        if (num == 253) name = "Skip";
        if (num == 254) name = "None";
        if (num == 255) name = "Dead";
        return name;
    }
    public static string PadRightV2(this object text, int num)
    {
        int bc = 0;
        var t = text.ToString();
        foreach (char c in t) bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
        return t?.PadRight(Mathf.Max(num - (bc - t.Length), 0));
    }
    public static void DumpLog()
    {
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/TOHE-logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}TOHE-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new(@$"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(@filename);

        if (PlayerControl.LocalPlayer != null)
            HudManager.Instance?.Chat?.AddChat(PlayerControl.LocalPlayer, string.Format(GetString("Message.DumpfileSaved"), $"TOHE - v{Main.PluginVersion}-{t}.log"));

        SendMessage(string.Format(GetString("Message.DumpcmdUsed"), PlayerControl.LocalPlayer.GetNameWithRole()));

        ProcessStartInfo psi = new("Explorer.exe") { Arguments = "/e,/select," + @filename.Replace("/", "\\") };
        Process.Start(psi);
    }
    
    
    public static string SummaryTexts(byte id, bool disableColor = true, bool check = false)
    {
        var name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
        if (id == PlayerControl.LocalPlayer.PlayerId) name = DataManager.player.Customization.Name;
        else name = GetPlayerById(id)?.Data.PlayerName ?? name;

        var taskState = Main.PlayerStates?[id].TaskState;

        Main.PlayerStates.TryGetValue(id, out var playerState);

        string TaskCount;

        if (taskState.hasTasks)
        {
            Color CurrentСolor;
            var TaskCompleteColor = Color.green; // Color after task completion
            var NonCompleteColor = taskState.CompletedTasksCount > 0 ? Color.yellow : Color.white; // Uncountable out of person is white

            if (Workhorse.IsThisRole(id))
                NonCompleteColor = Workhorse.RoleColor;

            CurrentСolor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

            if (playerState.MainRole is CustomRoles.Crewpostor)
                CurrentСolor = Color.red;

            if (playerState.SubRoles.Contains(CustomRoles.Workhorse))
                GetRoleColor(playerState.MainRole).ShadeColor(0.5f);

            TaskCount = ColorString(CurrentСolor, $" ({taskState.CompletedTasksCount}/{taskState.AllTasksCount})");
        }
        else { TaskCount = GetProgressText(id); }

        var disconnectedText = playerState.deathReason != PlayerState.DeathReason.etc && playerState.Disconnected ? $"({GetString("Disconnected")})" : string.Empty;
        string summary = $"{ColorString(Main.PlayerColors[id], name)} - {GetDisplayRoleAndSubName(id, id, true)}{GetSubRolesText(id, summary: true)}{TaskCount} {GetKillCountText(id)} 『{GetVitalText(id, true)}』{disconnectedText}";
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA:
                summary = $"{ColorString(Main.PlayerColors[id], name)} {GetKillCountText(id, ffa: true)}";
                break;
        }
        return check && GetDisplayRoleAndSubName(id, id, true).RemoveHtmlTags().Contains("INVALID:NotAssigned")
            ? "INVALID"
            : disableColor ? summary.RemoveHtmlTags() : summary;
    }
    public static string RemoveHtmlTagsTemplate(this string str) => Regex.Replace(str, "", "");
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", "");
    public static string RemoveHtmlTagsIfNeccessary(this string str) => str.Replace("<color=", "<").Length > 1200 ? str.RemoveHtmlTags() : str.Replace("<color=", "<");

    public static void FlashColor(Color color, float duration = 1f)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud.FullScreen == null) return;
        var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
        if (obj == null)
        {
            obj = UnityEngine.Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
            obj.name = "FlashColor_FullScreen";
        }
        hud.StartCoroutine(Effects.Lerp(duration, new Action<float>((t) =>
        {
            obj.SetActive(t != 1f);
            obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a / 2)); //アルファ値を0→目標→0に変化させる
        })));
    }

    public static Dictionary<string, Sprite> CachedSprites = [];
    public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            if (CachedSprites.TryGetValue(path + pixelsPerUnit, out var sprite)) return sprite;
            Texture2D texture = LoadTextureFromResources(path);
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            return CachedSprites[path + pixelsPerUnit] = sprite;
        }
        catch
        {
            Logger.Error($"Failed to read Texture： {path}", "LoadSprite");
        }
        return null;
    }
    public static Texture2D LoadTextureFromResources(string path)
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            ImageConversion.LoadImage(texture, ms.ToArray(), false);
            return texture;
        }
        catch
        {
            Logger.Error($"Failed to read Texture： {path}", "LoadTextureFromResources");
        }
        return null;
    }
    public static string ColorString(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";
    public static string ColorStringWithoutEnding(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}";
    /// <summary>
    /// Darkness:１の比率で黒色と元の色を混ぜる。マイナスだと白色と混ぜる。
    /// </summary>
    public static Color ShadeColor(this Color color, float Darkness = 0)
    {
        bool IsDarker = Darkness >= 0; //黒と混ぜる
        if (!IsDarker) Darkness = -Darkness;
        float Weight = IsDarker ? 0 : Darkness; //黒/白の比率
        float R = (color.r + Weight) / (Darkness + 1);
        float G = (color.g + Weight) / (Darkness + 1);
        float B = (color.b + Weight) / (Darkness + 1);
        return new Color(R, G, B, color.a);
    }

    public static void SetChatVisibleForEveryone()
    {
        if (!GameStates.IsInGame || !AmongUsClient.Instance.AmHost) return;
        
        MeetingHud.Instance = UnityEngine.Object.Instantiate(HudManager.Instance.MeetingPrefab);
        MeetingHud.Instance.ServerStart(PlayerControl.LocalPlayer.PlayerId);
        AmongUsClient.Instance.Spawn(MeetingHud.Instance, -2, SpawnFlags.None);
        MeetingHud.Instance.RpcClose();
    }

    public static void SetChatVisibleSpecific(this PlayerControl player)
    {
        if (!GameStates.IsInGame || !AmongUsClient.Instance.AmHost || GameStates.IsMeeting) return;

        if (player.AmOwner)
        {
            HudManager.Instance.Chat.SetVisible(true);
            return;
        }

        if (player.IsModClient())
        {
            var modsend = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShowChat, SendOption.Reliable, player.OwnerId);
            modsend.WritePacked(player.OwnerId);
            modsend.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(modsend);
            return;
        }

        var customNetId = AmongUsClient.Instance.NetIdCnt++;
        var vanillasend = MessageWriter.Get(SendOption.Reliable);
        vanillasend.StartMessage(6);
        vanillasend.Write(AmongUsClient.Instance.GameId);
        vanillasend.Write(player.OwnerId);

        vanillasend.StartMessage((byte)GameDataTag.SpawnFlag);
        vanillasend.WritePacked(1); // 1 Meeting Hud Spawn id
        vanillasend.WritePacked(-2); // Owned by host
        vanillasend.Write((byte)SpawnFlags.None);
        vanillasend.WritePacked(1);
        vanillasend.WritePacked(customNetId);

        vanillasend.StartMessage(1);
        vanillasend.WritePacked(0);
        vanillasend.EndMessage();

        vanillasend.EndMessage();
        vanillasend.EndMessage();

        vanillasend.StartMessage(6);
        vanillasend.Write(AmongUsClient.Instance.GameId);
        vanillasend.Write(player.OwnerId);
        vanillasend.StartMessage((byte)GameDataTag.RpcFlag);
        vanillasend.WritePacked(customNetId);
        vanillasend.Write((byte)RpcCalls.CloseMeeting);
        vanillasend.EndMessage();
        vanillasend.EndMessage();

        AmongUsClient.Instance.SendOrDisconnect(vanillasend);
        vanillasend.Recycle();
    }

    public static int AllPlayersCount => Main.PlayerStates.Values.Count(state => state.countTypes != CountTypes.OutOfGame);
    public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
    public static bool IsAllAlive => Main.PlayerStates.Values.All(state => state.countTypes == CountTypes.OutOfGame || !state.IsDead);
    public static int PlayersCount(CountTypes countTypes) => Main.PlayerStates.Values.Count(state => state.countTypes == countTypes);
    public static int AlivePlayersCount(CountTypes countTypes) => Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));
    public static bool GetRoleByName(string name, out CustomRoles role)
    {
        role = new();

        if (name == "" || name == string.Empty) return false;

        if ((TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.SChinese) == SupportedLangs.SChinese)
        {
            Regex r = new("[\u4e00-\u9fa5]+$");
            MatchCollection mc = r.Matches(name);
            string result = string.Empty;
            for (int i = 0; i < mc.Count; i++)
            {
                if (mc[i].ToString() == "是") continue;
                result += mc[i]; //匹配结果是完整的数字，此处可以不做拼接的
            }
            name = FixRoleNameInput(result.Replace("是", string.Empty).Trim());
        }
        else name = name.Trim().ToLower();

        foreach (var rl in CustomRolesHelper.AllRoles)
        {
            if (rl.IsVanilla()) continue;
            var roleName = GetString(rl.ToString()).ToLower().Trim().Replace(" ", "");
            string nameWithoutId = Regex.Replace(name.Replace(" ", ""), @"^\d+", "");
            if (nameWithoutId == roleName)
            {
                role = rl;
                return true;
            }
        }
        return false;
    }
    public static string FixRoleNameInput(string text)
    {
        text = text.Replace("着", "者").Trim().ToLower();
        return text switch
        {
            // Because of partial translation conflicts (zh-cn and zh-tw)
            // Need to wait for follow-up finishing
            
            /*
            // GM
            "GM(遊戲大師)" or "管理员" or "管理" or "gm" or "GM" => GetString("GM"),
            
            // 原版职业
            "船員" or "船员" or "白板" or "天选之子" => GetString("CrewmateTOHE"),
            "工程師" or "工程师" => GetString("EngineerTOHE"),
            "科學家" or "科学家" => GetString("ScientistTOHE"),
            "守護天使" or "守护天使" => GetString("GuardianAngelTOHE"),
            "偽裝者" or "内鬼" => GetString("ImpostorTOHE"),
            "變形者" or "变形者" => GetString("ShapeshifterTOHE"),

            // 隱藏職業 and 隐藏职业
            "陽光開朗大男孩" or "阳光开朗大男孩" => GetString("Sunnyboy"),
            "吟遊詩人" or "吟游诗人" => GetString("Bard"),
            "核爆者" or "核武器" => GetString("Nuker"),

            // 偽裝者陣營職業 and 内鬼阵营职业
            "賞金獵人" or "赏金猎人" or "赏金" => GetString("BountyHunter"),
            "煙火工匠" or "烟花商人" or "烟花爆破者" or "烟花" => GetString("Fireworker"),
            "嗜血殺手" or "嗜血杀手" or "嗜血" => GetString("Mercenary"),
            "百变怪" or "千面鬼" or "千面" => GetString("ShapeMaster"),
            "吸血鬼" or "吸血" => GetString("Vampire"),
            "吸血鬼之王" or "吸血鬼女王"  => GetString("Vampiress"),
            "術士" or "术士" => GetString("Warlock"),
            "刺客" or "忍者" => GetString("Ninja"),
            "僵屍" or "僵尸" or"殭屍" or "丧尸" => GetString("Zombie"),
            "駭客" or "骇客" or "黑客" => GetString("Anonymous"),
            "礦工" or "矿工" => GetString("Miner"),
            "殺人機器" or "杀戮机器" or "杀戮" or "机器" or "杀戮兵器" => GetString("KillingMachine"),
            "通緝犯" or "逃逸者" or "逃逸" => GetString("Escapist"),
            "女巫" => GetString("Witch"),
            "傀儡師" or "傀儡师" or "傀儡" => GetString("Puppeteer"),
            "主謀" or "策划者" => GetString("Mastermind"),
            "時間竊賊" or "蚀时者" or "蚀时" or "偷时" => GetString("TimeThief"),
            "狙擊手" or "狙击手" or "狙击" => GetString("Sniper"),
            "送葬者" or "暗杀者" => GetString("Undertaker"),
            "裂縫製造者" or "裂缝制造者" => GetString("RiftMaker"),
            "邪惡的追踪者" or "邪恶追踪者" or "邪恶的追踪者" => GetString("EvilTracker"),
            "邪惡賭怪" or "邪恶赌怪" or "坏赌" or "恶赌" or "邪恶赌怪" => GetString("EvilGuesser"),
            "監管者" or "监管者" or "监管" => GetString("AntiAdminer"),
            "狂妄殺手" or "狂妄杀手" => GetString("Arrogance"),
            "自爆兵" or "自爆" => GetString("Bomber"),
            "清道夫" or "清道" => GetString("Scavenger"),
            "陷阱師" or "诡雷" => GetString("Trapster"),
            "歹徒" => GetString("Gangster"),
            "清潔工" or "清理工" or "清洁工" => GetString("Cleaner"),
            "球狀閃電" or "球状闪电" => GetString("Lightning"),
            "貪婪者" or "贪婪者" or "贪婪" => GetString("Greedy"),
            "被詛咒的狼" or "呪狼" => GetString("CursedWolf"),
            "換魂師" or "夺魂者" or "夺魂" => GetString("SoulCatcher"),
            "快槍手" or "快枪手" or "快枪" => GetString("QuickShooter"),
            "隱蔽者" or "隐蔽者" or "小黑人" => GetString("Camouflager"),
            "抹除者" or "抹除" => GetString("Eraser"),
            "肢解者" or "肢解" => GetString("Butcher"),
            "劊子手" or "刽子手" => GetString("Hangman"),
            "隱身人" or "隐匿者" or "隐匿" or "隐身" => GetString("Swooper"),
            "船鬼" => GetString("Crewpostor"),
            "野人" => GetString("Wildling"),
            "騙術師" or "骗术师" => GetString("Trickster"),
            "衛道士" or "卫道士" or "内鬼市长" => GetString("Vindicator"),
            "寄生蟲" or "寄生虫" => GetString("Parasite"),
            "分散者" or "分散" => GetString("Disperser"),
            "抑鬱者" or "抑郁者" or "抑郁" => GetString("Inhibitor"),
            "破壞者" or "破坏者" or "破坏" => GetString("Saboteur"),
            "議員" or "邪恶法官" or "议员" or "邪恶审判" => GetString("Councillor"),
            "眩暈者" or "眩晕者" or "眩晕" => GetString("Dazzler"),
            "簽約人" or "死亡契约" or "死亡" or "锲约" => GetString("Deathpact"),
            "吞噬者" or "吞噬" => GetString("Devourer"),
            "軍師" or "军师" => GetString("Consigliere"),
            "化型者" or "化形者" => GetString("Morphling"),
            "躁動者" or "龙卷风" => GetString("Twister"),
            "策畫者" or "潜伏者" or "潜伏" => GetString("Lurker"),
            "罪犯" => GetString("Convict"),
            "幻想家" or "幻想" => GetString("Visionary"),
            "逃亡者" or "逃亡" => GetString("Refugee"),
            "潛伏者" or "失败者" or "失败的man" or "失败" => GetString("Underdog"),
            "賭博者" or "速度者" or "速度" => GetString("Ludopath"),
            "懸賞者" or "教父" => GetString("Godfather"),
            "天文學家" or "天文学家" or "天文家" or "天文学" => GetString("Chronomancer"),
            "設陷者" or "设陷者" or "设陷" => GetString("Pitfall"),
            "狂戰士" or "狂战士" or "升级者" or "狂战士" => GetString("Berserker"),
            "壞迷你船員" or "坏迷你船员" or "坏小孩" or "坏迷你" => GetString("EvilMini"),
            "勒索者" or "勒索" => GetString("Blackmailer"),
            "教唆者" or "教唆" => GetString("Instigator"),

            // 船員陣營職業 and 船员阵营职业
            "擺爛人" or "摆烂人" or "摆烂" => GetString("Needy"),
            "大明星" or "明星" => GetString("SuperStar"),
            "網紅" or "网红" => GetString("Celebrity"),
            "清洗者" or "清洗" => GetString("Cleanser"),
            "守衛者" or "守卫者" => GetString("Keeper"),
            "俠客" or "侠客" or "正义使者" => GetString("Knight"),
            "市長" or "市长" => GetString("Mayor"),
            "被害妄想症" or "被害妄想" or "被迫害妄想症" or "被害" or "妄想" or "妄想症" => GetString("Paranoia"),
            "愚者" => GetString("Psychic"),
            "修理工" or "修理" or "修理大师" => GetString("Mechanic"),
            "警長" or "警长" => GetString("Sheriff"),
            "義警" or "义务警员" or "警员" => GetString("Vigilante"),
            "監禁者" or "狱警" or "狱卒" => GetString("Jailer"),
            "模仿者" or "模仿猫" or "模仿" => GetString("CopyCat"),
            "告密者" => GetString("Snitch"),
            "展現者" or "展现者" or "展现" => GetString("Marshall"),
            "增速師" or "增速者" or "增速" => GetString("SpeedBooster"),
            "法醫" or "法医" => GetString("Doctor"),
            "獨裁主義者" or "独裁者" or "独裁" => GetString("Dictator"),
            "偵探" or "侦探" => GetString("Detective"),
            "正義賭怪" or "正义赌怪" or "好赌" or "正义的赌怪" => GetString("NiceGuesser"),
            "賭場管理員" or "竞猜大师" or "竞猜" => GetString("GuessMaster"),
            "傳送師" or "传送师" => GetString("Transporter"),
            "時間大師" or "时间操控者" or "时间操控" => GetString("TimeManager"),
            "老兵" => GetString("Veteran"),
            "埋雷兵" => GetString("Bastion"),
            "保鑣" or "保镖" => GetString("Bodyguard"),
            "贗品商" or "赝品商" => GetString("Deceiver"),
            "擲彈兵" or "掷雷兵" => GetString("Grenadier"),
            "軍醫" or "医生" => GetString("Medic"),
            "占卜師" or "调查员" or "占卜师" => GetString("FortuneTeller"),
            "法官" or "正义法官" or "正义审判" => GetString("Judge"),
            "殯葬師" or "入殓师" => GetString("Mortician"),
            "通靈師" or "通灵师" => GetString("Mediumshiper"),
            "和平之鴿" or "和平之鸽" => GetString("Pacifist"),
            "窺視者" or "观察者" or "观察" => GetString("Observer"),
            "君主" => GetString("Monarch"),
            "預言家" or "预言家" or "预言" => GetString("Overseer"),
            "驗屍官" or "验尸官" or "验尸" => GetString("Coroner"),
            "正義的追蹤者" or "正义追踪者" or "正义的追踪者" => GetString("Tracker"),
            "商人" => GetString("Merchant"),
            "總統" or "总统" => GetString("President"),
            "獵鷹" or "猎鹰" => GetString("Hawk"),
            "捕快" or "下属" => GetString("Deputy"),
            "算命師" or "研究者" => GetString("Investigator"),
            "守護者" or "守护者" or "守护" => GetString("Guardian"),
            "賢者" or "瘾君子" or "醉酒" => GetString("Addict"),
            "鼹鼠" => GetString("Mole"),
            "藥劑師" or "炼金术士" or "药剂" => GetString("Alchemist"),
            "尋跡者" or "寻迹者" or "寻迹" or "寻找鸡腿" => GetString("Tracefinder"),
            "先知" or "神谕" or "神谕者" => GetString("Oracle"),
            "靈魂論者" or "灵魂论者" => GetString("Spiritualist"),
            "變色龍" or "变色龙" or "变色" => GetString("Chameleon"),
            "檢查員" or "检查员" or "检查" => GetString("Inspector"),
            "仰慕者" or "仰慕" => GetString("Admirer"),
            "時間之主" or "时间之主" or "回溯时间" => GetString("TimeMaster"),
            "十字軍" or "十字军" => GetString("Crusader"),
            "遐想者" or "遐想" => GetString("Reverie"),
            "瞭望者" or "瞭望员" => GetString("Lookout"),
            "通訊員" or "通信员" => GetString("Telecommunication"),
            "執燈人" or "执灯人" or "执灯" or "灯人" or "小灯人" => GetString("Lighter"),
            "任務管理員" or "任务管理者" => GetString("TaskManager"),
            "目擊者" or "目击者" or "目击" => GetString("Witness"),
            "換票師" or "换票师" => GetString("Swapper"),
            "警察局長" or "警察局长" => GetString("ChiefOfPolice"),
            "好迷你船員" or "好迷你船员" or "好迷你" or "好小孩" => GetString("NiceMini"),
            "間諜" or "间谍" => GetString("Spy"),
            "隨機者" or "萧暮" or "暮" or "萧暮不姓萧" => GetString("Randomizer"),
            "猜想者" or "猜想" or "谜团" => GetString("Enigma"),
            "船長" or "舰长" or "船长" => GetString("Captain"),
            "慈善家" or "恩人" => GetString("Benefactor"),

            // 中立陣營職業 and 中立阵营职业
            "小丑" or "丑皇" => GetString("Jester"),
            "縱火犯" or "纵火犯" or "纵火者" or "纵火" => GetString("Arsonist"),
            "焚燒狂" or "焚烧狂" or "焚烧" => GetString("Pyromaniac"),
            "神風特攻隊" or "神风特攻队" => GetString("Kamikaze"),
            "獵人" or "猎人" => GetString("Huntsman"),
            "恐怖分子" => GetString("Terrorist"),
            "暴民" or "处刑人" or "处刑" or "处刑者" => GetString("Executioner"),
            "律師" or "律师" => GetString("Lawyer"),
            "投機主義者" or "投机者" or "投机" => GetString("Opportunist"),
            "瑪利歐" or "马里奥" => GetString("Vector"),
            "豺狼" or "蓝狼" => GetString("Jackal"),
            "神" or "上帝" => GetString("God"),
            "冤罪師" or "冤罪师" or "冤罪" => GetString("Innocent"),
            "暗殺者" or "隐形者" =>GetString("Stealth"),
            "企鵝" or "企鹅" =>GetString("Penguin"),
            "鵜鶘" or "鹈鹕" => GetString("Pelican"),
            "疫醫" or "瘟疫学家" => GetString("PlagueDoctor"),
            "革命家" or "革命者" => GetString("Revolutionist"),
            "單身狗" => GetString("Hater"),
            "柯南" => GetString("Konan"),
            "玩家" => GetString("Demon"),
            "潛藏者" or "潜藏" => GetString("Stalker"),
            "工作狂" => GetString("Workaholic"),
            "至日者" or "至日" => GetString("Solsticer"),
            "集票者" or "集票" => GetString("Collector"),
            "挑釁者" or "自爆卡车" => GetString("Provocateur"),
            "嗜血騎士" or "嗜血骑士" => GetString("BloodKnight"),
            "瘟疫之源" or "瘟疫使者" => GetString("PlagueBearer"),
            "萬疫之神" or "瘟疫" => GetString("Pestilence"),
            "故障者" or "缺点者" or "缺点" => GetString("Glitch"),
            "跟班" or "跟班小弟" => GetString("Sidekick"),
            "追隨者" or "赌徒" or "下注" => GetString("Follower"),
            "魅魔" => GetString("Cultist"),
            "連環殺手" or "连环杀手" => GetString("SerialKiller"),
            "劍聖" or "天启" => GetString("Juggernaut"),
            "感染者" or "感染" => GetString("Infectious"),
            "病原體" or "病毒" => GetString("Virus"),
            "起訴人" or "起诉人" => GetString("Pursuer"),
            "怨靈" or "幽灵" => GetString("Phantom"),
            "挑戰者" or "决斗者" or "挑战者" => GetString("Pirate"),
            "炸彈王" or "炸弹狂" or "煽动者" => GetString("Agitater"),
            "獨行者" or "独行者" => GetString("Maverick"),
            "被詛咒的靈魂" or "诅咒之人" => GetString("CursedSoul"),
            "竊賊" or "小偷" => GetString("Pickpocket"),
            "背叛者" or "背叛" => GetString("Traitor"),
            "禿鷲" or "秃鹫" => GetString("Vulture"),
            "搗蛋鬼" or "任务执行者" => GetString("Taskinator"),
            "麵包師" or "面包师" => GetString("Baker"),
            "飢荒" or "饥荒" => GetString("Famine"),
            "靈魂召喚者" or "灵魂召唤者" => GetString("Spiritcaller"),
            "失憶者" or "失忆者" or "失忆" => GetString("Amnesiac"),
            "模仿家" or "效仿者" => GetString("Imitator"),
            "強盜" => GetString("Bandit"),
            "分身者" => GetString("Doppelganger"),
            "受虐狂" => GetString("PunchingBag"),
            "賭神" or "末日赌怪" => GetString("Doomsayer"),
            "裹屍布" or "裹尸布" => GetString("Shroud"),
            "月下狼人" or "狼人" => GetString("Werewolf"),
            "薩滿" or "萨满" => GetString("Shaman"),
            "冒險家" or "探索者" => GetString("Seeker"),
            "精靈" or "小精灵" or "精灵" => GetString("Pixie"),
            "咒魔" or "神秘者" => GetString("Occultist"),
            "靈魂收割者" or "灵魂收集者" or "灵魂收集" or "收集灵魂" => GetString("SoulCollector"),
            "薛丁格的貓" or "薛定谔的猫" => GetString("SchrodingersCat"),
            "暗戀者" or "浪漫者" => GetString("Romantic"),
            "報復者" or "复仇浪漫者" => GetString("VengefulRomantic"),
            "絕情者" or "无情浪漫者" => GetString("RuthlessRomantic"),
            "毒醫" or "投毒者" => GetString("Poisoner"),
            "代碼工程師" or "巫师" => GetString("HexMaster"),
            "幻影" or "魅影" => GetString("Wraith"),
            "掃把星" or "扫把星" => GetString("Jinx"),
            "魔藥師" or "药剂师" => GetString("PotionMaster"),
            "死靈法師" or "亡灵巫师" => GetString("Necromancer"),
            "測驗者" or "测验长" => GetString("Quizmaster"),

            // 附加職業 and 附加职业
            "絕境者" or "绝境者" => GetString("LastImpostor"),
            "超頻" or "超频波" or "超频" => GetString("Overclocked"),
            "戀人" or "恋人" => GetString("Lovers"),
            "叛徒" => GetString("Madmate"),
            "觀察者" or "窥视者" or "觀察" or "窥视" => GetString("Watcher"),
            "閃電俠" or "闪电侠" or "閃電" or "闪电" => GetString("Flash"),
            "持燈人" or "火炬" or "持燈" => GetString("Torch"),
            "靈媒" or "灵媒" or "靈媒" => GetString("Seer"),
            "破平者" or "破平" => GetString("Tiebreaker"),
            "膽小鬼" or "胆小鬼" or "膽小" or "胆小" => GetString("Oblivious"),
            "視障" or "迷幻者" or "視障" or "迷幻" => GetString("Bewilder"),
            "墨鏡" or "患者" => GetString("Sunglasses"),
            "加班狂" => GetString("Workhorse"),
            "蠢蛋" => GetString("Fool"),
            "復仇者" or "复仇者" or "復仇" or "复仇" => GetString("Avanger"),
            "Youtuber" or "UP主" or "YT" => GetString("Youtuber"),
            "利己主義者" or "利己主义者" or "利己主義" or "利己主义" => GetString("Egoist"),
            "竊票者" or "窃票者" or "竊票" or "窃票" => GetString("TicketsStealer"),
            //"雙重人格" or "双重人格" => GetString("Schizophrenic"),
            "保險箱" or "宝箱怪" => GetString("Mimic"),
            "賭怪" or "赌怪" => GetString("Guesser"),
            "死神" => GetString("Necroview"),
            "長槍" or "持枪" => GetString("Reach"),
            "魅魔小弟" => GetString("Charmed"),
            "乾淨" or "干净" => GetString("Cleansed"),
            "誘餌" or "诱饵" => GetString("Bait"),
            "陷阱師" or "陷阱师" => GetString("Trapper"),
            "被感染" or "感染" => GetString("Infected"),
            "防賭" or "不可被赌" => GetString("Onbound"),
            "反擊者" or "回弹者" or "回弹" => GetString("Rebound"),
            "平凡者" or "平凡" => GetString("Mundane"),
            "騎士" or "骑士" => GetString("Knighted"),
            "漠視" or "不受重视" or "被漠視的" => GetString("Unreportable"),
            "被傳染" or "传染性" => GetString("Contagious"),
            "幸運" or "幸运加持" => GetString("Lucky"),
            "倒霉" or "倒霉蛋" => GetString("Unlucky"),
            "虛無" or "无效投票" => GetString("VoidBallot"),
            "敏感" or "意识者" or "意识" => GetString("Aware"),
            "嬌嫩" or "脆弱" or "脆弱者" => GetString("Fragile"),
            "專業" or "双重猜测" => GetString("DoubleShot"),
            "流氓" => GetString("Rascal"),
            "無魂" or "没有灵魂" => GetString("Soulless"),
            "墓碑" => GetString("Gravestone"),
            "懶人" or "懒人" => GetString("Lazy"),
            "驗屍" or "尸检" => GetString("Autopsy"),
            "忠誠" or "忠诚" => GetString("Loyal"),
            "惡靈" or "恶灵" => GetString("EvilSpirit"),
            "狼化" or "招募" or "狼化的" or "被招募的" => GetString("Recruit"),
            "被仰慕" or "仰慕" => GetString("Admired"),
            "發光" or "光辉" => GetString("Glow"),
            "病態" or "患病者" or "患病的" or "患病" => GetString("Diseased"),
            "健康" or "健康的" or "健康者" => GetString("Antidote"),
            "固執者" or "固执者" or "固執" or "固执" => GetString("Stubborn"),
            "無影" or "迅捷" => GetString("Swift"),
            "反噬" or "食尸鬼" => GetString("Ghoul"),
            "嗜血者" => GetString("Bloodthirst"),
            "獵夢者" or "梦魇" or "獵夢"=> GetString("Mare"),
            "地雷" or "爆破者" or "爆破" => GetString("Burst"),
            "偵察員" or "侦察员" or "偵察" or "侦察" => GetString("Sleuth"),
            "笨拙" or "笨蛋" => GetString("Clumsy"),
            "敏捷" => GetString("Nimble"),
            "規避者" or "规避者" or "规避" => GetString("Circumvent"),
            "名人" or "网络员" or "网络" => GetString("Cyber"),
            "焦急者" or "焦急的" or "焦急" => GetString("Hurried"),
            "OIIAI" => GetString("Oiiai"),
            "順從者" or "影响者" or "順從" or "影响" => GetString("Influenced"),
            "沉默者" or "沉默" => GetString("Silent"),
            "易感者" or "易感" => GetString("Susceptible"),
            "狡猾" or "棘手者" or "棘手" => GetString("Tricky"),
            "彩虹" => GetString("Rainbow"),
            "疲勞者" or "疲劳者" or "疲勞" or "疲劳" => GetString("Tired"),
            "雕像" => GetString("Statue"),
            "没有搜集的繁体中文" or "雷达" => GetString("Radar"),

            // 幽靈職業 and 幽灵职业
            // 偽裝者 and 内鬼
            "爪牙" => GetString("Minion"),
            "黑手黨" or "黑手党" or "黑手" => GetString("Nemesis"),
            "嗜血之魂" or "血液伯爵" => GetString("Bloodmoon"),
            // 船員 and 船员
            "没有搜集的繁体中文" or "鬼怪" => GetString("Ghastly"),
            "冤魂" or "典狱长" => GetString("Warden"),
            "報應者" or "惩罚者" or "惩罚" or "报仇者" => GetString("Retributionist"),

            // 随机阵营职业
            "迷你船員" or "迷你船员" or "迷你" or "小孩" or "Mini" => GetString("Mini"),*/
            _ => text,
        };
    }
    public static void SendRolesInfo(string role, byte playerId, bool isDev = false, bool isUp = false)
    {
        if (Options.CurrentGameMode == CustomGameMode.FFA)
        {
            SendMessage(GetString("ModeDescribe.FFA"), playerId);
            return;
        }
        role = role.Trim().ToLower();
        if (role.StartsWith("/r")) _ = role.Replace("/r", string.Empty);
        if (role.StartsWith("/up")) _ = role.Replace("/up", string.Empty);
        if (role.EndsWith("\r\n")) _ = role.Replace("\r\n", string.Empty);
        if (role.EndsWith("\n")) _ = role.Replace("\n", string.Empty);
        if (role.StartsWith("/bt")) _ = role.Replace("/bt", string.Empty);

        if (role == "" || role == string.Empty)
        {
            Utils.ShowActiveRoles(playerId);
            return;
        }

        role = FixRoleNameInput(role).ToLower().Trim().Replace(" ", string.Empty);

        foreach (var rl in CustomRolesHelper.AllRoles)
        {
            if (rl.IsVanilla()) continue;
            var roleName = GetString(rl.ToString());
            if (role == roleName.ToLower().Trim().TrimStart('*').Replace(" ", string.Empty))
            {
                string devMark = "";
                if ((isDev || isUp) && GameStates.IsLobby)
                {
                    devMark = "▲";
                    if (CustomRolesHelper.IsAdditionRole(rl) || rl is CustomRoles.GM or CustomRoles.Mini || rl.IsGhostRole()) devMark = "";
                    if (rl.GetCount() < 1 || rl.GetMode() == 0) devMark = "";
                    if (isUp)
                    {
                        if (devMark == "▲") Utils.SendMessage(string.Format(GetString("Message.YTPlanSelected"), roleName), playerId);
                        else Utils.SendMessage(string.Format(GetString("Message.YTPlanSelectFailed"), roleName), playerId);
                    }
                    if (devMark == "▲")
                    {
                        byte pid = playerId == 255 ? (byte)0 : playerId;
                        GhostRoleAssign.forceRole.Remove(pid);
                        RoleAssign.SetRoles.Remove(pid);
                        RoleAssign.SetRoles.Add(pid, rl);
                    }
                    if (rl.IsGhostRole() && !rl.IsAdditionRole() && isDev && (rl.GetCount() >= 1 && rl.GetMode() > 0))
                    {
                        byte pid = playerId == 255 ? (byte)0 : playerId;
                        CustomRoles setrole = rl.GetCustomRoleTeam() switch
                        {
                            Custom_Team.Impostor => CustomRoles.ImpostorTOHE,
                            _ => CustomRoles.CrewmateTOHE

                        };
                        RoleAssign.SetRoles.Remove(pid);
                        RoleAssign.SetRoles.Add(pid, setrole);
                        GhostRoleAssign.forceRole[pid] = rl;

                        devMark = "▲";
                    }

                    if (isUp) return;
                }
                var Des = rl.GetInfoLong();
                var title = devMark + $"<color=#ffffff>" + rl.GetRoleTitle() + "</color>\n";
                var Conf = new StringBuilder();
                string rlHex = Utils.GetRoleColorCode(rl);
                if (Options.CustomRoleSpawnChances.ContainsKey(rl))
                {
                    Utils.ShowChildrenSettings(Options.CustomRoleSpawnChances[rl], ref Conf);
                    var cleared = Conf.ToString();
                    var Setting = $"<color={rlHex}>{GetString(rl.ToString())} {GetString("Settings:")}</color>\n";
                    Conf.Clear().Append($"<color=#ffffff>" + $"<size={ChatCommandPatch.Csize}>" + Setting + cleared + "</size>" + "</color>");

                }
                // Show role info
                Utils.SendMessage(Des, playerId, title, noReplay: true);

                // Show role settings
                Utils.SendMessage("", playerId, Conf.ToString(), noReplay: true);
                return;
            }
        }
        if (isUp) Utils.SendMessage(GetString("Message.YTPlanCanNotFindRoleThePlayerEnter"), playerId);
        else Utils.SendMessage(GetString("Message.CanNotFindRoleThePlayerEnter"), playerId);
        return;
    }
}
