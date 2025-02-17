using System;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using LLScreen;
using LLBML;
using LLBML.States;
using LLBML.Players;

namespace QuickRematch
{

    public static class CharacterRepicker_Patch
    {

        [HarmonyPatch(typeof(ScreenPlayersStage), nameof(ScreenPlayersStage.OnOpen))]
        [HarmonyPostfix]
        public static void SaveCharacter()
        {
            QuickRematch.StoreCharacterSelected(Player.GetLocalPlayer());
        }

        private static bool doRefresh = false;
        [HarmonyPatch(typeof(HDLIJDBFGKN), "GDHHGDONCKN")] // GameStatesLobbyOnline.ReceivedPlayerState
        [HarmonyPrefix]
        public static bool ReceivePlayerState_Prefix(JMLEHJLKPAC CNPOFPNNPAB, bool IDPADEAGKPJ)
        {
            bool inResults = IDPADEAGKPJ;
            if (inResults)
                return true;
            PlayerLobbyState pls = CNPOFPNNPAB;
            if (pls.playerNr == Player.LocalPlayerNumber)
            {
                doRefresh = 
                    pls.character != (Character)QuickRematch.storedCharacter.Value ||
                    pls.variant != (CharacterVariant)QuickRematch.storedVariant.Value ||
                    !pls.ready;
            }
            return true;
        }

        [HarmonyPatch(typeof(HDLIJDBFGKN), "GDHHGDONCKN")] // GameStatesLobbyOnline.ReceivedPlayerState
        [HarmonyPostfix]
        public static void ReceivePlayerState_ReselectCharacter(JMLEHJLKPAC CNPOFPNNPAB, bool IDPADEAGKPJ)
        {
            bool inResults = IDPADEAGKPJ;
            if (inResults)
                return;
            if (doRefresh)
            {
                QuickRematch.RestoreCharacter(Player.GetLocalPlayer());
                QuickRematch.QuickReready();
                doRefresh = false;
            }
        }
    }
    public static class QuickRematch_Patch
    {
        [HarmonyPatch(typeof(PostScreen), nameof(PostScreen.ShowButtonsNow))]
        [HarmonyPostfix]
        public static void RematchAfterGains(PostScreen __instance)
        {
            if (!QuickRematch.autoRematch.Value)
                return;

            QuickRematch.Log.LogInfo("Rematching");

            switch (__instance.resultButtons)
            {
                case NIPJFJKNGHO.DLPDHJFPKMJ: //ResultButtons.REMATCH_QUIT
                    GameStates.Send(new Message(Msg.SEL_REMATCH, -1, 1, null, -1));
                    break;
                case NIPJFJKNGHO.PJGPNEKNIGJ: //ResultButtons.CONTINUE
                case NIPJFJKNGHO.DNDFPMJJMJC: //ResultButtons.CONTINUE_QUIT
                    GameStates.Send(Msg.START, -1, -1);
                    break;
                default:
                    GameStates.Send(new Message(Msg.SEL_REMATCH, -1, 0, null, -1));
                    break;
            }
            QuickRematch.Log.LogInfo("Rematched");
        }

        [HarmonyPatch(typeof(PostScreen), nameof(PostScreen.CFillXpBar))]
        [HarmonyPostfix]
        public static IEnumerator DisableXpGains(IEnumerator incoming)
        {
            QuickRematch.Log.LogInfo("StoppedXpGains_PassPost");
            yield break;
        }

        [HarmonyPatch(typeof(CPNJEILDILH), nameof(CPNJEILDILH.PEORFKFKGGGG))]
        [HarmonyPostfix]
        public static IEnumerator DisableCurrencyGains(IEnumerator incoming)
        {
            QuickRematch.Log.LogInfo("StoppedCurrencyGains_PassPost");
            yield break;
        }
    }
}
