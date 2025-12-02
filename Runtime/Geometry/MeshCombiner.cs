using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace HRCG.Geometry
{
    public class MeshCombiner
    {
        public static GameObject CombineProBuilderMeshes(List<GameObject> meshObjects, string name = "Combined Mesh")
        {
            if (meshObjects == null || meshObjects.Count == 0)
            {
                Debug.LogWarning("No meshes to combine");
                return null;
            }

            List<ProBuilderMesh> pbMeshes = new List<ProBuilderMesh>();
            foreach (GameObject obj in meshObjects)
            {
                ProBuilderMesh pbMesh = obj.GetComponent<ProBuilderMesh>();
                if (pbMesh != null)
                {
                    pbMeshes.Add(pbMesh);
                }
            }

            if (pbMeshes.Count == 0)
            {
                Debug.LogWarning("No ProBuilderMesh components found");
                return null;
            }

            ProBuilderMesh combined = ProBuilderMesh.Create();
            combined.gameObject.name = name;

            List<CombineInstance> combineInstances = new List<CombineInstance>();
            
            foreach (ProBuilderMesh pbMesh in pbMeshes)
            {
                MeshFilter meshFilter = pbMesh.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    CombineInstance ci = new CombineInstance
                    {
                        mesh = meshFilter.sharedMesh,
                        transform = pbMesh.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);
                }
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
            
            combined.Clear();
            combined.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
            combined.ToMesh();
            combined.Refresh();

            return combined.gameObject;
        }

        public static GameObject CombineStandardMeshes(List<GameObject> meshObjects, Material material = null, string name = "Combined Mesh")
        {
            if (meshObjects == null || meshObjects.Count == 0)
            {
                Debug.LogWarning("No meshes to combine");
                return null;
            }

            GameObject combinedObject = new GameObject(name);
            MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();

            List<CombineInstance> combineInstances = new List<CombineInstance>();

            foreach (GameObject obj in meshObjects)
            {
                MeshFilter mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    CombineInstance ci = new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        transform = obj.transform.localToWorldMatrix
                    };
                    combineInstances.Add(ci);
                }
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

            meshFilter.sharedMesh = combinedMesh;

            if (material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            return combinedObject;
        }

        public static void OptimizeMesh(GameObject meshObject)
        {
            if (meshObject == null)
                return;

            MeshFilter meshFilter = meshObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            Mesh mesh = meshFilter.sharedMesh;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.Optimize();
        }

        public static void CleanupSourceObjects(List<GameObject> sourceObjects)
        {
            if (sourceObjects == null)
                return;

            foreach (GameObject obj in sourceObjects)
            {
                if (obj != null)
                {
                    Object.Destroy(obj);
                }
            }
        }
    }
}
