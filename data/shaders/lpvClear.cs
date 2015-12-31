#version 430
precision lowp float;

layout(local_size_x = 32, local_size_y = 32, local_size_z = 1) in;

layout(rgba32f) uniform image3D lpvImgR0;
layout(rgba32f) uniform image3D lpvImgG0;
layout(rgba32f) uniform image3D lpvImgB0;
layout(rgba32f) uniform image3D gvImgA0;

uniform vec3 lpvSize;

void main()
{
  const uint x = gl_GlobalInvocationID.x;
  const uint y = gl_GlobalInvocationID.y;
  const uvec3 s = uvec3(lpvSize);

  if((x < (s.x * s.y)) && (y < s.z))
  {
    imageStore(lpvImgR0, ivec3(x / s.x, x % s.x, y), vec4(0.0, 0.0, 0.0, 0.0));
    imageStore(lpvImgG0, ivec3(x / s.x, x % s.x, y), vec4(0.0, 0.0, 0.0, 0.0));
    imageStore(lpvImgB0, ivec3(x / s.x, x % s.x, y), vec4(0.0, 0.0, 0.0, 0.0));
    imageStore(gvImgA0, ivec3(x / s.x, x % s.x, y), vec4(0.0, 0.0, 0.0, 0.0));
  }
}