#version 450
layout(push_constant) uniform Params { float time; float aspect; float shells; float iterations; } p;
layout(location=0) out vec3 normal;
layout(location=1) out vec2 uv;
layout(location=2) out float shell;
void main() {
    const ivec2 corners[6] = ivec2[6](ivec2(0,0),ivec2(1,0),ivec2(0,1),ivec2(0,1),ivec2(1,0),ivec2(1,1));
    int cell = gl_VertexIndex / 6;
    ivec2 ij = ivec2(cell / 64, cell % 64) + corners[gl_VertexIndex % 6];
    uv = vec2(ij) / vec2(160,64);
    vec2 a = uv * 6.28318530718;
    vec3 n = vec3(cos(a.x)*cos(a.y), sin(a.y), sin(a.x)*cos(a.y));
    shell = float(gl_InstanceIndex) / p.shells;
    vec3 v = vec3(cos(a.x),0,sin(a.x)) + n*(0.42+0.2*shell);
    float angle = p.time*0.6;
    mat3 ry = mat3(cos(angle),0,-sin(angle), 0,1,0, sin(angle),0,cos(angle));
    float tilt = 0.55+0.25*sin(p.time*0.35);
    mat3 rx = mat3(1,0,0, 0,cos(tilt),sin(tilt), 0,-sin(tilt),cos(tilt));
    v = rx*ry*v;
    v.y -= 0.1*shell*shell;
    normal = normalize(rx*ry*n);
    float z = v.z+3.5;
    gl_Position = vec4(v.x*2.414/p.aspect, -v.y*2.414, (z-0.1)*50.0/49.9, z);
}
