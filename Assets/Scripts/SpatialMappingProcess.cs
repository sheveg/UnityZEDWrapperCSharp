using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using sl.extension;
using System.IO;
using System.Threading;
using System.Linq;

public class SpatialMappingProcess : MonoBehaviour {

    /// <summary>
    /// Name of the DLL for ZED extension plugin
    /// </summary>
    private const string m_NameDll = "sl_unitywrapper_extension";

    /// <summary>
    /// Possible States for the mapping process
    /// </summary>
    private enum SpatialMappingState
    {
        /// <summary>
        /// We did nothing yet.
        /// </summary>
        None,
        /// <summary>
        /// We started an async request to start the mapping process.
        /// </summary>
        StartRequest,
        /// <summary>
        /// The start request was succesful and we can start the mapping process.
        /// </summary>
        StartRequestSuccesful,
        /// <summary>
        /// The start request resulted in an error.
        /// </summary>
        StartRequestError,
        /// <summary>
        /// The ZED camera is currently mapping.
        /// </summary>
        Mapping,
        /// <summary>
        /// We started an async request to stop the mapping process.
        /// </summary>
        StopRequest,
        /// <summary>
        /// We succesfully stopped the mapping process and can retrieve the final mesh.
        /// </summary>
        StopRequestSuccesful,
        /// <summary>
        /// The stop request resulted in an error.
        /// </summary>
        StopRequestError
    }

    private SpatialMappingState m_MappingState;

    /// <summary>
    /// Arrays of a ZED mesh, note that it does not match exactly the ZED API definition of the mesh,
    /// therefore we did not define it in ZEDCommonExtension. 
    /// </summary>
    private struct ZEDMesh
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;

