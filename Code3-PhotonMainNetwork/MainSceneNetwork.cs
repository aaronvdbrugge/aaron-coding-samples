using System.Collections;
using System.Collections.Generic;
using Covalent.Scripts.System;
using Covalent.Scripts.Util.Native_Proxy;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEditor;
using UnityEngine;

public class MainSceneNetwork : MonoBehaviourPunCallbacks {

   // Static fields

   public static bool initialized = false;
   // Partner player ID
   public static int partnerPlayer = -1;
   public static bool amPrimaryPlayer = false;
   // Called to leave the game after waiting for Photon to disconnect
   static bool wantsToLeave = false;

   #region Public Fields

   [Header("References")]
   [Tooltip("Handles room joining logic (making sure both users go into the same room")]
   public TeamRoomJoin teamRoomJoin;

   [Tooltip("Will be fed to EnterGameArea automatically if createPlayer hasn't been called from native.")]
   public PopupManager popupManager;
   public GameObject playerPrefab;

   [Tooltip("We'll reference the nonPremiumSlots stored here to choose an initial skin.")]
   public InventoryPanel inventoryPanel;

   public TMP_Text partnerDisconnectText;
   // Class handling camera panning
   public CameraPanning cameraPanning;
   public DebugSettings debugSettings;

   [Header("Settings")]
   public string gameVersion = "1";

   [Tooltip("Only used in Debug mode")]
   public string sandboxRoomName = "SANDBOX";

   [Tooltip("Retry attempt long after disconnection")]
   public float reconnectTime = 65.0f;

   [Tooltip("Amount of seconds to try each reconnect")]
   public float reconnectInterval = 10.0f;

   [Tooltip("Immediate reconnect attempt")]
   public float initialReconnectDelay = 1.0f;

   [Tooltip("Kill player if they're backgrounded this long.")]
   public float maxBackgroundedTime = 60f;

   [Tooltip("Countdown to when the player disconnects because their partner disconnected.")]
   public float partnerDisconnectTime = 60.9f;

   [Tooltip("Change GUI window if partner is taking a long time to connect")]
   public float partnerTakingLongTime = 60.0f;

   [Tooltip("If the player hasn't chosen an avatar, they get a randomized full skin out of this pool")]
   public TextAsset defaultSkinsTextAsset;

   [Header("Runtime")]
   [Tooltip("Keep playing for testing purposes if partner disconnects")]
   public bool disablePartnerDisconnectForDebug = false;

   // Rejoin this previous room when losing connection
   public string previousRoom;

   public string[] defaultSkins // Parsed from text asset
   {
      get {
         // Tokenize/split and clean up default skins string into array
         if (_defaultSkins == null)
            _defaultSkins = StringUtil.TokenizeNewlineSeparated(defaultSkinsTextAsset.text);
         return _defaultSkins;
      }
   }
   string[] _defaultSkins = null;

   #endregion

   #region Private Fields

   [SerializeField]
   private Agora_Manager agoraManager;
   private bool needsToJoinRoom;

   // Reconnect logic
   private bool reconnecting = false;
   private float reconnectTimer = 0;

   // Initial connect
   private bool waitingForFirstPartnerConnection;
   private bool hadPartnerConnected;
   [SerializeField] private Vector3 startingSpawnPoint;
   [SerializeField] private Limbo limbo;

   // Return player to place before disconnect
   private bool gotLastKnownPlayerPosition = false;
   private Vector3 lastKnownPlayerPosition = Vector3.zero; // We'll use this so we can try to put the player in the same place after we disconnect.

   // Set timestamp for when app is backgrounded for autodisconnect
   private bool backgrounded = false;
   private System.DateTime dateTimeBackgrounded;

   // Don't reconnect if disconnected from inactivity
   private bool disconnectedDueToInactivity = false;

   // Variables for waiting for partner to connect
   private bool firstWaitForPartner = true;
   private float firstWaitForPartnerTimer = 0;
   private float partnerDisconnectTimer = 0;
   private float timeOut;
   private bool partnerHasDisconnected;

   private Vector3 gotoWhenPartnerArrives;

   #endregion

