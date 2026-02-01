shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

 uniform int blurSize: hint_range(0, 20) = 1;
 uniform float blurSmoothness: hint_range(0.0,1.5) = 1;

// Blurs the screen in the X-direction.
// void fragment() {
//     vec3 col = texture(screen_texture, SCREEN_UV).xyz * 0.16;
//     col += texture(screen_texture, SCREEN_UV + vec2(8.0 * SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.15;
//     col += texture(screen_texture, SCREEN_UV + vec2(8.0 * -SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.15;
//     col += texture(screen_texture, SCREEN_UV + vec2(12.0 * SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.12;
//     col += texture(screen_texture, SCREEN_UV + vec2(12.0 * -SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.12;
//     col += texture(screen_texture, SCREEN_UV + vec2(18.0 * SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.09;
//     col += texture(screen_texture, SCREEN_UV + vec2(18.0 * -SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.09;
//     col += texture(screen_texture, SCREEN_UV + vec2(24.0 * SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.05;
//     col += texture(screen_texture, SCREEN_UV + vec2(24.0 * -SCREEN_PIXEL_SIZE.x, 0.0)).xyz * 0.05;
//     COLOR.xyz = col / 1.6f;
// }
float GuassianWeight(int x, int y)
{
    int kernelSize = blurSize * 2 + 1;
    float sig = float(kernelSize) / (6.0 * max(0.001f, blurSmoothness));
    int sqrDst = x*x + y*y;
    float c = 2.0 * sig * sig;
    return exp(float(-sqrDst) / c);
}

void fragment() {
    vec4 sum = vec4(0);
    int numSamples = 0;
    float weightSum = 0.0;

    for (int offsetY = -blurSize; offsetY <= blurSize; offsetY++)
    {
    // for (int offsetX = -blurSize; offsetX <= blurSize; offsetX++)
    // {
        int offsetX = 0;
        // for (int offsetY = -blurSize; offsetY <= blurSize; offsetY++)
        // {
            vec2 uv2 = SCREEN_UV + vec2(float(offsetX), float(offsetY)) * SCREEN_PIXEL_SIZE.xy;
            vec4 sample = texture(screen_texture, uv2);
            float weight = GuassianWeight(offsetX, offsetY);
            weightSum += weight;
            sum += sample * weight;
        // }
    }	
	vec3 col = (sum / weightSum).rgb;
    COLOR = vec4(col, 1.0);
}