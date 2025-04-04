namespace UKML;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "wacfeld.ukml";
    public const string PLUGIN_NAME = "ULTRAKILL Mustn't Live";
    public const string PLUGIN_VERSION = "0.0.0";

    /// <summary> We need to have an instance of this in order to do patches </summary>
    readonly Harmony harmony = new(PLUGIN_GUID);
    
    public ManualLogSource Log => Logger;

    private void Awake()
    {
        harmony.PatchAll();
        Log.LogInfo($"Loaded {PLUGIN_NAME}");
    }

}