   void Awake() {
      PhotonPeer.RegisterType(typeof (AvatarConfig), 255, AvatarConfig.Serialize, AvatarConfig.Deserialize);
   }

   private void Start() {
      EnterGameArea();
   }

   private void FixedUpdate() {
      if (timeOut > 0 f) {
         timeOut -= Time.fixedDeltaTime;
         if (timeOut < 0) {
            PlayerDidLeaveGame();
         }
      }

      if (wantsToLeave && PhotonNetwork.NetworkClientState == ClientState.Disconnected) {
         // Player is allowed to leave
         DoLeaveGameActual();
      }

      // This logic only runs for the primary player, as a 'partner' sets teamRoomJoin.isWaitingForFriend
      if (reconnecting && !disconnectedDueToInactivity && !teamRoomJoin.isWaitingForFriend) {
         Reconnecting();
      }

      WaitForPartner();
      PartnerDisconnectCheck();
   }

   // Connect funtcion
   public void Connect() {
      // Before connecting, set up the user ID.
      // This will allow the match to find which room we're in.
      if (!PlayerManager.Instance.IsSandboxMode) {
         PhotonNetwork.AuthValues = new AuthenticationValues();
         PhotonNetwork.AuthValues.UserId = PlayerManager.Instance.SessionUser.user.id.ToString();
      }

      PhotonNetwork.ConnectUsingSettings();
      PhotonNetwork.GameVersion = gameVersion;
      needsToJoinRoom = true;
      timeOut = 0f;
   }

   // Enter the Game Area
   public void EnterGameArea() {
      PlayerPrefs.SetString("name", PlayerManager.Instance.SessionUser.user.name);
      PlayerPrefs.SetInt("id", PlayerManager.Instance.SessionUser.user.id);
      PlayerPrefs.SetString("partyId", PlayerManager.Instance.SessionUser.partyId);

      // Determine the ID of our partner player.
      // partyID is in the format  123:456
      // Partner player is the ID in this string that isn't our ID

      // Not in sandbox mode
      if (!PlayerManager.Instance.IsSandboxMode) {
         string[] strIds = PlayerManager.Instance.SessionUser.partyId.Split(':');
         bool testMode = false;
         waitingForFirstPartnerConnection = true;
         partnerPlayer = -1;

         foreach(string strID in strIds) {
            if (int.Parse(strID) != PlayerManager.Instance.SessionUser.user.id) // It's not this player's Id
            {
               partnerPlayer = int.Parse(strID);
            }
         }

         // A partner was not found
         if (partnerPlayer == -1) {
            testMode = true;
         }

         // Determine if this is the primary player (first player of the party id string)
         amPrimaryPlayer = testMode || (PlayerManager.Instance.SessionUser.user.id.ToString() == strIds[0]);
      } else {
         // In sandbox mode
         // This player is primary and this player is partner
         partnerPlayer = PlayerManager.Instance.SessionUser.user.id;
         amPrimaryPlayer = true;
      }

      PlayerManager.Instance.IsSecondaryPlayer = !amPrimaryPlayer;

      // Use the flow of "reconnecting" (without Disconnect) to connect for "reconnectTime"-amount of seconds
      reconnecting = true;
      reconnectTimer = 0.0f;
   }

   // Handle the Reconnecting
   private void Reconnecting() {
      reconnectTimer += Time.fixedDeltaTime;
      // We reach the end of waiting to reconnect
      if (reconnectTimer >= reconnectTime) {
         popupManager.SetToPopup("disconnected");
      } else if (Mathf.Floor((reconnectTimer - initialReconnectDelay) / reconnectInterval) >
         Mathf.Floor((reconnectTimer - initialReconnectDelay - Time.fixedDeltaTime) / reconnectInterval)) {
         // Runs Connect() after initialReconnectDelay once, then every reconnectInterval
         Connect();
      }
   }

