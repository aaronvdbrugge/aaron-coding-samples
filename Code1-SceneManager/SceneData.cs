using UnityEngine;

namespace SystemManagement
{
    [CreateAssetMenu(fileName = "SceneData", menuName = "/Scene Management/Create Scene", order = 0)]
    public sealed class SceneData : SceneDataBase
    {
        [field: SerializeField] public SubSceneData[] SubScenes { get; private set; }
    }
}