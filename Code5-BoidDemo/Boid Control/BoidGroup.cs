using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    public class BoidGroup : MonoBehaviour
    {
        public BoidAgent rocketPrefab;
        List<BoidAgent> rockets = new List<BoidAgent>();
        public BoidBehavior behavior;

        [Tooltip("Set this value to change the amount of rockets generated at the beginning.")]
        [Range(10, 100)]
        public int startingCount = 50;
        [Tooltip("Set this value to change the density of the rockets generated at the beginning.")]
        [Range(0.01F, 0.1F)]
        public float AgentDensity = 0.08f;
        [Tooltip("This value moves the offset of the rocket spawn point.")]
        [SerializeField]
        Vector2 spawnOffset;
        [Tooltip("How long to wait in seconds for spawning a new rocket after one is destroyed.")]
        public Vector2 waitRangeExtraSpawn;

        [Tooltip("This value sets the overall speed of the rockets.")]
        [Range(0.1f, 10f)]
        public float driveFactor = 10f;
        [Tooltip("The maximum speed with which rockets can travel.")]
        [Range(0.1f, 10f)]
        public float maxSpeed = 5f;
        [Tooltip("This value determines the radius of the CircleCollider2D which keeps track of neighbouring rockets.")]
        [Range(0.1f, 2f)]
        public float neighborRadius = 0.5f;
        [Tooltip("The context radius is multiplied by this factor to get the avoidance radius.")]
        [Range(0f, 2f)]
        public float avoidanceRadiusMultiplier = 0.5f;

        bool movingTo;
        Vector2 targetPointAllRockets;
        int generationCounter;
        float squareMaxSpeed;
        float squareNeighborRadius;
        float squareAvoidanceRadius;
        public float SquareAvoidanceRadius { get { return squareAvoidanceRadius; } }

        public static BoidGroup Instance;

        private void Awake()
        {
            Instance = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            squareMaxSpeed = maxSpeed * maxSpeed;
            squareNeighborRadius = neighborRadius * neighborRadius;
            squareAvoidanceRadius = squareNeighborRadius * avoidanceRadiusMultiplier * avoidanceRadiusMultiplier;

            for (int i = 0; i < startingCount; i++)
            {
                Spawn();
            }
        }

        // Update is called once per frame
        void Update()
        {
            foreach (BoidAgent rocket in rockets)
            {
                // Don't move the frozen rockets.
                if (rocket.frozen)
                    continue;

                List<Transform> context = rocket.context;

                // Manually move to this point if it is active. For example, from pressing the finger on the screen for Android/iOS.
                Vector2 move = new Vector2(0, 0);

                if (movingTo)
                {
                    move = targetPointAllRockets;
                    move -= (Vector2)rocket.transform.position;
                }
                else
                    move = behavior.CalculateMove(rocket, context, this);

                move *= driveFactor;
                if (move.sqrMagnitude > squareMaxSpeed)
                {
                    move = move.normalized * maxSpeed;
                }
                rocket.Move(move);
            }
        }

        public void SpawnNewRocket()
        {
            StartCoroutine(SpawningNewRocket(Random.Range(waitRangeExtraSpawn.x, waitRangeExtraSpawn.y)));
        }

        IEnumerator SpawningNewRocket(float delay)
        {
            yield return new WaitForSeconds(delay);

            Spawn();
        }

        public void RemoveRocket(BoidAgent agent)
        {
            if (rockets.Contains(agent))
                rockets.Remove(agent);
        }

        void Spawn()
        {
            // Don't spawn more than allowed
            if (rockets.Count > startingCount)
                return;

            generationCounter++;
            BoidAgent newRocket = Instantiate(
                    rocketPrefab,
                    spawnOffset + (Random.insideUnitCircle * startingCount * AgentDensity),
                    Quaternion.Euler(Vector3.forward * Random.Range(0f, 360f)),
                    transform
                    );
            newRocket.name = "Rocket " + (generationCounter + 1);
            newRocket.Initialize(this, neighborRadius);
            rockets.Add(newRocket);
        }

        public void MoveTo(Vector2 point)
        {
            targetPointAllRockets = point;
        }

        public void MoveAll(bool eval)
        {
            movingTo = eval;
        }
    }
}