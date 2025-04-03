using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SqulaRocketDemo
{
    public abstract class BoidBehavior : ScriptableObject
    {
        public abstract Vector2 CalculateMove(BoidAgent agent, List<Transform> context, BoidGroup group);
    }

}