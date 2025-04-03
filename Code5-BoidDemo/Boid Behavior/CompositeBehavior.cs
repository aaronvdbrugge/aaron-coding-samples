using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    [System.Serializable]
    public class BehaviorWeight
    {
        public BoidBehavior behavior;
        [Tooltip("Set how much this behavior should factor into the final movement result.")]
        public float startingWeight;
    }

    [CreateAssetMenu(menuName = "Boid/Behavior/Composite")]
    public class CompositeBehavior : BoidBehavior
    {
        public BehaviorWeight[] behaviors;

        public override Vector2 CalculateMove(BoidAgent agent, List<Transform> context, BoidGroup group)
        {
            // Set up move
            Vector2 move = Vector2.zero;

            // Iterate through behaviors
            for (int i = 0; i < behaviors.Length; i++)
            {
                // Retrieve weights from ControlManager
                float weight = WeightManager.Instance.GetWeightBasedOnIndex(i);

                Vector2 partialMove = behaviors[i].behavior.CalculateMove(agent, context, group) * weight;

                if (partialMove != Vector2.zero)
                {
                    if (partialMove.sqrMagnitude > weight * weight)
                    {
                        partialMove.Normalize();
                        partialMove *= weight;
                    }

                    move += partialMove;
                }
            }

            return move;
        }
    }
}