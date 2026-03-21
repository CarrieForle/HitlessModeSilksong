using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker;
using SkongGamemodes;
using System.IO;
using System.Linq;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace HitlessMode;
using static HitlessMode.HitlessModePlugin;

[BepInDependency("dervorce.hkss.gamemodemanager")]
[BepInDependency("org.silksong-modding.i18n")]
[BepInAutoPlugin(id: "io.github.carrieforle.hitlessmode")]
public partial class HitlessModePlugin : BaseUnityPlugin
{
	internal static GameModeManager.GameModeData FragileMode { private set; get; }
	internal static GameModeManager.GameModeData GlassMode { private set; get; }
	internal static ConfigEntry<bool> ConfigShowHp;
	internal static bool IsCustomMode => FragileMode.Enabled || GlassMode.Enabled || ConfigApplyAllModes;
	internal static new ManualLogSource Logger;
	private GameObject eventGo;
	private Transform? hp;
	private Transform? Hp
	{
		get
		{
			if (hp == null)
			{
				hp = GameCameras.SilentInstance.hudCanvasSlideOut.transform.Find("Health");
				PatchBlueHealthFsm();
			}

			return hp;
		}

		set => hp = value;
	}

	//=Debug variables==
	internal const bool ConfigDontDie = false;
	internal const bool ConfigApplyAllModes = false;
	//====

	private void Start()
	{
		Logger = base.Logger;

		eventGo = new GameObject("HideHealthEventRegister");
		DontDestroyOnLoad(eventGo);
		var er = eventGo.AddComponent<EventRegister>();
		er.SwitchEvent("HUD COMING IN");
		er.ReceivedEvent += TryHideHealth;
		er = eventGo.AddComponent<EventRegister>();
		er.SwitchEvent("HERO RESPAWNED");
		er.ReceivedEvent += TryHideHealth;
		er = eventGo.AddComponent<EventRegister>();
		er.SwitchEvent("SPOOL APPEAR");
		er.ReceivedEvent += TryHideHealth;

		var harmony = new Harmony(Id);
		harmony.PatchAll();

		ConfigShowHp = Config.Bind(
			"General",
			"ShowHp",
			false,
			Localized("SHOW_HP_OPTION")
		);

		FragileMode = GameModeManagerPlugin.Instance.Manager.Init(
			this,
			"Fragile Soul", // This is used as a key internally and not localizable.
			Localized("FRAGILE_MODE_DESCRIPTION"),
			steel: false
		);

		GlassMode = GameModeManagerPlugin.Instance.Manager.Init(
			this,
			"Glass Soul",
			Localized("GLASS_MODE_DESCRIPTION"),
			steel: true
		);
	}

	// HideHudMasks is called before this so no need to initialize hp
	internal void TryHideBlueHealth()
	{
		if (!IsCustomMode || ConfigShowHp.Value || Hp == null)
		{
			return;
		}

		ToggleBlueHealthRenderer(false);
	}

	private void TryHideHealth()
	{
		if (!IsCustomMode || ConfigShowHp.Value || Hp == null)
		{
			return;
		}

		ToggleHealthRenderer(false);

		foreach (Transform t in Hp)
		{
			var child = t.gameObject;
			
			// Disable Fractured Mask
			if (child.name == "Health 1")
			{
				var fm = child.transform.Find("Fractured Mask");

				if (fm)
				{
					fm.gameObject.SetActive(false);
					Logger.LogDebug("Disabled Fractured Mask");
				}
			}
		}
	}
	
	// filePath is relative to assembly location
	private Sprite? LoadSpriteFromFile(string filePath)
	{
		string fullPath = Path.Combine(Path.GetDirectoryName(Info.Location), filePath);
		Sprite? res = null;
		try
		{
			var payload = File.ReadAllBytes(fullPath);
			var texture = new Texture2D(0, 0);
			if (texture.LoadImage(payload))
			{
				var rect = new Rect(0, 0, texture.width, texture.height);
				var pivot = new Vector2(.5f, .5f);
				res = Sprite.Create(texture, rect, pivot);
			}
		}
		catch
		{

		}

		if (res == null)
		{
			Logger.LogWarning($"Failed to load sprite frorm \"{fullPath}\"");
		}

		return res;
	}

	private void ToggleBlueHealthRenderer(bool enable)
	{
		ToggleRenderer("Blue Health", enable);
	}

	private void ToggleHealthRenderer(bool enable)
	{
		ToggleRenderer("Health", enable);
	}

	private void ToggleRenderer(string name, bool enable)
	{
		if (Hp == null)
		{
			return;
		}

		int count = 0;
		foreach (Transform t in Hp)
		{
			var go = t.gameObject;
			if (!go.name.StartsWith(name))
			{
				continue;
			}

			var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
			foreach (var r in renderers)
			{
				r.enabled = enable;
			}

			count += renderers.Length;
		}

		Logger.LogDebug($"Toggle({enable}) {count} \"{name}\" renderer");
	}

	// It would be too early to hide on "ADD BLUE HEALTH" event, so we directly patch the FSM.
	private void PatchBlueHealthFsm()
	{
		if (Hp == null)
		{
			return;
		}

		var fsm = Hp.GetComponents<PlayMakerFSM>()
			.FirstOrDefault(fsm => fsm.FsmName == "Blue Health Control");
		if (fsm == null)
		{
			return;
		}

		var state = fsm.fsm.States
			.FirstOrDefault(s => s.name == "REALLY Approaching Max?");
		if (state == null)
		{
			return;
		}

		var hideBlueHealthAction = new HideBlueHealthAction(this);
		state.actions = [hideBlueHealthAction, .. state.actions];
		hideBlueHealthAction.Init(state);
		Logger.LogDebug("Patched Blue Health FSM");
	}

	internal static LocalisedString Localized(string key)
	{
		return new LocalisedString($"Mods.{Id}", key);
	}

	internal static bool IsSlotCustomMode(int saveSlotIndex)
	{
		return PlayerPrefs.GetInt($"{GlassMode.Name}_{saveSlotIndex}") == 1 || PlayerPrefs.GetInt($"{FragileMode.Name}_{saveSlotIndex}") == 1;
	}
}

class HideBlueHealthAction(HitlessModePlugin plugin) : FsmStateAction
{
	public override void OnEnter()
	{
		plugin.TryHideBlueHealth();
		Finish();
	}
};

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.TakeHealth))]
class Patch_DieInOneHit
{
	static void Postfix(PlayerData __instance, ref int amount, ref bool hasBlueHealth, ref bool allowFracturedMaskBreak)
	{
		if (!IsCustomMode || ConfigDontDie)
		{
			return;
		}

		__instance.health = 0;
	}
}

[HarmonyPatch(typeof(SaveSlotButton), nameof(SaveSlotButton.PresentSaveSlot))]
class Patch_ZeroProfileHealthBar
{
	static void Postfix(SaveSlotButton __instance)
	{
		if (!ConfigShowHp.Value && IsSlotCustomMode(__instance.SaveSlotIndex))
		{
			foreach (var image in __instance.healthSlots.healthImages)
			{
				image.gameObject.SetActive(false);
			}
		}
	}
}

[HarmonyPatch(typeof(EventRegister), nameof(EventRegister.SendEvent), [typeof(string), typeof(GameObject)])]
class Patch_EventRegister
{
	static void Postfix(EventRegister __instance, string eventName, GameObject excludeGameObject)
	{
		var goStr = excludeGameObject != null ? excludeGameObject.ToString() : "null";
		Logger.LogDebug($"EventRegister.SendEvent(\"{eventName}\", {goStr})");
	}
}