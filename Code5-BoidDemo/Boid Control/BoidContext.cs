using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    public class BoidContext : MonoBehaviour
    {
        public BoidAgent agent;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.GetComponent<BoidAgent>())
            {
                agent.context.Add(collision.gameObject.transform);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponent<BoidAgent>())
            {
                agent.context.Remove(collision.gameObject.transform);
            }
        }
    }
}