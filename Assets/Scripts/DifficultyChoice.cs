using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DifficultyChoice : MonoBehaviour
{
    public enum Difficulty { Facile, Normal, Hardcore }

    public Difficulty difficulty = Difficulty.Normal;
    public FruitSpawnerManager spawner;
    public TMP_Text label;
    [Min(0.4f)] public float spawnInterval = 1.5f;
    [Min(0f)] public float launchForce = 6f;
    [Min(0.1f)] public float respawnDelay = 0.6f;

    [Header("Selection")]
    public Color outlineColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Min(0.001f)] public float outlineWidth = 0.008f;
    [Range(0f, 1f)] public float selectedTextOutlineWidth = 0.25f;

    public static Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;
    public bool IsAnimating { get; private set; }

    private Renderer[] fruitRenderers;
    private Collider[] fruitColliders;
    private bool[] rendererStates;
    private bool[] colliderStates;
    private FontStyles originalTextStyle;
    private Material textMaterial;
    private float originalTextOutlineWidth;
    private Color32 originalTextOutlineColor;
    private bool originalTextOutlineEnabled;
    private readonly List<MeshRenderer> selectionOutlines = new List<MeshRenderer>();
    private bool isSelected;

    private void Awake()
    {
        if (spawner == null)
            spawner = FindAnyObjectByType<FruitSpawnerManager>();
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            originalTextStyle = label.fontStyle;
            // Une instance propre à ce texte évite de surligner les autres choix.
            textMaterial = label.fontMaterial;
            originalTextOutlineWidth = label.outlineWidth;
            originalTextOutlineColor = label.outlineColor;
            originalTextOutlineEnabled = textMaterial.IsKeywordEnabled("OUTLINE_ON");
        }

        var renderers = new List<Renderer>();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            // Le texte reste visible pendant l'animation de coupe.
            if (renderer.GetComponent<TMP_Text>() == null)
                renderers.Add(renderer);
        }
        fruitRenderers = renderers.ToArray();
        fruitColliders = GetComponentsInChildren<Collider>(true);
        rendererStates = new bool[fruitRenderers.Length];
        colliderStates = new bool[fruitColliders.Length];

        for (int i = 0; i < fruitRenderers.Length; i++)
            rendererStates[i] = fruitRenderers[i].enabled;
        for (int i = 0; i < fruitColliders.Length; i++)
            colliderStates[i] = fruitColliders[i].enabled;

        CreateSelectionOutline();
        UpdateSelectionVisual(false);
    }

    private void Start()
    {
        // Normal est le choix initial au lancement de la scène.
        if (difficulty == Difficulty.Normal)
            ApplySelection();
    }

    public void SelectDifficulty()
    {
        if (IsAnimating)
            return;

        ApplySelection();
        StartCoroutine(CutFeedback());
    }

    private void ApplySelection()
    {
        CurrentDifficulty = difficulty;
        if (spawner != null)
        {
            // Le délai aléatoire reste positif pour chaque difficulté.
            spawner.spawnInterval = Mathf.Max(spawnInterval, spawner.minRandomDelay + 0.05f);
            spawner.launchForce = launchForce;
        }

        foreach (var choice in FindObjectsByType<DifficultyChoice>(FindObjectsInactive.Include))
            choice.UpdateSelectionVisual(choice == this);
    }

    private void UpdateSelectionVisual(bool selected)
    {
        isSelected = selected;
        if (label != null)
        {
            // Chaque difficulté garde sa couleur, même lorsqu'elle est choisie.
            label.color = difficulty == Difficulty.Facile
                ? new Color(0.2f, 1f, 0.3f)
                : difficulty == Difficulty.Normal
                    ? new Color(1f, 0.55f, 0.1f)
                    : new Color(1f, 0.18f, 0.15f);
            label.fontStyle = selected
                ? originalTextStyle | FontStyles.Bold
                : originalTextStyle;

            if (selected || originalTextOutlineEnabled)
                textMaterial.EnableKeyword("OUTLINE_ON");
            else
                textMaterial.DisableKeyword("OUTLINE_ON");
            label.outlineColor = selected ? (Color32)outlineColor : originalTextOutlineColor;
            label.outlineWidth = selected ? selectedTextOutlineWidth : originalTextOutlineWidth;
            label.UpdateMeshPadding();
        }
        UpdateOutlineVisibility();
    }

    private void CreateSelectionOutline()
    {
        Material material = Resources.Load<Material>("Materials/DifficultySelectionOutline");
        if (material == null)
            return;

        foreach (var source in fruitRenderers)
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (!source.enabled || filter == null || filter.sharedMesh == null)
                continue;

            // Une coque sans collider dessine le contour sans modifier le fruit.
            var outlineObject = new GameObject("Selection Outline");
            outlineObject.transform.SetParent(source.transform, false);
            outlineObject.layer = 2; // Ignore Raycast
            outlineObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer outline = outlineObject.AddComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            outline.sharedMaterials = materials;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            outline.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var properties = new MaterialPropertyBlock();
            properties.SetColor("_OutlineColor", outlineColor);
            properties.SetFloat("_OutlineWidth", outlineWidth);
            properties.SetVector("_OutlineCenter", filter.sharedMesh.bounds.center);
            outline.SetPropertyBlock(properties);
            outline.enabled = false;
            selectionOutlines.Add(outline);
        }
    }

    private void UpdateOutlineVisibility()
    {
        foreach (var outline in selectionOutlines)
            if (outline != null) outline.enabled = isSelected && !IsAnimating;
    }

    private IEnumerator CutFeedback()
    {
        IsAnimating = true;
        UpdateOutlineVisibility();
        foreach (var renderer in fruitRenderers)
            if (renderer != null) renderer.enabled = false;
        foreach (var collider in fruitColliders)
            if (collider != null) collider.enabled = false;

        yield return new WaitForSeconds(respawnDelay);
        RestoreFruit();
    }

    private void RestoreFruit()
    {
        if (fruitRenderers != null)
            for (int i = 0; i < fruitRenderers.Length; i++)
                if (fruitRenderers[i] != null) fruitRenderers[i].enabled = rendererStates[i];
        if (fruitColliders != null)
            for (int i = 0; i < fruitColliders.Length; i++)
                if (fruitColliders[i] != null) fruitColliders[i].enabled = colliderStates[i];
        IsAnimating = false;
        UpdateOutlineVisibility();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreFruit();
    }
}
