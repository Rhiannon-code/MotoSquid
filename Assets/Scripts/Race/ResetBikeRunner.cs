using MotoSquid.Combat;
using System.Collections;
using UnityEngine;

namespace MotoSquid.Race
{
public class ResetBikeRunner : MonoBehaviour
{
    public static GameObject Spawn(float delay, System.Action callback)
    {
        var go     = new GameObject("[ResetBikeRunner]");
        var runner = go.AddComponent<ResetBikeRunner>();
        runner._delay    = delay;
        runner._callback = callback;
        return go;
    }

    private float         _delay;
    private System.Action _callback;

    private void Start() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(_delay);
        _callback?.Invoke();
        Destroy(gameObject);
    }
}
}