   // Wait for a Partner
   private void WaitForPartner() {
      if (waitingForFirstPartnerConnection && Player_Controller_Mobile.mine.playerPartner.GetPartner() != null) {
         waitingForFirstPartnerConnection = false;
         hadPartnerConnected = true;
      }

      // Wait for partner to join
      bool partnerWaitPopups = false;
      if (initialized && !disconnectedDueToInactivity && firstWaitForPartner) {
         if (Player_Controller_Mobile.mine.playerPartner.GetPartner() != null) {
            firstWaitForPartner = false;
            // Enable camera panning
            cameraPanning.enabled = true;
            Player_Controller_Mobile.mine.transform.position = gotoWhenPartnerArrives;
         } else {
            partnerWaitPopups = true;
         }
      }

      // This is secondary player, waiting for first to join; show waiting popup
      if (teamRoomJoin.isWaitingForFriend && !disconnectedDueToInactivity && firstWaitForPartner &&
         PhotonNetwork.IsConnectedAndReady)
         partnerWaitPopups = true;

      // Show waiting popup
      if (partnerWaitPopups) {
         // Player connected, but our partner is not connected
         // Display the first popup, until it's been a while, then display the second popup
         firstWaitForPartnerTimer += Time.fixedDeltaTime;
         if (firstWaitForPartnerTimer < partnerTakingLongTime)
            popupManager.SetToPopup("waiting_for_partner");
         else
            popupManager.SetToPopup("partner_long_time");
      }
   }

   // Check for a Disconnected Partner
   private void PartnerDisconnectCheck() {
      // Logic for partner disconnecting
      if (hadPartnerConnected && !disablePartnerDisconnectForDebug &&
         Player_Controller_Mobile.mine.playerPartner.GetPartner() == null) {
         // Only run if player is not disconnected and not waiting
         if (initialized && !disconnectedDueToInactivity) {
            partnerHasDisconnected = true;

            // If Leave popup is not open
            if (popupManager.curPopup != "leave_ok")
               popupManager.SetToPopup("disconnecting_partner");

            partnerDisconnectTimer += Time.fixedDeltaTime;

            // Time to disconnect.
            if (partnerDisconnectTimer >= partnerDisconnectTime) {
               // Use "disconnectedDueToInactivity" logic to prevent reconnecting
               disconnectedDueToInactivity = true;
               firstWaitForPartner = false;
               cameraPanning.enabled = true;
               popupManager.SetToPopup("disconnected_partner");
               PhotonNetwork.Disconnect();
               agoraManager.DisconnectWithoutRetry();
            } else {
               partnerDisconnectText.text = $ "YOU WILL LEAVE THE ARCADE IN 0:{(int)(partnerDisconnectTime - partnerDisconnectTimer)}";
            }
         }
      } else {
         if (partnerHasDisconnected) {
            // Reset
            partnerDisconnectTimer = 0;
            // Close popup
            if (popupManager.curPopup == "disconnecting_partner")
               popupManager.SetToPopup("");
            partnerHasDisconnected = false;
         }
      }
   }

   // Join the Room
   public void JoinRoom() {
      // Should look for a room
      if (string.IsNullOrEmpty(previousRoom)) {
         // Handles room joining logic
         teamRoomJoin.myId = PlayerManager.Instance.SessionUser.user.id.ToString();
         teamRoomJoin.matchId = partnerPlayer.ToString();
         teamRoomJoin.amPrimaryPlayer = amPrimaryPlayer;

         if (string.IsNullOrEmpty(sandboxRoomName) || PlayerManager.Instance.IsSandboxMode == false)
            teamRoomJoin.StartJoin(); // Starts the process of real matchmaking
         else
            teamRoomJoin.StartJoinSandbox();
      } else {
         // Connect to the previous room; (re)create if it no longer exists because it became empty
         PhotonNetwork.JoinOrCreateRoom(previousRoom, teamRoomJoin.GetRoomOptions(), TypedLobby.Default);
      }

      needsToJoinRoom = false;
   }

   // Player chose to leave
   public static void PlayerDidLeaveGame() {
      PhotonNetwork.Disconnect();
      wantsToLeave = true;
   }

   // Called after Photon has disconnected and player chose to leave
   void DoLeaveGameActual() {
#if !UNITY_EDITOR
      NativeProxy.PlayerDidLeaveGame();
#endif
      // Reset static values
      wantsToLeave = false; // reset static value
      initialized = false; // reset static value
      partnerPlayer = -1;
      amPrimaryPlayer = false;

      // Clean up after Agora. Ready it for a possible different voice chat
      AgoraConnectionManager.Instance.LeaveChannel();

      SceneLoadManager.Instance.LeaveGameArea();
   }

