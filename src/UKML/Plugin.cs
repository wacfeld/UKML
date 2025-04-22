namespace UKML;

using System;
using System.Collections.Generic;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Reflection;
using UnityEngine.AI;
using System.Runtime.InteropServices;

//using UnityEngine.AddressableAssets.ResourceLocators;
//using UnityEngine.ResourceManagement.ResourceLocations;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "wacfeld.ukml";
    public const string PLUGIN_NAME = "ULTRAKILL Mustn't Live";
    public const string PLUGIN_VERSION = "0.4.2";

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
    // set hardDamageMultiplier to 50%
    static void Prefix(ref float hardDamageMultiplier)
    {
        hardDamageMultiplier = 0.5f;
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

        gameObject.GetComponent<Rigidbody>().AddForce(gameObject.transform.forward * 20000f);

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
                if(st.health < st.originalHealth/2)
                {
                    __instance.EnrageDelayed();
                    return;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("StompHit")]
class PatchCerbStomp
{
    static void Postfix(GameObject ___currentStompWave, AssetReference ___stompWave, StatueBoss __instance, EnemyIdentifier ___eid)
    {
        ___currentStompWave = UnityEngine.Object.Instantiate(___stompWave.ToAsset(), __instance.transform.position, Quaternion.identity);
        PhysicalShockwave component = ___currentStompWave.GetComponent<PhysicalShockwave>();
        component.transform.rotation = __instance.transform.rotation;
        component.transform.Rotate(Vector3.forward * 90, Space.Self);
        component.damage = 25;
        component.speed = 75f;
        if (component.TryGetComponent<AudioSource>(out var component2))
        {
            component2.enabled = false;
        }
        component.damage = Mathf.RoundToInt((float)component.damage * ___eid.totalDamageModifier);
        component.maxSize = 100f;
        component.enemy = true;
        component.enemyType = EnemyType.Cerberus;
    }
}

[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("Tackle")]
class PatchTackle
{
    static void Postfix(StatueBoss __instance, ref int ___extraTackles)
    {
        if(__instance.enraged)
        {
            ___extraTackles++;
        }
    }
}

[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("Dash")]
class PatchDash
{
    static void Postfix(GameObject ___currentStompWave, AssetReference ___stompWave, StatueBoss __instance, EnemyIdentifier ___eid, ref int ___extraTackles)
    {
        float angle = 45f;
        if(__instance.enraged)
        {
            if(___extraTackles == 0)
            {
                angle = 0f;
            }
            else
            {
                angle = (___extraTackles == 2) ? 45f : 135f;
            }
        }
        else
        {
            angle = (___extraTackles == 1) ? 45f : 135f;
        }

        ___currentStompWave = UnityEngine.Object.Instantiate(___stompWave.ToAsset(), __instance.transform.position, Quaternion.identity);
        PhysicalShockwave component = ___currentStompWave.GetComponent<PhysicalShockwave>();
        component.transform.rotation = __instance.transform.rotation;
        component.transform.Rotate(Vector3.forward * angle, Space.Self);
        component.damage = 25;
        component.speed = 75f;

        component.damage = Mathf.RoundToInt((float)component.damage * ___eid.totalDamageModifier);
        component.maxSize = 100f;
        component.enemy = true;
        component.enemyType = EnemyType.Cerberus;
    }
}

// halve inter-dash cooldown
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("StopDash")]
class PatchStopDash
{
    static void Postfix(StatueBoss __instance, ref float ___realSpeedModifier)
    {
        if (__instance.IsInvoking("DelayedTackle"))
        {
            __instance.CancelInvoke("DelayedTackle");
            
            float delay = 0.25f;
            if (!__instance.enraged && UnityEngine.Random.value > 0.75f)
            {
                delay += 0.5f;
            }
            __instance.Invoke("DelayedTackle", delay / ___realSpeedModifier);
        }
    }
}

// double attack cooldown rate
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("Update")]
class PatchCerbCooldown
{
    static void Postfix(StatueBoss __instance)
    {
        int n = (__instance.enraged) ? 3 : 1;
        for(int i = 0; i < n; i++)
        {
            if(__instance.attackCheckCooldown > 0f)
            {
                __instance.attackCheckCooldown = Mathf.MoveTowards(__instance.attackCheckCooldown, 0f, Time.deltaTime);
            }
        }

    }
}

