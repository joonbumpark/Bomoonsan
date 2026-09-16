// 절차적 지형용 URP 커스텀 셰이더 (Shader Graph 미사용, 순수 HLSL)
// - 메시에 의미 있는 UV가 없으므로 Triplanar Mapping으로 텍스처/노멀맵을 3축 투사한다
// - 경사도(월드 노멀의 Y)를 기준으로 평지(Flat)와 절벽(Cliff) 텍스처를 블렌딩한다
// - ProceduralTerrainMesh.cs가 heightGradient로 굽는 _BaseMap(기본 컬러)을 곱해서
//   텍스처와 자연스럽게 섞는다. 메시에 Vertex Color가 있으면 그것도 함께 곱한다.
Shader "Mountains/TerrainBlend"
{
    Properties
    {
        [Header(Base Color Blending)]
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _BaseMap ("Height Gradient (base tint texture)", 2D) = "white" {}
        _HeightTintStrength ("Height Gradient Tint Strength", Range(0, 1)) = 1
        _MinHeight ("Terrain Min Height (World Y)", Float) = 0
        _MaxHeight ("Terrain Max Height (World Y)", Float) = 120

        [Header(Flat Ground Triplanar)]
        _FlatAlbedo ("Flat Albedo (grass/dirt)", 2D) = "white" {}
        _FlatNormal ("Flat Normal Map", 2D) = "bump" {}
        _FlatTiling ("Flat Tiling", Float) = 0.1

        [Header(Cliff Triplanar)]
        _CliffColor ("Cliff Color Tint", Color) = (1, 1, 1, 1)
        _CliffAlbedo ("Cliff Albedo (rock)", 2D) = "white" {}
        _CliffNormal ("Cliff Normal Map", 2D) = "bump" {}
        _CliffTiling ("Cliff Tiling", Float) = 0.1

        [Header(Path Triplanar)]
        _PathColor ("Path Color Tint", Color) = (0.55, 0.44, 0.32, 1)
        _PathAlbedo ("Path Albedo (dirt/gravel)", 2D) = "white" {}
        _PathNormal ("Path Normal Map", 2D) = "bump" {}
        _PathMask ("Path Mask (baked, R channel)", 2D) = "black" {}
        _Tiling ("Path Tiling", Float) = 0.1

        [Header(Triplanar Settings)]
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 32)) = 4

        [Header(Slope Settings)]
        _SlopeCutoff ("Slope Cutoff (0=flat, 1=vertical)", Range(0, 1)) = 0.18
        _SlopeSmoothness ("Slope Blend Smoothness", Range(0.001, 0.5)) = 0.12
        _HeightBlendSharpness ("Height Blend Sharpness", Range(0.01, 0.9)) = 0.2

        [Header(Surface)]
        _NormalStrength ("Normal Map Strength", Range(0, 2)) = 1

        [Header(Toon Shading)]
        [Toggle(_TOON_SHADING_ON)] _ToonShading ("Enable Toon Shading", Float) = 1
        _ToonBands ("Light Bands", Range(2, 6)) = 3
        _ShadowTint ("Shadow Band Brightness", Range(0, 1)) = 0.65
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1.1

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1, 0.96, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _TOON_SHADING_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FlatAlbedo);    SAMPLER(sampler_FlatAlbedo);
            TEXTURE2D(_FlatNormal);    SAMPLER(sampler_FlatNormal);
            TEXTURE2D(_CliffAlbedo);   SAMPLER(sampler_CliffAlbedo);
            TEXTURE2D(_CliffNormal);   SAMPLER(sampler_CliffNormal);
            TEXTURE2D(_PathAlbedo);    SAMPLER(sampler_PathAlbedo);
            TEXTURE2D(_PathNormal);    SAMPLER(sampler_PathNormal);
            TEXTURE2D(_PathMask);      SAMPLER(sampler_PathMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CliffColor;
                float4 _PathColor;
                float _HeightTintStrength;
                float _MinHeight;
                float _MaxHeight;
                float _Tiling;
                float _FlatTiling;
                float _CliffTiling;
                float _TriplanarSharpness;
                float _SlopeCutoff;
                float _SlopeSmoothness;
                float _HeightBlendSharpness;
                float _NormalStrength;
                float _ToonBands;
                float _ShadowTint;
                float _AmbientStrength;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR; // 버텍스 컬러 없는 메시는 자동으로 흰색(1,1,1,1)이 들어온다
                float2 uv         : TEXCOORD0; // ProceduralTerrainMesh가 채우는 평면 UV(0..1) — 길 마스크 샘플링용
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 vertexColor : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.vertexColor = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            // ---- Triplanar 유틸리티 ----
            // UV 없이 월드 좌표를 3개 평면(X/Y/Z를 바라보는 방향)에 각각 투사해서 샘플링하고,
            // 월드 노멀이 어느 축에 가까운지로 세 결과를 섞는다. 경사가 아무리 급해도 텍스처
            // 밀도가 항상 일정하게 유지되어(= UV 매핑처럼 압축/스트레칭되지 않아) 절벽에서도
            // 텍스처가 늘어지지 않는다.
            float3 TriplanarWeights(float3 normalWS, float sharpness)
            {
                float3 blend = pow(abs(normalWS), sharpness);
                return blend / max(blend.x + blend.y + blend.z, 1e-5);
            }

            float3 SampleTriplanarAlbedo(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 blend, float tiling)
            {
                float3 uvX = positionWS.zyx; // X축 투사면: ZY 평면
                float3 uvY = positionWS.xzy; // Y축 투사면: XZ 평면
                float3 uvZ = positionWS.xyz; // Z축 투사면: XY 평면

                half3 colX = SAMPLE_TEXTURE2D(tex, samp, uvX.xy * tiling).rgb;
                half3 colY = SAMPLE_TEXTURE2D(tex, samp, uvY.xy * tiling).rgb;
                half3 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ.xy * tiling).rgb;

                return colX * blend.x + colY * blend.y + colZ * blend.z;
            }

            // Whiteout Blend 방식의 Triplanar 노멀 매핑. 각 축의 탄젠트 공간 노멀맵 값을
            // 월드 축에 맞게 재배치한 뒤 블렌드 가중치로 합산하고 다시 정규화한다.
            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 positionWS, float3 blend,
                                          float3 normalWS, float tiling, float strength)
            {
                float3 uvX = positionWS.zyx;
                float3 uvY = positionWS.xzy;
                float3 uvZ = positionWS.xyz;

                float3 tNormalX = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvX.xy * tiling), strength);
                float3 tNormalY = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvY.xy * tiling), strength);
                float3 tNormalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uvZ.xy * tiling), strength);

                float3 signVec = sign(normalWS);

                tNormalX = float3(tNormalX.xy + normalWS.zy, abs(tNormalX.z) * signVec.x);
                tNormalY = float3(tNormalY.xy + normalWS.xz, abs(tNormalY.z) * signVec.y);
                tNormalZ = float3(tNormalZ.xy + normalWS.xy, abs(tNormalZ.z) * signVec.z);

                float3 worldNormal =
                    tNormalX.zyx * blend.x +
                    tNormalY.xzy * blend.y +
                    tNormalZ.xyz * blend.z;

                return normalize(worldNormal);
            }

            // 단순 Lerp는 두 텍스처를 직선 그라디언트로만 섞어서 경계가 평평한 띠처럼
            // 보인다. Height Blend는 각 텍스처 고유의 "높이"(heightA/heightB)가 튀어나온
            // 곳부터 먼저 섞이게 만들어 울퉁불퉁한 입체적 경계를 만든다. 별도 높이맵 텍스처가
            // 없으므로 이미 샘플링한 Albedo의 밝기(luminance)를 가짜 높이맵으로 재사용한다.
            float HeightBlend(float heightA, float heightB, float t, float sharpness)
            {
                float ma = max(heightA + (1.0 - t), heightB + t) - sharpness;
                float bA = max(heightA + (1.0 - t) - ma, 0.0);
                float bB = max(heightB + t - ma, 0.0);
                return bB / max(bA + bB, 1e-5);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 triplanarBlend = TriplanarWeights(normalWS, _TriplanarSharpness);

                // ---- 경사도 기반 Flat/Cliff 선택 (0=평지, 1=수직절벽) ----
                float slope = 1.0 - saturate(normalWS.y);
                float slopeT = smoothstep(_SlopeCutoff - _SlopeSmoothness, _SlopeCutoff + _SlopeSmoothness, slope);

                float3 flatAlbedoRaw = SampleTriplanarAlbedo(TEXTURE2D_ARGS(_FlatAlbedo, sampler_FlatAlbedo), IN.positionWS, triplanarBlend, _FlatTiling);
                float3 cliffAlbedoRaw = SampleTriplanarAlbedo(TEXTURE2D_ARGS(_CliffAlbedo, sampler_CliffAlbedo), IN.positionWS, triplanarBlend, _CliffTiling);

                // 경사도(slopeT)를 기본 블렌드 비율로 쓰되, 실제 경계는 각 텍스처의 밝기를
                // 가짜 높이맵 삼아 Height Blend로 울퉁불퉁하게 다듬는다. 틴트를 입히기 전
                // 원본 텍스처 밝기로 계산해야 틴트 색상에 따라 블렌드 경계 모양이 흔들리지 않는다.
                float flatHeight = dot(flatAlbedoRaw, float3(0.299, 0.587, 0.114));
                float cliffHeight = dot(cliffAlbedoRaw, float3(0.299, 0.587, 0.114));
                float blendT = HeightBlend(flatHeight, cliffHeight, slopeT, _HeightBlendSharpness);

                // ---- 색 틴트: Flat은 높이 그라디언트, Cliff는 고정 컬러 ----
                // 절벽(Cliff)은 고도에 따라 색이 바뀌는 높이 그라디언트를 타지 않고,
                // 항상 같은 암반 색(_CliffColor)으로 보이게 한다 — Flat만 높이 그라디언트로
                // 물든다. 길(Path)은 아래에서 이 틴트가 적용된 뒤에 따로 얹어서, 어느 쪽
                // 틴트에도 물들지 않고 텍스처 고유 색을 그대로 유지한다.
                float normHeight = saturate((IN.positionWS.y - _MinHeight) / max(_MaxHeight - _MinHeight, 0.0001));
                float3 baseTint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(0.5, normHeight)).rgb;
                float3 heightTint = lerp(half3(1, 1, 1), baseTint, _HeightTintStrength);

                float3 flatAlbedo = flatAlbedoRaw * heightTint;
                float3 cliffAlbedo = cliffAlbedoRaw * _CliffColor.rgb;
                float3 texAlbedo = lerp(flatAlbedo, cliffAlbedo, blendT);

                float3 flatNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_FlatNormal, sampler_FlatNormal), IN.positionWS, triplanarBlend, normalWS, _FlatTiling, _NormalStrength);
                float3 cliffNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_CliffNormal, sampler_CliffNormal), IN.positionWS, triplanarBlend, normalWS, _CliffTiling, _NormalStrength);
                float3 blendedNormal = normalize(lerp(flatNormal, cliffNormal, blendT));

                // ---- 기본 컬러 블렌딩 ----
                // _BaseColor와 메시의 Vertex Color(없으면 자동으로 흰색)를 곱해서
                // "절차적 메시의 기본 컬러"와 텍스처가 자연스럽게 섞이게 한다.
                float3 albedo = texAlbedo * _BaseColor.rgb * IN.vertexColor.rgb;

                // ---- 길(Path) 블렌딩 ----
                // ProceduralTerrainMesh.BakePathMaskTexture가 정점 해상도와 무관하게 구운
                // 고해상도 마스크 텍스처를, 메시가 이미 갖고 있는 평면 UV0으로 샘플링해서
                // 위에서 구한(이미 각자의 틴트가 적용된) Flat/Cliff 결과 위에 길 텍스처를
                // 한 번 더 얹는다. 단순 lerp로 섞으면 마스크 값 그대로 깨끗한 경계가 생겨서
                // "길을 그 위에 덮어씌운" 것처럼 보이므로, Flat/Cliff와 같은 Height Blend를
                // 재사용해서 풀의 밝기(가짜 높이)가 튀어나온 곳은 풀이 길 위로 삐져나온 것처럼
                // 보이게 한다.
                float pathT = SAMPLE_TEXTURE2D(_PathMask, sampler_PathMask, IN.uv).r;
                if (pathT > 0.0)
                {
                    float3 pathAlbedo = SampleTriplanarAlbedo(TEXTURE2D_ARGS(_PathAlbedo, sampler_PathAlbedo), IN.positionWS, triplanarBlend, _Tiling) * _PathColor.rgb;
                    float3 pathNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_PathNormal, sampler_PathNormal), IN.positionWS, triplanarBlend, normalWS, _Tiling, _NormalStrength);

                    float baseHeight = dot(albedo, float3(0.299, 0.587, 0.114));
                    float pathHeight = dot(pathAlbedo, float3(0.299, 0.587, 0.114));
                    float pathBlendT = HeightBlend(baseHeight, pathHeight, pathT, _HeightBlendSharpness);

                    albedo = lerp(albedo, pathAlbedo, pathBlendT);
                    blendedNormal = normalize(lerp(blendedNormal, pathNormal, pathBlendT));
                }

                // ---- 툰(셀) 셰이딩 ----
                // 사실적 PBR 대신, 빛을 받는 정도(N·L)를 _ToonBands 단계로 딱딱 끊어서
                // 카툰 특유의 경계가 뚜렷한 명암을 만든다. 그림자맵 감쇠도 함께 곱해서
                // 그림자 진 곳은 가장 어두운 밴드로 떨어지게 한다.
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = dot(blendedNormal, mainLight.direction);
                float lightTerm = saturate(NdotL) * mainLight.shadowAttenuation;

