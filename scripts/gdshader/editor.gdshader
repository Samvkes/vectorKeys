shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

uniform float brightness = 1.0;
uniform float contrast = 1.0;
uniform float saturation = 1.0;
uniform float t = 0.003;

void fragment() {
    vec3 c = textureLod(screen_texture, SCREEN_UV, 0.0).rgb;
    vec3 c1 = textureLod(screen_texture, SCREEN_UV + vec2(0,t), 0.0).rgb;
    vec3 c2 = textureLod(screen_texture, SCREEN_UV + vec2(0,-t), 0.0).rgb;
    vec3 c3 = textureLod(screen_texture, SCREEN_UV + vec2(t,0), 0.0).rgb;
    vec3 c4 = textureLod(screen_texture, SCREEN_UV + vec2(-t,0), 0.0).rgb;
	
	vec3 c5 = textureLod(screen_texture, SCREEN_UV + vec2(t,t), 0.0).rgb;
    vec3 c6 = textureLod(screen_texture, SCREEN_UV + vec2(t,-t), 0.0).rgb;
    vec3 c7 = textureLod(screen_texture, SCREEN_UV + vec2(-t,t), 0.0).rgb;
    vec3 c8 = textureLod(screen_texture, SCREEN_UV + vec2(-t,-t), 0.0).rgb;

	c += c1 + c2 + c3 + c4 + c5 + c6 + c7 + c8;
	c /= 9.0;
	c.rgb -= 0.2f;
    COLOR.rgb = c;
}