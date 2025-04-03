using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SqulaRocketDemo
{
    public class WeightManager : MonoBehaviour
    {
        public float coherenceWeight, separationWeight, alignmentWeight;

        bool _holdingMouse;

        public static WeightManager Instance;

        private void Awake()
        {
            Instance = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            CompositeBehavior compBehavior = (CompositeBehavior)BoidGroup.Instance.behavior;
            alignmentWeight = compBehavior.behaviors[0].startingWeight;
            coherenceWeight = compBehavior.behaviors[1].startingWeight;
            separationWeight = compBehavior.behaviors[2].startingWeight;
        }

        public void SetWeightBasedOnIndex(int index, float value)
        {
            switch (index)
            {
                case 0:
                    alignmentWeight = value;
                    break;
                case 1:
                    coherenceWeight = value;
                    break;
                case 2:
                    separationWeight = value;
                    break;
                default:
                    break;
            }
        }

        public float GetWeightBasedOnIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return alignmentWeight;
                case 1:
                    return coherenceWeight;
                case 2:
                    return separationWeight;
                default:
                    CompositeBehavior compBehavior = (CompositeBehavior)BoidGroup.Instance.behavior;
                    alignmentWeight = compBehavior.behaviors[3].startingWeight;
                    return alignmentWeight;
            }
        }
    }
}