#if defined(_TOON_SHADING_ON)
                float bands = max(_ToonBands, 1.0);
                float shadeValue = floor(lightTerm * bands) / max(bands - 1.0, 1.0);
                shadeValue = saturate(shadeValue);
#else
                // 토글이 꺼지면 밴드 없이 부드러운(연속적인) 명암으로 돌아간다.
                float shadeValue = lightTerm;
#endif
                float shade = lerp(_ShadowTint, 1.0, shadeValue);

                float3 ambient = SampleSH(blendedNormal) * _AmbientStrength;
                float3 litColor = albedo * (mainLight.color * shade + ambient);

                // ---- 림 라이트 ----
                // 시야 방향과 노멀이 수직에 가까울수록(실루엣 가장자리) 밝아지는 테두리 빛.
                // 빛을 받는 쪽에서만 은은하게 보이도록 NdotL로 한 번 더 걸러준다.
                float rimFactor = pow(1.0 - saturate(dot(viewDirWS, blendedNormal)), _RimPower);
                float3 rim = _RimColor.rgb * rimFactor * _RimStrength * saturate(NdotL + 0.3);

                float3 finalColor = litColor + rim;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // 그림자를 드리우고 받으려면 이 패스가 반드시 필요하다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(ShadowAttributes IN)
            {
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
                return positionCS;
            }

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                OUT.positionHCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
