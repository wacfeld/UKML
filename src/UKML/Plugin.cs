namespace UKML;

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "wacfeld.ukml";
    public const string PLUGIN_NAME = "ULTRAKILL Mustn't Live";
    public const string PLUGIN_VERSION = "0.1.0";

    readonly Harmony harmony = new(PLUGIN_GUID);
    
    // TODO stop game from muting non-error messages at the start, and also figure out how to log messages from inside patches
    public ManualLogSource Log => Logger;

    private void Awake()
    {
        harmony.PatchAll();
        Log.LogInfo($"Loaded {PLUGIN_NAME}");
    }
}

[HarmonyPatch(typeof(NewMovement))]
[HarmonyPatch("GetHurt")]
class PatchGetHurt
{
    // TODO make this only apply to difficulty 5
    // set hardDamageMultiplier to 1
    static void Prefix(ref float hardDamageMultiplier)
    {
        hardDamageMultiplier = 1f;
    }
}

[HarmonyPatch(typeof(SpiderBody))]
[HarmonyPatch("Start")]
class PatchMauriceStart
{
    static void Postfix(ref float ___coolDownMultiplier, ref int ___maxBurst, ref EnemyIdentifier ___eid)
    {
        ___coolDownMultiplier = 500f;
        ___maxBurst = 1;
    }
}

[HarmonyPatch(typeof(SpiderBody))]
[HarmonyPatch("Update")]
class PatchMauriceUpdate
{
    static void Postfix(SpiderBody __instance, ref EnemyIdentifier ___eid, ref int ___beamsAmount, ref float ___beamProbability)
    {
        if(___eid.beenGasolined)
        {
            __instance.Enrage();
        }
        if(__instance.isEnraged)
        {
            ___beamsAmount = 2;
            ___beamProbability = 1f;
        }
    }
}

[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("Start")]
class PatchProjectile
{
    static void Postfix(ref float ___speed, ref float ___turningSpeedMultiplier)
    {
        ___speed *= 2f;
        ___turningSpeedMultiplier *= 2f;
    }
}

//[HarmonyPatch(typeof(Zombie))]
//[HarmonyPatch("Start")]
//class PatchZombieStart
//{
//    static void Postfix()
//    {
//        Console.WriteLine("i'm a zombie!");
//    }
//}

//[HarmonyPatch(typeof(Punch))]
//[HarmonyPatch("ParryProjectile")]
//class PatchParryProjectile
//{
//    static void Postfix(Projectile proj)
//    {
//        proj.speed /= 20f;
//    }
//}