using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SqulaRocketDemo
{
    public class ControlManager : MonoBehaviour
    {
        bool _holdingMouse;

        // Update is called once per frame
        void Update()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                // Move the cube if the screen has the finger moving.
                if (touch.phase == TouchPhase.Began)
                {
                    if (!IsPointerOverUIObject())
                    {
                        Vector2 pos = Camera.main.ScreenToWorldPoint(touch.position);
                        BoidGroup.Instance.MoveTo(pos);
                        BoidGroup.Instance.MoveAll(true);
                    }
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    Vector2 pos = Camera.main.ScreenToWorldPoint(touch.position);
                    BoidGroup.Instance.MoveTo(pos);
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    BoidGroup.Instance.MoveAll(false);
                }
            }
#endif
#if UNITY_EDITOR || UNITY_WEBGL
            if (_holdingMouse)
            {
                Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                BoidGroup.Instance.MoveTo(pos);
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverUIObject())
                {
                    Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    BoidGroup.Instance.MoveTo(pos);
                    BoidGroup.Instance.MoveAll(true);
                    _holdingMouse = true;
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                BoidGroup.Instance.MoveAll(false);
                _holdingMouse = false;
            }
#endif
        }

        public static bool IsPointerOverUIObject()
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
            return results.Count > 0;
        }
    }
}