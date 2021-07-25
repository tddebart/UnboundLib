﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Photon.Pun;
using TMPro;
using UnboundLib.GameModes;
using UnboundLib.Utils.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnboundLib.Networking
{
    internal static class SyncModClients
    {
        private static readonly float timeoutTime = 5f;
        private static readonly Vector3 offset = new Vector3(0f, 60f, 0f);

        private static List<string> clientSideGUIDs = new List<string>();

        private static Dictionary<int, string[]> extra = new Dictionary<int, string[]>();
        private static Dictionary<int, string[]> missing = new Dictionary<int, string[]>();
        private static Dictionary<int, string[]> mismatch = new Dictionary<int, string[]>();

        private static Dictionary<int, string[]> clientsServerSideMods = new Dictionary<int, string[]>();
        private static Dictionary<int, string[]> clientsServerSideGUIDs = new Dictionary<int, string[]>();
        private static Dictionary<int, string[]> clientsModVersions = new Dictionary<int, string[]>();

        private static List<string> hostsServerSideMods = new List<string>();
        private static List<string> hostsServerSideGUIDs = new List<string>();
        private static List<string> hostsModVersions = new List<string>();

        private static Dictionary<string, PluginInfo> loadedMods = new Dictionary<string, PluginInfo>();
        private static List<string> loadedGUIDs = new List<string>();
        private static List<string> loadedModNames = new List<string>();
        private static List<string> loadedVersions = new List<string>();

        internal static void RequestSync()
        {
            if (PhotonNetwork.OfflineMode) return;
            UnityEngine.Debug.Log("REQUESTING SYNC...");

            NetworkingManager.RPC(typeof(SyncModClients), "SyncLobby", new object[] { });
        }

        [UnboundRPC]
        internal static void SyncLobby()
        {
            Reset();
            LocalSetup();
            if (PhotonNetwork.IsMasterClient)
            {
                UnityEngine.Debug.Log("SYNCING...");
                CheckLobby();
                Unbound.Instance.StartCoroutine(Check());

            }
        }

        internal static System.Collections.IEnumerator Check()
        {
            bool timeout = false;
            float startTime = Time.time;
            while (clientsServerSideGUIDs.Keys.Count < PhotonNetwork.PlayerList.Except(new List<Photon.Realtime.Player> { PhotonNetwork.LocalPlayer }).ToList().Count)
            {
                UnityEngine.Debug.Log("WAITING");

                if (Time.time > startTime + timeoutTime)
                {
                    timeout = true;
                    break;
                }

                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);
            UnityEngine.Debug.Log("FINISHED WAITING.");
            FindDifferences();
            MakeFlags();
            yield break;
        }

        internal static void LocalSetup()
        {
            loadedMods = BepInEx.Bootstrap.Chainloader.PluginInfos;

            foreach (string ID in loadedMods.Keys)
            {
                if (!clientSideGUIDs.Contains(loadedMods[ID].Metadata.GUID))
                {
                    loadedGUIDs.Add(loadedMods[ID].Metadata.GUID);
                    loadedModNames.Add(loadedMods[ID].Metadata.Name);
                    loadedVersions.Add(loadedMods[ID].Metadata.Version.ToString());
                }
            }
        }

        internal static void CheckLobby()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                NetworkingManager.RPC(typeof(SyncModClients), "SendModList", new object[] {  });
            }
        }

        internal static void Reset()
        {
            clientsServerSideMods = new Dictionary<int, string[]>();
            clientsServerSideGUIDs = new Dictionary<int, string[]>();
            clientsModVersions = new Dictionary<int, string[]>();

            extra = new Dictionary<int, string[]>();
            missing = new Dictionary<int, string[]>();
            mismatch = new Dictionary<int, string[]>();

            hostsServerSideMods = new List<string>();
            hostsServerSideGUIDs = new List<string>();
            hostsModVersions = new List<string>();
            loadedGUIDs = new List<string>();
            loadedModNames = new List<string>();
            loadedVersions = new List<string>();
        }

        internal static void FindDifferences()
        {

            foreach (int actorID in clientsServerSideGUIDs.Keys)
            {
                missing[actorID] = hostsServerSideGUIDs.Except(clientsServerSideGUIDs[actorID]).ToArray();
                extra[actorID] = clientsServerSideGUIDs[actorID].Except(hostsServerSideGUIDs).ToArray();
                mismatch[actorID] = clientsServerSideGUIDs[actorID].Except(extra[actorID]).Except(missing[actorID]).Where(guid => HostVersionFromGUID(guid) != VersionFromGUID(actorID, guid)).ToArray();
            }

        }
        private static string HostVersionFromGUID(string GUID)
        {
            return hostsModVersions.Where((v, i) => hostsServerSideGUIDs[i] == GUID).FirstOrDefault();
        }
        private static string ModIDFromGUID(int actorID, string GUID)
        {
            return clientsServerSideMods[actorID].Where((v, i) => clientsServerSideGUIDs[actorID][i] == GUID).FirstOrDefault();
        }
        private static string VersionFromGUID(int actorID, string GUID)
        {
            return clientsModVersions[actorID].Where((v, i) => clientsServerSideGUIDs[actorID][i] == GUID).FirstOrDefault();
        }

        internal static void RegisterClientSideMod(string GUID)
        {
            if (!clientSideGUIDs.Contains(GUID)) { clientSideGUIDs.Add(GUID); }
        }

        internal static void MakeFlags()
        {
            UnityEngine.Debug.Log("MAKING FLAGS...");

            // add a host flag for the host
            NetworkingManager.RPC(typeof(SyncModClients), "AddFlags", new object[] { PhotonNetwork.LocalPlayer.ActorNumber, new string[] { "✓ " + PhotonNetwork.CurrentRoom.GetPlayer(PhotonNetwork.LocalPlayer.ActorNumber).NickName, "HOST" }, false });

            // if a player timed out, figure out which one(s) it was

            int[] timeoutIDs = PhotonNetwork.CurrentRoom.Players.Values.Select(p => p.ActorNumber).Except(clientsServerSideGUIDs.Keys).Except(new int[] { PhotonNetwork.LocalPlayer.ActorNumber }).ToArray();

            foreach (int timeoutID in timeoutIDs)
            {
                NetworkingManager.RPC(typeof(SyncModClients), "AddFlags", new object[] { timeoutID, new string[] { "✗ " + PhotonNetwork.CurrentRoom.GetPlayer(timeoutID).NickName, "UNMODDED"}, false });

            }

            foreach (int actorID in clientsServerSideGUIDs.Keys)
            {
                List<string> flags = new List<string>();

                if (missing[actorID].Length == 0 && extra[actorID].Length == 0 && mismatch[actorID].Length == 0)
                {
                    flags.Add("✓ " + PhotonNetwork.CurrentRoom.GetPlayer(actorID).NickName);
                    flags.Add("ALL MODS SYNCED");
                    UnityEngine.Debug.Log(PhotonNetwork.CurrentRoom.GetPlayer(actorID).NickName + " is synced!");

                    NetworkingManager.RPC(typeof(SyncModClients), "AddFlags", new object[] {actorID, flags.ToArray(), false});
                    continue;
                }
                else
                {
                    flags.Add("✗ " + PhotonNetwork.CurrentRoom.GetPlayer(actorID).NickName);
                }
                foreach (string missingGUID in missing[actorID])
                {
                    flags.Add("MISSING: " + ModIDFromGUID(actorID, missingGUID) + " (" + missingGUID + ") Version: "+VersionFromGUID(actorID, missingGUID));
                }
                foreach (string versionGUID in mismatch[actorID])
                {
                    flags.Add("VERSION: " + ModIDFromGUID(actorID, versionGUID) + " (" + versionGUID + ") Version: " + VersionFromGUID(actorID, versionGUID) + "\nHost has: " + HostVersionFromGUID(versionGUID));
                }
                foreach (string extraGUID in extra[actorID])
                {
                    flags.Add("EXTRA: " + ModIDFromGUID(actorID, extraGUID) + " (" + extraGUID + ") Version: " + VersionFromGUID(actorID, extraGUID));
                }
                NetworkingManager.RPC(typeof(SyncModClients), "AddFlags", new object[] { actorID, flags.ToArray(), true });
            }

            foreach (int actorID in PhotonNetwork.CurrentRoom.Players.Values.Select(p => p.ActorNumber).Except(clientsServerSideGUIDs.Keys).Except(new int[] { PhotonNetwork.LocalPlayer.ActorNumber }).ToArray())
            {
                NetworkingManager.RPC(typeof(SyncModClients), "AddFlags", new object[] { actorID, new string[] { "✗ " + PhotonNetwork.CurrentRoom.GetPlayer(actorID).NickName, "UNMODDED" }, true });
            }
        }

        [UnboundRPC]
        private static void AddFlags(int actorID, string[] flags, bool error)
        {
            UnityEngine.Debug.Log("ADDING FLAGS");

            // display the sync status of each player here

            // each player has a unique actorID, which is tied to their Nickname (displayed in the lobby) by PhotonNetwork.CurrentLobby.GetPlayer(actorID).NickName

            // AddFlags adds an array of strings for one actorID when called. the array of strings are the status and warning messages for syncing mods

            // the first entry in the array is a simple "Good" "Bad" (checkmark or X) and ideally would always be shown next to the player's name in the lobby

            // if (error=true) then the text should ideally be red

            // when a player hovers over (with mouse) the green/red check/X it should display a textbox or something with the full error/warning messages - each entry on a new line

            
            var nickName = PhotonNetwork.CurrentRoom.GetPlayer(actorID).NickName;

            // Check if player is using RWF
            if (UIHandler.instance.transform.Find("Canvas/PrivateRoom"))
            {
                // Make UI in RWF
                GameObject parent;
                var _uiHolder = MenuHandler.modOptionsUI.LoadAsset<GameObject>("uiHolder");
                var _checkmark =  MenuHandler.modOptionsUI.LoadAsset<GameObject>("checkmark");
                var _redx = MenuHandler.modOptionsUI.LoadAsset<GameObject>("redx");
                // Check if uiHolder has already been made
                if (!UIHandler.instance.transform.Find("Canvas/PrivateRoom/uiHolder(Clone)"))
                {
                    parent = GameObject.Instantiate(_uiHolder,UIHandler.instance.transform.Find("Canvas/PrivateRoom"));
                    parent.GetComponent<RectTransform>().position = new Vector3(-35, 18, 0);
                    parent.GetOrAddComponent<AutoUpdate>();
                }
                else
                {
                    parent = UIHandler.instance.transform.Find("Canvas/PrivateRoom/uiHolder(Clone)").gameObject;
                }

                GameObject playerObj;
                if (!parent.transform.Find(nickName))
                {
                    playerObj = GameObject.Instantiate(_uiHolder, parent.transform);
                    playerObj.name = nickName;
                }
                else
                {
                    playerObj = parent.transform.Find(nickName).gameObject;
                }

                if (!playerObj.transform.Find(nickName))
                {
                    var flag = flags[0];
                    if (flag.Contains("✓ "))
                    {
                        var check = GameObject.Instantiate(_checkmark, playerObj.transform);
                        check.GetComponent<RectTransform>().position = new Vector3(-34.5f, 18, 0);
                        var _hover = check.AddComponent<CheckHover>();
                        _hover.texts = flags;
                    } else if (flag.Contains("✗ "))
                    {
                        var redcheck = GameObject.Instantiate(_redx, playerObj.transform);
                        redcheck.GetComponent<RectTransform>().position = new Vector3(-34.5f, 18, 0);
                        var _hover = redcheck.AddComponent<CheckHover>();
                        _hover.texts = flags;
                    }
                    var text = MenuHandler.CreateText(nickName, playerObj, out var uGUI, 20, false, error ? Color.red : new Color(0.902f, 0.902f, 0.902f, 1f), null, null, TextAlignmentOptions.MidlineLeft );
                    text.name = nickName;
                    var hover = text.AddComponent<CheckHover>();
                    hover.texts = flags;
                    var uGUIMargin = uGUI.margin;
                    uGUIMargin.z = 1600;
                    uGUI.margin = uGUIMargin;
                    uGUI.fontSizeMin = 25;
                    var layout = text.AddComponent<LayoutElement>();
                    layout.preferredWidth = 300;
                    layout.preferredHeight = 100;
                    
                    var rectTrans = text.GetComponent<RectTransform>();
                    rectTrans.pivot = Vector2.zero;
                }
                Unbound.Instance.ExecuteAfterSeconds(0.1f, () =>
                {
                    parent.GetComponent<VerticalLayoutGroup>().SetLayoutVertical();
                });
                GameModeManager.AddHook(GameModeHooks.HookGameStart, handler => disableSyncModUI(parent));
            }
            else
            {
                // Remove old ui if it exists
                if (UIHandler.instance.transform.Find("Canvas/LoadingScreen/Match found/uiHolder"))
                {
                    GameObject.Destroy(UIHandler.instance.transform.Find("Canvas/LoadingScreen/Match found/uiHolder").gameObject);
                }
                // Make UI without RWF
                var parent = new GameObject("uiHolder");
                parent.transform.parent = UIHandler.instance.transform.Find("Canvas/LoadingScreen/Match found");
                parent.transform.localScale = new Vector3(1, 1, 1);
                var localposition = parent.transform.localPosition;
                localposition = new Vector3(-950, 400, 0);
                parent.transform.localPosition = localposition;
                var text = MenuHandler.CreateText(nickName, parent, out _);
                text.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 200);
                text.GetComponent<RectTransform>().pivot = Vector2.zero;
            }
            //var UIHolder = new GameObject();
        }

        private static IEnumerator disableSyncModUI(GameObject parent)
        {
            GameObject.Destroy(parent);
            yield break;
        } 

        [UnboundRPC]
        private static void SendModList()
        {
            UnityEngine.Debug.Log("SENDING MODS...");
            NetworkingManager.RPC(typeof(SyncModClients), "ReceiveModList", new object[] { loadedGUIDs.ToArray(), loadedModNames.ToArray(), loadedVersions.ToArray(), PhotonNetwork.LocalPlayer.ActorNumber });
        }

        [UnboundRPC]
        private static void ReceiveModList(string[] serverSideGUIDs, string[] serverSideMods, string[] versions, int actorID)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                UnityEngine.Debug.Log("RECEIVING MODS...");

                if (PhotonNetwork.LocalPlayer.ActorNumber == actorID)
                {
                    hostsServerSideGUIDs = serverSideGUIDs.ToList();
                    hostsServerSideMods = serverSideMods.ToList();
                    hostsModVersions = versions.ToList();
                }
                else
                {
                    clientsServerSideGUIDs[actorID] = serverSideGUIDs;
                    clientsServerSideMods[actorID] = serverSideMods;
                    clientsModVersions[actorID] = versions;
                }

            }
        }
    }

    internal class AutoUpdate : MonoBehaviour
    {
        private readonly float delay = 1f;
        private float startTime;

        void Start()
        {
            startTime = Time.time;
        }
        void Update()
        {
            if (Time.time > startTime + delay)
            {
                startTime = Time.time;
                SyncModClients.MakeFlags();
            }
        }
    }

    internal class CheckHover : MonoBehaviour
    {
        public string[] texts;

        private GUIStyle guiStyleFore;

        private void Start()
        {
            guiStyleFore = new GUIStyle();
            guiStyleFore.normal.textColor = Color.white;  
            guiStyleFore.alignment = TextAnchor.UpperLeft ;
            guiStyleFore.wordWrap = true;
            var background = new Texture2D(1, 1);
            background.SetPixel(0,0, Color.gray);
            background.Apply();
            guiStyleFore.normal.background = background;
            guiStyleFore.fontSize = 20;
        }
        private void OnGUI()
        {
            if (IsOverThisObject() && texts != Array.Empty<string>() && Input.mousePosition.x < Screen.width/4)
            {
                GUILayout.BeginArea(new Rect(Input.mousePosition.x + 25, Screen.height - Input.mousePosition.y + 25, 300, 50*texts.Length));
                GUILayout.BeginVertical();
                foreach (var t in texts)
                {
                    GUILayout.Label (t, guiStyleFore);
                }
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        private bool IsOverThisObject()
        {
            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);
            foreach (var raycast in raycastResults)
            {
                if (raycast.gameObject.name == gameObject.name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}