   private void InitializePlayer() {
      if (Player_Controller_Mobile.mine == null) {
         // Set AvatarConfig data to be sent to other players with OnPhotonInstantiate
         object[] initArray = new object[] {
            new AvatarConfig(), PlayerManager.Instance.SessionUser.user.name, PlayerManager.Instance.SessionUser.user.id, partnerPlayer
         };

         bool savedAvatarConfig = PlayerPrefs.HasKey("AvatarConfig");
         if (savedAvatarConfig) {
            AvatarConfig config = JsonUtility.FromJson < AvatarConfig > (PlayerPrefs.GetString("AvatarConfig"));

            // Handle the special case that they used to have premium, but don't anymore.
            // If they're using a premium skin, they'll have to re-randomize.
            if (!PlayerManager.Instance.SessionUser.user.isPaidPremium) {
               foreach(string premium in inventoryPanel.premiumSkins) {
                  if (config.IsUsingSkin(premium)) {
                     PlayerPrefs.DeleteKey("AvatarConfig");
                     savedAvatarConfig = false;
                  }
               }
            }

            // Save config to data to send
            if (savedAvatarConfig) {
               initArray[0] = config;
            }
         }

         if (!savedAvatarConfig) {
            // The player has not explicitly selected a skin.
            // The goal is to get an even distribution of the default non-premium skins.
            // Choose from the least used skin types.

            // Skin string code to usage count
            Dictionary < string, int > skinCounts = new Dictionary < string, int > ();

            // Foreach pair ActorId to Photon's Player class
            foreach(var player in PhotonNetwork.CurrentRoom.Players) {
               if (player.Value.CustomProperties["AvatarConfig"] != null) {
                  AvatarConfig playerConfig = (AvatarConfig) player.Value.CustomProperties["AvatarConfig"];
                  if (!skinCounts.ContainsKey(playerConfig.fullSkin)) {
                     skinCounts.Add(playerConfig.fullSkin, 1);
                  } else
                     skinCounts[playerConfig.fullSkin]++;
               }
            }

            // Find the minimum amount of skins used from skin types
            int minimum = int.MaxValue;
            foreach(string skin in defaultSkins) {
               if (!skinCounts.ContainsKey(skin))
                  minimum = 0;
               else
                  minimum = Mathf.Min(minimum, skinCounts[skin]);
            }

            // Now choose randomly among skins that were at this minimum value
            List < string > randSkinPool = new List < string > ();
            foreach(string skin in defaultSkins) {
               if (!skinCounts.ContainsKey(skin) || skinCounts[skin] <= minimum)
                  randSkinPool.Add(skin);
            }

            // Set the fullSkin value of AvatarConfig initArray[0]
            ((AvatarConfig)(initArray[0])).fullSkin = randSkinPool[Random.Range(0, randSkinPool.Count)];
         }

         Vector3 spawnPoint;

         if (firstWaitForPartner)
            spawnPoint = limbo.gameObject.transform.position;
         else
            spawnPoint = gotLastKnownPlayerPosition ? lastKnownPlayerPosition : startingSpawnPoint;

         PhotonNetwork.Instantiate(playerPrefab.name, spawnPoint, Quaternion.identity, 0, initArray);

         // Set LocalPlayer's CustomProperties so other players can get this data
         ExitGames.Client.Photon.Hashtable me = new ExitGames.Client.Photon.Hashtable();
         me.Add("myJSON", PlayerManager.Instance.SessionUserJson);
         PhotonNetwork.LocalPlayer.SetCustomProperties(me, null, null);

         initialized = true;
      }
   }

   // Wrappers
   private static void UpdatePlayersInRoom(string[] unityJSONList, int count) {
#if !UNITY_EDITOR
      NativeProxy.UpdatePlayersInRoom(unityJSONList, count);
#endif
   }

   public static void FailureToConnect(string error) {
#if !UNITY_EDITOR
      NativeProxy.FailureToConnect(error);
#endif
   }

   private static void FailureToJoinRoom(string error) {
#if !UNITY_EDITOR
      NativeProxy.FailureToJoinRoom(error);
#endif
   }

