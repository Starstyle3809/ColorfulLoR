using UnityEngine;
using System.Collections;

/// <summary>
/// 擴充版：加入第一關專用的「逃跑」機制。
/// </summary>
public class EnemyMarker : MonoBehaviour
{
    public BattleUnit battleUnit;
    public float escapeSpeed = 8f;

    public void Escape()
    {
        StartCoroutine(EscapeRoutine());
    }

    private IEnumerator EscapeRoutine()
    {
        Transform playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        Vector3 escapeDir = (transform.position - playerTransform.position).normalized;
        escapeDir.y = 0;

        float elapsed = 0f;
        while (elapsed < 3f)
        {
            transform.position += escapeDir * escapeSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(escapeDir);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}