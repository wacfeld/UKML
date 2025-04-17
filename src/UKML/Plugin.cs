namespace UKML;

using System;
using System.Collections.Generic;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Reflection;

//using UnityEngine.AddressableAssets.ResourceLocators;
//using UnityEngine.ResourceManagement.ResourceLocations;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "wacfeld.ukml";
    public const string PLUGIN_NAME = "ULTRAKILL Mustn't Live";
    public const string PLUGIN_VERSION = "0.3.2";

    readonly Harmony harmony = new(PLUGIN_GUID);
    
    public ManualLogSource Log => Logger;

    private static bool addressableInit = false;
    public static GameObject shockwave;
    public static GameObject explosion;

    public static T LoadObject<T>(string path)
    {
        if (!addressableInit)
        {
            Addressables.InitializeAsync().WaitForCompletion();
            addressableInit = true;
        }
        return Addressables.LoadAssetAsync<T>(path).WaitForCompletion();
    }

    private void LoadAll()
    {
        shockwave = LoadObject<GameObject>("Assets/Prefabs/Attacks and Projectiles/PhysicalShockwave.prefab");
        explosion = LoadObject<GameObject>("Assets/Prefabs/Attacks and Projectiles/Explosions/Explosion.prefab");
        Console.WriteLine("loaded assets!");
    }

    private void Awake()
    {
        harmony.PatchAll();

        LoadAll();

        Log.LogInfo($"Loaded {PLUGIN_NAME} v{PLUGIN_VERSION}");
    }
}

[HarmonyPatch(typeof(NewMovement))]
[HarmonyPatch("GetHurt")]
class PatchGetHurt
{
    // set hardDamageMultiplier to 100%
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
    // store the number of beams fired for each SpiderBody instance
    // 0 = parryable, 1 = unparryable
    public static Dictionary<int, bool> beamParryable = new Dictionary<int, bool>();

    static void Prefix(SpiderBody __instance)
    {
        int id = __instance.GetInstanceID();
        if(!beamParryable.ContainsKey(id))
        {
            beamParryable.Add(id, false);
        }

        beamParryable[id] = !beamParryable[id];

        if (beamParryable[id])
        {
            __instance.spark = MonoSingleton<DefaultReferenceManager>.Instance.parryableFlash;
        }
        else
        {
            __instance.spark = MonoSingleton<DefaultReferenceManager>.Instance.unparryableFlash;
        }
    }

    // we do the actually parryable field setting in the postfix
    static void Postfix(SpiderBody __instance, ref bool ___parryable, Vector3 ___predictedPlayerPos, EnemyIdentifier ___eid)
    {
        int id = __instance.GetInstanceID();
        if(!beamParryable[id])
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

// TODO check if OrbSpawn still needs to be completely overwritten
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("OrbSpawn")]
class PatchCerbThrow
{
    public static Dictionary<int, int> orbBounces = new Dictionary<int, int>();

