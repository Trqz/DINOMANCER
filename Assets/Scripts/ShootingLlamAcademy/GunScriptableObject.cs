using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

[CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
public class GunScriptableObject : ScriptableObject
{
    public GunType type;
    public string gunName;
    public GameObject modelPrefab;
    public Vector3 spawnPoint;
    public Vector3 spawnRotation;

    public DamageConfigScriptableObject damageConfig;
    public ShootConfigScriptableObject shootConfig;
    public TrailConfigScriptableObject trailConfig;

    private MonoBehaviour activeMonoBehaviour;
    private GameObject model;
    private float lastShootTime;
    private ParticleSystem shootSystem;
    private Camera playerCamera;
    private ObjectPool<TrailRenderer> trailPool;
    private Animator animator;
    private AudioSource audioSource;

    public void Spawn(Transform parent, MonoBehaviour activeMonoBehaviour, Camera camera)
    {
        this.activeMonoBehaviour = activeMonoBehaviour;
        lastShootTime = 0; //because scriptable objects are not reset in editor, only in build
        trailPool = new ObjectPool<TrailRenderer>(CreateTrail);
        model = Instantiate(modelPrefab);
        model.transform.SetParent(parent, false);
        model.transform.localPosition = spawnPoint;
        model.transform.localRotation = Quaternion.Euler(spawnRotation);

        shootSystem = model.GetComponentInChildren<ParticleSystem>();
        //playerCamera = activeMonoBehaviour.GetComponentInChildren<Camera>();
        playerCamera = camera;

        animator = parent.GetComponent<Animator>();

        audioSource = model.GetComponent<AudioSource>();
    }

    public void Shoot()
    {
        if(Time.time > shootConfig.fireRate + lastShootTime)
        {
            lastShootTime = Time.time;
            shootSystem.Play();
            animator.SetTrigger("Shoot");
            audioSource.Play();
            Vector3 shootDirection = playerCamera.transform.forward
                + new Vector3(
                    Random.Range(
                        -shootConfig.spread.x,
                        shootConfig.spread.x                        
                    ),
                    Random.Range(
                        -shootConfig.spread.y,
                        shootConfig.spread.y
                    ),
                    Random.Range(
                        -shootConfig.spread.z,
                        shootConfig.spread.z
                    )
                );
            shootDirection.Normalize();

            if(Physics.Raycast(
                    playerCamera.transform.position,
                    shootDirection,
                    out RaycastHit hit,
                    float.MaxValue,
                    shootConfig.hitMask
                ))
            {
                activeMonoBehaviour.StartCoroutine(
                    PlayTrail(
                        shootSystem.transform.position,
                        hit.point,
                        hit
                    )
                );
            }
            else
            {
                activeMonoBehaviour.StartCoroutine(
                    PlayTrail(
                        shootSystem.transform.position,
                        shootSystem.transform.position + (shootDirection * trailConfig.missDistance),
                        new RaycastHit()
                    )
                );
            }
        }
    }

    private IEnumerator PlayTrail(Vector3 startPoint, Vector3 endPoint, RaycastHit hit)
    {
        TrailRenderer instance = trailPool.Get();
        instance.gameObject.SetActive(true);
        instance.transform.position = startPoint;
        yield return null; //avoid influence from last frame's position

        instance.emitting = true;

        float distance = Vector3.Distance(startPoint, endPoint);
        float remainingDistance = distance;
        while(remainingDistance > 0)
        {
            instance.transform.position = Vector3.Lerp(
                startPoint,
                endPoint,
                Mathf.Clamp01(1 - (remainingDistance / distance))
            );
            remainingDistance -= trailConfig.trailSpeed * Time.deltaTime;

            yield return null;
        }

        instance.transform.position = endPoint;

        
        
        if(hit.collider != null)
        {
            /* code using the impact system tutorial: https://www.youtube.com/watch?v=kT2ZxjMuT_4
            * can be removed and use another sollution
            * if decided to use it, return to follow the tutorial in https://youtu.be/E-vIMamyORg?t=695
            SurfaceManager.Instance.HandleImpact();
            */

            if(hit.collider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damageConfig.GetDamage());
            }
        }

        yield return new WaitForSeconds(trailConfig.duration);
        yield return null;
        instance.emitting = false;
        instance.gameObject.SetActive(false);
        trailPool.Release(instance);
    }

    private TrailRenderer CreateTrail()
    {
        GameObject instance = new GameObject("Bullet Trail");
        TrailRenderer trail = instance.AddComponent<TrailRenderer>();
        trail.colorGradient = trailConfig.color;
        trail.material = trailConfig.material;
        trail.widthCurve = trailConfig.widthCurve;
        trail.time = trailConfig.duration;
        trail.minVertexDistance = trailConfig.minVertexDistance;

        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return trail;
    }
}
