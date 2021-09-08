using System;
using UnityEditor;
#if UNITY_2020_1_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SmartTexture
{
    [CustomEditor(typeof(SmartTextureImporter), true)]
    class SmartTextureImporterEditor : ScriptedImporterEditor
    {
        internal static class Styles
        {
            public static readonly GUIContent[] labelChannels =
            {
                EditorGUIUtility.TrTextContent("Texture",
                    "This texture source channel will be packed into the Output texture red channel"),
                EditorGUIUtility.TrTextContent("Texture",
                    "This texture source channel will be packed into the Output texture green channel"),
                EditorGUIUtility.TrTextContent("Texture",
                    "This texture source channel will be packed into the Output texture blue channel"),
                EditorGUIUtility.TrTextContent("Texture",
                    "This texture source channel will be packed into the Output texture alpha channel"),
            };

            public static readonly GUIContent invertColor =
                EditorGUIUtility.TrTextContent("Invert Color", "If enabled outputs the inverted color (1.0 - color)");

            public static readonly GUIContent remapRange =
                EditorGUIUtility.TrTextContent("Remap", "Remaps the input texture value");

            public static readonly GUIContent useLuminance = EditorGUIUtility.TrTextContent("Use Luminance",
                "If enabled, outputs the combined rgb luminance value.");

            public static readonly GUIContent channel = EditorGUIUtility.TrTextContent("Input Channel",
                "Selects the RGBA channel to output, defaults to Red.");

            public static readonly GUIContent readWrite = EditorGUIUtility.TrTextContent("Read/Write Enabled",
                "Enable to be able to access the raw pixel data from code.");

            public static readonly GUIContent generateMipMaps = EditorGUIUtility.TrTextContent("Generate Mip Maps");
            public static readonly GUIContent streamingMipMaps = EditorGUIUtility.TrTextContent("Streaming Mip Maps");

            public static readonly GUIContent streamingMipMapsPrio =
                EditorGUIUtility.TrTextContent("Streaming Mip Maps Priority");

            public static readonly GUIContent sRGBTexture = EditorGUIUtility.TrTextContent("sRGB (Color Texture)",
                "Texture content is stored in gamma space. Non-HDR color textures should enable this flag (except if used for IMGUI).");

            public static readonly GUIContent format = EditorGUIUtility.TrTextContent("Output Texture Format");

            public static readonly GUIContent textureFilterMode = EditorGUIUtility.TrTextContent("Filter Mode");
            public static readonly GUIContent textureWrapMode = EditorGUIUtility.TrTextContent("Wrap Mode");

            public static readonly GUIContent textureAnisotropicLevel =
                EditorGUIUtility.TrTextContent("Anisotropic Level");

            public static readonly GUIContent crunchCompression =
                EditorGUIUtility.TrTextContent("Use Crunch Compression");

            public static readonly GUIContent useExplicitTextureFormat =
                EditorGUIUtility.TrTextContent("Use Explicit Texture Format");

            public static readonly string[] textureSizeOptions =
            {
                "32", "64", "128", "256", "512", "1024", "2048", "4096", "8192",
            };

            public static readonly string[] textureCompressionOptions =
                Enum.GetNames(typeof(TextureImporterCompression));

            public static readonly string[] textureFormat = Enum.GetNames(typeof(TextureFormat));
            public static readonly string[] resizeAlgorithmOptions = Enum.GetNames(typeof(TextureResizeAlgorithm));
        }

        SerializedProperty m_StreamingMipMaps;
        SerializedProperty m_StreamingMipMapPriority;
        readonly SerializedProperty[] m_InputTextures = new SerializedProperty[4];
        readonly SerializedProperty[] m_InputTextureSettings = new SerializedProperty[4];

        SerializedProperty m_IsReadable;
        SerializedProperty m_sRGBTexture;

        SerializedProperty m_EnableMipMap;

        SerializedProperty m_FilterMode;
        SerializedProperty m_WrapMode;
        SerializedProperty m_AnisotropicLevel;

        SerializedProperty m_TexturePlatformSettings;
        SerializedProperty m_TextureFormat;
        SerializedProperty m_UseExplicitTextureFormat;

        bool m_ShowAdvanced = false;

        const string k_AdvancedTextureSettingName = "SmartTextureImporterShowAdvanced";

        public override void OnEnable()
        {
            base.OnEnable();
            CacheSerializedProperties();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_ShowAdvanced = EditorPrefs.GetBool(k_AdvancedTextureSettingName, m_ShowAdvanced);


            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Red Input", EditorStyles.boldLabel);
            DrawInputTexture(0);

            var textureFormat = m_UseExplicitTextureFormat.boolValue
                ? (TextureFormat) m_TextureFormat.intValue
                : TextureFormat.RGBA32;
            var graphicsFormat = GraphicsFormatUtility.GetGraphicsFormat(textureFormat, m_sRGBTexture.boolValue);
            var componentCount = GraphicsFormatUtility.GetComponentCount(graphicsFormat);

            if (componentCount >= 2)
            {
                EditorGUILayout.LabelField("Green Input", EditorStyles.boldLabel);
                DrawInputTexture(1);
            }

            if (componentCount >= 3)
            {
                EditorGUILayout.LabelField("Blue Input", EditorStyles.boldLabel);
                DrawInputTexture(2);
            }

            if (componentCount >= 4)
            {
                EditorGUILayout.LabelField("Alpha Input", EditorStyles.boldLabel);
                DrawInputTexture(3);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();


            EditorGUILayout.LabelField("Output Texture", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(m_EnableMipMap, Styles.generateMipMaps);
                // EditorGUILayout.PropertyField(m_StreamingMipMaps, Styles.streamingMipMaps);
                // EditorGUILayout.PropertyField(m_StreamingMipMapPriority, Styles.streamingMipMapsPrio);
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(m_FilterMode, Styles.textureFilterMode);
                EditorGUILayout.PropertyField(m_WrapMode, Styles.textureWrapMode);

                EditorGUILayout.IntSlider(m_AnisotropicLevel, 0, 16, Styles.textureAnisotropicLevel);
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(m_IsReadable, Styles.readWrite);
                EditorGUILayout.PropertyField(m_sRGBTexture, Styles.sRGBTexture);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // TODO: Figure out how to apply PlatformTextureImporterSettings on ScriptedImporter
            DrawTextureImporterSettings();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }

        void DrawInputTexture(int index)
        {
            if (index < 0 || index >= 4)
                return;

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(m_InputTextures[index], Styles.labelChannels[index]);

            SerializedProperty remapRange = m_InputTextureSettings[index].FindPropertyRelative("remapRange");
            DrawMinMaxSlider(remapRange);

            SerializedProperty invertColor = m_InputTextureSettings[index].FindPropertyRelative("invertColor");
            invertColor.boolValue = EditorGUILayout.Toggle(Styles.invertColor, invertColor.boolValue);

            SerializedProperty useLuminance = m_InputTextureSettings[index].FindPropertyRelative("useLuminance");
            useLuminance.boolValue = EditorGUILayout.Toggle(Styles.useLuminance, useLuminance.boolValue);

            if (!useLuminance.boolValue)
            {
                SerializedProperty channel = m_InputTextureSettings[index].FindPropertyRelative("channel");
                EditorGUILayout.PropertyField(channel, Styles.channel);
            }

            EditorGUILayout.Space();

            EditorGUI.indentLevel--;
        }

        void DrawTextureImporterSettings()
        {
            SerializedProperty maxTextureSize = m_TexturePlatformSettings.FindPropertyRelative("m_MaxTextureSize");
            SerializedProperty resizeAlgorithm = m_TexturePlatformSettings.FindPropertyRelative("m_ResizeAlgorithm");
            SerializedProperty textureCompression =
                m_TexturePlatformSettings.FindPropertyRelative("m_TextureCompression");
            SerializedProperty textureCompressionCrunched =
                m_TexturePlatformSettings.FindPropertyRelative("m_CrunchedCompression");

            EditorGUILayout.LabelField("Texture Platform Settings", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();
                int sizeOption = EditorGUILayout.Popup("Texture Size", (int) Mathf.Log(maxTextureSize.intValue, 2) - 5,
                    Styles.textureSizeOptions);
                if (EditorGUI.EndChangeCheck())
                    maxTextureSize.intValue = 32 << sizeOption;

                EditorGUI.BeginChangeCheck();
                int resizeOption = EditorGUILayout.Popup("Resize Algorithm", resizeAlgorithm.intValue,
                    Styles.resizeAlgorithmOptions);
                if (EditorGUI.EndChangeCheck())
                    resizeAlgorithm.intValue = resizeOption;

                EditorGUILayout.LabelField("Compression", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool explicitFormat = EditorGUILayout.Toggle(Styles.useExplicitTextureFormat,
                        m_UseExplicitTextureFormat.boolValue);
                    if (EditorGUI.EndChangeCheck())
                        m_UseExplicitTextureFormat.boolValue = explicitFormat;

                    using (new EditorGUI.DisabledScope(explicitFormat))
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        int compressionOption = EditorGUILayout.Popup("Texture Type", textureCompression.intValue,
                            Styles.textureCompressionOptions);
                        if (EditorGUI.EndChangeCheck())
                            textureCompression.intValue = compressionOption;

                        EditorGUI.BeginChangeCheck();
                        var oldWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 100f;
                        bool crunchOption = EditorGUILayout.Toggle(Styles.crunchCompression,
                            textureCompressionCrunched.boolValue);
                        EditorGUIUtility.labelWidth = oldWidth;
                        if (EditorGUI.EndChangeCheck())
                            textureCompressionCrunched.boolValue = crunchOption;
                        GUILayout.EndHorizontal();
                    }

                    using (new EditorGUI.DisabledScope(!explicitFormat))
                    {
                        EditorGUI.BeginChangeCheck();

                        int format = EditorGUILayout
                            .EnumPopup("Texture Format", (TextureFormat) m_TextureFormat.intValue).GetHashCode();

                        if (EditorGUI.EndChangeCheck())
                        {
                            if (!SystemInfo.SupportsTextureFormat((TextureFormat) format))
                                EditorGUILayout.HelpBox("Texture format unsupported.", MessageType.Warning, true);

                            m_TextureFormat.intValue = format;
                        }
                    }
                }
            }
        }


        void CacheSerializedProperties()
        {
            SerializedProperty inputTextures = serializedObject.FindProperty("m_InputTextures");
            SerializedProperty inputTexturesSettings = serializedObject.FindProperty("m_InputTextureSettings");

            for (int i = 0; i < 4; ++i)
            {
                m_InputTextures[i] = inputTextures.GetArrayElementAtIndex(i);
                m_InputTextureSettings[i] = inputTexturesSettings.GetArrayElementAtIndex(i);
            }

            m_IsReadable = serializedObject.FindProperty("m_IsReadable");
            m_sRGBTexture = serializedObject.FindProperty("m_sRGBTexture");

            m_EnableMipMap = serializedObject.FindProperty("m_EnableMipMap");
            m_StreamingMipMaps = serializedObject.FindProperty("m_StreamingMipMaps");
            m_StreamingMipMapPriority = serializedObject.FindProperty("m_StreamingMipMapPriority");

            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_WrapMode = serializedObject.FindProperty("m_WrapMode");
            m_AnisotropicLevel = serializedObject.FindProperty("m_AnisotropicLevel");

            m_TexturePlatformSettings = serializedObject.FindProperty("m_TexturePlatformSettings");
            m_TextureFormat = serializedObject.FindProperty("m_TextureFormat");
            m_UseExplicitTextureFormat = serializedObject.FindProperty("m_UseExplicitTextureFormat");
        }

        static void DrawMinMaxSlider(SerializedProperty property)
        {
            using (var horizontal = new EditorGUILayout.HorizontalScope())
            {
                using (var propertyScope = new EditorGUI.PropertyScope(horizontal.rect, Styles.remapRange, property))
                {
                    var v = property.vector2Value;

                    const int kFloatFieldWidth = 50;
                    const int kSeparatorWidth = 5;

                    float indentOffset = EditorGUI.indentLevel * 15f;
                    var lineRect = EditorGUILayout.GetControlRect();
                    var labelRect = new Rect(lineRect.x, lineRect.y, EditorGUIUtility.labelWidth - indentOffset,
                        lineRect.height);
                    var floatFieldLeft = new Rect(labelRect.xMax, lineRect.y, kFloatFieldWidth + indentOffset,
                        lineRect.height);
                    var sliderRect = new Rect(floatFieldLeft.xMax + kSeparatorWidth - indentOffset, lineRect.y,
                        lineRect.width - labelRect.width - kFloatFieldWidth * 2 - kSeparatorWidth * 2, lineRect.height);
                    var floatFieldRight = new Rect(sliderRect.xMax + kSeparatorWidth - indentOffset, lineRect.y,
                        kFloatFieldWidth + indentOffset, lineRect.height);

                    EditorGUI.PrefixLabel(labelRect, propertyScope.content);
                    v.x = EditorGUI.FloatField(floatFieldLeft, v.x);
                    EditorGUI.MinMaxSlider(sliderRect, ref v.x, ref v.y, 0.0f, 1.0f);
                    v.y = EditorGUI.FloatField(floatFieldRight, v.y);

                    property.vector2Value = v;

                }

            }

        }
    }
}