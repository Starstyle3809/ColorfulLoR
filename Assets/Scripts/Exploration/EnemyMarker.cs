using UnityEngine;
using System.Collections;
using System.Collections.Generic; // ★ 新增

public class EnemyMarker : MonoBehaviour
{
    public BattleUnit battleUnit;
    public float escapeSpeed = 8f;

    [Header("群組戰鬥設定")]
    [Tooltip("碰到此敵人時，會一起捲入戰鬥的同夥")]
    public List<EnemyMarker> linkedEnemies = new List<EnemyMarker>();

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