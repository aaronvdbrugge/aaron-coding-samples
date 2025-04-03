using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    [CreateAssetMenu(menuName = "Boid/Behavior/Separation")]
    public class SeparationBehavior : BoidBehavior
    {
        public override Vector2 CalculateMove(BoidAgent agent, List<Transform> context, BoidGroup group)
        {
            //if no neighbors, return no adjustment
            if (context.Count == 0)
                return Vector2.zero;

            //add all points together and average
            Vector2 avoidanceMove = Vector2.zero;
            int nAvoid = 0;

            foreach (Transform item in context)
            {
                if (Vector2.SqrMagnitude(item.position - agent.transform.position) < group.SquareAvoidanceRadius)
                {
                    nAvoid++;
                    avoidanceMove += (Vector2)(agent.transform.position - item.position);
                }
            }
            if (nAvoid > 0)
                avoidanceMove /= nAvoid;

            return avoidanceMove;
        }
    }
}