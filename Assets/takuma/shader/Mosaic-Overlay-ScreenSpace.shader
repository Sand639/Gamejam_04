Shader "Custom/Mosaic-Overlay-ScreenSpace-URP"
{
    Properties
    {
        _CellPix ("Cell Pixel Size", Float) = 40
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Mosaic"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _CellPix;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = vertexInput.positionCS;
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // スクリーン座標
                float2 uv = input.screenPos.xy / input.screenPos.w;

                // 画面サイズから1セルのUVサイズを計算
                float2 cellSize =
                    _CellPix / _ScreenParams.xy;

                // UVをモザイク単位に丸める
                float2 mosaicUV =
                    (floor(uv / cellSize) + 0.5) * cellSize;

                // カメラが描画したOpaque Textureを取得
                half3 color = 
                    SampleSceneColor(mosaicUV);

                return half4(color, 1.0);            }

            ENDHLSL
        }
    }
}