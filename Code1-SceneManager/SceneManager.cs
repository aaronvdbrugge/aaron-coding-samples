using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Extensions;
using FeatureFramework;
using Messaging;

namespace SystemManagement
{
    /// <summary>
    /// This class manages the loading and unloading of the scenes
    /// and sends related messages when asynchronous loading is completed or failed.
    /// </summary>
    public class SceneManager : BehaviorSingleton<SceneManager>
    {
        public Scene MainScene => _mainScene;
        public List<Scene> SubScenes => _subScenes;
        
        private Scene _mainScene;
        private List<Scene> _subScenes = new List<Scene>();
        private SceneData[] _allSceneData = null;

        public void LoadAdditiveSceneAsyncBySceneName(string sceneName, List<string> subSceneNameList)
        {
            Debug.Log($"LoadAdditiveSceneAsyncBySceneName {sceneName}");
            
            IMessageBus messageBus = MessageBusManager.Resolve;
            if (sceneName.IsValid() == false)
            {
                messageBus.Publish(new SceneLoadingFailed(sceneName));
                return;
            }
            MessageBusManager.Resolve.Publish(new OpenLoadingScreen());
            ClearTemporaryScenes();
            
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            IEnumerator loadEnumerator = ProcessAsyncOperation(loadOperation, () =>
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid() == false)
                {
                    messageBus.Publish(new SceneLoadingFailed(sceneName));
                    return;
                }

                _mainScene = scene;
                SceneManager.SetActiveScene(_mainScene);

                bool hasSubScene = subSceneNameList != null && subSceneNameList.Count > 0;
                messageBus.Publish(new SceneLoadingFinishedMessage(sceneName, hasSubScene));
                
                if(hasSubScene == false)
                {
                    return;
                }

                for(int i = 0; i < subSceneNameList.Count; i++)
                {
                    LoadAdditiveSubSceneAsync(subSceneNameList[i]);
                }
            });

