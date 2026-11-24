using System.Collections;
using System.Collections.Generic;
using EzySlice;
using UnityEngine;

public class VFXPreviewButton : MonoBehaviour
{
    public ParticleSystem effectPrefab;
    public ParticleSystem effectPrefabQuest;
    public Transform spawnPoint;
    public Transform visual;
    [Min(0.1f)] public float effectLifetime = 5f;
    [Min(0.1f)] public float respawnDelay = 1f;

    public bool IsCutting { get; private set; }

    private void Start()
    {
#if UNITY_ANDROID
        effectPrefab = effectPrefabQuest;
#endif
    }

    public void Cut(Vector3 contactPoint, Material cutMaterial)
    {
        if (IsCutting || visual == null) return;

        MeshFilter[] meshes = visual.GetComponentsInChildren<MeshFilter>();
        if (meshes.Length == 0) return;
        Bounds bounds = meshes[0].GetComponent<Renderer>().bounds;
        foreach (var mesh in meshes)
            bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);

        var pieces = new List<GameObject>();
        bool meshWasCut = false;
        foreach (var mesh in meshes)
        {
            SlicedHull hull = mesh.gameObject.Slice(bounds.center, Vector3.up, cutMaterial);
            if (hull != null)
            {
                foreach (var piece in new[] {
                    hull.CreateUpperHull(mesh.gameObject, cutMaterial),
                    hull.CreateLowerHull(mesh.gameObject, cutMaterial) })
                {
                    if (piece == null) continue;
                    piece.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
                    piece.transform.localScale = mesh.transform.lossyScale;
                    pieces.Add(piece);
                }
                meshWasCut = true;
            }
            else
            {
                // Les petites pieces hors du plan de coupe (meche, sommet) tombent aussi.
                var part = new GameObject("Morceau VFX");
                part.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
                part.transform.localScale = mesh.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mesh.GetComponent<Renderer>().sharedMaterials;
                pieces.Add(part);
            }
        }

        if (!meshWasCut)
        {
            foreach (var piece in pieces) Destroy(piece);
            return;
        }

        IsCutting = true;
        visual.gameObject.SetActive(false);
        foreach (var piece in pieces)
        {
            if (piece == null) continue;
            piece.name = "Morceau VFX";
            piece.AddComponent<BoxCollider>();
            Rigidbody body = piece.AddComponent<Rigidbody>();
            body.linearVelocity = (piece.GetComponent<Renderer>().bounds.center - bounds.center).normalized * 0.8f;
            body.angularVelocity = Random.insideUnitSphere * 2f;
            Destroy(piece, respawnDelay);
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        if (effectPrefab != null)
        {
            ParticleSystem effect = Instantiate(effectPrefab,
                spawnPoint != null ? point.position : contactPoint, point.rotation);
            effect.Play(true);
            Destroy(effect.gameObject, effectLifetime);
        }
        StartCoroutine(Reappear());
    }

    private IEnumerator Reappear()
    {
        yield return new WaitForSeconds(respawnDelay);
        visual.gameObject.SetActive(true);
        IsCutting = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (visual != null) visual.gameObject.SetActive(true);
        IsCutting = false;
    }
}
