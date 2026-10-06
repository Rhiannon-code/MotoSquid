// =================================================
// Realistic Engine Sounds 2
// Copyright © 2026 Skril Studio
//
// https://skrilstudio.com
// https://www.facebook.com/skrilstudio
// =================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SkrilStudio
{
    static class ExtensionHelpers
    {
        // this script is shared between multiple Skril Studio assets, mostly used for adding compatibility for other publishers assets
        public static GameObject GetFirstParentWithComponent<T>(this GameObject gameObject)
        {
            GameObject result = null;
            GameObject tempGameObject = gameObject.transform.parent.gameObject;
            while (result == null && tempGameObject != null)
            {
                if (tempGameObject.GetComponent<T>() != null)
                {
                    result = tempGameObject;
                }
                else
                {
                    tempGameObject = tempGameObject.transform.parent.gameObject;
                }
            }
            return result;
        }
    }
}
