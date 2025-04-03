using UnityEngine;

namespace SystemManagement
{
    public abstract class SceneDataBase : ScriptableObject
    {
        [field: SerializeField] public SceneReference Scene;
    }
}