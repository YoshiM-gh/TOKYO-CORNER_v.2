using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Extensions;
using UnityEngine;

namespace CharacterCustomizationTool.FaceManagement
{
    [Serializable]
    public class FaceMesh
    {
        public FaceType Type;
        public Mesh Mesh;

        public FaceMesh(Mesh mesh)
        {
            Type = Parse(mesh);
            Mesh = mesh;
        }

        public static FaceType Parse(Mesh mesh) => Enum.Parse<FaceType>(mesh.name.Split("_").Last().ToCapital());
    }
}