// increase cerb animation speed
[HarmonyPatch(typeof(StatueBoss))]
[HarmonyPatch("SetSpeed")]
class PatchCerbAnim
{
    static void Postfix(StatueBoss __instance, Animator ___anim)
    {
        if(__instance.enraged)
        {
            ___anim.speed *= 1.5f;
        }
        else
        {
            ___anim.speed *= 1.2f;
        }
    }
}

// guttertanks enrage if punch whiffs, regardless of tripping or not
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("PunchStop")]
class PatchGTEnrage
{
    public static HashSet<int> enraged = new HashSet<int>();
    public static Dictionary<int, GameObject> effects = new Dictionary<int, GameObject>();
    static void Postfix(Guttertank __instance, ref bool ___punchHit, Machine ___mach)
    {
        if(!___punchHit)
        {
            int id = __instance.GetInstanceID();
            // if already enraged, no need to do anything
            if (enraged.Contains(id))
            {
                return;
            }

            // add to set of enraged guttertanks
            enraged.Add(id);

            // create enrage effect and put in dictionary
            Console.WriteLine("enraging!");
            GameObject enrageEffect = UnityEngine.Object.Instantiate(MonoSingleton<DefaultReferenceManager>.Instance.enrageEffect, ___mach.chest.transform);
            effects.Add(id, enrageEffect);
        }
    }
}

// remove the enragement effect from guttertanks upon death
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("Death")]
class PatchGTUnenrage
{
    static void Postfix(Guttertank __instance)
    {
        int id = __instance.GetInstanceID();
        if (PatchGTEnrage.effects.ContainsKey(id))
        {
            Console.WriteLine("destroying enrage effect");
            UnityEngine.Object.Destroy(PatchGTEnrage.effects[id]);
            PatchGTEnrage.effects.Remove(id);
        }

        if(PatchGTEnrage.enraged.Contains(id))
        {
            PatchGTEnrage.enraged.Remove(id);
        }
    }
}

// if the GT is enraged then we overwrite the original function and mark the fired rocket for large explosion
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("FireRocket")]
class PatchGTFire
{
    public static Dictionary<int, GameObject> enragedRocketEffects = new Dictionary<int, GameObject>();

    //static void Postfix()
    //{
    //    Console.WriteLine("FireRocket postfix");
    //}

    static bool Prefix(Guttertank __instance, Vector3 ___overrideTargetPosition, EnemyIdentifier ___eid, ref int ___difficulty, ref float ___shootCooldown)
    {
        //Console.WriteLine("FireRocket prefix");

        UnityEngine.Object.Instantiate(__instance.rocketParticle, __instance.shootPoint.position, Quaternion.LookRotation(___overrideTargetPosition - __instance.shootPoint.position));
        Grenade grenade = UnityEngine.Object.Instantiate(__instance.rocket, MonoSingleton<WeaponCharges>.Instance.rocketFrozen ? (__instance.shootPoint.position + __instance.shootPoint.forward * 2.5f) : __instance.shootPoint.position, Quaternion.LookRotation(___overrideTargetPosition - __instance.shootPoint.position));
        grenade.proximityTarget = ___eid.target;
        grenade.ignoreEnemyType.Add(___eid.enemyType);
        grenade.originEnemy = ___eid;
        if (___eid.totalDamageModifier != 1f)
        {
            grenade.totalDamageMultiplier = ___eid.totalDamageModifier;
        }
        if (___difficulty == 1)
        {
            grenade.rocketSpeed *= 0.8f;
        }
        else if (___difficulty == 0)
        {
            grenade.rocketSpeed *= 0.6f;
        }
        ___shootCooldown = UnityEngine.Random.Range(0.75f, 1.25f) - ((___difficulty >= 4) ? 0.5f : 0f);

        int id = __instance.GetInstanceID();
        if (PatchGTEnrage.enraged.Contains(id))
        {
            // add enragement effect to rocket
            Console.WriteLine("enraging rocket");
            GameObject enrageEffect = UnityEngine.Object.Instantiate(MonoSingleton<DefaultReferenceManager>.Instance.enrageEffect, grenade.rb.transform);
            AudioSource aud = enrageEffect.GetComponent<AudioSource>();
            aud.pitch = 3f;

            // add to dictionary
            int rocketId = grenade.GetInstanceID();
            if(!enragedRocketEffects.ContainsKey(rocketId))
            {
                enragedRocketEffects.Add(rocketId, enrageEffect);
            }
        }

        // skip original
        return false;
    }
}

