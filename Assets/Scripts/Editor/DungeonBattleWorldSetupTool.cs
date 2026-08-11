using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DungeonBattleWorldSetupTool
{
    private const string ScenePath = "Assets/Scenes/ClientScene.unity";
    private const string PresentationFolder =
        "Assets/Resources/Presentation/DungeonWorld";
    private const string RenderTexturePath =
        PresentationFolder + "/DungeonBattleWorld.renderTexture";
    private const string ActorPrefabPath =
        PresentationFolder + "/DungeonWorldActor.prefab";
    private const string AreaPreviewPrefabPath =
        PresentationFolder + "/DungeonBattleAreaPreview.prefab";
    private const string ArenaRingMeshPath =
        PresentationFolder + "/DungeonArenaRing.asset";
    private const int WorldLayer = 8;
    private const int ForegroundLayer = 9;

    [MenuItem(
        PS260714EditorMenu.Root + "Dungeon/Apply 2.5D Battle World",
        false,
        240)]
    public static void Apply()
    {
        EnsureFolder(PresentationFolder);
        EnsureWorldLayers();

        RenderTexture renderTexture = EnsureRenderTexture();
        Material groundMaterial = EnsureMaterial(
            "DungeonGround",
            new Color(0.271f, 0.282f, 0.302f, 1f),
            0f,
            0.1f);
        Material wallMaterial = EnsureMaterial(
            "DungeonWall",
            new Color(0.455f, 0.514f, 0.569f, 1f),
            0f,
            0.14f);
        GameObject actorPrefab = LoadRequiredActorPrefab();
        GameObject areaPreviewPrefab = LoadRequiredAreaPreviewPrefab();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        DungeonBoardView board = FindSceneBoard();
        if (board == null)
        {
            throw new InvalidOperationException(
                "ClientScene does not contain DungeonBoardView.");
        }
        RemoveLegacyBoardVisuals(board.transform);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.cullingMask &= ~(1 << WorldLayer);
            mainCamera.cullingMask &= ~(1 << ForegroundLayer);
            EditorUtility.SetDirty(mainCamera);
        }

        GameObject worldRoot = FindRoot(scene, "DungeonBattleWorld");
        if (worldRoot == null)
        {
            worldRoot = new GameObject("DungeonBattleWorld");
            SceneManager.MoveGameObjectToScene(worldRoot, scene);
        }
        SetLayerRecursively(worldRoot, WorldLayer);

        Camera worldCamera = EnsureWorldCamera(worldRoot.transform, renderTexture);
        Camera foregroundCamera = EnsureForegroundCamera(
            worldRoot.transform,
            worldCamera);
        Transform environmentRoot = EnsureChild(
            worldRoot.transform,
            "Environment");
        Transform actorRoot = EnsureChild(worldRoot.transform, "Actors");
        Transform vfxRoot = EnsureChild(worldRoot.transform, "VFX");
        SetLayerRecursively(environmentRoot.gameObject, WorldLayer);
        SetLayerRecursively(actorRoot.gameObject, ForegroundLayer);
        SetLayerRecursively(vfxRoot.gameObject, ForegroundLayer);

        Transform arenaRing = ApplySimplifiedArenaRevision(
            environmentRoot,
            groundMaterial,
            wallMaterial,
            EnsureArenaRingMesh());
        Transform ground = environmentRoot.Find("geoGround");
        EnsureLights(worldRoot.transform);
        SpriteRenderer backdrop = EnsureBackdrop(environmentRoot);
        GameObject preview = EnsurePreview(
            actorRoot,
            actorPrefab,
            worldCamera);
        RawImage output = EnsureWorldOutput(
            board.transform as RectTransform,
            renderTexture);
        DungeonWorldInputView input = EnsureWorldInput(
            board.transform as RectTransform);

        SerializedObject serializedBoard = new(board);
        Assign(serializedBoard, "worldPresentationRoot", worldRoot);
        Assign(serializedBoard, "worldOutput", output.gameObject);
        Assign(serializedBoard, "worldCamera", worldCamera);
        Assign(serializedBoard, "worldForegroundCamera", foregroundCamera);
        Assign(serializedBoard, "worldActorRoot", actorRoot);
        Assign(serializedBoard, "worldVfxRoot", vfxRoot);
        Assign(serializedBoard, "worldActorPrefab", actorPrefab);
        Assign(serializedBoard, "worldAreaPreviewPrefab", areaPreviewPrefab);
        Assign(serializedBoard, "worldActorPreview", preview);
        Assign(serializedBoard, "worldInputView", input);
        Assign(serializedBoard, "worldBackdrop", backdrop);
        Assign(serializedBoard, "worldGround", ground);
        Assign(serializedBoard, "worldArenaRing", arenaRing);
        DungeonBattleCoreWorldGaugeView coreGauge =
            environmentRoot.GetComponentInChildren<
                DungeonBattleCoreWorldGaugeView>(true);
        if (coreGauge == null)
        {
            throw new InvalidOperationException(
                "The authored battle core world gauge is missing from ClientScene.");
        }
        Assign(serializedBoard, "worldBattleCoreGauge", coreGauge);
        DungeonHudPresentationSO presentation =
            DungeonHudPresentation.Load();
        AssignFloat(
            serializedBoard,
            "worldAllyHeight",
            presentation.WorldAllyHeight);
        AssignFloat(
            serializedBoard,
            "worldEnemyHeight",
            presentation.WorldEnemyHeight);
        serializedBoard.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(board);

        BattleVfxPlayer vfxPlayer = board.GetComponent<BattleVfxPlayer>();
        if (vfxPlayer != null)
        {
            SerializedObject serializedVfx = new(vfxPlayer);
            Assign(serializedVfx, "worldCamera", worldCamera);
            Assign(serializedVfx, "spawnRoot", vfxRoot);
            serializedVfx.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vfxPlayer);
        }

        worldRoot.SetActive(true);
        output.gameObject.SetActive(true);
        preview.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied the simplified 2.5D battle arena to ClientScene.");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static DungeonBoardView FindSceneBoard()
    {
        DungeonBoardView[] boards =
            UnityEngine.Object.FindObjectsByType<DungeonBoardView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        return boards.Length > 0 ? boards[0] : null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    private static void EnsureWorldLayers()
    {
        SerializedObject tagManager = new(
            AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        EnsureLayer(layers, WorldLayer, "DungeonWorld");
        EnsureLayer(layers, ForegroundLayer, "DungeonForeground");
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureLayer(
        SerializedProperty layers,
        int index,
        string layerName)
    {
        SerializedProperty layer = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrWhiteSpace(layer.stringValue))
        {
            layer.stringValue = layerName;
            return;
        }

        if (!string.Equals(
                layer.stringValue,
                layerName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Layer {index} is already used by '{layer.stringValue}'.");
        }
    }

    private static RenderTexture EnsureRenderTexture()
    {
        RenderTexture texture =
            AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (texture != null)
            return texture;

        texture = new RenderTexture(1024, 1024, 24)
        {
            name = "DungeonBattleWorld",
            antiAliasing = 4,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
        };
        texture.Create();
        AssetDatabase.CreateAsset(texture, RenderTexturePath);
        return texture;
    }

    private static Material EnsureMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness)
    {
        string path = PresentationFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit shader was not found.");

        bool created = material == null;
        if (created)
        {
            material = new Material(shader)
            {
                name = name,
            };
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.color = color;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (created)
            AssetDatabase.CreateAsset(material, path);
        else
            EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject LoadRequiredActorPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            ActorPrefabPath);
        if (prefab == null ||
            prefab.GetComponent<DungeonWorldActorPrefabView>() == null)
        {
            throw new InvalidOperationException(
                $"Authored dungeon actor prefab is missing or invalid: {ActorPrefabPath}");
        }
        return prefab;
    }

    private static GameObject LoadRequiredAreaPreviewPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AreaPreviewPrefabPath);
        if (prefab == null ||
            prefab.GetComponent<DungeonBattleAreaPreviewPrefabView>() == null)
        {
            throw new InvalidOperationException(
                $"Authored battle area preview prefab is missing or invalid: {AreaPreviewPrefabPath}");
        }
        return prefab;
    }

    private static Sprite LoadPreviewSprite()
    {
        GameObject allyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Presentation/DungeonAllyActor.prefab");
        Image previewImage = allyPrefab != null
            ? allyPrefab.GetComponent<Image>()
            : null;
        return previewImage != null ? previewImage.sprite : null;
    }

    private static Camera EnsureWorldCamera(
        Transform parent,
        RenderTexture renderTexture)
    {
        Transform cameraTransform = parent.Find("DungeonWorldCamera");
        bool created = cameraTransform == null;
        if (created)
        {
            GameObject cameraObject = new("DungeonWorldCamera");
            cameraObject.layer = WorldLayer;
            cameraTransform = cameraObject.transform;
            cameraTransform.SetParent(parent, false);
        }

        DungeonHudPresentationSO presentation =
            DungeonHudPresentation.Load();
        cameraTransform.localPosition =
            presentation.WorldCameraLocalPosition;
        cameraTransform.localRotation = Quaternion.Euler(
            presentation.WorldCameraLocalEulerAngles);

        Camera camera = cameraTransform.GetComponent<Camera>();
        if (camera == null)
            camera = cameraTransform.gameObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.105f, 0.115f, 0.13f, 1f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 80f;
        camera.depth = -10f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.fieldOfView = presentation.WorldCameraFieldOfView;
        camera.cullingMask = (1 << WorldLayer) | 1;
        camera.targetTexture = renderTexture;

        UniversalAdditionalCameraData additional =
            camera.GetComponent<UniversalAdditionalCameraData>();
        if (additional == null)
            additional = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        additional.renderPostProcessing = true;
        return camera;
    }

    private static Camera EnsureForegroundCamera(
        Transform parent,
        Camera baseCamera)
    {
        Transform cameraTransform = parent.Find("DungeonForegroundCamera");
        if (cameraTransform == null)
        {
            GameObject cameraObject = new("DungeonForegroundCamera");
            cameraTransform = cameraObject.transform;
            cameraTransform.SetParent(parent, false);
        }

        cameraTransform.gameObject.layer = ForegroundLayer;
        cameraTransform.localPosition = baseCamera.transform.localPosition;
        cameraTransform.localRotation = baseCamera.transform.localRotation;

        Camera camera = cameraTransform.GetComponent<Camera>();
        if (camera == null)
            camera = cameraTransform.gameObject.AddComponent<Camera>();
        camera.CopyFrom(baseCamera);
        camera.cullingMask = 1 << ForegroundLayer;
        camera.clearFlags = CameraClearFlags.Depth;
        camera.depth = baseCamera.depth + 1f;
        camera.targetTexture = null;

        UniversalAdditionalCameraData foregroundData =
            camera.GetComponent<UniversalAdditionalCameraData>();
        if (foregroundData == null)
        {
            foregroundData = camera.gameObject.AddComponent<
                UniversalAdditionalCameraData>();
        }
        foregroundData.renderType = CameraRenderType.Overlay;
        foregroundData.renderPostProcessing = false;
        SerializedObject serializedForeground = new(foregroundData);
        SerializedProperty clearDepth =
            serializedForeground.FindProperty("m_ClearDepth");
        if (clearDepth != null)
        {
            clearDepth.boolValue = false;
            serializedForeground.ApplyModifiedPropertiesWithoutUndo();
        }

        UniversalAdditionalCameraData baseData =
            baseCamera.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null)
        {
            baseData = baseCamera.gameObject.AddComponent<
                UniversalAdditionalCameraData>();
        }
        baseData.renderType = CameraRenderType.Base;
        if (!baseData.cameraStack.Contains(camera))
            baseData.cameraStack.Add(camera);
        return camera;
    }

    private static Transform ApplySimplifiedArenaRevision(
        Transform environmentRoot,
        Material groundMaterial,
        Material ringMaterial,
        Mesh ringMesh)
    {
        GameObject ground = CreatePrimitive(
            environmentRoot,
            "geoGround",
            PrimitiveType.Cube,
            Vector3.zero,
            Vector3.one,
            Quaternion.identity,
            groundMaterial);
        ConfigurePrimitive(
            ground,
            new Vector3(0f, -0.05f, 0f),
            new Vector3(40f, 0.1f, 40f),
            Quaternion.identity,
            groundMaterial);
        MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
        if (groundRenderer != null)
        {
            groundRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        RemoveGeneratedEnvironmentObject(environmentRoot, "geoCoreFloor");
        RemoveGeneratedEnvironmentObject(
            environmentRoot,
            "grpBackdropArchitecture");

        Transform wallRoot = EnsureChild(environmentRoot, "grpCoreWall");
        for (int index = wallRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = wallRoot.GetChild(index);
            if (child.name.StartsWith("geoWall_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        Transform ringTransform = wallRoot.Find("geoArenaRing");
        if (ringTransform == null)
        {
            GameObject ringObject = new("geoArenaRing");
            ringTransform = ringObject.transform;
            ringTransform.SetParent(wallRoot, false);
        }

        ringTransform.localPosition = Vector3.zero;
        ringTransform.localRotation = Quaternion.identity;
        ringTransform.localScale = Vector3.one;
        MeshFilter filter = ringTransform.GetComponent<MeshFilter>();
        if (filter == null)
            filter = ringTransform.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = ringMesh;
        MeshRenderer renderer = ringTransform.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = ringTransform.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        SetLayerRecursively(ringTransform.gameObject, WorldLayer);
        return ringTransform;
    }

    private static Mesh EnsureArenaRingMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArenaRingMeshPath);
        bool created = mesh == null;
        if (created)
            mesh = new Mesh();

        BattleArenaRingMeshBuilder.Populate(
            mesh,
            2.14f,
            2.34f,
            0.08f,
            96);
        if (created)
            AssetDatabase.CreateAsset(mesh, ArenaRingMeshPath);
        else
            EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static void RemoveGeneratedEnvironmentObject(
        Transform parent,
        string name)
    {
        Transform target = parent != null ? parent.Find(name) : null;
        if (target != null)
            UnityEngine.Object.DestroyImmediate(target.gameObject);
    }

    private static void ConfigurePrimitive(
        GameObject instance,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        if (instance == null)
            return;

        instance.transform.localPosition = localPosition;
        instance.transform.localScale = localScale;
        instance.transform.localRotation = localRotation;
        SetLayerRecursively(instance, WorldLayer);
        Collider collider = instance.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
        MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static GameObject CreatePrimitive(
        Transform parent,
        string name,
        PrimitiveType type,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject instance = GameObject.CreatePrimitive(type);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localScale = localScale;
        instance.transform.localRotation = localRotation;
        SetLayerRecursively(instance, WorldLayer);
        Collider collider = instance.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
        MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        return instance;
    }

    private static void EnsureLights(Transform parent)
    {
        Transform directionalTransform = parent.Find("WorldKeyLight");
        if (directionalTransform == null)
        {
            GameObject lightObject = new("WorldKeyLight");
            directionalTransform = lightObject.transform;
            directionalTransform.SetParent(parent, false);
            directionalTransform.localRotation =
                Quaternion.Euler(48f, -32f, 0f);
        }

        directionalTransform.localRotation = Quaternion.Euler(48f, -32f, 0f);
        Light keyLight = directionalTransform.GetComponent<Light>();
        if (keyLight == null)
            keyLight = directionalTransform.gameObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.color = new Color(0.92f, 0.95f, 1f, 1f);
        keyLight.intensity = 1.05f;
        keyLight.shadows = LightShadows.Soft;

        EnsurePointLight(
            parent,
            "WorldFillLightLeft",
            new Vector3(-3.4f, 2.8f, -1.4f),
            new Color(0.72f, 0.8f, 0.9f, 1f));
        EnsurePointLight(
            parent,
            "WorldFillLightRight",
            new Vector3(3.4f, 2.4f, 1.8f),
            new Color(0.68f, 0.74f, 0.82f, 1f));
        SetLayerRecursively(directionalTransform.gameObject, WorldLayer);
    }

    private static void EnsurePointLight(
        Transform parent,
        string name,
        Vector3 position,
        Color color)
    {
        Transform lightTransform = parent.Find(name);
        if (lightTransform == null)
        {
            GameObject lightObject = new(name);
            lightTransform = lightObject.transform;
            lightTransform.SetParent(parent, false);
        }

        lightTransform.localPosition = position;
        Light light = lightTransform.GetComponent<Light>();
        if (light == null)
            light = lightTransform.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 3.5f;
        light.range = 8f;
        light.shadows = LightShadows.None;
        SetLayerRecursively(lightTransform.gameObject, WorldLayer);
    }

    private static SpriteRenderer EnsureBackdrop(Transform parent)
    {
        Transform backdropTransform = parent.Find("imgBattleBackdrop");
        if (backdropTransform == null)
        {
            GameObject backdropObject = new("imgBattleBackdrop");
            backdropObject.layer = WorldLayer;
            backdropTransform = backdropObject.transform;
            backdropTransform.SetParent(parent, false);
        }

        backdropTransform.localPosition = new Vector3(0f, 4.2f, 7.2f);
        backdropTransform.localRotation = Quaternion.Euler(50f, 0f, 0f);

        SpriteRenderer renderer =
            backdropTransform.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = backdropTransform.gameObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = -100;
        renderer.gameObject.SetActive(false);
        return renderer;
    }

    private static GameObject EnsurePreview(
        Transform actorRoot,
        GameObject actorPrefab,
        Camera worldCamera)
    {
        Transform existing = actorRoot.Find("DungeonWorldActor_Preview");
        GameObject preview = existing != null
            ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(
                actorPrefab,
                actorRoot);
        preview.name = "DungeonWorldActor_Preview";
        preview.transform.localPosition = Vector3.zero;
        Transform billboard = preview.transform.Find(
            "grpVerticalBillboard");
        Transform sprite = preview.transform.Find(
            "grpVerticalBillboard/imgActor");
        if (sprite != null)
        {
            SpriteRenderer renderer = sprite.GetComponent<SpriteRenderer>();
            Sprite previewSprite = LoadPreviewSprite();
            if (renderer != null && previewSprite != null)
            {
                renderer.sprite = previewSprite;
                const float previewHeight = 1.85f;
                float spriteHeight = Mathf.Max(
                    0.0001f,
                    previewSprite.bounds.size.y);
                float scale = previewHeight / spriteHeight;
                sprite.localScale = Vector3.one * scale;
                sprite.localPosition = new Vector3(
                    0f,
                    -previewSprite.bounds.min.y * scale,
                    0f);
            }
            sprite.localRotation = Quaternion.identity;
        }
        if (billboard != null && worldCamera != null)
            billboard.rotation = worldCamera.transform.rotation;
        SetLayerRecursively(preview, ForegroundLayer);
        return preview;
    }

    private static RawImage EnsureWorldOutput(
        RectTransform board,
        RenderTexture renderTexture)
    {
        if (board == null)
            throw new InvalidOperationException("Dungeon board RectTransform is missing.");

        Transform existing = board.Find("imgBattleWorldOutput");
        RawImage output;
        if (existing == null)
        {
            GameObject outputObject = new(
                "imgBattleWorldOutput",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            RectTransform rect = outputObject.GetComponent<RectTransform>();
            rect.SetParent(board, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetSiblingIndex(Mathf.Min(2, board.childCount - 1));
            output = outputObject.GetComponent<RawImage>();
            output.raycastTarget = false;
        }
        else
        {
            output = existing.GetComponent<RawImage>();
            if (output == null)
                output = existing.gameObject.AddComponent<RawImage>();
        }
        output.texture = renderTexture;
        output.color = Color.white;
        return output;
    }

    private static DungeonWorldInputView EnsureWorldInput(
        RectTransform board)
    {
        if (board == null)
            throw new InvalidOperationException("Dungeon board RectTransform is missing.");

        Transform existing = board.Find("imgBattleWorldInput");
        DungeonWorldInputView input = existing != null
            ? existing.GetComponent<DungeonWorldInputView>()
            : null;
        if (existing != null && input == null)
        {
            throw new InvalidOperationException(
                "imgBattleWorldInput must contain DungeonWorldInputView.");
        }
        if (input != null)
            return input;

        GameObject inputObject = new(
            "imgBattleWorldInput",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DungeonWorldInputView));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.SetParent(board, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = inputObject.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        inputObject.transform.SetAsLastSibling();
        return inputObject.GetComponent<DungeonWorldInputView>();
    }

    private static void RemoveLegacyBoardVisuals(Transform board)
    {
        if (board == null)
            return;

        string[] legacyNames =
        {
            "grpDungeonGrid",
            "imgBoardShadow",
            "imgBoardSurface",
            "imgBattleCoreWall",
            "imgBattleCoreInterior",
            "grpBattleAllies",
        };
        foreach (string legacyName in legacyNames)
        {
            Transform legacy = board.Find(legacyName);
            if (legacy != null)
                UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;

        GameObject childObject = new(name);
        childObject.layer = WorldLayer;
        child = childObject.transform;
        child.SetParent(parent, false);
        return child;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
        }
        return null;
    }

    private static void Assign(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found.");
        }
        property.objectReferenceValue = value;
    }

    private static void AssignFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found.");
        }
        property.floatValue = value;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