    static bool Prefix(StatueBoss __instance, Light ___orbLight, Vector3 ___projectedPlayerPos, EnemyIdentifier ___eid, ref bool ___orbGrowing, ParticleSystem ___part)
    {
        //Console.WriteLine("spawning orb!");

        GameObject gameObject = UnityEngine.Object.Instantiate(__instance.orbProjectile.ToAsset(), new Vector3(___orbLight.transform.position.x, __instance.transform.position.y + 3.5f, ___orbLight.transform.position.z), Quaternion.identity);
        gameObject.transform.LookAt(___projectedPlayerPos);

        gameObject.GetComponent<Rigidbody>().AddForce(gameObject.transform.forward * 60000f);

        if (gameObject.TryGetComponent<Projectile>(out var component))
        {
            int id = component.GetInstanceID();
            orbBounces.Add(id, 0);

            component.target = ___eid.target;
        }
        ___orbGrowing = false;
        ___orbLight.range = 0f;
        ___part.Play();

        // skip the original
        return false;
    }
}

//[HarmonyPatch(typeof(Projectile))]
//[HarmonyPatch("Update")]
//class PatchCerbUpdate
//{
//    static void Prefix(Projectile __instance)
//    {
//        Console.WriteLine("parried = " + __instance.parried);
//        Console.WriteLine("boosted = " + __instance.boosted);
//    }
//}

// make cerb projectiles bounce off surfaces like sawblades
[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("FixedUpdate")]
class PatchCerbProj
{
    static bool Prefix(Projectile __instance, Rigidbody ___rb, Vector3 ___origScale, AudioSource ___aud, ref float ___radius)
    {
        // don't run if ball has been parried
        if(__instance.parried || __instance.boosted)
        {
            return true;
        }

        int id = __instance.GetInstanceID();
        // don't run if not cerb projectile
        if(!PatchCerbThrow.orbBounces.ContainsKey(id))
        {
            return true;
        }
        // if it's out of bounces then let it run its course
        if(PatchCerbThrow.orbBounces[id] >= 5)
        {
            return true;
        }

        // otherwise run our own code and skip the original
        if (!__instance.hittingPlayer && !__instance.undeflectable && !__instance.decorative && __instance.speed != 0f && __instance.homingType == HomingType.None)
        {
            ___rb.velocity = __instance.transform.forward * __instance.speed;
        }
        if (__instance.decorative && __instance.transform.localScale.x < ___origScale.x)
        {
            ___aud.pitch = __instance.transform.localScale.x / ___origScale.x * 2.8f;
            __instance.transform.localScale = Vector3.Slerp(__instance.transform.localScale, ___origScale, Time.deltaTime * __instance.speed);
        }

        //if (__instance.precheckForCollisions)
        //{
        //    LayerMask layerMask = LayerMaskDefaults.Get(LMD.EnemiesAndEnvironment);
        //    layerMask = (int)layerMask | 4;
        //    if (Physics.SphereCast(__instance.transform.position, ___radius, ___rb.velocity.normalized, out var hitInfo, ___rb.velocity.magnitude * Time.fixedDeltaTime, layerMask))
        //    {
        //        __instance.transform.position = __instance.transform.position + ___rb.velocity.normalized * hitInfo.distance;

        //        MethodInfo meth = __instance.GetType().GetMethod("Collided", BindingFlags.NonPublic | BindingFlags.Instance);
        //        meth.Invoke(__instance, new object[] { hitInfo.collider });
        //        //Collided(hitInfo.collider);
        //    }
        //}

        // adapted from Nail.FixedUpdate()
        RaycastHit[] array = ___rb.SweepTestAll(___rb.velocity.normalized, ___rb.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Ignore);
        if (array == null || array.Length == 0)
        {
            return false;
        }
        Array.Sort(array, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
        for (int i = 0; i < array.Length; i++)
        {
            GameObject gameObject = array[i].transform.gameObject;
            if ((gameObject.layer == 10 || gameObject.layer == 11) && (gameObject.gameObject.CompareTag("Head") || gameObject.gameObject.CompareTag("Body") || gameObject.gameObject.CompareTag("Limb") || gameObject.gameObject.CompareTag("EndLimb") || gameObject.gameObject.CompareTag("Enemy")))
            {
                return false;
            }
            else
            {
                if (!LayerMaskDefaults.IsMatchingLayer(gameObject.layer, LMD.Environment) && gameObject.layer != 26 && !gameObject.CompareTag("Armor"))
                {
                    continue;
                }

                // bounce the ball
                //base.transform.position = array[i].point;
                Vector3 norm = array[i].normal;
                ___rb.velocity = Vector3.Reflect(___rb.velocity.normalized, array[i].normal) * ___rb.velocity.magnitude;
               
                // increase bounce counter
                PatchCerbThrow.orbBounces[id]++;

                // create a shockwave!
                GameObject wave = UnityEngine.Object.Instantiate(Plugin.shockwave, ___rb.transform.position, Quaternion.identity);
                PhysicalShockwave component = wave.GetComponent<PhysicalShockwave>();
                component.damage = 25;
                component.speed = 75f;
                component.maxSize = 100f;
                component.enemy = true;
                component.enemyType = EnemyType.Cerberus;
                component.transform.rotation = Quaternion.FromToRotation(component.transform.rotation * Vector3.up, norm);

                // create an explosion
                Explosion[] explosions = UnityEngine.Object.Instantiate(Plugin.explosion, ___rb.transform.position, Quaternion.identity).GetComponentsInChildren<Explosion>();
                foreach (Explosion component2 in explosions)
                {
                    component2.maxSize *= 1.5f;
                    component2.damage = Mathf.RoundToInt(__instance.damage);
                    component2.enemy = true;
                    component2.canHit = AffectedSubjects.PlayerOnly;
                    component2.enemyDamageMultiplier = 0;
                }

                MonoSingleton<StainVoxelManager>.Instance.TryIgniteAt(__instance.transform.position);

                break;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(Projectile))]
[HarmonyPatch("Start")]
class PatchCerbOrbStart
{
    static void Postfix(Projectile __instance)
    {
        int id = __instance.GetInstanceID();
        if(PatchCerbThrow.orbBounces.ContainsKey(id))
        {
            __instance.ignoreExplosions = true;
        }
    }
}

// make explosions ignore cerb balls so that bouncing works
[HarmonyPatch(typeof(Explosion))]
[HarmonyPatch("Collide")]
class PatchExplosionOrb
{
    static bool Prefix(Collider other, Explosion __instance)
    {
        // player-caused explosions will still affect cerb balls
        if(__instance.enemy == false)
        {
            return true;
        }

        Projectile component = other.GetComponent<Projectile>();
        if(component != null)
        {
            int id = component.GetInstanceID();
            if(PatchCerbThrow.orbBounces.ContainsKey(id))
            {
                return false;
            }
        }
        return true;
    }
}

// check the health of all other present cerbs, enrage if any are below half
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("Update")]
class PatchCerbEnrage
{
    static void Postfix(StatueBoss __instance)
    {
        // no need to check anything if already enraged
        if(__instance.enraged)
        {
            return;
        }

        int id = __instance.GetInstanceID();

        StatueBoss[] cerbs = (StatueBoss[]) Resources.FindObjectsOfTypeAll(typeof(StatueBoss));
        Console.WriteLine("there are " + cerbs.Length + " cerbs");
        foreach(StatueBoss c in cerbs)
        {
            int other_id = c.GetInstanceID();
            if(other_id == id)
            {
                continue;
            }

            var field = typeof(StatueBoss).GetField("st", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            Statue st = (Statue) field.GetValue(c);
            if(st != null)
            {
                if(st.health <= st.originalHealth/2)
                {
                    __instance.EnrageDelayed();
                    return;
                }
            }
        }
    }
}
