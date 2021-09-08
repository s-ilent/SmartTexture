using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SmartTexture
{
// TODO: Convert from sRGB to linear color space if necessary.
// TODO: Texture compression / Format

    [Serializable]
    public enum TextureChannel
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3
    }

    [Serializable, Flags]
    public enum TextureChannelMask
    {
        Red = 1 << 0,
        Green = 1 << 1,
        Blue = 1 << 2,
        Alpha = 1 << 3,
    }

    /// <summary>
    /// Containts settings that apply color modifiers to each channel.
    /// </summary>
    [Serializable]
    public struct TexturePackingSettings
    {
        /// <summary>
        /// Outputs the inverted color (1.0 - color)
        /// </summary>
        public bool invertColor;

        /// <summary>
        /// Uses the combined rgb luminance factor.
        /// </summary>
        public bool useLuminance;

        /// <summary>
        /// Remaps the channel.
        /// </summary>
        public Vector2 remapRange;

        /// <summary>
        /// Outputs the selected channel.
        /// </summary>
        public TextureChannel channel;
    }

    public static class TextureExtension
    {
        static ComputeShader s_PackChannelCs;

        static ComputeShader packChannelCs
        {
            get
            {
                if (s_PackChannelCs == null)
                {
                    s_PackChannelCs = Resources.Load<ComputeShader>("PackChannel");
                }

                return s_PackChannelCs;
            }
        }

        static readonly int s_RedMap = Shader.PropertyToID("_RedMap");
        static readonly int s_GreenMap = Shader.PropertyToID("_GreenMap");
        static readonly int s_BlueMap = Shader.PropertyToID("_BlueMap");
        static readonly int s_AlphaMap = Shader.PropertyToID("_AlphaMap");

        static readonly int s_RedChannelParams = Shader.PropertyToID("_RedChannelParams");
        static readonly int s_GreenChannelParams = Shader.PropertyToID("_GreenChannelParams");
        static readonly int s_BlueChannelParams = Shader.PropertyToID("_BlueChannelParams");
        static readonly int s_AlphaChannelParams = Shader.PropertyToID("_AlphaChannelParams");

        static readonly int s_Output = Shader.PropertyToID("_Output");
        static readonly int s_OutputSize = Shader.PropertyToID("_OutputSize");

        public static void PackChannels(this Texture2D mask, Texture2D[] inputTextures,
            TexturePackingSettings[] settings, GraphicsFormat graphicsFormat, bool srgb, bool mipmaps)
        {
            if (inputTextures == null || inputTextures.Length != 4)
            {
                Debug.LogError("Invalid parameter to PackChannels. An array of 4 textures is expected");
                return;
            }

            if (!packChannelCs)
            {
                Debug.LogError("Coudn't find `PackChannels` compute shader.");
                return;
            }

            if (settings == null)
            {
                settings = new TexturePackingSettings[4];
                for (int i = 0; i < settings.Length; ++i)
                {
                    settings[i].remapRange = new Vector2(0.0f, 1.0f);
                }
            }

            int width = mask.width;
            int height = mask.height;

            packChannelCs.SetTexture(0, s_RedMap, inputTextures[0] != null ? inputTextures[0] : Texture2D.blackTexture);
            packChannelCs.SetTexture(0, s_GreenMap,
                inputTextures[1] != null ? inputTextures[1] : Texture2D.blackTexture);
            packChannelCs.SetTexture(0, s_BlueMap,
                inputTextures[2] != null ? inputTextures[2] : Texture2D.blackTexture);
            packChannelCs.SetTexture(0, s_AlphaMap,
                inputTextures[3] != null ? inputTextures[3] : Texture2D.blackTexture);

            packChannelCs.SetVector(s_RedChannelParams, GetShaderChannelParams(settings[0]));
            packChannelCs.SetVector(s_GreenChannelParams, GetShaderChannelParams(settings[1]));
            packChannelCs.SetVector(s_BlueChannelParams, GetShaderChannelParams(settings[2]));
            packChannelCs.SetVector(s_AlphaChannelParams, GetShaderChannelParams(settings[3]));

            var rtForm = GraphicsFormatUtility.GetRenderTextureFormat(graphicsFormat);
            var rtDesc = new RenderTextureDescriptor(width, height, rtForm, 0)
            {
                sRGB = srgb,
                useMipMap = mipmaps,
#if UNITY_2020_1_OR_NEWER
            mipCount = mipmaps ? mask.mipmapCount : 1,
#endif
                autoGenerateMips = false,
                enableRandomWrite = true,
            };

            var rt = new RenderTexture(rtDesc);
            rt.Create();

            packChannelCs.SetTexture(0, s_Output, rt);
            packChannelCs.SetVector(s_OutputSize, new Vector4(width, height, 1.0f / width, 1.0f / height));
            packChannelCs.Dispatch(0, (rt.width + 7) / 8, (rt.height + 7) / 8, 1);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            mask.ReadPixels(new Rect(0, 0, width, height), 0, 0, mipmaps);
            mask.Apply(mipmaps);

            RenderTexture.active = previous;
            rt.Release();
        }

        static Vector4 GetShaderChannelParams(in TexturePackingSettings settings)
        {
            float channel = settings.useLuminance ? 5 : (int) settings.channel + 1;
            channel *= (settings.invertColor ? -1.0f : 1.0f);
            return new Vector4(channel, settings.remapRange.x, settings.remapRange.y, 0.0f);
        }
    }
}