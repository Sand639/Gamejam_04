using UnityEngine;

/// <summary>
/// **立てた中指にモザイクをかける。**
///
/// 手（<see cref="YubisumaThumb"/> と同じオブジェクト）に付ける。
/// モザイクの板（`Assets/takuma/prefabs/mosaic.prefab`）を、
/// **中指の付け根から先までの骨に毎フレーム合わせて**動かす。
/// アニメーションで指が立っていく途中にも、ちゃんとついていく。
///
/// 板は**中指が立って見えているときだけ**出す（握りこぶしや「いっせーの」の間は出さない）。
///
/// モザイクの板は、手の外（プレイヤーの下）に置いてある。
/// 右手のプレハブは左右反転（Scale X = -1）しているので、手の子にすると板の大きさが狂うため。
/// </summary>
public class YubisumaFingerMosaic : MonoBehaviour
{
    [Tooltip("この手の指の上げ下げ。立って見えているかを見る")]
    [SerializeField] private YubisumaThumb hand;

    [Tooltip("中指の付け根の骨（MiddleFinger）")]
    [SerializeField] private Transform fingerRoot;

    [Tooltip("中指の先の骨（Middle3 の下の _end）")]
    [SerializeField] private Transform fingerTip;

    [Tooltip("モザイクの板")]
    [SerializeField] private Transform mosaic;

    [Tooltip("この手を映しているカメラ。板をカメラのほうへ向けるのに使う")]
    [SerializeField] private Camera viewCamera;

    [Header("板の大きさ")]
    [Tooltip("板をどこから始めるか（付け根＝0、指先＝1）。付け根の骨は握りこぶしの中にあるので、途中から始める")]
    [Range(0f, 1f)]
    [SerializeField] private float startAlong = 0.15f;

    [Tooltip("指先からさらに先へ、どれだけはみ出させるか（指の長さに対する割合）")]
    [SerializeField] private float beyondTip = 0.12f;

    [Tooltip("指の長さに対する、板の横幅")]
    [SerializeField] private float widthScale = 0.34f;

    [Tooltip("板をカメラのほうへ少し出す距離（m）。指にめり込んで隠れないように")]
    [SerializeField] private float towardCamera = 0.35f;

    [Header("モザイクの粗さ")]
    [Tooltip("モザイクのマス目の大きさ（画面の高さが 1080 のときのピクセル数）。窓の大きさに合わせて自動で変わる。" +
             "マテリアル（mosaic.mat）の値は 1 になっていてモザイクがかからないため、ここで決める")]
    [SerializeField] private float cellPixelsAt1080 = 32f;

    private static readonly int CellPixId = Shader.PropertyToID("_CellPix");

    private Renderer mosaicRenderer;

    private void Awake()
    {
        if (hand == null)
        {
            hand = GetComponent<YubisumaThumb>();
        }

        if (mosaic != null)
        {
            mosaicRenderer = mosaic.GetComponentInChildren<Renderer>();
            SetVisible(false);
        }
    }

    private void OnDisable()
    {
        // 当てられて手が非表示になったら、モザイクも消す（手の外に置いてあるため、自動では消えない）
        SetVisible(false);
    }

    private void LateUpdate()
    {
        bool show = hand != null && hand.IsShownRaised &&
                    fingerRoot != null && fingerTip != null && mosaic != null;

        SetVisible(show);

        if (!show)
        {
            return;
        }

        // アニメーションが骨を動かしたあと（LateUpdate）に、指の位置へ合わせる
        Vector3 root = fingerRoot.position;
        Vector3 tip = fingerTip.position;
        Vector3 along = tip - root;
        float length = along.magnitude;

        if (length < 1e-4f)
        {
            return;
        }

        // 握りこぶしから出ている部分（途中～指先の少し先）だけを覆う
        Vector3 start = root + along * startAlong;
        Vector3 end = tip + along * beyondTip;
        Vector3 center = (start + end) * 0.5f;
        float boardLength = (end - start).magnitude;

        Vector3 toCamera = viewCamera != null ? (viewCamera.transform.position - center).normalized : Vector3.back;

        // カメラのほうを向け、縦を指の向きにそろえる
        mosaic.position = center + toCamera * towardCamera;
        mosaic.rotation = Quaternion.LookRotation(-toCamera, along);
        mosaic.localScale = new Vector3(length * widthScale, boardLength, 1f);

        // マス目の大きさを、窓の大きさに合わせる
        mosaicRenderer.material.SetFloat(CellPixId, Mathf.Max(2f, cellPixelsAt1080 * Screen.height / 1080f));
    }

    private void SetVisible(bool visible)
    {
        if (mosaicRenderer != null && mosaicRenderer.enabled != visible)
        {
            mosaicRenderer.enabled = visible;
        }
    }
}
