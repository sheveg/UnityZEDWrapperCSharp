using UnityEngine;
using System.Runtime.InteropServices;
using sl;
using System.IO;
using System.Threading;
using System.Linq;

public class ZedSDKExtensionTest : MonoBehaviour {

    public int Threshold = 1000;

    const string nameDll = "sl_unitywrapper_extension";

    ZEDCamera zed;

    UnityEngine.Mesh generatedMesh;

    bool mapping;

    bool stop;

    bool meshRequest;

    [DllImport(nameDll, EntryPoint = "startSpatialMapping")]
    private static extern int startSpatialMapping();

    [DllImport(nameDll, EntryPoint = "mappingLoop")]
    private static extern int mappingLoop();

    [DllImport(nameDll, EntryPoint = "stopSpatialMapping")]
    private static extern int stopSpatialMapping();

    [DllImport(nameDll, EntryPoint = "getMappingState")]
    private static extern int getMappingState();

    [DllImport(nameDll, EntryPoint = "getVertices")]
    private static extern bool getVertices(Vector3[] vert, int size);

    [DllImport(nameDll, EntryPoint = "getNormals")]
    private static extern bool getNormals(Vector3[] norm, int size);

    [DllImport(nameDll, EntryPoint = "getUVs")]
    private static extern bool getUVs(Vector2[] uv, int size);

    [DllImport(nameDll, EntryPoint = "getTriangles")]
    private static extern bool getTriangles(uint3[] triangles, int size);

    [DllImport(nameDll, EntryPoint = "getMeshSize")]
    private static extern int getMeshSize();

    [DllImport(nameDll, EntryPoint = "getTrianglesSize")]
    private static extern int getTrianglesSize();

    [DllImport(nameDll, EntryPoint = "getTexture")]
    private static extern bool getTexture(byte[] texture, int width, int height);

    [DllImport(nameDll, EntryPoint = "getTextureWidth")]
    private static extern int getTextureWidth();

    [DllImport(nameDll, EntryPoint = "getTextureHeight")]
    private static extern int getTextureHeight();

    private static Vector3[] vertices;

    private static Vector3[] normals;

    private static Vector2[] uvs;

    private static uint3[] triangles;

    private static int meshSize = 0;

    private static int trianglesSize = 0;

