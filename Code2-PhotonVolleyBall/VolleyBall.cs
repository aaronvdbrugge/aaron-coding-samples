using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolleyBall : MonoBehaviourPun {

   [Header("Sprites")]
   public SpriteRenderer ballSprite;
   [SerializeField] private SpriteRenderer indicatorSprite;
   [SerializeField] private SpriteRenderer ringSprite;

   [Header("Particle effect")]
   [SerializeField] private GameObject confettiPrefab;
   [SerializeField] private Vector2 confettiSpawnPosition;

   [Header("Settings")]
   [SerializeField] private float collisionHeight = 1.0f;

   [Tooltip("First hit takes this long to reach other side")]
   [SerializeField] private float arcTimeSlow = 4.0f;
   [SerializeField] private float arcTimeFast = 1.0f;
   [SerializeField] private float arcHeight = 5.0f;

   [Tooltip("In this many hits, it reaches arcTimeFast")]
   public int maxSpeedupHits = 10;

   [Tooltip("Change indicator color based on lerpProgress, which goes from 0 to 1 (then e.g. green to red)")]
   [SerializeField] private Gradient indicatorRingColors;
   [SerializeField] private float indicatorRingStartScale = 1.0f;
   [SerializeField] private float indicatorRingEndScale = 0.5f;

   [Tooltip("Location of the Indicator on the lerpProgress of 0-1 scale")]
   public float indicatorStepRatio = 0.9f;

   [Tooltip("Arena placement data")]
   [SerializeField] private ArenaManager arenaManager;

   [Tooltip("Offset the sprite from gameObject. Used by effects class.")]
   public Vector2 spriteOffset = Vector2.zero;

   [Header("Runtime Network replicated")]
   public int hitStreak;
   [SerializeField] private Vector2 lerpStart;
   [SerializeField] private Vector2 lerpEnd;
   [SerializeField] private float lerpProgress = 1; // make sure to start this at 1 so the ball doesn't move at the start

   // Calculate 'zPos' based on lerp values
   public float zPosVolleyball => GetZPos();

   [SerializeField] private IsoSpriteSorting ballSpriteSorter;

   // Start values

   // Store original position
   private Vector2 originalPosition;
   // Original y value, so we can add z value from arc to it
   private float ballSpriteYOriginal;
   // Original sprite sorting value, so we can add from the arc value to it
   private float ballSortingYOriginal;
   // Wait until player joins a room
   private bool netInitialized = false;
   // Used by player that does not own this PhotonView to retrieve state information
   private float lastAskedForState = 0.0f;

   private void Awake() {
      ballSpriteSorter = ballSprite.GetComponent < IsoSpriteSorting > ();
   }

   private void Start() {
      originalPosition = transform.position;
      arenaManager.originalPosition = originalPosition;
      ballSpriteYOriginal = ballSprite.transform.localPosition.y;
      ballSortingYOriginal = ballSpriteSorter.SorterPositionOffset.y;
      lerpProgress = 1.0f;
      lerpEnd = transform.position;
   }

   // Handle the OnTriggerStay2D behavior
   void OnTriggerStay2D(Collider2D other) {
      // If ball started on north side, and is still on north side, it cannot be hit
      if (arenaManager.IsOnNorthSide(lerpStart) == arenaManager.IsOnNorthSide(transform.position) && lerpProgress < 1) {
         return;
      }

      // Colliding player
      Player_Controller_Mobile playerController = other.GetComponent<Player_Controller_Mobile>();

      // Only the controlling client will set target destination
      if (playerController != null && photonView.IsMine && MainSceneNetwork.initialized) {
         // Prevent double hits on the same side
         if (arenaManager.IsOnNorthSide(playerController.transform.position) != arenaManager.IsOnNorthSide(transform.position))
            return;

         Player_Hop playerHop = playerController.playerHop;

         // Take into consideration jump height to calculate overlap
         float playerZ = playerHop.zPos;
         bool collision = false;
         // If collision is above volleyball pos and within collision height
         if (playerZ >= zPosVolleyball && playerZ <= zPosVolleyball + collisionHeight) {
            collision = true;
         }
         // Check if collision is above player pos and within collision height
         else if (zPosVolleyball >= playerZ && zPosVolleyball <= playerZ + playerHop.collisionHeight) {
            collision = true;
         }

         if (collision) {
            Vector2 newLerpStart = transform.position;
            Vector2 newLerpEnd = arenaManager.GetRandomLandingPosition(!arenaManager.IsOnNorthSide(transform.position));

            // Flip lerp progress into opposite direction
            float newLerpProgress = 0;
            if (lerpProgress < 1) {
               // If ball did not reach ground yet, subtract that from lerpProgress to make ball reach
               // enemy player faster.
               // e.g. If ball was at 0.8 lerpProgress and player hits it, it will reach enemy '0.2' faster than usual.
               newLerpProgress = 1 - lerpProgress;
            }

            photonView.RPC("HitBall", RpcTarget.All,
               new object[] {
                  newLerpStart,
                  newLerpEnd,
                  newLerpProgress,
                  hitStreak + 1
               });
         }
      }
   }

   // Gameplay related code
   void FixedUpdate() {
      if (!netInitialized) {
         if (MainSceneNetwork.initialized) {
            // This player does not own the ball, so request its state
            if (!photonView.IsMine && Time.time - lastAskedForState >= 0.5 f) {
               // Request sending the ball state to this player
               photonView.RPC("RequestBallState", RpcTarget.MasterClient, new object[] {
                  PhotonNetwork.LocalPlayer.ActorNumber
               });
               lastAskedForState = Time.time;
            }
            // The player owns the ball
            else {
               netInitialized = true;
            }
         }
      } else {
         // Ball hit the ground
         if (lerpProgress >= 1.0f && hitStreak > 0) {
            if (photonView.IsMine && MainSceneNetwork.initialized) {
               hitStreak = 0;
               photonView.RPC("ResetHitStreak", RpcTarget.All);
            }
         }

         // Volleyball animation
         transform.position = Vector2.Lerp(lerpStart, lerpEnd, lerpProgress);
         // LerpProgress always goes up
         lerpProgress = Mathf.Min(1.0f, lerpProgress + Time.fixedDeltaTime / GetTotalArcTime());
      }
   }

   // Visual related code
   private void Update() {
      if (lerpProgress < 1) {
         // Place indicator near landing spot
         indicatorSprite.transform.position = Vector3.Lerp(lerpStart, lerpEnd, indicatorStepRatio);

         // Change color
         Color spriteColor = indicatorRingColors.Evaluate(lerpProgress);
         indicatorSprite.color = spriteColor;
         ringSprite.color = spriteColor;

         // Scale ring
         ringSprite.transform.localScale = Mathf.Lerp(indicatorRingStartScale, indicatorRingEndScale, lerpProgress) * Vector2.one;
      }

      // Move ball sprite, it is separate from the main gameobject which has the collider
      ballSprite.transform.localPosition = new Vector2(
         0,
         ballSpriteYOriginal + zPosVolleyball
      ) + spriteOffset;

      // Calculate sprite sorting offset
      ballSpriteSorter.SorterPositionOffset.y = ballSortingYOriginal - (zPosVolleyball / ballSpriteSorter.transform.localScale.y);
   }

   public float GetZPos() {
      // Negative parabolic
      // Max jump y==1 on x==0.5
      float hopParabolic = -Mathf.Pow(2 * lerpProgress - 1, 2) + 1;

      // Get actual arc height at that point
      float hopHeight = hopParabolic * arcHeight;

      return hopHeight;
   }

   [PunRPC]
   public void HitBall(Vector2 lerpStart, Vector2 lerpEnd, float lerpProgress, int hitStreak) {
      // Ball state received
      netInitialized = true;
      this.lerpStart = lerpStart;
      this.lerpEnd = lerpEnd;
      this.lerpProgress = lerpProgress;
      this.hitStreak = hitStreak;
   }

   // Ball hits the ground
   [PunRPC]
   public void ResetHitStreak() {
      hitStreak = 0;
      Instantiate(confettiPrefab, confettiSpawnPosition, Quaternion.identity);
   }

   // Called when a new player joins
   [PunRPC]
   public void RequestBallState(int requestingPlayerActorNum) {
      if (photonView.IsMine && MainSceneNetwork.initialized) {
         Photon.Realtime.Player player = PhotonUtil.GetPlayerByActorNumber(requestingPlayerActorNum);
         if (player != null)
            photonView.RPC("HitBall", player, new object[] {
               lerpStart,
               lerpEnd,
               lerpProgress,
               hitStreak
            }); // Give them the info they requested
      }
   }

   // Speed up total arc time value every successive hit, to speed up the ball
   // Min value arcTimeSlow (e.g. 4), max value arcTimeFast (e.g. 1)
   public float GetTotalArcTime() {
      return Mathf.Lerp(arcTimeSlow, arcTimeFast, Mathf.Min(1.0f, hitStreak / (float) maxSpeedupHits));
   }

   void OnDrawGizmos() {
      // Visualize collision height
      Gizmos.color = Color.yellow;
      Gizmos.DrawLine(transform.position, transform.position + new Vector3(0, collisionHeight, 0));
   }
}
