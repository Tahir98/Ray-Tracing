using UnityEngine;
using System.Data.Common;
using System.Collections.Generic;

public class RayTracingMaster : MonoBehaviour {
    public ComputeShader RayTracingShader;
    private RenderTexture _target;

    private Camera _camera;

    public Texture SkyboxTexture;

    private uint _currentSample = 0;
    private Material _addMaterial;

    public int RayTracingCount = 1;

    public Light DirectionalLight;

    public float conservation = 0.90f;

    public float width = 5;
    public float height = 5;
    public float gravity = -10;

    private bool updateBalls = true;

    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 velocity;
    };

    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 50;
    public float SpherePlacementRadius = 20.0f;
    private ComputeBuffer _sphereBuffer;

    private List<Sphere> spheres;
    public float metallnes = 0.8f;
    private void OnEnable() {
        _currentSample = 0;
        SetUpScene();
    }

    private void OnDisable() {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }

    private void SetUpScene() {
        spheres = new List<Sphere>();

        Random.InitState((int)(Time.time * 60));

        for (uint i = 0; i < SpheresMax; i++) {
            Sphere sphere = new Sphere();
            //sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            sphere.radius = 0.5f;
            Vector2 randomPos = new Vector2(Random.value * width, Random.value * width);
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            Color color = Random.ColorHSV(0,1,0,1,0.34f,1);
            bool metal = Random.value < 0.8f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            //sphere.albedo = Vector3.zero;
            //sphere.specular = new Vector3(Random.value, Random.value, Random.value) * metallnes;

            sphere.velocity = new Vector3(Random.value * 4 - 2, Random.value * 4 - 2, Random.value * 4 - 2);

            spheres.Add(sphere);
        }

        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        _sphereBuffer = new ComputeBuffer(spheres.Count, 52);
        _sphereBuffer.SetData(spheres);
    }

    private void Awake() {
        _camera = GetComponent<Camera>();
    }

    private void SetShaderParameters() {
        Mathf.Clamp(RayTracingCount, 1, 8);

        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayTracingShader.SetInt("_RayTracingCount", RayTracingCount + 1);

        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));

        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        SetShaderParameters();
        Render(destination);
    }

    private void Render(RenderTexture destination) {
        InitRenderTexture();

        //Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);

        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        //Blit the result to the screen
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", _currentSample);

        Graphics.Blit(_target, destination, _addMaterial);
        _currentSample++;
    }

    private void InitRenderTexture() {
        if (_target == null || _target.width != Screen.width
            || _target.height != Screen.height) {

            _currentSample = 0;

            //Release render texture if we already one
            if (_target != null)
                _target.Release();

            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }

    private void Update() {
        if (transform.hasChanged) {
            _currentSample = 0;
            transform.hasChanged = false;
        }

        float delta = Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.F)) {
            SetUpScene();
        }

        if (Input.GetKey(KeyCode.E)) {


            for (int i = 0; i < spheres.Count; i++) {
                Sphere sphere = spheres[i];

                Vector3 direction = (new Vector3(-sphere.position.x, 0, -sphere.position.z)).normalized;
                float length = (new Vector3(-sphere.position.x, 0, -sphere.position.z)).magnitude;

                sphere.velocity.x += direction.z * 0.9f * delta * 60.0f;
                sphere.velocity.z -= direction.x * 0.9f * delta * 60.0f;
                sphere.velocity.y += 0.07f / length;

                spheres[i] = sphere;
            }
        }

        if (Input.GetKey(KeyCode.Q)) {


            for (int i = 0; i < spheres.Count; i++) {
                Sphere sphere = spheres[i];
                Vector3 direction = (new Vector3(-sphere.position.x, 0, -sphere.position.z)).normalized;
                float slope = Mathf.Atan2(sphere.position.z, sphere.position.x);

                sphere.velocity.x -= direction.z * 0.1f * delta * 60.0f - 0.2f * Mathf.PI * direction.x * delta * 60.0f;
                sphere.velocity.z += direction.x * 0.1f * delta * 60.0f + 0.2f * Mathf.PI * direction.z * delta * 60.0f;
                sphere.velocity.y += 0.1f * delta * 60.0f;

                spheres[i] = sphere;
            }
        }

        if(Input.GetKeyDown(KeyCode.C)) {
            updateBalls = !updateBalls; 
        }

        if(updateBalls) {
            for (int i = 0; i < spheres.Count; i++) {
                Sphere sphere = spheres[i];

                if (sphere.position.y != sphere.radius && sphere.velocity.y != 0.0f) {
                    sphere.velocity.y += gravity * (delta);
                }

                sphere.position.x += sphere.velocity.x * (delta);
                sphere.position.y += sphere.velocity.y * (delta);
                sphere.position.z += sphere.velocity.z * (delta);

                if (sphere.position.y - sphere.radius <= 0) {
                    if (sphere.velocity.y < 0.1f) {
                        sphere.position.y = sphere.radius;
                        sphere.velocity.y *= -conservation;
                    }
                    else {
                        sphere.position.y = sphere.radius;
                        sphere.velocity.y = 0;
                    }

                }
                else if (sphere.position.y + sphere.radius >= height) {
                    sphere.position.y = (height - sphere.radius) - (sphere.position.y + sphere.radius - height);
                    sphere.velocity.y *= -conservation;
                }

                if (sphere.position.x - sphere.radius <= -(width)) {
                    if (sphere.velocity.x < -0.1f) {
                        sphere.velocity.x *= -conservation;
                        sphere.position.x = -2.0f * (width) - sphere.position.x + 2.0f * sphere.radius;
                    }
                    else {
                        sphere.velocity.x = 0;
                        sphere.position.x = -(width) + sphere.radius;
                    }
                }
                else if (sphere.position.x + sphere.radius >= (width)) {
                    if (sphere.velocity.x > 0.1f) {
                        sphere.velocity.x *= -conservation;
                        sphere.position.x = 2.0f * (width) - sphere.position.x - 2.0f * sphere.radius;
                    }
                    else {
                        sphere.velocity.x = 0;
                        sphere.position.x = (width) - sphere.radius;
                    }
                }


                if (sphere.position.z - sphere.radius <= -(width)) {
                    if (sphere.velocity.z < -0.1f) {
                        sphere.velocity.z *= -conservation;
                        sphere.position.z = -2.0f * (width) - sphere.position.z + 2.0f * sphere.radius;
                    }
                    else {
                        sphere.velocity.z = 0;
                        sphere.position.z = -(width) + sphere.radius;
                    }
                }
                else if (sphere.position.z + sphere.radius >= (width)) {
                    if (sphere.velocity.z > 0.1f) {
                        sphere.velocity.z *= -conservation;
                        sphere.position.z = 2.0f * (width) - sphere.position.z - 2.0f * sphere.radius;
                    }
                    else {
                        sphere.velocity.z = 0;
                        sphere.position.z = (width) - sphere.radius;
                    }
                }

                _currentSample++;

                for (int j = i + 1; j < spheres.Count; j++) {
                    Sphere sphere2 = spheres[j];

                    float rad = sphere.radius + sphere2.radius;

                    float d = (sphere.position - sphere2.position).sqrMagnitude;
                    if (d <= rad * rad) {
                        d = Mathf.Sqrt(d);
                        float overlap = 0.55f * (d - sphere.radius - sphere2.radius);
                        Sphere temp = sphere;
                        sphere.position.x -= overlap * (sphere.position.x - sphere2.position.x) / d;
                        sphere.position.y -= overlap * (sphere.position.y - sphere2.position.y) / d;
                        sphere.position.z -= overlap * (sphere.position.z - sphere2.position.z) / d;

                        sphere2.position.x += overlap * (temp.position.x - sphere2.position.x) / d;
                        sphere2.position.y += overlap * (temp.position.y - sphere2.position.y) / d;
                        sphere2.position.z += overlap * (temp.position.z - sphere2.position.z) / d;

                        float disX = sphere2.position.x - sphere.position.x;
                        float disY = sphere2.position.y - sphere.position.y;
                        float disZ = sphere2.position.z - sphere.position.z;

                        d = (sphere.position - sphere2.position).magnitude;

                        float nx = disX / d;
                        float ny = disY / d;
                        float nz = disZ / d;

                        float kx = sphere.velocity.x - sphere2.velocity.x;
                        float ky = sphere.velocity.y - sphere2.velocity.y;
                        float kz = sphere.velocity.z - sphere2.velocity.z;

                        float p = 2.0f * (nx * kx + ny * ky + nz * kz) / 2.0f;

                        sphere.velocity.x = (sphere.velocity.x - p * nx) * conservation;
                        sphere.velocity.y = (sphere.velocity.y - p * ny) * conservation;
                        sphere.velocity.z = (sphere.velocity.z - p * nz) * conservation;

                        sphere2.velocity.x = (sphere2.velocity.x + p * nx) * conservation;
                        sphere2.velocity.y = (sphere2.velocity.y + p * ny) * conservation;
                        sphere2.velocity.z = (sphere2.velocity.z + p * nz) * conservation;

                    }

                    spheres[j] = sphere2;
                }
                spheres[i] = sphere;
            }

            _currentSample = 0;
        }

        _sphereBuffer.SetData(spheres);
    }
}
