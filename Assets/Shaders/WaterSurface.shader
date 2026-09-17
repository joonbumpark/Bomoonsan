// 호수 표면용 URP 커스텀 셰이더 (Shader Graph 미사용, 순수 HLSL, TerrainBlend.shader와 같은 컨벤션)
// - 물가(shoreline)는 마스크로 그리지 않는다. 평평한 물 쿼드를 호수 bbox 전체에 그대로
//   깔아두고, 먼저 그려진 불투명 지형이 깊이 테스트로 잘라내게 한다 — 수면보다 높은
//   지형은 물을 픽셀 단위로 정확히 가리므로 물가가 실제 지오메트리와 항상 일치한다
//   (마스크로 그리면 텍스처 해상도/보간 오차 때문에 미세한 틈이 남았다).
// - _MaskTex의 R채널은 "사용자가 그린 폴리곤 안쪽인지"만 담아, 호수가 아닌 저지대로
//   물이 번지는 것만 막는다. G채널(정규화된 수심)은 얕은 색↔깊은 색 보간 + 포말 위치에 쓴다.
// - "물처럼 보이게" 하는 다섯 가지를 텍스처 없이 순수 계산으로 얹는다(각각 Strength를
//   0으로 두면 꺼짐): (1) 여러 사인파를 합쳐 노멀을 흔드는 잔물결 반짝임, (2) 그 노멀
//   기준 Blinn-Phong 스페큘러, (3) 환경 반사(Reflection Probe가 있으면 주변 지형/나무,
//   없으면 스카이박스 폴백), (4) 그 반사량을 시야각에 따라 키우는 프레넬(정면에서도
//   _ReflectionBase만큼은 반사되게 바닥을 깔아둔다), (5) 수심이 0에 가까운 물가에 흰 포말 라인(정점 지오메트리는
//   그대로 평평하게 둬서 depth-cutout 방식의 물가 정확도를 깨지 않는다 — 흔드는 건 노멀뿐).
// - 라이팅은 TerrainBlend의 툰 셰이딩 없이 GetMainLight + SampleSH로만 심플하게 계산한다.
Shader "Mountains/WaterSurface"
{
    Properties
    {
        _MaskTex ("Water Mask (baked, R=coverage G=depth)", 2D) = "black" {}
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.65, 0.65, 0.55)
        _DeepColor ("Deep Color", Color) = (0.08, 0.25, 0.35, 0.9)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1.0

        [Header(Ripple Shimmer)]
        _WaveScale ("Wave Scale", Float) = 0.15
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveStrength ("Wave Normal Strength", Range(0, 1)) = 0.15

        [Header(Specular Highlight)]
        _SpecularColor ("Specular Color", Color) = (1, 1, 0.95, 1)
        _SpecularPower ("Specular Power", Range(1, 256)) = 60
        _SpecularStrength ("Specular Strength", Range(0, 4)) = 1.5

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength (전체 배수)", Range(0, 1)) = 0.6
        _ReflectionBase ("Reflection When Looking Down (정면 반사량)", Range(0, 1)) = 0.35
        _FresnelPower ("Fresnel Power (클수록 스칠 때만 강해짐)", Range(0.5, 8)) = 3
        _ReflectionRoughness ("Reflection Roughness (0=거울, 1=흐릿)", Range(0, 1)) = 0.05

        [Header(Shore Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth ("Foam Width (normalized depth)", Range(0, 1)) = 0.08
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // ZTest LEqual(기본값)을 명시적으로 남겨둔다 — 물가를 잘라내는 게 바로 이
            // 깊이 테스트라, 나중에 실수로 Always로 바꾸면 물이 지형을 뚫고 나온다.
            ZTest LEqual
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // Reflection Probe 및 Skybox 반사 바인딩을 위한 필수 Multi-compile 키워드
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _AmbientStrength;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float4 _SpecularColor;
                float _SpecularPower;
                float _SpecularStrength;
                float _FresnelPower;
                float _ReflectionStrength;
                float _ReflectionBase;
                float _ReflectionRoughness;
                float4 _FoamColor;
                float _FoamWidth;
                float _FoamStrength;
            CBUFFER_END

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
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 여러 사인파를 합쳐서(방향/주파수/속도를 서로 다르게) 평평한 물 표면의
            // 노멀만 흔든다 — 실제 정점을 움직이지 않으므로 depth-cutout 방식의 물가
            // 정확도는 그대로 유지되면서, 빛을 받을 때마다 반짝이는 잔물결처럼 보인다.
            float3 RippleNormal(float2 positionXZ, float3 flatNormalWS)
            {
                float2 p = positionXZ * _WaveScale;
                float t = _Time.y * _WaveSpeed;

                float wave1 = sin(p.x * 1.0 + p.y * 0.6 + t * 1.0);
                float wave2 = sin(p.x * -0.7 + p.y * 1.3 + t * 1.4);
                float wave3 = sin(p.x * 0.4 - p.y * 0.9 + t * 0.7);

                float2 tilt = float2(wave1 + wave3, wave2 - wave3) * _WaveStrength;
                float3 rippled = normalize(float3(tilt.x, 1.0, tilt.y));

                // flatNormalWS는 항상 (0,1,0)에 가깝지만(물 쿼드는 평평함), 혹시 있을
                // 기울어진 배치에도 대응하도록 그 기준으로 흔든다.
                return normalize(flatNormalWS + rippled - float3(0, 1, 0));
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // R = 폴리곤 안쪽인지(0 또는 1), G = 정규화된 수심(색 보간 + 포말 위치용).
                // R은 이진값이라 0.5를 문턱으로 자른다 — 텍스처 이중선형 필터링이
                // 만드는 1텍셀 폭 전이 구간을 가운데서 깔끔하게 나눈다.
                float2 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).rg;
                clip(mask.r - 0.5);
                float depth = mask.g;

                float3 flatNormalWS = normalize(IN.normalWS);
                float3 normalWS = RippleNormal(IN.positionWS.xz, flatNormalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 ambient = SampleSH(normalWS) * _AmbientStrength;
                float3 lightTerm = mainLight.color * (NdotL * mainLight.shadowAttenuation) + ambient;

                float4 tint = lerp(_ShallowColor, _DeepColor, depth);
                float3 litColor = tint.rgb * lightTerm;

                // ---- 스페큘러 하이라이트 ----
                // 잔물결로 흔든 노멀 기준 Blinn-Phong. 그림자 속에서는 반짝이지 않게
                // shadowAttenuation도 곱한다.
                float3 halfDirWS = normalize(mainLight.direction + viewDirWS);
                float specTerm = pow(saturate(dot(normalWS, halfDirWS)), _SpecularPower) * _SpecularStrength;
                float3 specular = mainLight.color * specTerm * _SpecularColor.rgb * mainLight.shadowAttenuation;

                // ---- 환경 반사 ----
                // 반사색은 잔물결로 흔든 노멀 기준 반사 벡터로 환경을 샘플링해서 얻는다 —
                // 씬에 Reflection Probe가 있으면 그걸(주변 지형/나무), 없으면 스카이박스
                // 기반 전역 폴백(하늘)을 비춘다. 큐브맵 한 번 샘플링이라 모바일에서도 가볍다.
                //
                // 반사량은 프레넬(스치듯 볼수록 1에 가까움)에만 맡기지 않고 _ReflectionBase를
                // 바닥으로 깐다 — 프레넬만 쓰면 3인칭 카메라처럼 수면을 내려다보는 각도에서
                // 값이 1~2%까지 떨어져서 반사가 사실상 안 보인다.
                float fresnelCurve = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                float3 reflectVector = reflect(-viewDirWS, normalWS);
                float3 envColor = GlossyEnvironmentReflection(reflectVector, _ReflectionRoughness, 1.0);
                float reflectAmount = saturate(lerp(_ReflectionBase, 1.0, fresnelCurve) * _ReflectionStrength);

                // ---- 물가 포말 ----
                // 수심(depth)이 0에 가까운 물가일수록 흰 테두리를 얹는다.
                float foam = (1.0 - smoothstep(0.0, max(_FoamWidth, 1e-4), depth)) * _FoamStrength;

                float3 finalColor = lerp(litColor, envColor, reflectAmount) + specular;
                finalColor = lerp(finalColor, _FoamColor.rgb, saturate(foam));

                return half4(finalColor, tint.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}