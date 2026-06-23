using UnityEngine;
using System.Collections;

public class UnitAnimator : MonoBehaviour
{
    [Header("綁定你的紙片人")]
    public SpriteRenderer spriteRenderer; // 把底下帶有圖片的子物件拖進來

    [Header("動作圖片設定")]
    public Sprite idleSprite;    // 站立
    public Sprite dashSprite;    // 攻擊1 (衝刺預備)
    public Sprite attackSprite1; // 攻擊2 (起手)
    public Sprite attackSprite2; // 攻擊3 (發力)
    public Sprite guardSprite;   // 防禦2

    private Vector3 _baseLocalPos;

    private void Awake()
    {
        // 紀錄紙片人一開始的中心位置，閃避完才回得來
        if (spriteRenderer != null) _baseLocalPos = spriteRenderer.transform.localPosition;
    }

    public void PlayIdle()
    {
        StopAllCoroutines();
        ResetPosition();
        if (spriteRenderer && idleSprite) spriteRenderer.sprite = idleSprite;
    }

    public void PlayDash()
    {
        StopAllCoroutines();
        ResetPosition();
        if (spriteRenderer && dashSprite) spriteRenderer.sprite = dashSprite;
    }

    public void PlayGuard()
    {
        StopAllCoroutines();
        ResetPosition();
        if (spriteRenderer && guardSprite) spriteRenderer.sprite = guardSprite;
    }

    public void PlayAttack()
    {
        StopAllCoroutines();
        ResetPosition();
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        // 播放「起手」動作
        if (spriteRenderer && attackSprite1) spriteRenderer.sprite = attackSprite1;
        yield return new WaitForSeconds(0.15f); // 稍微卡肉一下，增加力度感

        // 播放「發力」動作
        if (spriteRenderer && attackSprite2) spriteRenderer.sprite = attackSprite2;
        yield return new WaitForSeconds(0.3f);

        // 攻擊結束，自動回到待機
        PlayIdle();
    }

    public void PlayDodge()
    {
        StopAllCoroutines();
        ResetPosition();
        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        if (spriteRenderer == null) yield break;
        if (idleSprite) spriteRenderer.sprite = idleSprite; // 閃避時保持待機姿勢

        Transform spriteTransform = spriteRenderer.transform;

        // ==========================================
        // ★ 關鍵修正：將 (dir, 0, 0) 改為 (0, 0, -1.5f)
        // 因為角色已經轉身面向對手，本地的 Z 軸才是畫面的左右（前後退）。
        // -1.5f 代表「往後滑步拉開距離」，這在畫面上就是完美的左右閃避！
        // ==========================================
        Vector3 dodgePos = _baseLocalPos + new Vector3(0, 0, -1.5f);

        // 瞬間往後(畫面左右)滑開
        float t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            spriteTransform.localPosition = Vector3.Lerp(_baseLocalPos, dodgePos, t / 0.1f);
            yield return null;
        }

        yield return new WaitForSeconds(0.15f); // 停頓一下，展現閃避的從容感

        // 順滑回到原位繼續對峙
        t = 0;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            spriteTransform.localPosition = Vector3.Lerp(dodgePos, _baseLocalPos, t / 0.1f);
            yield return null;
        }
        spriteTransform.localPosition = _baseLocalPos;
    }

    private void ResetPosition()
    {
        if (spriteRenderer) spriteRenderer.transform.localPosition = _baseLocalPos;
    }
}