        public ZEDMesh(Vector3[] _vertices, Vector3[] _normals, Vector2[] _uv, int[] _triangles)
        {
            vertices = _vertices;
            normals = _normals;
            uv = _uv;
            triangles = _triangles;
        }
    }

    private ZEDMesh m_CurrentMesh;

    /// <summary>
    /// We need a lock object as we do the mapping requests in new threads because of performance reasons.
    /// </summary>
    private object m_LockObject = new object();

    /// <summary>
    /// Boolean to check whether the plugin is currently executing the mappingLoop method so we dont read the data,
    /// when the mapping thread is writing to it.
    /// </summary>
    private bool m_ReceivingInputFromDll = false;

    /// <summary>
    /// At the start of the scene we create an empty gameObject which represents the ZED Scan.
    /// </summary>
    private GameObject m_GeneratedGO;

    private Mesh m_GeneratedMesh;

    private Thread m_MappingThread;

    private Thread m_StartRequestThread;

    private Thread m_StopRequestThread;

    /// <summary>
    /// For performance reasons we do not call mappingLoop not each frame but rather each time step.
    /// </summary>
    [SerializeField]
    private float m_MappingUpdateTime = 0.01f;

    private float m_CurrentMappingTimer = 0f;

    #region Plugin Methods
    [DllImport(m_NameDll, EntryPoint = "startSpatialMapping")]
    private static extern int startSpatialMapping();

    [DllImport(m_NameDll, EntryPoint = "mappingLoop")]
    private static extern int mappingLoop();

    [DllImport(m_NameDll, EntryPoint = "stopSpatialMapping")]
    private static extern int stopSpatialMapping();

    [DllImport(m_NameDll, EntryPoint = "getMappingState")]
    private static extern int getMappingState();

    [DllImport(m_NameDll, EntryPoint = "getVertices")]
    private static extern bool getVertices(Vector3[] vert, int size);

    [DllImport(m_NameDll, EntryPoint = "getNormals")]
    private static extern bool getNormals(Vector3[] norm, int size);

    [DllImport(m_NameDll, EntryPoint = "getUVs")]
    private static extern bool getUVs(Vector2[] uv, int size);

    [DllImport(m_NameDll, EntryPoint = "getTriangles")]
    private static extern bool getTriangles(UInt3[] triangles, int size);

    [DllImport(m_NameDll, EntryPoint = "getMeshSize")]
    private static extern int getMeshSize();

    [DllImport(m_NameDll, EntryPoint = "getTrianglesSize")]
    private static extern int getTrianglesSize();

    [DllImport(m_NameDll, EntryPoint = "getTexture")]
    private static extern bool getTexture(byte[] texture, int width, int height);

    [DllImport(m_NameDll, EntryPoint = "getTextureWidth")]
    private static extern int getTextureWidth();

    [DllImport(m_NameDll, EntryPoint = "getTextureHeight")]
    private static extern int getTextureHeight();
    #endregion // Plugin Methods

   // Create an empty gameObject for the ZED Scan
    void Start () {
        m_GeneratedGO = new GameObject("ZED Scan");
        // Initialize the generated gameobject
        m_GeneratedMesh = m_GeneratedGO.AddComponent<MeshFilter>().mesh;
        var meshRenderer = m_GeneratedGO.AddComponent<MeshRenderer>();

        Material meshMat = new Material(Shader.Find("Standard"));
        meshRenderer.sharedMaterial = meshMat;
    }
	
	// Update is called once per frame
	void Update () {
        m_CurrentMappingTimer += Time.deltaTime;
        if (m_MappingState == SpatialMappingState.Mapping)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                m_StopRequestThread = new Thread(new ThreadStart(stopMappingProcess));
                m_StopRequestThread.Start();
            }

            if (m_CurrentMappingTimer > m_MappingUpdateTime && !m_ReceivingInputFromDll)
            {
                // do the mapping loop 
                m_MappingThread = new Thread(mappingLoopProcess);
                m_MappingThread.Start();
                // reset mesh and timer
                m_CurrentMappingTimer = 0f;
                updateMesh();
            }
        }
        // request a mapping process in a new thread
        if ((m_MappingState == SpatialMappingState.None
            || m_MappingState == SpatialMappingState.StopRequestSuccesful)
            && Input.GetKeyDown(KeyCode.Space))
        {
            ERROR_CODE err = ERROR_CODE.FAILURE;
            err = (ERROR_CODE)startSpatialMapping();

            if (err != ERROR_CODE.SUCCESS)
                return;

            m_MappingState = SpatialMappingState.StartRequestSuccesful;

            //m_StartRequestThread = new Thread(new ThreadStart(startMappingProcess));
            //m_StartRequestThread.Start();
        }
        // start the first mapping process and change the state to mapping
        if (m_MappingState == SpatialMappingState.StartRequestSuccesful)
        {
            m_MappingThread = new Thread(new ThreadStart(mappingLoopProcess));
            m_MappingThread.Start();

            m_MappingState = SpatialMappingState.Mapping;
        }
        // print an error message and reset the state
        if (m_MappingState == SpatialMappingState.StartRequestError)
        {
            m_MappingState = SpatialMappingState.None;
            Debug.Log("Start of spatial mapping was not succesful! Check your ZED connection!");
        }
        // retrieve the mesh a final time and reset the state
        if (m_MappingState == SpatialMappingState.StopRequestSuccesful)
        {
            m_MappingState = SpatialMappingState.None;
            updateMesh();

            Debug.Log("Mesh was succesfully retrieved!");
        }
        // print an error message and reset the state
        if (m_MappingState == SpatialMappingState.StopRequestError)
        {
            m_MappingState = SpatialMappingState.None;
            updateMesh();
            updateTexture();
            Debug.Log("Mesh was not retrieved! Something went wrong!");
        }
    }

    private void OnApplicationQuit()
    {
        if (m_MappingState == SpatialMappingState.Mapping)
        {
            stopSpatialMapping();
        }

        if (m_MappingThread != null)
            m_MappingThread.Join();
        if (m_StartRequestThread != null)
            m_StartRequestThread.Join();
        if (m_StopRequestThread != null)
            m_StopRequestThread.Join();
    }

    private void updateMesh()
    {
        ZEDMesh zedMesh = getMeshFromZEDAPI();
        m_GeneratedMesh.Clear();
        // set the mesh properties
        
        m_GeneratedMesh.SetVertices(zedMesh.vertices.ToList());
        m_GeneratedMesh.SetNormals(zedMesh.normals.ToList());
        m_GeneratedMesh.SetTriangles(zedMesh.triangles.ToList(), 0);
        m_GeneratedMesh.SetUVs(0, zedMesh.uv.ToList());
        // recalculate stuff
        m_GeneratedMesh.RecalculateBounds();
        m_GeneratedMesh.RecalculateNormals();
        m_GeneratedMesh.RecalculateTangents();
    }

    private void updateTexture()
    {
        int texWidth = getTextureWidth();
        int texHeight = getTextureHeight();

        byte[] textureByte = new byte[texHeight * texWidth * 3];
        getTexture(textureByte, texWidth, texHeight);

        Texture2D texture2D = new Texture2D(texWidth, texHeight);
        Color[] colors = new Color[texWidth * texHeight];
        for (int i = 0; i < texHeight * texWidth * 3; i += 3)
        {
            colors[i / 3].b = textureByte[i] / (float)255;
            colors[i / 3].g = textureByte[i + 1] / (float)255;
            colors[i / 3].r = textureByte[i + 2] / (float)255;
            colors[i / 3].a = 1f;
        }
        texture2D.SetPixels(colors);
        texture2D.Apply();

        m_GeneratedGO.GetComponent<Renderer>().material.SetTexture("_MainTex", texture2D);
    }

    /// <summary>
    /// Start the mapping process. Use this in a seperate thread.
    /// </summary>
    private void startMappingProcess()
    {
        lock (m_LockObject)
        {
            ERROR_CODE err = ERROR_CODE.FAILURE;
            Thread tempThread = new Thread(() => 
            {
                err = (ERROR_CODE)startSpatialMapping();
            });
            tempThread.Start();
            tempThread.Join();
            
            if (err == ERROR_CODE.SUCCESS)
                m_MappingState = SpatialMappingState.StartRequestSuccesful;
            else
            {
                m_MappingState = SpatialMappingState.StartRequestError;
                Debug.Log(err);
            }
        }
    }

    /// <summary>
    /// Stop the mapping process. Use this in a seperate thread.
    /// </summary>
    private void stopMappingProcess()
    {
        lock (m_LockObject)
        {
            ERROR_CODE err = ERROR_CODE.FAILURE;
            Thread tempThread = new Thread(() =>
            {
                err = (ERROR_CODE)stopSpatialMapping();
            });
            tempThread.Start();
            tempThread.Join();

            if (err == ERROR_CODE.SUCCESS)
                m_MappingState = SpatialMappingState.StopRequestSuccesful;
            else
            {
                m_MappingState = SpatialMappingState.StopRequestError;
                Debug.Log(err);
            }
           // UnityThread.executeInUpdate(updateTexture);
        }
    }

    /// <summary>
    /// Update the mesh from the ZED scan. Use this in a seperate thread.
    /// </summary>
    private void mappingLoopProcess()
    {
        if (m_ReceivingInputFromDll)
            return;
        lock(m_LockObject)
        {
            m_ReceivingInputFromDll = true;
            mappingLoop();
            m_ReceivingInputFromDll = false;
        };
    }

    /// <summary>
    /// Retrieves the mesh arrays from the ZED API.
    /// </summary>
    /// <returns></returns>
    private ZEDMesh getMeshFromZEDAPI()
    {
        // get the size of the arrays first
        int meshSize = getMeshSize();
        int trianglesSize = getTrianglesSize();
        
        Vector3[] vertices = new Vector3[meshSize];
        Vector3[] normals = new Vector3[meshSize];
        Vector2[] uv = new Vector2[meshSize];
        UInt3[] triangles = new UInt3[trianglesSize];
        // the plugin fills the arrays 
        // but the c# garbage collector is responsible for them as we declared them with a size here
        getVertices(vertices, meshSize);
        getNormals(normals, meshSize);
        getUVs(uv, meshSize);
        getTriangles(triangles, trianglesSize);

        // we need to copy the triangles from a format of a vector3 of ints to a flat int array
        int[] unityTriangles = new int[trianglesSize * 3];
        for (int i = 0; i < unityTriangles.Length; i += 3)
        {
            // skip triangles which reference vertices which are not loaded yet for whatever reason
            if ((i / 3) > meshSize - 1)
                continue;
            
            unityTriangles[i] = triangles[i / 3].x;
            unityTriangles[i + 1] = triangles[i / 3].y;
            unityTriangles[i + 2] = triangles[i / 3].z;
        }

        return new ZEDMesh(vertices, normals, uv, unityTriangles);
    }
}
