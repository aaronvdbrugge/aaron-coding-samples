using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SqulaRocketDemo
{
    public class ControlSlider : MonoBehaviour
    {
        [Tooltip("Set this integer to match the index of the behavior you want to adjust on BoidGroup.behavior.behaviors[i]")]
        [SerializeField]
        int behaviorIndex;

        [SerializeField]
        public Slider slider;

        public void Start()
        {
            slider.onValueChanged.AddListener(delegate { ValueChangeCheck(); });

            CompositeBehavior compBehavior = (CompositeBehavior)BoidGroup.Instance.behavior;
            slider.value = compBehavior.behaviors[behaviorIndex].startingWeight;
        }

        // Invoked when the value of the slider changes.
        public void ValueChangeCheck()
        {
            CompositeBehavior compBehavior = (CompositeBehavior)BoidGroup.Instance.behavior;
            WeightManager.Instance.SetWeightBasedOnIndex(behaviorIndex, slider.value);
        }
    }
}