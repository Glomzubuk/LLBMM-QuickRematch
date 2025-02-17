using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using LLBML;
using LLBML.States;
using LLBML.Players;
using LLBML.GameEvents;
using LLBML.Utils;

namespace QuickRematch
{
    [BepInPlugin(PluginInfos.PLUGIN_ID, PluginInfos.PLUGIN_NAME, PluginInfos.PLUGIN_VERSION)]
    [BepInDependency(LLBML.PluginInfos.PLUGIN_ID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(TEXMOD_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("no.mrgentle.plugins.llb.modmenu", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("LLBlaze.exe")]
    class QuickRematch : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; } = null;
        public static QuickRematch Instance { get; private set; } = null;

        public const string TEXMOD_GUID = "no.mrgentle.plugins.llb.texturemod";
        public static BaseUnityPlugin texmodInstance = null;
        public static MethodInfo TM_getSkinMethod = null;
        public static MethodInfo TM_setSkinMethod = null;

        public static ConfigEntry<bool> autoRematch;
        public static ConfigEntry<bool> autoSelectCharacter;
        public static ConfigEntry<bool> autoReready;
        public static ConfigEntry<bool> reselectRandom;
        public static ConfigEntry<bool> storeCharacterOnRelaunch;
        public static ConfigEntry<int> storedCharacter;
        public static ConfigEntry<int> storedVariant;
        public static ConfigEntry<string> storedSkin;

        public static Character characterSelected = Character.NONE;
        public static CharacterVariant variantSelected = CharacterVariant.DEFAULT;
        public static byte[] skinSelected = null;

        void Awake()
        {
            Instance = this;
            Log = this.Logger;
            InitModOptions(this.Config);

            var harmoInstance = new Harmony(PluginInfos.PLUGIN_ID);
            Logger.LogInfo("Patching QuickRematch");
            harmoInstance.PatchAll(typeof(QuickRematch_Patch));
            Logger.LogInfo("Patching CharacterRepicker");
            harmoInstance.PatchAll(typeof(CharacterRepicker_Patch));
        }

        void Start()
        {
            Logger.LogInfo($"{PluginInfos.PLUGIN_NAME} Started");

            if (storeCharacterOnRelaunch.Value)
            {
                characterSelected = (Character)storedCharacter.Value;
                variantSelected = (CharacterVariant)storedVariant.Value;
                skinSelected = Convert.FromBase64String(storedSkin.Value);
            }

            LLBML.Utils.ModDependenciesUtils.RegisterToModMenu(this.Info);
            LobbyEvents.OnLobbyReady += (o, a) => {
                RestoreCharacter(Player.GetLocalPlayer());
                QuickReready();
            };
            LobbyEvents.OnUserCharacterPick += OnUserCharacterPick;
            LobbyEvents.OnUserSkinClick += OnUserSkinClick;

            if (ModDependenciesUtils.IsModLoaded(TEXMOD_GUID))
            {
                var TMInfos = BepInEx.Bootstrap.Chainloader.PluginInfos[TEXMOD_GUID];
                if (TMInfos.Metadata.Version.Major >= 2 && TMInfos.Metadata.Version.Minor >= 3)
                {
                    texmodInstance = BepInEx.Bootstrap.Chainloader.PluginInfos[TEXMOD_GUID].Instance;
                    TM_getSkinMethod = AccessTools.Method(texmodInstance.GetType(), "External_GetSkinHashFromPlayer", new [] { typeof(int) });
                    TM_setSkinMethod = AccessTools.Method(texmodInstance.GetType(), "External_AssignSkinToPlayer", new[] { typeof(int), typeof(byte[]) });
                }
            }
        }

        void InitModOptions(ConfigFile config)
        {
            autoRematch = config.Bind<bool>("Toggles", "autoRematch", true);
            autoSelectCharacter = config.Bind<bool>("Toggles", "autoSelectCharacter", true);
            autoReready = config.Bind<bool>("Toggles", "autoReready", true);
            reselectRandom = config.Bind<bool>("Toggles", "reselectRandom", true);
            storeCharacterOnRelaunch = config.Bind<bool>("Toggles", "storeCharacterOnRelaunch", false);
            storedCharacter = config.Bind<int>("Storage", "storedCharacter", (int)Character.NONE, new ConfigDescription("", null, "modmenu_hidden"));
            storedVariant = config.Bind<int>("Storage", "storedVariant", (int)CharacterVariant.DEFAULT, new ConfigDescription("", null, "modmenu_hidden"));
            storedSkin = config.Bind<string>("Storage", "storedSkin", "", new ConfigDescription("", null, "modmenu_hidden"));
        }

        private void OnUserCharacterPick(PlayersCharacterButton pcb, OnUserCharacterPickArgs args)
        {
            this.StartCoroutine(SaveCharacterAndVariantLater(Player.GetLocalPlayer()));
        }

        private void OnUserSkinClick(PlayersSelection ps, OnUserSkinClickArgs args)
        {
            if (args.clickerNr == -1 || (args.clickerNr == Player.LocalPlayerNumber && args.toSkinNr == args.clickerNr))
            {
                this.StartCoroutine(SaveCharacterAndVariantLater(Player.GetLocalPlayer()));
            }
        }

        private IEnumerator SaveCharacterAndVariantLater(Player player)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            //TODO shitty fix, find a better way
            StoreCharacterSelected(player);
        }

        public static void StoreCharacterSelected(Player player)
        {
            if (player.CharacterSelectedIsRandom && reselectRandom.Value)
            {
                characterSelected = Character.RANDOM;
                variantSelected = CharacterVariant.DEFAULT;
            }
            else
            {
                characterSelected = player.Character;
                variantSelected = player.CharacterVariant;
            }
            StoreCharacterAndVariant(characterSelected, variantSelected);
        }



        public static void StoreCharacterAndVariant(Character character, CharacterVariant variant = CharacterVariant.DEFAULT)
        {
            characterSelected = character;
            variantSelected = variant;

            if (texmodInstance != null)
            {
                try
                {
                    skinSelected = TM_getSkinMethod.Invoke(texmodInstance, new object[] { Player.GetLocalPlayer().nr }) as byte[];
                }
                catch (Exception e)
                {
                    Log.LogError("Caught exception trying to get the skin of the current player: " + e);
                }
            }

            Log.LogInfo("Saving char: " + QuickRematch.characterSelected + " | " + QuickRematch.variantSelected);
            if (storeCharacterOnRelaunch.Value)
            {
                storedCharacter.Value = (int)characterSelected;
                storedVariant.Value = (int)variantSelected;
                storedSkin.Value = Convert.ToBase64String(skinSelected ?? new byte[0]);
            }
        }

        public static void RestoreCharacter(Player player)
        {
            if (!autoSelectCharacter.Value || characterSelected == Character.NONE)
                return;

            Log.LogInfo("Restoring char to " + characterSelected + " | " + variantSelected);
            GameStates.DirectProcess(new Message(Msg.SEL_CHAR, player.nr, (int)characterSelected));
            //player.Character = characterSelected;
            if (characterSelected != Character.RANDOM && variantSelected != CharacterVariant.DEFAULT)
            {
                Log.LogInfo("Restoring variant as well");
                player.CharacterVariant = variantSelected;
            }

            if (texmodInstance != null && skinSelected?.Length > 0 )
            {
                try
                {
                    TM_setSkinMethod.Invoke(texmodInstance, new object[] { Player.GetLocalPlayer().nr, skinSelected });
                }
                catch (Exception e)
                {
                    Log.LogError("Caught exception trying to set the skin of the current player: " + e);
                }
            }
            GameStatesLobbyUtils.RefreshLocalPlayerState();
        }


        public static void QuickReready()
        {
            if (autoReready.Value)
            {
                Log.LogInfo("Quick Readying");
                if (GameStates.IsInOnlineLobby())
                {
                    GameStatesLobbyUtils.MakeSureReadyIs(true, false);
                    Player.GetLocalPlayer().ready = true;
                }
                GameStatesLobbyUtils.RefreshLocalPlayerState();
            }
            else
            {
                Log.LogDebug("ReReady was disabled.");
            }
        }
    }
}
