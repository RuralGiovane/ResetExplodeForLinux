#version 450
layout(push_constant) uniform Params { float time; float aspect; float shells; float iterations; } p;
layout(location=0) in vec3 normal;
layout(location=1) in vec2 uv;
layout(location=2) in float shell;
layout(location=0) out vec4 color;
void main() {
    vec2 h = fract(floor(uv*vec2(900,380))*vec2(123.34,456.21));
    h += dot(h,h+45.32);
    float noise = fract(h.x*h.y);
    vec4 a=vec4(noise,uv,shell), b=a.wzyx+0.5;
    for(int k=0;k<int(p.iterations);k++) {
        a=fma(a,vec4(1.0001),b*0.0003);
        b=fma(b,vec4(0.9997),a*0.0002);
    }
    float acc=fract(dot(a,b));
    if(shell>0 && noise+acc*1e-6<shell) discard;
    float light=max(dot(normalize(normal),normalize(vec3(-0.4,0.8,-0.5))),0)*0.75+0.25;
    float c=light*mix(0.18,1.0,shell)*(0.9+0.1*acc);
    color=vec4(vec3(c),mix(1.0,0.55,shell));
}
