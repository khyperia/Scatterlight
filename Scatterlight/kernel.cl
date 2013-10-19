#define Scale -2.0f
#define minRadius2 0.125f
#define fixedRadius2 1.0f
#define foldingLimit 1.0f
#define MaxRayDist 64
#define NormalEpsilon 0.125f
#define NormDistCount 5
#define Quality 0.5f
#define AoStepcount 5
#define SlowRender 2
#define DofPickup 512.0f
#define FogDensity 0.0625f
#define FocalPlane 3
#define BackgroundColor (float3)(0.5f, 0.4f, 0.4f)

#define IntMax 2147483647

int Rand(int seed) {
	return seed * 0x5DEECE66D + 0xB;
}

float3 RayDir(float3 look, float3 up, float2 screenCoords, float fov) {
	float3 right = cross(look, up);
	float3 realUp = cross(right, look);
	return normalize(right * screenCoords.x * fov + realUp * screenCoords.y * fov + look);
}

float De(float3 z) {
	float3 offset = z;
	float dz = 1.0;
	for (int n = 0; n < 2048; n++) {
		z = clamp(z, -foldingLimit, foldingLimit) * 2.0 - z;
		
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
		dz = dz * fabs(Scale) + 1.0;
	}
	return length(z) / fabs(dz);
}

float NormDist(float3 pos, float3 dir, float totalDistance, int width) {
	for (int i = 0; i < NormDistCount; i++)
		totalDistance += De(pos + dir * totalDistance) - totalDistance / (width * Quality);
	return totalDistance;
}

float3 Normal(float3 pos) {
	float deAtPos = De(pos);
	return normalize((float3)(
		deAtPos - De(pos + (float3)(-NormalEpsilon * deAtPos, 0, 0)),
		deAtPos - De(pos + (float3)(0, -NormalEpsilon * deAtPos, 0)),
		deAtPos - De(pos + (float3)(0, 0, -NormalEpsilon * deAtPos))
	));
}

float3 AO(float3 pos, float3 normal) {
	float delta = De(pos);
	float totalDistance = delta;
	for (int i = 0; i < AoStepcount; i++)
		totalDistance += De(pos + normal * totalDistance);
	float value = totalDistance / (delta * pown(2.0f, AoStepcount));
	const float3 light = (float3)(1, 1, 1);
	const float3 dark = (float3)(0, 0.2f, 0.4f);
	return mix(dark, light, value);
}

float3 LambertCosine(float3 lightDir, float3 normal) {
	float value = dot(-lightDir, normal) * 0.5f + 0.5f;
	const float3 light = (float3)(0.9f, 1.0f, 1.1f);
	const float3 dark = (float3)(0.5f, 0.25f, 0.0f);
	return mix(dark, light, value);
}

float3 Shadow(float3 position, float focalDistance)
{
	focalDistance *= 8.0f;
	float totalDistance = De(position);
	float dist = totalDistance;
	const float3 dir = normalize((float3)(1.0f, 1.0f, 1.0f));
	while (dist * 16 > totalDistance)
	{
		dist = De(position + dir * totalDistance);
		totalDistance += dist;
		if (totalDistance > focalDistance)
			return (float3)(1.0f, 1.0f, 1.0f);
	}
	const float3 light = (float3)(1.0f, 1.0f, 1.0f);
	const float3 dark = (float3)(0.4f, 0.3f, 0.6f);
	return mix(dark, light, max(totalDistance / focalDistance * 2 - 1, 0.0f));
}

float3 Fog(float3 color, float focalDistance, float totalDistance) {
	float ratio = totalDistance / focalDistance;
	float value = 1 / exp(ratio * FogDensity);
	return mix(BackgroundColor, mix(color, (float3)(0.0f, 0.5f, 1.0f), value * 0.1f), value);
}

__kernel void Main(__global float4* screen, int width, int height, float4 position, float4 lookat, float4 updir, int frame, int trueFrame, float fov) {
	if ((get_group_id(0) + get_group_id(1) + trueFrame) % SlowRender != 0)
		return;
	int x = get_global_id(0);
	int y = get_global_id(1);
	if (x >= width || y >= height)
		return;

	float2 screenCoords = (float2)((float)x / width * 2 - 1, ((float)y / height * 2 - 1) * height / width);

	float3 pos = position.xyz;
	float3 look = lookat.xyz;
	float3 up = updir.xyz;
	
	int rand = Rand(x + y * width + (int)dot(pos, pos) + frame);
	for (int i = 0; i < 3; i++)
		rand = Rand(rand);

	if (frame != 0)
	{
		int randx = Rand(rand);
		int randy = Rand(randx);
		rand = randy;
		screenCoords += (float2)((float)randx / IntMax / width, (float)randy / IntMax / height);
	}

	float3 rayDir = RayDir(look, up, screenCoords, fov);

	float totalDistance = De(pos);
	float focalDistance = totalDistance * FocalPlane;
	float distance = totalDistance;
	for (int i = 0; i < 1024 && totalDistance < MaxRayDist && distance * width * Quality * 4 > totalDistance * fov; i++) {
		distance = De(pos + rayDir * totalDistance) - totalDistance * fov / (width * Quality);
		totalDistance += distance;
	}
	
	float3 color = (float3)(1, 1, 1);

	if (totalDistance < MaxRayDist) {
		totalDistance = NormDist(pos, rayDir, totalDistance, width / fov);
		float3 finalPos = pos + rayDir * totalDistance;
		float3 normal = Normal(finalPos);
		color *= AO(finalPos, normal);
		color *= LambertCosine(rayDir, normal);
		//color *= Shadow(finalPos, focalDistance);
		color = Fog(color, focalDistance, totalDistance);
	} else {
		color = BackgroundColor;
	}

	float3 finalColor = clamp(color, 0.0f, 1.0f);
	if (frame > SlowRender) {
		int frameFixed = frame / SlowRender - 1;
		finalColor = (finalColor + screen[y * width + x].xyz * frameFixed) / (frameFixed + 1);

		if (frameFixed > 0 && x > 0 && x < width - 1 && y > 0 && y < height - 1) {
			float dofPickup = totalDistance / focalDistance;
			dofPickup = dofPickup + 1 / dofPickup - 2;
			dofPickup *= dofPickup * (float)DofPickup / width;
			int pixelCount = 0;
			for (int dy = -1; dy < 2; dy++) {
				for (int dx = -1; dx < 2; dx++) {
					if (dy != 0 || dx != 0) {
						float4 otherPixelValue = screen[(y + dy) * width + x + dx];
						if (otherPixelValue.w * 1.2f > totalDistance) {
							finalColor += otherPixelValue.xyz * dofPickup;
							pixelCount++;
						}
					}
				}
			}
			finalColor /= 1.0f + dofPickup * pixelCount;
		}
	}
	screen[y * width + x] = (float4)(finalColor, totalDistance);
}