// override Grenade.Explode() in order to modify rocket behaviour for guttertanks
[HarmonyPatch(typeof(Grenade))]
[HarmonyPatch("Explode")]
class PatchRocketExplode
{
    static void Postfix(Grenade __instance)
    {
        // destroy rocket enragement effect if present
        int id = __instance.GetInstanceID();
        if (PatchGTFire.enragedRocketEffects.ContainsKey(id))
        {
            UnityEngine.Object.Destroy(PatchGTFire.enragedRocketEffects[id]);
            PatchGTFire.enragedRocketEffects.Remove(id);
        }
    }
}

// ProximityExplosion() just calls Explode()
// we override it to implement enraged Guttertank rockets
[HarmonyPatch(typeof(Grenade))]
[HarmonyPatch("ProximityExplosion")]
class PatchRocketProxExplode
{
    static bool Prefix(Grenade __instance, ref bool ___exploded)
    {
        int id = __instance.GetInstanceID();
        // if it's not an enraged rocket allow the ordinary protocol to take over
        if(!PatchGTFire.enragedRocketEffects.ContainsKey(id))
        {
            return true;
        }

        float sizemult = 5f;

        if (___exploded)
        {
            return false;
        }
        ___exploded = true;
        int checkSize = Mathf.RoundToInt(3 * sizemult);

        MonoSingleton<StainVoxelManager>.Instance.TryIgniteAt(__instance.transform.position, checkSize);

        GameObject gameObject = UnityEngine.Object.Instantiate(__instance.explosion, __instance.transform.position, Quaternion.identity);
        Explosion[] components = gameObject.GetComponentsInChildren<Explosion>();
        foreach (Explosion explosion in components)
        {
            explosion.sourceWeapon = __instance.sourceWeapon;
            explosion.hitterWeapon = __instance.hitterWeapon;
            explosion.isFup = false;
            if (__instance.enemy)
            {
                explosion.enemy = true;
            }
            if (__instance.ignoreEnemyType.Count > 0)
            {
                explosion.toIgnore = __instance.ignoreEnemyType;
            }
            explosion.maxSize *= 1.5f * sizemult;
            explosion.speed *= 3f;
            if (__instance.totalDamageMultiplier != 1f)
            {
                explosion.damage = (int)((float)explosion.damage * __instance.totalDamageMultiplier);
            }
            if ((bool)__instance.originEnemy)
            {
                explosion.originEnemy = __instance.originEnemy;
            }
            if(explosion.damage != 0)
            {
                explosion.rocketExplosion = true;
            }
            else // get rid of 0 damage leading explosion
            {
                UnityEngine.Object.Destroy(explosion);
            }
        }
        //gameObject.transform.localScale *= sizemult;
        UnityEngine.Object.Destroy(__instance.gameObject);

        // destroy rocket enragement effect if present
        if (PatchGTFire.enragedRocketEffects.ContainsKey(id))
        {
            UnityEngine.Object.Destroy(PatchGTFire.enragedRocketEffects[id]);
            PatchGTFire.enragedRocketEffects.Remove(id);
        }

        // skip original
        return false;
    }
}

[HarmonyPatch]
class PatchLandmine
{
    // Explode() is overloaded so we have to identify it like this
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(Landmine),
            "Explode",
            new Type[] { typeof(bool) }
        );
    }

    public static HashSet<int> parriedByPlayer = new HashSet<int>();

    // override the original explode function so we can set the enemy flag properly
    static bool Prefix(bool super, Landmine __instance, ref bool ___exploded, GameObject ___superExplosion, GameObject ___explosion)
    {
        if(!___exploded)
        {
            // create explosion
            ___exploded = true;
            Explosion[] components = UnityEngine.Object.Instantiate(super ? ___superExplosion : ___explosion, __instance.transform.position, Quaternion.identity).GetComponentsInChildren<Explosion>();

            // if not parried by player, set enemy to true
            int id = __instance.GetInstanceID();
            bool enemy = !parriedByPlayer.Contains(id);
            foreach(Explosion explosion in components)
            {
                explosion.enemy = enemy;
            }

            UnityEngine.Object.Destroy(__instance.gameObject);
        }
        return false;
    }
}

[HarmonyPatch(typeof(Landmine))]
[HarmonyPatch("Parry")]
class PatchLandmineParry
{
    static void Postfix(Landmine __instance, GameObject ___parryZone, ref Vector3 ___movementDirection)
    {
        int id = __instance.GetInstanceID();
        if(!PatchLandmine.parriedByPlayer.Contains(id))
        {
            PatchLandmine.parriedByPlayer.Add(id);
        }

        // override disabling of parry zone after parry
        ___parryZone.SetActive(value: true);
    }
}

