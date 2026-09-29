// 디졸브(노이즈로 깎여 사라지는) 연출용 URP 커스텀 셰이더 (Shader Graph 미사용, 순수 HLSL)
//
// URP Lit을 통째로 포크하면 버전이 오를 때마다 include 구조를 따라가야 하므로, 이 프로젝트의
// TreeFade.shader와 같은 방식으로 필요한 패스만 직접 쓴다. PT 로우폴리 모델은 플랫 셰이딩이라
// 메인 디렉셔널 + SH 앰비언트만으로도 URP Lit과 거의 같아 보인다.
//
// 패스 구성과 이유:
//  - UniversalForward : 본체 렌더링. 메인 라이트 그림자를 "받는다".
//  - ShadowCaster     : 그림자를 "드리운다". 여기서도 같은 클립을 해야 몸이 사라진 만큼
//                       그림자도 같이 뚫린다(빠뜨리면 그림자만 멀쩡히 남는다).
//  - DepthNormals     : URP-Balanced-Renderer의 SSAO가 Source=Depth Normals라, 이 패스가
//                       없으면 이 오브젝트만 AO에서 빠져 살짝 떠 보인다.
//  - DepthOnly        : 나중에 Depth Texture를 켤 때를 대비. 클립은 동일하게 적용한다.
//
// 노이즈는 UV가 아니라 오브젝트 공간 좌표로 삼축 투사(triplanar)해서 샘플링한다 — PT 모델은
// 색 팔레트 아틀라스를 쓰기 때문에 UV가 작은 색 조각에 몰려 있어서, UV로 노이즈를 뽑으면
// 면마다 거의 단색이 나와 디졸브 패턴이 생기지 않는다. 깔끔한 UV를 가진 모델이라면
// Use UV Noise 체크로 UV 샘플링으로 바꿀 수 있다.
Shader "Mountains/Dissolve"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        _NoiseMap ("Dissolve Noise (R channel)", 2D) = "gray" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale (object space)", Float) = 1
        [Toggle(_DISSOLVE_NOISE_UV)] _UseUvNoise ("Use UV Noise", Float) = 0

        [Header(Edge Glow)]
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.35, 0.1, 1)
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 200

        // 네 패스가 완전히 같은 클립 계산을 써야 하므로(다르면 본체와 그림자 실루엣이
        // 어긋난다) 공통 코드를 SubShader 수준에 한 번만 둔다.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _NoiseMap_ST;
            float4 _BaseColor;
            float4 _EdgeColor;
            float _DissolveAmount;
            float _NoiseScale;
            float _EdgeWidth;
            float _UseUvNoise;
        CBUFFER_END

        half SampleDissolveNoise(float2 uv, float3 positionOS, float3 normalOS)
        {
#if defined(_DISSOLVE_NOISE_UV)
            return SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, TRANSFORM_TEX(uv, _NoiseMap)).r;
#else
            // 삼축 투사: 면이 향한 방향의 가중치로 세 평면 샘플을 섞는다(TerrainBlend와 같은 방식).
            float3 blend = abs(normalOS) + 1e-4;
            blend /= (blend.x + blend.y + blend.z);

            float3 p = positionOS * _NoiseScale;
            half nx = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.yz).r;
            half ny = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.xz).r;
            half nz = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, p.xy).r;
            return nx * blend.x + ny * blend.y + nz * blend.z;
#endif
        }

        // 노이즈 텍스처에 완전한 흰색(1.0) 픽셀이 있으면 _DissolveAmount가 1이어도
        // clip(1 - 1) = 0이라 살아남는다(clip은 음수만 버린다) — 임계값을 아주 살짝
        // 넘겨서 1일 때 확실히 전부 사라지게 한다.
        void ClipDissolve(half noise)
        {
            clip(noise - _DissolveAmount * 1.0001);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _DISSOLVE_NOISE_UV
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 normalOS    : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.normalOS = IN.normalOS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half noise = SampleDissolveNoise(IN.uv, IN.positionOS, IN.normalOS);
                ClipDissolve(noise);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS);
                half3 color = albedo * (mainLight.color * NdotL * mainLight.shadowAttenuation + ambient);

                // 잘려나가기 직전(노이즈가 임계값 바로 위)인 픽셀만 1에 가까워져 경계가 타오른다.
                // _DissolveAmount가 0일 때는 아직 시작 전이므로 발광을 완전히 끈다 — 안 그러면
                // 노이즈가 낮은 영역이 가만히 있어도 빛난다.
                half band = saturate(1.0 - (noise - _DissolveAmount) / max(_EdgeWidth, 1e-4));
                color += _EdgeColor.rgb * band * step(1e-4, _DissolveAmount);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma shader_feature_local _DISSOLVE_NOISE_UV
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalOS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

                OUT.positionHCS = positionCS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = IN.normalOS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                ClipDissolve(SampleDissolveNoise(IN.uv, IN.positionOS, IN.normalOS));
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma shader_feature_local _DISSOLVE_NOISE_UV

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct DepthNormalsVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 normalOS    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes IN)
            {
                DepthNormalsVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.normalOS = IN.normalOS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings IN) : SV_Target
            {
                ClipDissolve(SampleDissolveNoise(IN.uv, IN.positionOS, IN.normalOS));
                // URP의 _CameraNormalsTexture는 월드 공간 노멀을 담는다.
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag

            #pragma shader_feature_local _DISSOLVE_NOISE_UV

            struct DepthOnlyAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct DepthOnlyVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 normalOS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            DepthOnlyVaryings DepthOnlyVert(DepthOnlyAttributes IN)
            {
                DepthOnlyVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = IN.normalOS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 DepthOnlyFrag(DepthOnlyVaryings IN) : SV_Target
            {
                ClipDissolve(SampleDissolveNoise(IN.uv, IN.positionOS, IN.normalOS));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
