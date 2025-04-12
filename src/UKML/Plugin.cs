namespace UKML;

using System;
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
class PatchMauriceStart
{
    static void Postfix(ref float ___coolDownMultiplier, ref int ___maxBurst, ref EnemyIdentifier ___eid)
    {
        ___coolDownMultiplier = 500f;
        ___maxBurst = 5;
    }
}

[HarmonyPatch(typeof(SpiderBody))]
[HarmonyPatch("Update")]
class PatchMauriceUpdate
{
    //static void Prefix(EnemyIdentifier ___eid)
    //{
    //    ___eid.totalSpeedModifier = 2f;
    //}

    static void Postfix(SpiderBody __instance, EnemyIdentifier ___eid, ref int ___beamsAmount, ref float ___beamProbability)
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

        //Console.WriteLine("speed multiplier " + ___eid.totalSpeedModifier);
    }
}

//[HarmonyPatch(typeof(EnemyIdentifier))]
//[HarmonyPatch("UpdateModifiers")]
//class PatchEIDUpdate
//{
//    static void postfix(EnemyIdentifier __instance)
//    {
//        __instance.totalSpeedModifier = 2f;
//        Console.WriteLine("I have been called");
//    }
//}

[HarmonyPatch(typeof(EnemyIdentifier))]
[HarmonyPatch("IsTypeFriendly")]
class PatchFriendly
{
    static void Postfix(ref bool __result)
    {
        Console.WriteLine("I have been called");
        __result = true;
    }
}

//[HarmonyPatch(typeof(EnemyIdentifier))]
//[HarmonyPatch("Update")]
//class PatchEID
//{
//    static void Postfix(EnemyIdentifier __instance)
//    {
//        Console.WriteLine("hi");
//        //__instance.immuneToFriendlyFire = true;
//    }
//}

[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("Start")]
class PatchProjectileStart
{
    static void Postfix(Projectile __instance, ref float ___speed, ref float ___turningSpeedMultiplier)
    {
        ___speed *= 2f;
        ___turningSpeedMultiplier *= 2f;
        //Console.WriteLine("I'm a projectile! friendly=" + __instance.friendly.ToString());
    }
}

[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("Collided")]
class PatchCollided
{
    static void Prefix(Projectile __instance, ref bool ___active, Collider other)
    {
        if (___active && (other.gameObject.CompareTag("Head") || other.gameObject.CompareTag("Body") || other.gameObject.CompareTag("Limb") || other.gameObject.CompareTag("EndLimb")) && !other.gameObject.CompareTag("Armor"))
        {
            EnemyIdentifierIdentifier componentInParent2 = other.gameObject.GetComponentInParent<EnemyIdentifierIdentifier>();
            EnemyIdentifier enemyIdentifier = null;
            if (componentInParent2 != null && componentInParent2.eid != null)
            {
                enemyIdentifier = componentInParent2.eid;
            }
            if (enemyIdentifier != null)
            {
                __instance.safeEnemyType = enemyIdentifier.enemyType;
            }
        }
    }
}

//[HarmonyPatch(typeof(Projectile))]
//[HarmonyPatch("Update")]
//class PatchProjectileUpdate
//{
//    static void Postfix(Projectile __instance)
//    {
//        if(__instance.friendly)
//        {
//            Console.WriteLine("I'm friendly!");
//        }
//    }
//}

//[HarmonyPatch(typeof(Zombie))]
//[HarmonyPatch("Start")]
//class PatchZombieStart
//{
//    static void Postfix(ref EnemyIdentifier ___eid)
//    {
//        Console.WriteLine("i'm a zombie!");
//        Console.WriteLine("my EnemyType is " + ___eid.enemyType.ToString());
//    }
//}

//[HarmonyPatch(typeof(ZombieIgnorizer))]
//[HarmonyPatch("Start")]
//class PatchZombieIgnorizerStart
//{
//    static void Postfix()
//    {
//        Console.WriteLine("i'm a ZombieIgnorizer!");
//    }
//}

//[HarmonyPatch(typeof(ZombieMelee))]
//[HarmonyPatch("Start")]
//class PatchZombieMeleeStart
//{
//    static void Postfix()
//    {
//        Console.WriteLine("i'm a ZombieMelee!");
//    }
//}

//[HarmonyPatch(typeof(ZombieProjectiles))]
//[HarmonyPatch("Start")]
//class PatchZombieProjectilesStart
//{
//    static void Postfix(ZombieProjectiles __instance)
//    {
//        Console.WriteLine("i'm a ZombieProjectiles!");
//        Console.WriteLine("hasMelee: " + __instance.hasMelee.ToString());
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