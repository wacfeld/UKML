namespace UKML;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

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
class PatchMaurice
{
    static void Postfix(ref float ___coolDownMultiplier, ref int ___maxBurst, ref EnemyIdentifier ___eid)
    {
        ___coolDownMultiplier = 500f;
        ___maxBurst = 1;
    }
}

[HarmonyPatch(typeof(SpiderBody))]
[HarmonyPatch("ShootProj")]
class MauriceProjectile
{
    static void Postfix(ref GameObject ___currentProj)
    {
        Projectile proj = ___currentProj.GetComponent<Projectile>();
        proj.speed *= 2;
    }
}
