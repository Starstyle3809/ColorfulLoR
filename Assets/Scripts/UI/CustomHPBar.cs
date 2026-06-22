using UnityEngine;
using UnityEngine.UI;

public class CustomHPBar : MonoBehaviour
{
    [Header("請依序放入 1血 到 5血 的圖片 (HP_1 ~ HP_5)")]
    public Image[] hpBlocks; // 陣列長度請設為 5

    // 負責更新血條顯示狀態的方法
    public void UpdateHP(int currentHP)
    {
        for (int i = 0; i < hpBlocks.Length; i++)
        {
            if (hpBlocks[i] != null)
            {
                // 陣列索引 i 從 0 開始。
                // 如果目前血量 currentHP 是 3：i=0,1,2 會開啟顯示，i=3,4 會隱藏。
                hpBlocks[i].enabled = i < currentHP;
            }
        }
    }
}