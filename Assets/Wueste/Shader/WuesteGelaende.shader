Shader "Wueste/Gelaende"
{
    Properties
    {
        _SplatMap("Splat (Sand, Fels, Ton, Kies)", 2D) = "red" {}
        _MacroMap("Makro", 2D) = "gray" {}
        _SandMap("Sand", 2D) = "white" {}
        [Normal] _SandNormal("Sand Normal", 2D) = "bump" {}
        _SandTile("Sand Kachelgroesse (m)", Float) = 3
        _SandTile2("Sand Kachelgroesse 2 (m)", Float) = 7.7
        [Normal] _RippleNormal("Windrippel Normal", 2D) = "bump" {}
        _RippleTile("Rippel Kachelgroesse (m)", Float) = 4
        _RippleStrength("Rippel Staerke", Range(0, 2)) = 0.7
        _WindAngle("Windrichtung (Bogenmass)", Float) = 0.4887
        _RockMap("Fels", 2D) = "white" {}
        [Normal] _RockNormal("Fels Normal", 2D) = "bump" {}
        _RockTile("Fels Kachelgroesse (m)", Float) = 2.6
        _RockTile2("Fels Kachelgroesse 2 (m)", Float) = 9
        _ClayMap("Ton", 2D) = "white" {}
        [Normal] _ClayNormal("Ton Normal", 2D) = "bump" {}
        _ClayTile("Ton Kachelgroesse (m)", Float) = 4
        _ClayTint("Ton Tint", Color) = (1, 0.82, 0.62, 1)
        _GravelMap("Kies", 2D) = "white" {}
        [Normal] _GravelNormal("Kies Normal", 2D) = "bump" {}
        _GravelTile("Kies Kachelgroesse (m)", Float) = 2.5
        _SmoothSand("Glanz Sand", Range(0, 1)) = 0.04
        _SmoothRock("Glanz Fels", Range(0, 1)) = 0.08
        _SmoothClay("Glanz Ton", Range(0, 1)) = 0.05
        _SmoothGravel("Glanz Kies", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _SplatMap_ST;
            float _SandTile;
            float _SandTile2;
            float _RippleTile;
            float _RippleStrength;
            float _WindAngle;
            float _RockTile;
            float _RockTile2;
            float _ClayTile;
            half4 _ClayTint;
            float _GravelTile;
            half _SmoothSand;
            half _SmoothRock;
            half _SmoothClay;
            half _SmoothGravel;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SplatMap); SAMPLER(sampler_SplatMap);
            TEXTURE2D(_MacroMap);
            TEXTURE2D(_SandMap); SAMPLER(sampler_SandMap);
            TEXTURE2D(_SandNormal);
            TEXTURE2D(_RippleNormal);
            TEXTURE2D(_RockMap);
            TEXTURE2D(_RockNormal);
            TEXTURE2D(_ClayMap);
            TEXTURE2D(_ClayNormal);
            TEXTURE2D(_GravelMap);
            TEXTURE2D(_GravelNormal);


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.uv = i.uv;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float2 Rot(float2 p, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            half3 TexS(TEXTURE2D_PARAM(tex, smp), float2 uv)
            {
                return SAMPLE_TEXTURE2D(tex, smp, uv).rgb;
            }

            half3 NormS(TEXTURE2D_PARAM(tex, smp), float2 uv, half scale)
            {
                return UnpackNormalScale(SAMPLE_TEXTURE2D(tex, smp, uv), scale);
            }

            half Crisp(half w, half l)
            {
                return smoothstep(0.38h, 0.62h, w + (l - 0.5h) * 0.5h);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float2 wuv = i.positionWS.xz;
                half4 sp = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, i.uv);
                half3 mac = SAMPLE_TEXTURE2D(_MacroMap, sampler_SplatMap, i.uv).rgb * 2.0h;

                float2 su1 = wuv / _SandTile;
                float2 su2 = Rot(wuv, 0.55) / _SandTile2;
                half3 sand = lerp(TexS(TEXTURE2D_ARGS(_SandMap, sampler_SandMap), su1), TexS(TEXTURE2D_ARGS(_SandMap, sampler_SandMap), su2), 0.4h);
                half3 tSand = NormS(TEXTURE2D_ARGS(_SandNormal, sampler_SandMap), su1, 0.6h);

                half flat = saturate((N.y - 0.90h) / 0.075h);
                float2 ru = Rot(wuv, -_WindAngle) / _RippleTile;
                half3 tR = NormS(TEXTURE2D_ARGS(_RippleNormal, sampler_SandMap), ru, _RippleStrength * flat);
                tR.xy = Rot(tR.xy, _WindAngle);
                tSand = half3(tSand.xy + tR.xy, tSand.z * tR.z);

                float2 gu1 = wuv / _GravelTile;
                float2 gu2 = Rot(wuv, 0.7) / (_GravelTile * 2.4);
                half3 grav = lerp(TexS(TEXTURE2D_ARGS(_GravelMap, sampler_SandMap), gu1), TexS(TEXTURE2D_ARGS(_GravelMap, sampler_SandMap), gu2), 0.4h);
                half3 tGrav = NormS(TEXTURE2D_ARGS(_GravelNormal, sampler_SandMap), gu1, 1.0h);

                float2 cu = wuv / _ClayTile;
                half3 clay = TexS(TEXTURE2D_ARGS(_ClayMap, sampler_SandMap), cu) * _ClayTint.rgb;
                half3 tClay = NormS(TEXTURE2D_ARGS(_ClayNormal, sampler_SandMap), cu, 1.0h);

                float3 bw = pow(abs(N), 4.0);
                bw /= (bw.x + bw.y + bw.z);
                float2 ux = i.positionWS.zy;
                float2 uy = i.positionWS.xz;
                float2 uz = i.positionWS.xy;
                half3 rockA = TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), ux / _RockTile) * bw.x + TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), uy / _RockTile) * bw.y + TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), uz / _RockTile) * bw.z;
                float k2 = 1.0 / _RockTile2;
                half3 rockB = TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), (ux + float2(0.37, 0.61)) * k2) * bw.x + TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), (uy + float2(0.37, 0.61)) * k2) * bw.y + TexS(TEXTURE2D_ARGS(_RockMap, sampler_SandMap), (uz + float2(0.37, 0.61)) * k2) * bw.z;
                half3 rock = lerp(rockA, rockB, 0.35h);
                half3 tX = NormS(TEXTURE2D_ARGS(_RockNormal, sampler_SandMap), ux / _RockTile, 1.0h);
                half3 tY = NormS(TEXTURE2D_ARGS(_RockNormal, sampler_SandMap), uy / _RockTile, 1.0h);
                half3 tZ = NormS(TEXTURE2D_ARGS(_RockNormal, sampler_SandMap), uz / _RockTile, 1.0h);

                half lg = dot(grav, half3(0.3h, 0.59h, 0.11h));
                half lc = dot(clay, half3(0.3h, 0.59h, 0.11h));
                half lr = dot(rock, half3(0.3h, 0.59h, 0.11h));
                half fg = Crisp(sp.a, lg);
                half fc = Crisp(sp.b, lc);
                half fr = Crisp(sp.g, lr);

                half3 tCol = lerp(tSand, tGrav, fg);
                tCol = lerp(tCol, tClay, fc);
                half3 tPl = half3(tCol.xy + N.xz, abs(tCol.z) * N.y);
                float3 nPlanar = tPl.xzy;

                half3 wX = half3(tX.xy + N.zy, abs(tX.z) * N.x);
                half3 wY = half3(tY.xy + N.xz, abs(tY.z) * N.y);
                half3 wZ = half3(tZ.xy + N.xy, abs(tZ.z) * N.z);
                float3 nRock = wX.zyx * bw.x + wY.xzy * bw.y + wZ.xyz * bw.z;

                float3 nW = normalize(lerp(normalize(nPlanar), normalize(nRock), fr));

                half3 alb = lerp(sand, grav, fg);
                alb = lerp(alb, clay, fc);
                alb = lerp(alb, rock, fr);
                alb *= mac;

                half sm = lerp(_SmoothSand, _SmoothGravel, fg);
                sm = lerp(sm, _SmoothClay, fc);
                sm = lerp(sm, _SmoothRock, fr);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = nW;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(nW);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = alb;
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = sm;
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1;
                s.emission = half3(0, 0, 0);
                s.alpha = 1;

                half4 col = UniversalFragmentPBR(inputData, s);
                col.rgb = MixFog(col.rgb, inputData.fogCoord);
                col.a = 1;
                return col;
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
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct SAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVary { float4 positionCS : SV_POSITION; };
            SVary ShadowVert(SAttr i)
            {
                SVary o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                return o;
            }
            half4 ShadowFrag(SVary i) : SV_Target { return 0; }
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
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct DAttr { float4 positionOS : POSITION; };
            struct DVary { float4 positionCS : SV_POSITION; };
            DVary DepthVert(DAttr i)
            {
                DVary o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }
            half DepthFrag(DVary i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex NVert
            #pragma fragment NFrag
            struct NAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct NVary { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            NVary NVert(NAttr i)
            {
                NVary o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 NFrag(NVary i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
