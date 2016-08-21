#ifndef MOISE_INCLUDED
#define NOISE_INCLUDED

#define oct 8
#define per 0.5
#define PI 3.1415926
#define cCorners 1.0 / 16.0
#define cSides 1.0 / 8.0
#define cCenter 1.0 / 4.0

float interpolate(float a, float b, float x) {
	float f = (1.0 - cos(x * PI)) * 0.5;
	return a * (1.0 - f) + b * f;
}

float rnd(float2 p) {
	return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float3 rnd3(float2 p)
{
    return 2.0 * (float3(rnd(p * 1), rnd(p * 2), rnd(p * 3)) - 0.5);
}

float irnd(float2 p) {
	float2 i = floor(p);
	float2 f = frac(p);
	float4 v = float4(rnd(float2(i.x, i.y)),
		rnd(float2(i.x + 1.0, i.y)),
		rnd(float2(i.x, i.y + 1.0)),
		rnd(float2(i.x + 1.0, i.y + 1.0)));
	return interpolate(interpolate(v.x, v.y, f.x), interpolate(v.z, v.w, f.x), f.y);
}

float noise(float2 p) {
	float t = 0.0;
	for (int i = 0; i < oct; i++) {
		float freq = pow(2.0, float(i));
		float amp = pow(per, float(oct - i));
		t += irnd(float2(p.x / freq, p.y / freq)) * amp;
	}
	return t;
}

float snoise(float2 p, float2 q, float2 r) {
	return noise(float2(p.x, p.y)) *        q.x  *        q.y +
		noise(float2(p.x, p.y + r.y)) *        q.x  * (1.0 - q.y) +
		noise(float2(p.x + r.x, p.y)) * (1.0 - q.x) *        q.y +
		noise(float2(p.x + r.x, p.y + r.y)) * (1.0 - q.x) * (1.0 - q.y);
}

#endif //NOISE_INCLUDED