Shader "Custom/StarLight"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Cull Off Lighting Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 5.0

            #pragma vertex   vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            uint  shootingStarCache;
            float shootingStarSize;
            int   hugeStarRatio;
            //Z per X
            float aspect;

            struct ShadingObjectBuffer
            {
                uint   id;
                bool   isShooting;
                float3 pos;
                float3 dir;
                float  power;
                float4 color;
                float  starSize;
                float  hugeStarSize;
                float  random;
                float  twinkle;
            };

            StructuredBuffer<ShadingObjectBuffer> buf;

            struct v2g
            {
                float4 pos        : SV_POSITION;
                float4 col        : COLOR;
                //x:starsize, y:hugeStarSize.
                float2 starSize   : TEXCOORD1;
                //x:id, y:isShooting.
                float2 idShoot    : TEXCOORD2;
                //x:random, y:twinkle factor.
                float2 rndTwinkle : TEXCOORD3;
            };

            v2g vert(uint id: SV_VertexID)
            {
                v2g o;
                o.pos          = float4(buf[id].pos, 1);
                o.col          = buf[id].color;
                o.starSize     = float2(buf[id].starSize,buf[id].hugeStarSize);
                o.idShoot      = float2((float)id,(buf[id].isShooting) ? 1 : 0);
                o.rndTwinkle.x = buf[id].random;
                o.rndTwinkle.y = (id.x % 2 == 0) ? sin(_Time.x * buf[id].twinkle * buf[id].random) : cos(_Time.x * buf[id].twinkle * buf[id].random);
                return o;
            }

            struct g2f
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 idShoot    : TEXCOORD2;
                float2 rndTwinkle : TEXCOORD3;
                float4 col        : COLOR;
            };

            [maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> outStream)
            {
                g2f output;
                float4 pos     = input[0].pos;
                float2 sSize   = input[0].starSize;
                fixed  twinkle = input[0].rndTwinkle.y * 0.6 + 0.4;

                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        //shooting star is 1, otherwise 0.
                        fixed isShoot = input[0].idShoot.y;
                        //huge star && !shooting star is 1, otherwise 0.
                        fixed isHuge  = step(1, 1 - isShoot - (input[0].idShoot.x % hugeStarRatio));
                        //ordinary star is 1.
                        fixed isNrml  = step(1, 1 - isShoot - isHuge);

                        output.pos  = pos;
                        //shooting star vert pos.
                        output.pos += float4((x * shootingStarSize * aspect - shootingStarSize / 2 * aspect), 0, (y * shootingStarSize - shootingStarSize / 2), 0) * isShoot;
                        //huge star vert pos.
                        output.pos += float4((x * sSize.y * aspect - sSize.y / 2 * aspect) * twinkle, 0, (y * sSize.y - sSize.y / 2) * twinkle, 0) * isHuge;
                        //ordinary star vert pos.
                        output.pos += float4((x * sSize.x * aspect - sSize.x / 2 * aspect) * twinkle, 0, (y * sSize.x - sSize.x / 2) * twinkle, 0) * isNrml;

                        output.pos = mul(UNITY_MATRIX_MVP, output.pos);
                        output.uv = float2(x,y);
                        output.idShoot = input[0].idShoot;
                        output.col = input[0].col;
                        output.rndTwinkle = input[0].rndTwinkle;
                        outStream.Append(output);
                    }
                }
                outStream.RestartStrip();
            }

            fixed4 frag(g2f i) : COLOR
            {
                if(i.idShoot.x < (float)shootingStarCache && i.idShoot.y == 0)
                {
                    discard;
                }
                float2 uv = i.uv * 2 - 1;
                float2 rt = i.rndTwinkle;
                fixed twinkleCol = (i.idShoot.x % hugeStarRatio == 0 || i.idShoot.y == 1) ? 1 : rt.y * 0.3 + 0.7;
                fixed glare = 0.1;
                fixed4 col;

                if ((i.idShoot.x % 3 != 0 || i.idShoot.x % hugeStarRatio == 0) && i.idShoot.y == 0)
                {
                    fixed shapeDpth  = 0.18;
                    fixed rotateSpd  = _Time.x * 2;
                    fixed rndStarEdg = rt.x * 0.5 + 0.5;
                    fixed edgCount   = 3.0 * (rt.x * 0.3 + 1);
                    fixed shape = pow(abs(1 - abs(sin((atan2(uv.y * rndStarEdg, uv.x) + rotateSpd) * edgCount))), 2) * rt.x * shapeDpth * rt.y;
                    col = fixed4((glare / clamp(distance(uv, fixed2(0, 0)) - shape, 0, 1) - glare).xxxx);
                }
                else
                {
                    col = fixed4(glare / distance(uv, fixed2(0, 0)).xxxx);
                    col.a = pow(col.a, 1.8);
                }
                if (i.idShoot.y == 0)
                {
                    col *= i.col;
                }
                return col * twinkleCol;
            }
            ENDCG
        }
    }
    Fallback Off
}