#ifdef GL_ES
    precision mediump float;
#endif

uniform vec2 u_resolution;
uniform float u_time;

float arre(float c){
    return 22.0 * c;
}

void main(){
    float d = arre(10.0);
    vec2 uv = gl_FragCoord.xy / u_resolution * 2.0 - 1.0;
    uv.x = uv.x * (u_resolution.x / u_resolution.y);
    // uv = cos(sin(uv*1.)*sin(u_time/8.)*8.);
    uv = mod(uv, 0.2);
    uv = cos(sin(uv*1.3)*2.4)- .9;
    gl_FragColor = vec4(smoothstep(0.9,0.99,1.0 - abs(sin(length(uv)*44.0 + u_time))), 0.1, 0.2,1);
}