// This makes it possible for unity to pre-burst-compile these job types

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselBox>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselCapsule>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselCylinder>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselHemisphere>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselSphere>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselStadium>))]
