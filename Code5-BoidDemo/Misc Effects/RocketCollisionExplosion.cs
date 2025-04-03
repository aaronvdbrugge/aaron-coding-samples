using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    public class RocketCollisionExplosion : MonoBehaviour
    {
        public BoidAgent agent;

        [SerializeField]
        ParticleSystem explosion;

        bool _exploded;
        bool _active;
        public bool Active { get { return _active; } }

        void Start()
        {
            // Delay starting this check so rockets don't crash into each other at the beginning.
            StartCoroutine(DelayActivate(1F));
        }

        public void GettingBlownUp()
        {
            // Do not explode twice
            if (_exploded || !_active)
                return;

            // Debug.Log("GETTING BLOWN UP: " + agent.gameObject.name);

            BoidGroup.Instance.SpawnNewRocket();

            // Remove from active rocket slist
            BoidGroup.Instance.RemoveRocket(agent);

            _exploded = true;
            _active = false;

            StartCoroutine(RemoveRocket(0.1F));
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Do not explode twice
            if (_exploded || !_active)
                return;

            RocketCollisionExplosion otherRocket = collision.GetComponent<RocketCollisionExplosion>();

            if (otherRocket != null)
            {
                BoidAgent otherAgent = otherRocket.agent;

                if (otherAgent != null && otherRocket.Active)
                {
                    // Debug.Log("BLOWING UP: " + agent.gameObject.name + ", to target: " + otherAgent.gameObject.name);

                    otherRocket.GettingBlownUp();

                    agent.Freeze(true);
                    agent.Sprite(false);

                    explosion.gameObject.SetActive(true);
                    explosion.Play();

                    // Stop this from exploding twice.
                    _exploded = true;

                    // Remove from active rocket slist
                    BoidGroup.Instance.RemoveRocket(agent);

                    // Spawn a new rocket
                    BoidGroup.Instance.SpawnNewRocket();

                    StartCoroutine(RemoveRocket(1F));
                }
            }
        }

        IEnumerator DelayActivate(float delay)
        {
            yield return new WaitForSeconds(delay);

            _active = true;
        }

        IEnumerator RemoveRocket(float delay)
        {
            yield return new WaitForSeconds(delay);

            // Debug.Log("Destroy rocket: " + agent.gameObject.name);

            Destroy(agent.gameObject);
        }

        public void Check(bool eval)
        {
            _active = eval;
        }
    }
}