    private int subMeshIndex = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct uint3
    {
        public int x;
        public int y;
        public int z;

        public uint3(int _x, int _y, int _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct uchar3
    {
        public byte r;
        public byte g;
        public byte b;
    }

    private float currentTimer = 0f;

    private float meshUpdateTime = 0.01f;

    private GameObject generatedGO;

    private Thread mappingThread;

    private object lockObject = new object();

    bool receivingInputFromDll = false;

    Vector3[] endMeshVertices;
    Vector3[] endMeshNormals;
    Vector2[] endMeshUVs;
    int[] endMeshTriangles;

    private void mappingProcess()
    {
        if (receivingInputFromDll)
            return;
        lock (lockObject)
        {
            receivingInputFromDll = true;
            mappingLoop();
            receivingInputFromDll = false;

        };
    }

    private void Start()
    {
        generatedGO = new GameObject("ZED Scan");
        // Initialize the generated gameobject
        generatedMesh = generatedGO.AddComponent<MeshFilter>().mesh;
        var meshRenderer = generatedGO.AddComponent<MeshRenderer>();

        Material meshMat = new Material(Shader.Find("Standard"));
        meshRenderer.sharedMaterial = meshMat;
        //Thread a = new Thread(UnityM)

        

    }

    private void Update()
    {
        currentTimer += Time.deltaTime;
        if (mapping)
        {

            if (Input.GetKeyDown(KeyCode.S))
            {
                stopSpatialMapping();
                mapping = false;

                generatedMesh = generatedGO.GetComponent<MeshFilter>().mesh;
                generatedMesh.Clear();

                getMesh();

                endMeshVertices = new Vector3[meshSize];
                endMeshNormals = new Vector3[meshSize];
                endMeshUVs = new Vector2[meshSize];

                for (int i = 0; i < meshSize; i++)
                {
                    endMeshVertices[i] = vertices[i];
                    endMeshNormals[i] = normals[i];
                    endMeshUVs[i] = uvs[i];
                }

                Debug.Log("MeshSize: " + meshSize);

                generatedMesh.SetVertices(endMeshVertices.ToList());
                generatedMesh.SetNormals(endMeshNormals.ToList());
                generatedMesh.SetUVs(0, endMeshUVs.ToList());

                int trianglesSizeUnity = triangles.Length * 3;
                endMeshTriangles = new int[trianglesSizeUnity];

                for (int i = 0; i < trianglesSizeUnity; i += 3)
                {
                    endMeshTriangles[i] = triangles[i / 3].x;
                    endMeshTriangles[i + 1] = triangles[i / 3].y;
                    endMeshTriangles[i + 2] = triangles[i / 3].z;
                }

                Debug.Log("TrianglesSize: " + trianglesSizeUnity);

                generatedMesh.SetTriangles(endMeshTriangles, 0);

                generatedMesh.RecalculateBounds();
                generatedMesh.RecalculateNormals();

                Debug.Log("penis");

            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                vertices = new Vector3[getMeshSize()];
                getVertices(vertices, vertices.Length);
                Debug.Log(vertices.Length);
            }
        }
        if (Input.GetKeyDown(KeyCode.A) && !mapping)
        {
            sl.ERROR_CODE err = ERROR_CODE.FAILURE;
            //Thread startThread = new Thread(() => 
            //{
            //    err = (sl.ERROR_CODE)startSpatialMapping();
            //});
            //startThread.Start();
            err = (sl.ERROR_CODE)startSpatialMapping();

            if (err != ERROR_CODE.SUCCESS)
                return;

            mapping = true;
            mappingThread = new Thread(new ThreadStart(mappingProcess));
            mappingThread.Start();        
        }

        if ((currentTimer > meshUpdateTime) && mapping && !receivingInputFromDll)
        {
            //lock (lockObject)
            //{
            //    if (receivingInputFromDll)
            //        return;
            //}
            mappingThread = new Thread(mappingProcess);
            mappingThread.Start();

            currentTimer = 0f;
            generatedMesh = generatedGO.GetComponent<MeshFilter>().mesh;
            generatedMesh.Clear();
      
            getMesh();

            Vector3[] meshVertices = new Vector3[meshSize];
            Vector3[] meshNormals = new Vector3[meshSize];
            Vector2[] meshUVs = new Vector2[meshSize];

            for (int i = 0; i < meshSize; i++)
            {
                meshVertices[i] = vertices[i];
                meshNormals[i] = normals[i];
                meshUVs[i] = uvs[i];
            }

            generatedMesh.SetVertices(meshVertices.ToList());
            generatedMesh.SetNormals(meshNormals.ToList());
            //generatedMesh.SetUVs(0, meshUVs.ToList());

            int trianglesSizeUnity = triangles.Length * 3;
            int[] meshTriangles = new int[trianglesSizeUnity];

            for (int i = 0; i < trianglesSizeUnity; i += 3)
            {
                // skip triangles which reference vertices which are not loaded yet for whatever reason
                if ((i / 3) > meshSize - 1)
                    continue;

                meshTriangles[i] = triangles[i / 3].x;
                meshTriangles[i + 1] = triangles[i / 3].y;
                meshTriangles[i + 2] = triangles[i / 3].z;
            }

            generatedMesh.SetTriangles(meshTriangles, 0);

            generatedMesh.RecalculateBounds();
            generatedMesh.RecalculateNormals();

            //Debug.Log("Vertices: " + mesh.vertices.Length);
            //Debug.Log("Triangles: " + mesh.triangles.Length);

            //int textureWidth = getTextureWidth();
            //int textureHeight = getTextureHeight();

            //byte[] meshTexture = new byte[textureWidth * textureHeight * 3];
            //getTexture(meshTexture, textureWidth, textureHeight);

            //Texture2D tex = new Texture2D(textureWidth, textureHeight);
            //Color[] texColors = new Color[textureWidth * textureHeight];
            //for (int i = 0; i < textureHeight * textureWidth * 3; i += 3)
            //{
            //    texColors[i / 3].r = meshTexture[i] / (float)255;
            //    texColors[i / 3].g = meshTexture[i + 1] / (float)255;
            //    texColors[i / 3].b = meshTexture[i + 2] / (float)255;
            //    texColors[i / 3].a = 1f;
            //}

            //int zeroR = 0, zeroG = 0, zeroB = 0;
            //for (int i = 0; i < textureHeight * textureWidth; i++)
            //{
            //    if (texColors[i].r == 0)
            //        zeroR++;

            //    if (texColors[i].g == 0)
            //        zeroG++;

            //    if (texColors[i].b == 0)
            //        zeroB++;
            //}
            //// dafuq so many black pixels
            //Debug.Log("R: " + zeroR + " G: " + zeroG + " B: " + zeroB);

            //tex.SetPixels(texColors);

            //SaveTextureToFile(tex, "testTexture.png");

            //for (int i = 900; i < 1000; i++)
            //{
            //    Debug.Log(string.Format("r: {0}, g: {1}, b: {2}", texColors[i].r, texColors[i].g, texColors[i].b));
            //}

            //img.texture = tex;
        }

    }

    private float calculateMeshMaxDistance(UnityEngine.Mesh mesh)
    {
        float maxDistance = -1f;
        float currentDistance;
        for (int i = 0; i < mesh.vertexCount - 1; i++)
        {
            if ((currentDistance = Vector3.Distance(mesh.vertices[i], mesh.vertices[i + 1])) > maxDistance)
                maxDistance = currentDistance;
        }
        return maxDistance;
    }

    private void getMesh()
    {
        //if (getMeshSize() > Threshold)
        //    return;

        meshSize = getMeshSize();
        trianglesSize = getTrianglesSize();

        vertices = new Vector3[meshSize];
        normals = new Vector3[meshSize];
        uvs = new Vector2[meshSize];
        triangles = new uint3[trianglesSize];

        getVertices(vertices, meshSize);
        getNormals(normals, meshSize);
        getUVs(uvs, meshSize);
        getTriangles(triangles, trianglesSize);

        //if (meshSize > 2000)
        //{
        //    Debug.Log(vertices[0].x + " : " + vertices[0].y + " : " + vertices[0].z);
        //}
        //Debug.Log(vertices[0].x + " : " + vertices[0].y + " : " + vertices[0].z);
        //Debug.Log(vertices[0].x + " : " + vertices[0].y + " : " + vertices[0].z);
    }

    private void OnApplicationQuit()
    {
        if (mapping)
        {            
            stopSpatialMapping();
            
        }
        if (mappingThread != null)
            mappingThread.Join();
           
    }

    public static void SaveTextureToFile(Texture2D tex, string filename)
    {
        byte[] bytes = tex.EncodeToPNG();
        var filestream = File.Open(Application.dataPath + "/" + filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        var binarywriter = new BinaryWriter(filestream);
        binarywriter.Write(bytes);
        filestream.Close();
    }
}