   #region MonoBehaviour CallBacks

   private void OnApplicationFocus(bool focused) {
#if !UNITY_EDITOR
      if (focused)
         AppWillEnterForeground();
      else
         AppDidEnterBackground();
#endif
   }

   // Called on PhotonNetwork.NetworkClientState == ClientState.Joined
   public override void OnJoinedRoom() {
      // Save room name in case of disconnect.
      previousRoom = PhotonNetwork.CurrentRoom.Name;
      reconnecting = false;
      InitializePlayer();
      // Call UpdatePlayerList for Native
      UpdatePlayerList();
   }

   public override void OnConnectedToMaster() {
      NetworkManager.Instance.Connected = true;

      if (needsToJoinRoom)
         JoinRoom(); // use name of the current scene to determine room.
      else
         FailureToConnect("Failed to connect to Photon Server");
   }

   // Disconnected from Photon
   // It's possible this runs from unwanted disconnect, in that case we do not leave yet
   public override void OnDisconnected(DisconnectCause cause) {
      NetworkManager.Instance.Connected = false;

      // Save last known position for reconnecting
      lastKnownPlayerPosition = Player_Controller_Mobile.mine.transform.position;
      gotLastKnownPlayerPosition = true;

      initialized = false;
      needsToJoinRoom = false;

      // Player already confirmed they want to leave
      if (wantsToLeave)
         DoLeaveGameActual();
      else if (!reconnecting) {
         reconnecting = true;
         reconnectTimer = 0.0f;
      }

      if (reconnecting)
         popupManager.SetToPopup("reconnecting");
   }

   public override void OnJoinRoomFailed(short returnCode, string message) {
      if (returnCode == ErrorCode.GameDoesNotExist)
         previousRoom = null; // Don't try to reconnect a second time to the room the player was connected to

      FailureToJoinRoom($"Failed to join room. Error Code: {returnCode} Error Message: {message}");
   }

   // Called when custom player-properties are changed
   public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) {
      UpdatePlayerList();
   }

   public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) {
      UpdatePlayerList();
   }

   #endregion

   #region NativeApp Functions

   public void UpdatePlayerList() {
      List < string > playerJsons = new List < string > ();

      // Local player JSON data
      playerJsons.Add(PlayerManager.Instance.SessionUserJson);

      foreach(Photon.Realtime.Player player in PhotonNetwork.PlayerList) {
         if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber &&
            player.CustomProperties.ContainsKey("myJSON")) {
            playerJsons.Add((string) player.CustomProperties["myJSON"]);
         }
      }

      // Send information to Native app
      UpdatePlayersInRoom(playerJsons.ToArray(), playerJsons.Count);
   }

   #endregion

   [ContextMenu("Simulate application foregrounded")]
   public void AppWillEnterForeground() {
      Debug.Log("Application foregrounded.");
      if (backgrounded) {
         backgrounded = false;

         // Calculate total time backgrounded
         float seconds = (float)(System.DateTime.Now - dateTimeBackgrounded).TotalSeconds;

         if (seconds > maxBackgroundedTime) {
            disconnectedDueToInactivity = true;
            initialized = false;
            popupManager.SetToPopup("disconnected_inactivity");
            PhotonNetwork.Disconnect();
            // Disconnect Agora along with Photon.
            agoraManager.DisconnectWithoutRetry();
         }
      }
   }

   [ContextMenu("Simulate application backgrounded")]
   public void AppDidEnterBackground() {
      // Make sure all Remote Procedure Calls go out
      PhotonNetwork.SendAllOutgoingCommands();
      backgrounded = true;
      dateTimeBackgrounded = System.DateTime.Now;
   }

   // Skip waiting for a partner for debugging
   public void DisableWaitForPartner() {
      firstWaitForPartner = false;
      cameraPanning.enabled = true;
      disablePartnerDisconnectForDebug = true;
      Player_Controller_Mobile.mine.transform.position = gotoWhenPartnerArrives;
   }

   // For testing purposes to easily clear PLayerPrefs
   [ContextMenu("Clear PlayerPrefs")]
   public void ClearPlayerPrefs() {
      PlayerPrefs.DeleteAll();
   }

}