// override Guttertank.Update()
// when freezeframe active, replace fire rocket with mine punch
// TODO when enraged, add SRS cannonball attack
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("Update")]
class PatchGTUpdate
{
    public static Dictionary<int, bool> punchParryable = new Dictionary<int, bool>();
    public static HashSet<int> punchedMines = new HashSet<int>();

    static bool Prefix(Guttertank __instance, ref bool ___dead, EnemyIdentifier ___eid, ref bool ___inAction, ref bool ___overrideTarget,
        ref Vector3 ___overrideTargetPosition, ref bool ___trackInAction, ref bool ___moveForward, ref float ___lineOfSightTimer, ref float ___shootCooldown,
        ref float ___mineCooldown, ref int ___difficulty, ref float ___punchCooldown, Animator ___anim, NavMeshAgent ___nma, ref bool ___lookAtTarget, ref bool ___punching,
        SwingCheck2 ___sc, ref bool ___punchHit, Machine ___mach, Collider ___col)
    {
        if(___dead || ___eid.target == null)
        {
            return false;
        }
        if(___inAction)
        {
            Vector3 headPosition = ___eid.target.headPosition;
            if (___overrideTarget)
            {
                headPosition = ___overrideTargetPosition;
            }
            if(___trackInAction || ___moveForward)
            {
                __instance.transform.rotation = Quaternion.RotateTowards(__instance.transform.rotation, Quaternion.LookRotation(new Vector3(headPosition.x, __instance.transform.position.y, headPosition.z) - __instance.transform.position), (float)(___trackInAction ? 360 : 90) * Time.deltaTime);
            }
        }
        else
        {
            RaycastHit hitInfo;
            bool flag = !Physics.Raycast(__instance.transform.position + Vector3.up, ___eid.target.headPosition - (__instance.transform.position + Vector3.up), out hitInfo, Vector3.Distance(___eid.target.position, __instance.transform.position + Vector3.up), LayerMaskDefaults.Get(LMD.Environment));
            ___lineOfSightTimer = Mathf.MoveTowards(___lineOfSightTimer, flag ? 1 : 0, Time.deltaTime * ___eid.totalSpeedModifier);
            if (___shootCooldown > 0f)
            {
                ___shootCooldown = Mathf.MoveTowards(___shootCooldown, 0f, Time.deltaTime * ___eid.totalSpeedModifier);
            }
            if (___mineCooldown > 0f)
            {
                ___mineCooldown = Mathf.MoveTowards(___mineCooldown, 0f, Time.deltaTime * ((___lineOfSightTimer >= 0.5f) ? 0.5f : 1f) * ___eid.totalSpeedModifier);
            }
            if (___lineOfSightTimer >= 0.5f)
            {
                if (___difficulty <= 1 && Vector3.Distance(__instance.transform.position, ___eid.target.position) > 10f && Vector3.Distance(__instance.transform.position, ___eid.target.PredictTargetPosition(0.5f)) > 10f)
                {
                    ___punchCooldown = ((___difficulty == 1) ? 1 : 2);
                }
                if (___punchCooldown <= 0f && (Vector3.Distance(__instance.transform.position, ___eid.target.position) < 10f || Vector3.Distance(__instance.transform.position, ___eid.target.PredictTargetPosition(0.5f)) < 10f))
                {
                    var method = typeof(Guttertank).GetMethod("Punch", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(__instance, null);
                    //Punch();
                }
                else if (___shootCooldown <= 0f && Vector3.Distance(__instance.transform.position, ___eid.target.PredictTargetPosition(1f)) > 15f)
                {
                    // if freezefrome active, punch a mine
                    if (MonoSingleton<WeaponCharges>.Instance.rocketFrozen)
                    {
                        MinePunch(__instance, ref ___inAction, ___nma, ref ___trackInAction, ref ___lookAtTarget, ref ___punching,
                            ref ___shootCooldown, ref ___difficulty, ___anim, ___sc, ref ___punchHit, ___mach, ref ___overrideTargetPosition, ___eid,
                            ref ___overrideTarget, ___col);
                    }
                    // otherwise fire like normal
                    else
                    {
                        var method = typeof(Guttertank).GetMethod("PrepRocket", BindingFlags.NonPublic | BindingFlags.Instance);
                        method.Invoke(__instance, null);
                        //PrepRocket();
                    }
                }
            }
            if (!___inAction && ___mineCooldown <= 0f)
            {
                var method = typeof(Guttertank).GetMethod("CheckMines", BindingFlags.NonPublic | BindingFlags.Instance);
                //if (CheckMines())
                if ((bool) method.Invoke(__instance, null))
                {
                    var method2 = typeof(Guttertank).GetMethod("PrepMine", BindingFlags.NonPublic | BindingFlags.Instance);
                    method2.Invoke(__instance, null);
                    //PrepMine();
                }
                else
                {
                    ___mineCooldown = 0.5f;
                }
            }
        }
        ___punchCooldown = Mathf.MoveTowards(___punchCooldown, 0f, Time.deltaTime * ___eid.totalSpeedModifier);
        ___anim.SetBool("Walking", ___nma.velocity.magnitude > 2.5f);
        // skip original
        return false;
    }

    static void MinePunch(Guttertank __instance, ref bool ___inAction, NavMeshAgent ___nma, ref bool ___trackInAction, ref bool ___lookAtTarget, ref bool ___punching,
        ref float ___shootCooldown, ref int ___difficulty, Animator ___anim, SwingCheck2 ___sc, ref bool ___punchHit, Machine ___mach, ref Vector3 ___overrideTargetPosition,
        EnemyIdentifier ___eid, ref bool ___overrideTarget, Collider ___col)
    {
        Console.WriteLine("mine punch!");

        // play punch animation and sound and unparryable flash
        ___anim.Play("Punch", 0, 0f);
        UnityEngine.Object.Instantiate(__instance.punchPrepSound, __instance.transform);
        UnityEngine.Object.Instantiate(MonoSingleton<DefaultReferenceManager>.Instance.parryableFlash,
            ___sc.transform.position + __instance.transform.forward, __instance.transform.rotation).transform.localScale *= 5f;

        // set state variables
        ___inAction = true;
        ___nma.enabled = false;
        ___trackInAction = true;
        ___lookAtTarget = true;
        ___punching = true;
        ___punchHit = true;

        // set parryable in dictionary
        int id = __instance.GetInstanceID();
        if (!punchParryable.ContainsKey(id))
        {
            punchParryable.Add(id, true);
        }
        else
        {
            punchParryable[id] = true;
        }
        ___mach.parryable = true;        

        // TODO play parry sound

        // set shot cooldown as normal
        ___shootCooldown = UnityEngine.Random.Range(1.25f, 1.75f) - ((___difficulty >= 4) ? 0.5f : 0f);

        // predict player position
        PredictTargetMine(__instance, ___eid, ref ___overrideTarget, ref ___difficulty, ref ___overrideTargetPosition, ___col);
    }

    // a copy of Guttertank.PredictTarget() but without the parryable flash, and adjusted for the speed of a parried mine
    static void PredictTargetMine(Guttertank __instance, EnemyIdentifier ___eid, ref bool ___overrideTarget, ref int ___difficulty,
        ref Vector3 ___overrideTargetPosition, Collider ___col)
    {
        if (___eid.target != null)
        {
            ___overrideTarget = true;
            float num = 1f;
            if (___difficulty == 1)
            {
                num = 0.75f;
            }
            else if (___difficulty == 0)
            {
                num = 0.5f;
            }
            ___overrideTargetPosition = ___eid.target.PredictTargetPosition((UnityEngine.Random.Range(0.75f, 1f) + Vector3.Distance(__instance.shootPoint.position, ___eid.target.headPosition) / 150f) * num);
            if (Physics.Raycast(___eid.target.position, Vector3.down, 15f, LayerMaskDefaults.Get(LMD.Environment)))
            {
                ___overrideTargetPosition = new Vector3(___overrideTargetPosition.x, ___eid.target.headPosition.y, ___overrideTargetPosition.z);
            }
            bool flag = false;
            if (Physics.Raycast(__instance.aimBone.position, ___overrideTargetPosition - __instance.aimBone.position, out var hitInfo, Vector3.Distance(___overrideTargetPosition, __instance.aimBone.position), LayerMaskDefaults.Get(LMD.EnvironmentAndBigEnemies)) && (!hitInfo.transform.TryGetComponent<Breakable>(out var component) || !component.playerOnly))
            {
                flag = true;
                ___overrideTargetPosition = ___eid.target.headPosition;
            }
            if (!flag && ___overrideTargetPosition != ___eid.target.headPosition && ___col.Raycast(new Ray(___eid.target.headPosition, (___overrideTargetPosition - ___eid.target.headPosition).normalized), out hitInfo, Vector3.Distance(___eid.target.headPosition, ___overrideTargetPosition)))
            {
                ___overrideTargetPosition = ___eid.target.headPosition;
            }
        }
    }
}

// if our mine is marked as punched by a guttertank, immediately activate and parry it after startup
[HarmonyPatch(typeof(Landmine))]
[HarmonyPatch("Start")]
class PatchLandmineStart
{
    static void Postfix(Landmine __instance, Rigidbody ___rb, ref bool ___activated, GameObject ___parryZone,
        ref bool ___parried, ref Vector3 ___movementDirection)
    {
        int id = __instance.GetInstanceID();
        if (PatchGTUpdate.punchedMines.Contains(id))
        {
            PatchGTUpdate.punchedMines.Remove(id);

            // manually activate
            ___rb.isKinematic = false;
            ___rb.useGravity = true;
            ___activated = true;
            ___parryZone.SetActive(value: true);

            // manually parry
            ___parried = true;
            ___movementDirection = __instance.transform.forward;
            ___rb.useGravity = true; // redundant, oh well
            __instance.SetColor(new Color(0f, 1f, 1f));
            __instance.Invoke("Explode", 3f);
        }
    }
}


// if punchParryable[id] is set, create a landmine and hurl it at the player
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("PunchActive")]
class PatchGTPunchParryable
{
    static void Postfix(Guttertank __instance, SwingCheck2 ___sc, ref bool ___moveForward, ref bool ___trackInAction, ref Vector3 ___overrideTargetPosition, EnemyIdentifier ___eid,
        Machine ___mach)
    {
        Console.WriteLine("punch active!");
        int id = __instance.GetInstanceID();
        if (PatchGTUpdate.punchParryable.ContainsKey(id) && PatchGTUpdate.punchParryable[id])
        {
            // create a landmine with the same position and rotation as a rocket
            //Vector3 minePos = __instance.shootPoint.position;
            //Vector3 mineDirection = (___overrideTargetPosition - minePos).normalized;
            //minePos += mineDirection * 3.5f;
            //minePos -= Vector3.up;
            Vector3 minePos = ___mach.chest.transform.position;
            Vector3 mineDirection = (___overrideTargetPosition - minePos).normalized;
            minePos += mineDirection * 10f;

            Landmine mine = UnityEngine.Object.Instantiate(__instance.landmine, minePos,
                Quaternion.LookRotation(___overrideTargetPosition - minePos));
            //Landmine mine = UnityEngine.Object.Instantiate(__instance.landmine, __instance.shootPoint.position + __instance.shootPoint.forward * 2.5f,
                //Quaternion.LookRotation(___overrideTargetPosition - __instance.shootPoint.position));
            if (mine.TryGetComponent<Landmine>(out var component))
            {
                component.originEnemy = ___eid;

                // store the mine ID for later so it knows to parry itself right after Start()
                int mineId = component.GetInstanceID();
                PatchGTUpdate.punchedMines.Add(mineId);
            }
        }
    }
}

