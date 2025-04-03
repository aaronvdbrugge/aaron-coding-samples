using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    [CreateAssetMenu(menuName = "Boid/Behavior/Alignment")]
    public class AlignmentBehavior : BoidBehavior
    {
        public override Vector2 CalculateMove(BoidAgent agent, List<Transform> context, BoidGroup group)
        {
            //if no neighbors, maintain current alignment
            if (context.Count == 0)
                return agent.transform.up;

            //add all points together and average
            Vector2 alignmentMove = Vector2.zero;

            foreach (Transform item in context)
            {
                alignmentMove += (Vector2)item.transform.up;
            }
            alignmentMove /= context.Count;

            return alignmentMove;
        }
    }
}