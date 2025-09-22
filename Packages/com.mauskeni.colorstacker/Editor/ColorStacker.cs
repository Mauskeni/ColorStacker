//---ColorStackrer---
//1.0.0  開発開始
//1.1.0  機能追加
//1.1.1  バグ修正
//1.2.0  白黒反転、デカール設定を追加
//1.2.1  プレビューサイズ変更プルダウンを追加

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MaskColorEditor : EditorWindow
{
    [System.Serializable]
    public class Layer
    {
        public Texture2D mask;
        public Color tintColor = Color.white;
        public bool enableTint = true; // Tint 有効/無効
        public BlendMode blendMode = BlendMode.Normal;
        public bool treatBlackAsTransparent = false; // 黒を透過扱いにするか
        public bool invertMask = false; // 白黒反転
        public bool enableDecal = false; // デカール有効化
        public Vector2 decalPosition = new Vector2(0.5f, 0.5f); // UV座標（中心）
        public Vector2 decalScale = Vector2.one; // 拡縮 (1 = 原寸でテクスチャがキャンバス全体を覆う)
        public float decalRotation = 0f; // 回転角度（度）
    }

    public enum BlendMode { Normal, Multiply, Additive, Screen, Overlay }

    private List<Layer> layers = new List<Layer>();
    private Texture2D previewTexture;
    
    // 追加: プレビューサイズの候補と現在の選択
    private string[] previewSizeOptions = new string[] { "128", "256", "512", "1024" };
    private int previewSizeIndex = 1; // デフォルト256

    private Vector2 scrollPos;
    private bool needsUpdate = true;

    // デフォルト保存先とファイル名
    public string defaultSaveFolder = "Assets/Mauskeni/Editor/ColorStacker/Pic";
    public string defaultFileName = "output.png";

    [MenuItem("Tools/MauWorks/ColorStacker")]
    public static void ShowWindow()
    {
        GetWindow<MaskColorEditor>("ColorStacker");
    }

    private void OnEnable()
    {
        if (layers.Count == 0)
        {
            layers.Add(new Layer());
            layers.Add(new Layer());
        }

        // デフォルト保存フォルダがなければ作成
        if (!Directory.Exists(defaultSaveFolder))
            Directory.CreateDirectory(defaultSaveFolder);
    }

    private void OnDisable()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("画像合成ツール", EditorStyles.boldLabel);

        for (int i = 0; i < layers.Count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            try
            {
                layers[i].mask = (Texture2D)EditorGUILayout.ObjectField("合成対象[" + (i + 1) + "]", layers[i].mask, typeof(Texture2D), false);
                layers[i].tintColor = EditorGUILayout.ColorField("Tint Color", layers[i].tintColor);
                layers[i].enableTint = EditorGUILayout.Toggle("Tint Color有効化", layers[i].enableTint);
                layers[i].treatBlackAsTransparent = EditorGUILayout.Toggle("黒を透過する", layers[i].treatBlackAsTransparent);
                layers[i].invertMask = EditorGUILayout.Toggle("白黒反転する", layers[i].invertMask);
                layers[i].blendMode = (BlendMode)EditorGUILayout.EnumPopup("Blend Mode", layers[i].blendMode);

                //---デカール項目---
                layers[i].enableDecal = EditorGUILayout.Toggle("デカールとして扱う", layers[i].enableDecal);
                if (layers[i].enableDecal)
                {
                    layers[i].decalPosition = EditorGUILayout.Vector2Field("位置(UV0-1)", layers[i].decalPosition);
                    layers[i].decalScale = EditorGUILayout.Vector2Field("スケール (1 = 原寸, 0.5 = 半分, 2 = 2倍)", layers[i].decalScale);
                    layers[i].decalRotation = EditorGUILayout.Slider("回転", layers[i].decalRotation, -180f, 180f);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("↑", GUILayout.Width(120)) && i > 0)
                {
                    var tmp = layers[i];
                    layers[i] = layers[i - 1];
                    layers[i - 1] = tmp;
                    needsUpdate = true;
                }
                if (GUILayout.Button("↓", GUILayout.Width(120)) && i < layers.Count - 1)
                {
                    var tmp = layers[i];
                    layers[i] = layers[i + 1];
                    layers[i + 1] = tmp;
                    needsUpdate = true;
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Remove Layer"))
                {
                    layers.RemoveAt(i);
                    needsUpdate = true;
                    continue;
                }
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        if (GUILayout.Button("Add Layer"))
        {
            layers.Add(new Layer());
            needsUpdate = true;
        }

        if (EditorGUI.EndChangeCheck())
        {
            needsUpdate = true;
        }

        // ---プレビューサイズ選択プルダウン---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("プレビュー設定", EditorStyles.boldLabel);
        previewSizeIndex = EditorGUILayout.Popup("プレビュー解像度", previewSizeIndex, previewSizeOptions);

        int previewSize = int.Parse(previewSizeOptions[previewSizeIndex]);

        if (needsUpdate)
        {
            if (previewTexture != null)
                DestroyImmediate(previewTexture);

            previewTexture = GeneratePreview(256);
            needsUpdate = false;
        }

        if (previewTexture != null)
        {
            GUILayout.Label("プレビュー", EditorStyles.boldLabel);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.ScaleToFit);

            if (GUILayout.Button("出力PNGを保存 (2048x2048)"))
            {
                string path = EditorUtility.SaveFilePanel("保存先を選択", defaultSaveFolder, defaultFileName, "png");
                if (!string.IsNullOrEmpty(path))
                {
                    // 選択したパスからフォルダとファイル名を分解して次回のデフォルトに設定
                    defaultSaveFolder = Path.GetDirectoryName(path);
                    defaultFileName = Path.GetFileName(path);
                    
                    Texture2D highRes = GeneratePreview(2048);
                    SaveTexture(highRes, path);
                    DestroyImmediate(highRes);
                    Debug.Log("保存しました: " + path);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    Texture2D GeneratePreview(int size)
    {
        if (layers.Count == 0 || layers.All(l => l.mask == null))
            return null;

        int width = size;
        int height = size;
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

        Color[] pixels = new Color[width * height];

        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].mask == null) continue;

            Texture2D mask = MakeReadable(layers[i].mask);
            // 保険として読み取り用テクスチャのwrapをClampにしておく（GetPixelBilinearの外挙動を防ぐ）
            mask.wrapMode = TextureWrapMode.Clamp;

            Color tint = layers[i].enableTint ? layers[i].tintColor : Color.white;
            BlendMode mode = layers[i].blendMode;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 0〜1 のUV座標（キャンバス上の位置）
                    Vector2 uv = new Vector2((float)x / (width - 1), (float)y / (height - 1));

                    Color maskColor;

                    if (layers[i].enableDecal)
                    {
                        // ===== 重要: デカール向けの「逆変換」手順 =====
                        // 1) キャンバス座標 -> デカール中心基準 (dx,dy)
                        float dx = uv.x - layers[i].decalPosition.x;
                        float dy = uv.y - layers[i].decalPosition.y;

                        // 2) 回転の逆（サンプリング点をデカール空間に戻すため -rotation）
                        float rad = -layers[i].decalRotation * Mathf.Deg2Rad; // 逆回転
                        float cos = Mathf.Cos(rad);
                        float sin = Mathf.Sin(rad);
                        float rx = dx * cos - dy * sin;
                        float ry = dx * sin + dy * cos;

                        // 3) スケールの逆（decalScale が 0.5 -> 画像が半分のサイズになる）
                        //    ※decalScale の意味: 1 = テクスチャがキャンバス全体を覆う、0.5 = 半分の大きさ
                        if (layers[i].decalScale.x == 0f) layers[i].decalScale.x = 0.0001f;
                        if (layers[i].decalScale.y == 0f) layers[i].decalScale.y = 0.0001f;
                        rx /= layers[i].decalScale.x;
                        ry /= layers[i].decalScale.y;

                        // 4) デカールローカル座標 -> テクスチャUV（デカールテクスチャは中心が (0.5,0.5) と仮定）
                        Vector2 sampleUV = new Vector2(rx + 0.5f, ry + 0.5f);

                        // 5) 範囲外は透明（これでRepeatによるタイル化を防ぐ）
                        if (sampleUV.x < 0f || sampleUV.x > 1f || sampleUV.y < 0f || sampleUV.y > 1f)
                        {
                            maskColor = Color.clear;
                        }
                        else
                        {
                            maskColor = mask.GetPixelBilinear(sampleUV.x, sampleUV.y);
                        }
                    }
                    else
                    {
                        // 通常モード：画像をキャンバス全体にフィット
                        maskColor = mask.GetPixelBilinear(uv.x, uv.y);
                    }

                    // 白黒反転処理
                    if (layers[i].invertMask)
                    {
                        maskColor = new Color(1f - maskColor.r, 1f - maskColor.g, 1f - maskColor.b, maskColor.a);
                    }

                    // 黒透過 & 白に近いほど alpha を強く
                    if (layers[i].treatBlackAsTransparent)
                    {
                        if (maskColor.r < 0.01f && maskColor.g < 0.01f && maskColor.b < 0.01f)
                        {
                            maskColor.a = 0f;
                        }
                        else
                        {
                            float luminance = (maskColor.r + maskColor.g + maskColor.b) / 3f;
                            maskColor.a = luminance;
                        }
                    }

                    int idx = y * width + x;
                    pixels[idx] = ApplyMask(pixels[idx], maskColor, tint, mode, layers[i].enableTint);
                }
            }

            if (mask != layers[i].mask)
                DestroyImmediate(mask);
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    Texture2D MakeReadable(Texture2D tex)
    {
        if (tex.isReadable) return tex;

        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        // ここのwrapModeはClampにしておく（念のため）
        readable.wrapMode = TextureWrapMode.Clamp;
        readable.filterMode = tex.filterMode;

        return readable;
    }

    Color ApplyMask(Color baseColor, Color maskColor, Color tintColor, BlendMode mode, bool enableTint)
    {
        float maskValue = maskColor.a;
        if (maskValue <= 0.001f) return baseColor;

        Color maskTint = enableTint
            ? new Color(tintColor.r, tintColor.g, tintColor.b, tintColor.a * maskValue)
            : new Color(maskColor.r, maskColor.g, maskColor.b, maskValue);

        Color blended = baseColor;

        switch (mode)
        {
            case BlendMode.Normal:
                blended = Color.Lerp(baseColor, maskTint, maskTint.a);
                break;
            case BlendMode.Multiply:
                blended = baseColor * maskTint;
                break;
            case BlendMode.Additive:
                blended = baseColor + maskTint * maskTint.a;
                break;
            case BlendMode.Screen:
                blended = Color.Lerp(baseColor, Color.white - (Color.white - baseColor) * (Color.white - maskTint), maskTint.a);
                break;
            case BlendMode.Overlay:
                blended.r = (baseColor.r < 0.5f) ? (2 * baseColor.r * maskTint.r) : (1 - 2 * (1 - baseColor.r) * (1 - maskTint.r));
                blended.g = (baseColor.g < 0.5f) ? (2 * baseColor.g * maskTint.g) : (1 - 2 * (1 - baseColor.g) * (1 - maskTint.g));
                blended.b = (baseColor.b < 0.5f) ? (2 * baseColor.b * maskTint.b) : (1 - 2 * (1 - baseColor.b) * (1 - maskTint.b));
                blended.a = Mathf.Max(baseColor.a, maskTint.a);
                break;
        }

        blended.a = Mathf.Clamp01(baseColor.a + maskTint.a);
        return blended;
    }

    void SaveTexture(Texture2D tex, string path)
    {
        byte[] pngData = tex.EncodeToPNG();
        if (pngData != null)
        {
            File.WriteAllBytes(path, pngData);
            AssetDatabase.Refresh();
        }
    }
}
