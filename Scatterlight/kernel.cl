#define Scale -2.5f
#define minRadius2 0.125f
#define fixedRadius2 1.0f
#define foldingLimit 1.0f
#define MaxRayDist 64
#define NormalEpsilon 0.125f
#define NormDistCount 5
#define Quality 8
#define AoStepcount 5
#define DofPickup 0.005f
#define FogDensity 0.0625f
#define FocalPlane 3
#define BackgroundColor (float3)(0.5f, 0.5f, 0.5f)

#define IntMax 2147483647

int Rand(int seed) {
	return seed * 0x5DEECE66D + 0xB;
}

float3 Rotate(float3 u, float theta, float3 vec)
{
	float cost = cos(theta);
	float cost1 = 1 - cost;
	float sint = sin(theta);
	return (float3)(
		(cost + u.x * u.x * cost1) * vec.x +		(u.x * u.y * cost1 - u.z * sint) * vec.y +	(u.x * u.z * cost1 + u.y * sint) * vec.z,
		(u.y * u.x * cost1 + u.z * sint) * vec.x +	(cost + u.y * u.y * cost1) * vec.y +		(u.y * u.z * cost1 - u.x * sint) * vec.z,
		(u.z * u.x * cost1 - u.y * sint) * vec.x +	(u.z * u.y * cost1 + u.x * sint) * vec.y +	(cost + u.z * u.z * cost1) * vec.z
	);
}

float3 RayDir(float3 look, float3 up, float2 screenCoords, float fov) {
	float angle = atan2(screenCoords.y, -screenCoords.x);
	float dist = length(screenCoords) * fov;

	float3 axis = Rotate(look, angle, up);
	float3 direction = Rotate(axis, dist, look);

	return direction;
}

int ApplyDof(float3* position, float3* lookat, float focalPlane, int rand)
{
	const float dofAmount = DofPickup;
	int randx = Rand(rand);
	int randy = Rand(randx);
	float3 focalPosition = *position + *lookat * focalPlane;
	float3 xShift = cross((float3)(0, 0, 1), *lookat);
	float3 yShift = cross(*lookat, xShift);
	*lookat = normalize(*lookat + (float)randx / IntMax * dofAmount * xShift + (float)randy / IntMax * dofAmount * yShift);
	*position = focalPosition - *lookat * focalPlane;
	return randy;
}

float De(float3 z) {
	float3 offset = z;
	float dz = 1.0f;
	for (int n = 0; n < 2048; n++) {
		z = clamp(z, -foldingLimit, foldingLimit) * 2.0f - z;

		float r2 = dot(z, z);

		if (r2 > 65536)
			break;

		if (r2 < minRadius2) { 
			float temp = fixedRadius2 / minRadius2;
			z *= temp;
			dz *= temp;
		} else if (r2 < fixedRadius2) { 
			float temp = fixedRadius2 / r2;
			z *= temp;
			dz *= temp;
		}

		z = Scale * z + offset;
		dz = dz * fabs(Scale) + 1.0f;
	}
	return length(z) / dz;
}

float Trace(float3 origin, float3 direction, float quality)
{
	float distance = De(origin);

	float totalDistance = distance;
	for (int i = 0; i < 512 && totalDistance < MaxRayDist && distance * quality * 4 > totalDistance; i++) {
		distance = De(origin + direction * totalDistance) - totalDistance / quality;
		totalDistance += distance;
	}
	return totalDistance;
}

float NormDist(float3 pos, float3 dir, float totalDistance, int width) {
	for (int i = 0; i < NormDistCount; i++)
		totalDistance += De(pos + dir * totalDistance) - totalDistance / (sqrt((float)width) * Quality);
	return totalDistance;
}

float3 Normal(float3 pos) {
	const float delta = FLT_EPSILON * 16;
	float dppn = De(pos + (float3)(delta, delta, -delta));
	float dpnp = De(pos + (float3)(delta, -delta, delta));
	float dnpp = De(pos + (float3)(-delta, delta, delta));
	float dnnn = De(pos + (float3)(-delta, -delta, -delta));

	return normalize((float3)(
		(dppn + dpnp) - (dnpp + dnnn),
		(dppn + dnpp) - (dpnp + dnnn),
		(dpnp + dnpp) - (dppn + dnnn)
	));
}

float3 LambertCosine(float3 lightDir, float3 normal) {
	float value = dot(-lightDir, normal) * 0.5f + 0.5f;
	const float3 light = (float3)(1.0f, 1.0f, 1.0f);
	const float3 dark = (float3)(0.5f, 0.25f, 0.0f);
	return mix(dark, light, value);
}

float3 AO(float3 pos, float3 normal) {
	float delta = De(pos);
	float totalDistance = delta;
	for (int i = 0; i < AoStepcount; i++)
		totalDistance += De(pos + normal * totalDistance);

	float value = totalDistance / (delta * pown(2.0f, AoStepcount));
	const float3 light = (float3)(1, 1, 1);
	const float3 dark = (float3)(-0.5f, 0.0f, 0.2f);
	return mix(dark, light, value);
}

float3 Fog(float3 color, float focalDistance, float totalDistance) {
	float ratio = totalDistance / focalDistance;
	float value = 1 / exp(ratio * FogDensity);
	return mix(BackgroundColor, mix(color, (float3)(0.0f, 0.5f, 1.0f), value * 0.1f), value);
}

float3 Postprocess(float totalDistance, float3 origin, float3 direction, float focalDistance, float widthOverFov)
{
	if (totalDistance > MaxRayDist)
		return BackgroundColor;
	totalDistance = NormDist(origin, direction, totalDistance, widthOverFov);
	float3 finalPos = origin + direction * totalDistance;
	float3 normal = Normal(finalPos);
	float3 color = AO(finalPos, normal);
	color *= LambertCosine(direction, normal);
	color *= LambertCosine((float3)(0.57735026919f, 0.57735026919f, 0.57735026919f), normal);
	color = Fog(color, focalDistance, totalDistance);
	return clamp(color, 0.0f, 1.0f);
}

__kernel void Main(__global float4* screen, int width, int height, float4 position, float4 lookat, float4 updir, int frame, float fov, int slowRender, float focalDistance) {
	if ((get_group_id(0) + get_group_id(1) + frame) % slowRender != 0)
		return;
	int x = get_global_id(0);
	int y = get_global_id(1);
	if (x >= width || y >= height)
		return;
	
	float2 screenCoords = (float2)((float)x / width * 2 - 1, ((float)y / height * 2 - 1) * height / width);

	float3 pos = position.xyz;
	float3 look = lookat.xyz;
	float3 up = updir.xyz;

	int rand = Rand(x * width * height + y * width + frame * 10);
	for (int i = 0; i < 4; i++)
		rand = Rand(rand);
	
	if (frame != 0)
		rand = ApplyDof(&pos, &look, focalDistance, rand);

	float3 rayDir = RayDir(look, up, screenCoords, fov);

	float totalDistance = Trace(pos, rayDir, sqrt((float)width) * Quality / fov);
	
	float3 color = Postprocess(totalDistance, pos, rayDir, focalDistance, width / fov);

	if (frame > slowRender)
	{
		int frameFixed = frame / slowRender - 1;
		color = (color + screen[y * width + x].xyz * frameFixed) / (frameFixed + 1);
	}
	screen[y * width + x] = (float4)(color, totalDistance);
}
