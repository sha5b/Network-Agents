using UnityEngine;
using ECS.Core;

namespace ECS.Components
{
    public class TerrainDataComponent : ComponentBase
    {
        public enum BiomeType
        {
            Plains,
            Forest,
            Mountains,
            Desert
        }

        [Header("Terrain Data")]
        public float[,] heightData;
        public BiomeType[,] biomeData;
        public float maxHeight = 100f;
        public float minHeight = -50f;

        [Header("Generation Parameters")]
        public float noiseScale = 50f;
        public int octaves = 4;
        public float persistence = 0.5f;
        public float lacunarity = 2f;
        public Vector2 offset;

        // Edge data for smooth transitions
        private float[] northEdge;
        private float[] southEdge;
        private float[] eastEdge;
        private float[] westEdge;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Material terrainMaterial;

        public override void Initialize()
        {
            base.Initialize();

            // Ensure we have all required mesh components
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
                Debug.Log($"[{gameObject.name}] Added MeshFilter");
            }

            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                Debug.Log($"[{gameObject.name}] Added MeshRenderer");
            }

            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
                Debug.Log($"[{gameObject.name}] Added MeshCollider");
            }

            // Create default material if none assigned
            if (meshRenderer.sharedMaterial == null)
            {
                if (terrainMaterial == null)
                {
                    terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    {
                        color = Color.green
                    };
                }
                meshRenderer.sharedMaterial = terrainMaterial;
                Debug.Log($"[{gameObject.name}] Assigned default material");
            }
        }

        public void InitializeArrays(int size)
        {
            heightData = new float[size, size];
            biomeData = new BiomeType[size, size];
            
            // Initialize edge arrays
            northEdge = new float[size];
            southEdge = new float[size];
            eastEdge = new float[size];
            westEdge = new float[size];
            
            Debug.Log($"[{gameObject.name}] Initialized arrays with size {size}");
        }

        public void SetEdgeData(float[] north, float[] south, float[] east, float[] west)
        {
            if (heightData == null) return;
            int size = heightData.GetLength(0);

            // Copy edge data
            if (north != null && north.Length == size) northEdge = (float[])north.Clone();
            if (south != null && south.Length == size) southEdge = (float[])south.Clone();
            if (east != null && east.Length == size) eastEdge = (float[])east.Clone();
            if (west != null && west.Length == size) westEdge = (float[])west.Clone();
        }

        public float[] GetEdge(ChunkStateComponent.EdgeDirection direction)
        {
            if (heightData == null) return null;
            int size = heightData.GetLength(0);
            float[] edge = new float[size];

            switch (direction)
            {
                case ChunkStateComponent.EdgeDirection.North:
                    for (int i = 0; i < size; i++) edge[i] = heightData[i, size - 1];
                    break;
                case ChunkStateComponent.EdgeDirection.South:
                    for (int i = 0; i < size; i++) edge[i] = heightData[i, 0];
                    break;
                case ChunkStateComponent.EdgeDirection.East:
                    for (int i = 0; i < size; i++) edge[i] = heightData[size - 1, i];
                    break;
                case ChunkStateComponent.EdgeDirection.West:
                    for (int i = 0; i < size; i++) edge[i] = heightData[0, i];
                    break;
            }

            return edge;
        }

        public float GetHeightAt(Vector2 localPosition)
        {
            if (heightData == null) return 0f;

            int x = Mathf.Clamp(Mathf.FloorToInt(localPosition.x), 0, heightData.GetLength(0) - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(localPosition.y), 0, heightData.GetLength(1) - 1);

            return heightData[x, z];
        }

        public BiomeType GetBiomeAt(Vector2 localPosition)
        {
            if (biomeData == null) return BiomeType.Plains;

            int x = Mathf.Clamp(Mathf.FloorToInt(localPosition.x), 0, biomeData.GetLength(0) - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(localPosition.y), 0, biomeData.GetLength(1) - 1);

            return biomeData[x, z];
        }

        public void UpdateMesh()
        {
            if (heightData == null || meshFilter == null) return;

            int size = heightData.GetLength(0);
            Vector3[] vertices = new Vector3[size * size];
            int[] triangles = new int[(size - 1) * (size - 1) * 6];
            Vector2[] uvs = new Vector2[size * size];
            Vector3[] normals = new Vector3[size * size];
            Color[] colors = new Color[size * size];

            // Generate vertices and UVs
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = z * size + x;
                    float height = heightData[x, z];

                    // Apply edge blending
                    if (z == 0 && southEdge != null) height = Mathf.Lerp(height, southEdge[x], 0.5f);
                    if (z == size - 1 && northEdge != null) height = Mathf.Lerp(height, northEdge[x], 0.5f);
                    if (x == 0 && westEdge != null) height = Mathf.Lerp(height, westEdge[z], 0.5f);
                    if (x == size - 1 && eastEdge != null) height = Mathf.Lerp(height, eastEdge[z], 0.5f);

                    vertices[index] = new Vector3(x, height, z);
                    uvs[index] = new Vector2((float)x / size, (float)z / size);
                    normals[index] = Vector3.up; // Will be recalculated later

                    // Set vertex color based on biome
                    colors[index] = GetBiomeColor(biomeData[x, z]);
                }
            }

            // Generate triangles
            int triangleIndex = 0;
            for (int z = 0; z < size - 1; z++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    int vertexIndex = z * size + x;

                    // First triangle
                    triangles[triangleIndex] = vertexIndex;
                    triangles[triangleIndex + 1] = vertexIndex + size;
                    triangles[triangleIndex + 2] = vertexIndex + 1;

                    // Second triangle
                    triangles[triangleIndex + 3] = vertexIndex + 1;
                    triangles[triangleIndex + 4] = vertexIndex + size;
                    triangles[triangleIndex + 5] = vertexIndex + size + 1;

                    triangleIndex += 6;
                }
            }

            // Create and assign mesh
            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uvs,
                normals = normals,
                colors = colors
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = mesh;
            }

            Debug.Log($"[{gameObject.name}] Updated mesh with {vertices.Length} vertices");
        }

        private Color GetBiomeColor(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Plains:
                    return new Color(0.4f, 0.8f, 0.4f); // Light green
                case BiomeType.Forest:
                    return new Color(0.2f, 0.6f, 0.2f); // Dark green
                case BiomeType.Mountains:
                    return new Color(0.6f, 0.6f, 0.6f); // Gray
                case BiomeType.Desert:
                    return new Color(0.9f, 0.8f, 0.5f); // Sand color
                default:
                    return Color.white;
            }
        }

        public void SetMaterial(Material material)
        {
            if (meshRenderer != null && material != null)
            {
                terrainMaterial = material;
                meshRenderer.sharedMaterial = material;
                Debug.Log($"[{gameObject.name}] Updated terrain material");
            }
        }
    }
}
