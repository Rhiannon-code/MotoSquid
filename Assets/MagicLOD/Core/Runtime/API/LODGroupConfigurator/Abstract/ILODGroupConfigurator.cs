using System.Collections.Generic;
using UnityEngine;

namespace NGS.MagicLOD.Runtime
{
    public interface ILODGroupConfigurator
    {
        public LODGroupDescriptor CreateLODGroupDescriptor(IReadOnlyList<Mesh> meshes, IReadOnlyList<Renderer> renderers);

        public ILODGroupConfigurator Clone();
    }
}