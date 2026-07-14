// Namespace shim for the modular monolith. After the Services/lib restructure, PRISM's types live in
// scoped namespaces (Prism.Contracts, Prism.Services.*, Prism.Lib.*). Prism.Core orchestration and the
// Services/ composition glue reference those types by simple name; these global usings keep that working
// without a using directive in every file. See jb/docs (restructure) for the namespace map.
global using Prism.Core;
global using Prism.Contracts;
global using Prism.Config;
global using Prism.Services.Matching;
global using Prism.Services.Transform;
global using Prism.Services.Generate;
global using Prism.Services.Upscale;
global using Prism.Lib.Excel;
global using Prism.Lib.Export;
global using Prism.Lib.Zip;
global using Prism.Lib.ImageNGP;
global using Prism.Lib.Ingress;
