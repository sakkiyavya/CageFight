using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageText : MonoBehaviour
{
    private TextMeshProUGUI _tmpText;
    private DamageTextPool _pool;

    // 动效状态变量
    private Vector3 _startPos;
    private float _elapsed;
    private float _duration = 0.8f;
    private float _randomX;
    private bool _isPlaying;

    private void Awake()
    {
        _tmpText = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 初始化并播放跳字动效。
    /// </summary>
    public void Init(int value, Color color, DamageTextPool pool)
    {
        _pool = pool;
        _tmpText.text = value.ToString();


        _tmpText.color = color; // 亮紫色


        // 初始化动画状态
        _startPos = transform.position;
        _elapsed = 0f;
        _randomX = Random.Range(-0.5f, 0.5f);
        _isPlaying = true;
    }
    

    private void Update()
    {
        if (!_isPlaying) return;

        _elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);

        // 1. 向上漂移 & 水平随机散开
        transform.position = _startPos + new Vector3(_randomX * progress, progress * 1.5f, 0);

        // 2. 极简的“弹出-回弹”缩放动画
        float scale;
        if (progress < 0.2f)
        {
            scale = Mathf.Lerp(0f, 1.3f, progress / 0.2f);
        }
        else
        {
            scale = Mathf.Lerp(1.3f, 1.0f, (progress - 0.2f) / 0.8f);
        }
        transform.localScale = new Vector3(scale, scale, 1f);

        // 3. 后半段自动淡出
        if (progress > 0.5f)
        {
            float fadeProgress = (progress - 0.5f) / 0.5f;
            Color tempColor = _tmpText.color;
            tempColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
            _tmpText.color = tempColor;
        }

        // 4. 动画结束后，自动放回对象池
        if (progress >= 1f)
        {
            _isPlaying = false;
            if (_pool != null)
            {
                _pool.ReturnToPool(gameObject);
            }
        }
    }
}
