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
    public const string PLUGIN_VERSION = "0.2.0";

    readonly Harmony harmony = new(PLUGIN_GUID);
    
    // TODO stop game from muting non-error messages at the start, and also figure out how to log messages from inside patches
    public ManualLogSource Log => Logger;

    private void Awake()
    {
        harmony.PatchAll();
        Log.LogInfo($"Loaded {PLUGIN_NAME} v{PLUGIN_VERSION}");
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

[HarmonyPatch(typeof(SpiderBody))]
[HarmonyPatch("BeamChargeEnd")]
class PatchMauriceBeamChargeEnd
{
    // the game does not distinguish between difficulties >= 4
    // we take advantage of this by using the variable to keep track of how many times the beam has fired
    // odd numbers are parryable, even numbers are parryable

    // we increment the difficulty in the prefix and then adjust the contents of SpiderBody.spark accordingly
    // this lets us avoid having to overwrite the entirety of BeamChargeEnd just to change the spark color
    static void Prefix(SpiderBody __instance, ref int ___difficulty)
    {
        if (___difficulty < 4)
        {
            return;
        }
        ___difficulty++;
        if (___difficulty >= 1000) // prevent overflow in extreme cases
        {
            ___difficulty -= 500;
        }

        if (___difficulty % 2 == 0)
        {
            __instance.spark = MonoSingleton<DefaultReferenceManager>.Instance.unparryableFlash;
        }
        else
        {
            __instance.spark = MonoSingleton<DefaultReferenceManager>.Instance.parryableFlash;
        }
    }

    // we do the actually parryable field setting in the postfix
    static void Postfix(SpiderBody __instance, ref bool ___parryable, ref int ___difficulty, Vector3 ___predictedPlayerPos, EnemyIdentifier ___eid)
    {
        if(___difficulty % 2 == 0)
        {
            ___parryable = false;
            //Console.WriteLine("unparryable!");
            //UnityEngine.Object.Instantiate<GameObject>(MonoSingleton<DefaultReferenceManager>.Instance.unparryableFlash, __instance.mouth.position, __instance.mouth.rotation).transform.LookAt(___predictedPlayerPos);
        }
        //else
        //{
        //    Console.WriteLine("parryable!");
        //}

        // detect BeamFire() invokes and replace them with a faster one
        if(__instance.IsInvoking("BeamFire"))
        {
            __instance.CancelInvoke("BeamFire");
            __instance.Invoke("BeamFire", 0.25f / ___eid.totalSpeedModifier);
        }
    }
}

[HarmonyPatch(typeof(Explosion))]
[HarmonyPatch("Start")]
class PatchExplosion
{
    static void Postfix(Explosion __instance)
    {
        //Console.WriteLine("hi, i'm an explosion!");
        //Console.WriteLine("enemy is " + __instance.enemy);
        //Console.WriteLine("friendlyFire is " + __instance.friendlyFire);
        //Console.WriteLine("canHit is " + __instance.canHit);
        if (__instance.enemy)
        {
            __instance.canHit = AffectedSubjects.PlayerOnly;
        }
    }
}

[HarmonyPatch(typeof(ContinuousBeam))]
[HarmonyPatch("Update")]
class PatchSchismBeam
{
    static void Postfix(ContinuousBeam __instance)
    {
        if (__instance.enemy)
        {
            __instance.canHitEnemy = false;
        }
        else
        {
            __instance.canHitEnemy = true;
        }
        //Console.WriteLine("I'm a ContinuousBeam! canHitEnemy = " + __instance.canHitEnemy);
        //Console.WriteLine("enemy = " + __instance.enemy);
    }
}

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
    // preemptively change the friendly enemy type of a projectile to the enemy it's about to try to hit, thus rendering all enemies immune to regular friendly fire
    // this still allows enemies to be hit by projectiles that have been parried/redirected by explosions, since their "friendly" field gets set to true
    // this also doesn't deal with explosions caused by projectiles (e.x. hideous masses, cerberus balls)
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

// we completely overwrite OrbSpawn so that we can set the difficulty field of the projectile we create
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("OrbSpawn")]
class PatchOrbSpawn
{
    static bool Prefix(StatueBoss __instance, Light ___orbLight, Vector3 ___projectedPlayerPos, ref int ___difficulty, EnemyIdentifier ___eid, ref bool ___orbGrowing, ParticleSystem ___part)
    {
        // do normal stuff if on lower difficulties
        if(___difficulty < 4)
        {
            return true;
        }

        //Console.WriteLine("spawning orb!");

        GameObject gameObject = UnityEngine.Object.Instantiate(__instance.orbProjectile.ToAsset(), new Vector3(___orbLight.transform.position.x, __instance.transform.position.y + 3.5f, ___orbLight.transform.position.z), Quaternion.identity);
        gameObject.transform.LookAt(___projectedPlayerPos);

        gameObject.GetComponent<Rigidbody>().AddForce(gameObject.transform.forward * 20000f);

        if (gameObject.TryGetComponent<Projectile>(out var component))
        {
            // set projectile's difficulty to 6 to indicate it's a cerb ball
            var field = typeof(Projectile).GetField("difficulty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance);
            field.SetValue(component, 6);
            //Console.WriteLine("projectile has difficulty " + field.GetValue(component));

            component.target = ___eid.target;
        }
        ___orbGrowing = false;
        ___orbLight.range = 0f;
        ___part.Play();

        // skip the original
        return false;
    }
}

// make cerb projectiles bounce off surfaces like sawblades
[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("FixedUpdate")]
class PatchCerbProj
{
    
}

//[HarmonyPatch(typeof(StatueBoss))]
//[HarmonyPatch("Update")]
//class PatchStatueBoss
//{
//    static void Postfix(StatueBoss __instance)
//    {
//        Console.WriteLine("i'm a statue boss!");
//        __instance.OrbSpawn();
//    }
//}