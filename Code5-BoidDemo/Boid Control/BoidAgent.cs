using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    public class BoidAgent : MonoBehaviour
    {
        BoidGroup agentGroup;
        public BoidGroup AgentGroup { get { return agentGroup; } }

        Collider2D agentCollider;
        public Collider2D AgentCollider { get { return agentCollider; } }

        [SerializeField]
        CircleCollider2D contextCollider;

        [SerializeField]
        SpriteRenderer sprite;

        public List<Transform> context = new List<Transform>();

        public bool frozen;

        // Start is called before the first frame update
        void Start()
        {
            agentCollider = GetComponent<Collider2D>();
        }

        public void Initialize(BoidGroup group, float contextRadius)
        {
            agentGroup = group;
            SetContextCollider(contextRadius);
        }

        public void Move(Vector2 velocity)
        {
            transform.up = velocity;
            transform.position += (Vector3)velocity * Time.deltaTime;
        }

        public void SetContextCollider(float radius)
        {
            contextCollider.radius = radius;
        }

        public void Freeze(bool eval)
        {
            frozen = eval;
        }

        public void Sprite(bool eval)
        {
            sprite.enabled = eval;
        }
    }
}