            messageBus.Publish(new SceneLoadingStartedMessage(sceneName));
            ICoroutineModule coroutineModule = CoroutineComponent.Instance.CoroutineModule;
            coroutineModule.StartCoroutine(loadEnumerator);
        }

        public void LoadAdditiveSceneAsyncBySceneDataName(string sceneDataName)
        {
            Debug.Log($"LoadAdditiveSceneAsyncBySceneDataName {sceneDataName}");
            
            SceneData sceneData = LoadSceneData(sceneDataName);
            if (sceneData == null)
            {
                Debug.LogError("SceneManager.LoadAdditiveSceneAsyncBySceneDataName() sceneDataName is unavailable");
                return;
            }

            LoadAdditiveSceneAsync(sceneData);
        }

        public void LoadAdditiveSceneAsync(SceneData sceneData)
        {
            Debug.Log($"LoadAdditiveSceneAsync {sceneData.name}");
            
            IMessageBus messageBus = MessageBusManager.Resolve;
            MessageBusManager.Resolve.Publish(new OpenLoadingScreen());
            if (sceneData == null)
            {
                messageBus.Publish(new SceneLoadingFailed(sceneData.Scene.SceneName));
                return;
            }

            List<string> subSceneNameList = new List<string>();
            if (sceneData.SubScenes != null)
            {
                for(int i = 0; i < sceneData.SubScenes.Length; i++)
                {
                    subSceneNameList.Add(sceneData.SubScenes[i].Scene.SceneName);
                }
            }

            LoadAdditiveSceneAsyncBySceneName(sceneData.Scene.SceneName, subSceneNameList);
        }
        
        /// <summary>
        /// Returns true if the current scene name matches is any of the given scene names.
        /// </summary>
        /// <param name="sceneNames">An array of scene names to check.</param>
        public bool IsCurrentSceneAny(string[] sceneNames)
        {
            foreach (string sceneName in sceneNames)
            {
                if (!sceneName.IsValid())
                {
                    continue;
                }

                if (IsCurrentScene(sceneName))
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Returns true if the current scene name matches the given scene name.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check.</param>
        public bool IsCurrentScene(string sceneName)
        {
            if (sceneName.IsValid() == false || _mainScene.IsValid() == false)
            {
                return false;
            }
            return _mainScene.name == sceneName;
        }

        private void LoadAdditiveSubSceneAsync(string sceneName)
        {
            Debug.Log($"LoadAdditiveSubSceneAsync {sceneName}");
            
            IMessageBus messageBus = MessageBusManager.Resolve;
            if (sceneName.IsValid() == false)
            {
                messageBus.Publish(new SubSceneLoadingFailed(sceneName));
                return;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            IEnumerator loadEnumerator = ProcessAsyncOperation(loadOperation, () =>
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);
                if (scene.IsValid() == false)
                {
                    messageBus.Publish(new SubSceneLoadingFailed(sceneName));
                    return;
                }

                _subScenes.Add(scene);
                messageBus.Publish(new SubSceneLoadingFinishedMessage(sceneName));
            });

            messageBus.Publish(new SubSceneLoadingStartedMessage(sceneName));
            ICoroutineModule coroutineModule = CoroutineComponent.Instance.CoroutineModule;
            coroutineModule.StartCoroutine(loadEnumerator);
        }

        public void UnloadSubScenes()
        {
            for (int i = 0; i < _subScenes.Count; i++)
            {
                UnloadSubSceneAsync(_subScenes[i]);
            }
        }

        private void ClearTemporaryScenes()
        {
            if (_mainScene.IsValid() == false)
            {
                return;
            }
            UnloadSceneAsync(_mainScene);
            UnloadSubScenes();
        }
        
        private void UnloadSceneAsync(Scene sceneToUnload)
        {
            Debug.Log($"UnloadSceneAsync {sceneToUnload.name}");
            
            if (sceneToUnload.IsValid() == false)
            {
                Debug.Log("SceneManager.UnloadSceneAsync() Scene is not valid");
                return;
            }
            IMessageBus messageBus = MessageBusManager.Resolve;
            ICoroutineModule coroutineModule = CoroutineComponent.Instance.CoroutineModule;

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload, UnloadSceneOptions.None);
            IEnumerator unloadEnumerator = ProcessAsyncOperation(unloadOperation, () =>
            {
                messageBus.Publish(new SceneUnloadingFinishedMessage(sceneToUnload.name));
            });

            messageBus.Publish(new SceneUnloadingStartedMessage(sceneToUnload.name));
            coroutineModule.StartCoroutine(unloadEnumerator);
        }

        private void UnloadSubSceneAsync(Scene sceneToUnload)
        {
            Debug.Log($"UnloadSubSceneAsync {sceneToUnload.name}");
            
            if (sceneToUnload.IsValid() == false)
            {
                Debug.Log("SceneManager.UnloadSubSceneAsync() Scene is not valid");
                return;
            }
            IMessageBus messageBus = MessageBusManager.Resolve;
            ICoroutineModule coroutineModule = CoroutineComponent.Instance.CoroutineModule;

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneToUnload, UnloadSceneOptions.None);
            IEnumerator unloadEnumerator = ProcessAsyncOperation(unloadOperation, () =>
            {
                messageBus.Publish(new SubSceneUnloadingFinishedMessage(sceneToUnload.name));
            });

            messageBus.Publish(new SubSceneUnloadingStartedMessage(sceneToUnload.name));
            coroutineModule.StartCoroutine(unloadEnumerator);
        }

        private IEnumerator ProcessAsyncOperation(AsyncOperation operation, Action onComplete)
        {
            while (!operation.isDone)
            {
                yield return null;
            }

            onComplete?.Invoke();
        }
        
        // This method loads a scene data asset by name if found; otherwise, it returns null.
        public SceneData LoadSceneData(string sceneDataName)
        {
            Debug.Log($"LoadSceneData {sceneDataName}");
            
            if (!sceneDataName.IsValid())
            {
                Debug.LogWarning("SceneManager.LoadSceneData() sceneDataName is not valid");
                return null;
            }
            
            if (_allSceneData == null)
            {
                _allSceneData = Resources.LoadAll<SceneData>("");
                if (_allSceneData == null || _allSceneData.Length == 0)
                {
                    return null;
                }
            }

            foreach (var sceneData in _allSceneData)
            {
                if (sceneData.name == sceneDataName)
                {
                    return sceneData;
                }
            }
            
            return null;
        }
    }
}