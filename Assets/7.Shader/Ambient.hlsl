void AmbientSH_float(float3 WorldNormal, out float3 Ambient)
{
#if SHADERGRAPH_PREVIEW
    Ambient = float3(0.1, 0.1, 0.1);
#else
    Ambient = SampleSH(WorldNormal);
#endif
}