// after GT punch over reset punchParryable, and also clear parryable flag
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("PunchStop")]
class PatchGTPunchStopParryable
{
    static void Postfix(Guttertank __instance, Machine ___mach)
    {
        int id = __instance.GetInstanceID();
        if (PatchGTUpdate.punchParryable.ContainsKey(id))
        {
            PatchGTUpdate.punchParryable[id] = false;
            ___mach.parryable = false;
        }
    }
}

// if it was a mine punch that got parried, don't play the PunchStagger animation
[HarmonyPatch(typeof(Guttertank))]
[HarmonyPatch("GotParried")]
class PatchGTParried
{
    static bool Prefix(Guttertank __instance, Machine ___mach)
    {
        int id = __instance.GetInstanceID();
        if(PatchGTUpdate.punchParryable.ContainsKey(id) && PatchGTUpdate.punchParryable[id])
        {
            ___mach.parryable = false;
            return false;
        }
        return true;
    }
}

//[HarmonyPatch(typeof(Guttertank))]
//[HarmonyPatch("PredictTarget")]
//class PatchGTPredict
//{
//    static void Postfix()
//    {
//        Console.WriteLine("predicting target!");
//    }
//}

//[HarmonyPatch(typeof(Guttertank))]
//[HarmonyPatch("PrepRocket")]
//class PatchGTPrepRocket
//{
//    static void Prefix()
//    {
//        Console.WriteLine("PrepRocket prefix");
//    }
//    static void Postfix()
//    {
//        Console.WriteLine("PrepRocket postfix");
//    }
//}