using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class AnchorManager : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject wallPrefab;
    public GameObject cubePrefab;
    public Transform controllerRight;
    public Transform controllerLeft; 
    public TextMeshProUGUI modeLabel;


    private GameObject selectedPrefab;
    private GameObject lastSpawnedObject;
    private Coroutine labelRoutine;
    private GameObject currentlyPointedObject = null;
    private GameObject lastHighlightedObject = null;

    private Dictionary<Guid, GameObject> anchorObjectMap = new();
    private const string AnchorListKey = "anchor_ids";

    private async void Start()
    {
        selectedPrefab = wallPrefab;
        ShowModeLabel("Wall Mode");

        await LoadSavedAnchors();
    }

    private void Update()
    {
        Ray ray = new Ray(controllerLeft.position, controllerLeft.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 3f))
        {
            GameObject hitObj = hit.collider.gameObject;
            if (hitObj != currentlyPointedObject)
            {
                // Restore last highlighted object to original color
                if (lastHighlightedObject != null && anchorObjectMap.ContainsValue(lastHighlightedObject))
                {
                    var anchor = lastHighlightedObject.GetComponent<OVRSpatialAnchor>();
                    if (anchor != null)
                    {
                        bool isSaved = PlayerPrefs.HasKey("anchor_" + anchor.Uuid);
                        UpdateObjectColor(lastHighlightedObject, isSaved);
                    }
                }

                // Highlight current object
                currentlyPointedObject = hitObj;
                lastHighlightedObject = hitObj;
                SetObjectColor(hitObj, Color.blue);
            }
        }
        else
        {
            // Clear highlight if nothing is pointed to
            if (lastHighlightedObject != null)
            {
                var anchor = lastHighlightedObject.GetComponent<OVRSpatialAnchor>();
                if (anchor != null)
                {
                    bool isSaved = PlayerPrefs.HasKey("anchor_" + anchor.Uuid);
                    UpdateObjectColor(lastHighlightedObject, isSaved);
                }

                currentlyPointedObject = null;
                lastHighlightedObject = null;
            }
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            selectedPrefab = (selectedPrefab == wallPrefab) ? cubePrefab : wallPrefab;
            string label = selectedPrefab == wallPrefab ? "Wall Mode" : "Cube Mode";
            ShowModeLabel(label);
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick) && selectedPrefab != null)
        {
            PlaceObjectWithAnchor();
        }

        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            if (currentlyPointedObject != null) ToggleAnchorSave(currentlyPointedObject);
        }

        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            if (currentlyPointedObject != null) DeleteObjectAndAnchor(currentlyPointedObject);
        }
    }

    private async void PlaceObjectWithAnchor()
    {
        Vector3 forwardFlat = Vector3.ProjectOnPlane(controllerRight.forward, Vector3.up).normalized;
        Vector3 spawnPos = controllerRight.position + forwardFlat * 0.5f;
        Quaternion spawnRot = Quaternion.LookRotation(forwardFlat, Vector3.up) * Quaternion.Euler(0, 90f, 0); 

        GameObject obj = Instantiate(selectedPrefab, spawnPos, spawnRot);
        OVRSpatialAnchor anchor = obj.AddComponent<OVRSpatialAnchor>();

        await Task.Yield();
        while (!anchor.Created)
            await Task.Yield();

        anchorObjectMap[anchor.Uuid] = obj;
        lastSpawnedObject = obj;

        UpdateObjectColor(obj, false);
        Debug.Log($" Spawned {selectedPrefab.name} with anchor UUID: {anchor.Uuid}");

        // Save object type for later restoration
        PlayerPrefs.SetString("type_" + anchor.Uuid, selectedPrefab == wallPrefab ? "wall" : "cube");
    }

    private async void ToggleAnchorSave(GameObject obj)
    {
        var anchor = obj.GetComponent<OVRSpatialAnchor>();
        if (anchor == null) { Debug.LogWarning(" No OVRSpatialAnchor found."); return; }

        string key = "anchor_" + anchor.Uuid;

        if (!PlayerPrefs.HasKey(key))
        {
            var result = await anchor.SaveAnchorAsync();
            if (result.Success)
            {
                PlayerPrefs.SetString(key, anchor.Uuid.ToString());
                AddAnchorToSavedList(anchor.Uuid);
                UpdateObjectColor(obj, true);
                Debug.Log($" Anchor saved! UUID: {anchor.Uuid}");
            }
            else
            {
                Debug.LogError($" Save failed: {result.Status}");
            }
        }
        else
        {
            var result = await anchor.EraseAnchorAsync();
            if (result.Success)
            {
                PlayerPrefs.DeleteKey(key);
                RemoveAnchorFromSavedList(anchor.Uuid);
                UpdateObjectColor(obj, false);
                Debug.Log($" Anchor unsaved: {anchor.Uuid}");
            }
            else
            {
                Debug.LogError($" Erase failed: {result.Status}");
            }
        }
    }

    private async void DeleteObjectAndAnchor(GameObject obj)
    {
        var anchor = obj.GetComponent<OVRSpatialAnchor>();
        if (anchor == null) return;

        var result = await anchor.EraseAnchorAsync();
        if (result.Success)
        {
            string uuid = anchor.Uuid.ToString();
            PlayerPrefs.DeleteKey("anchor_" + uuid);
            PlayerPrefs.DeleteKey("type_" + uuid);
            RemoveAnchorFromSavedList(anchor.Uuid);
            anchorObjectMap.Remove(anchor.Uuid);
            Destroy(obj);
            lastSpawnedObject = null;
            Debug.Log($" Deleted anchor and object {uuid}");
        }
    }

    private async Task LoadSavedAnchors()
    {
        string list = PlayerPrefs.GetString(AnchorListKey, "");
        List<Guid> savedUuids = new();

        foreach (var idStr in list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(idStr, out Guid uuid))
            {
                savedUuids.Add(uuid);
            }
        }

        if (savedUuids.Count == 0)
        {
            Debug.Log(" No saved anchors found.");
            return;
        }

        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(savedUuids, unboundAnchors);

        if (!result.Success)
        {
            Debug.LogError($" Failed to load anchors: {result.Status}");
            return;
        }

        foreach (var unbound in unboundAnchors)
        {
            var localized = await unbound.LocalizeAsync();
            if (!localized) continue;

            if (!unbound.TryGetPose(out Pose pose))
            {
                Debug.LogWarning($" Failed to get pose for anchor {unbound.Uuid}");
                continue;
            }

            string typeKey = "type_" + unbound.Uuid.ToString();
            string type = PlayerPrefs.GetString(typeKey, "wall");
            GameObject prefab = type == "cube" ? cubePrefab : wallPrefab;

            GameObject obj = Instantiate(prefab, pose.position, pose.rotation);
            var anchor = obj.AddComponent<OVRSpatialAnchor>();
            unbound.BindTo(anchor);

            anchorObjectMap[anchor.Uuid] = obj;
            UpdateObjectColor(obj, true);

            Debug.Log($" Restored anchor: {anchor.Uuid}, Type: {type}");
        }
    }

    private void AddAnchorToSavedList(Guid uuid)
    {
        string list = PlayerPrefs.GetString(AnchorListKey, "");
        var ids = new HashSet<string>(list.Split(','));
        ids.Add(uuid.ToString());
        PlayerPrefs.SetString(AnchorListKey, string.Join(",", ids));
    }

    private void RemoveAnchorFromSavedList(Guid uuid)
    {
        string list = PlayerPrefs.GetString(AnchorListKey, "");
        var ids = new HashSet<string>(list.Split(','));
        ids.Remove(uuid.ToString());
        PlayerPrefs.SetString(AnchorListKey, string.Join(",", ids));
    }

    private void UpdateObjectColor(GameObject obj, bool saved)
    {
        var rend = obj.GetComponent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = saved ? Color.green : Color.red;
        }
    }

    private void ShowModeLabel(string text)
    {
        if (labelRoutine != null)
            StopCoroutine(labelRoutine);
        labelRoutine = StartCoroutine(ShowLabelCoroutine(text));
    }

    private IEnumerator ShowLabelCoroutine(string text)
    {
        modeLabel.text = text;
        modeLabel.alpha = 1;

        yield return new WaitForSeconds(2f);

        float fadeDuration = 1f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            modeLabel.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        modeLabel.alpha = 0;
    }

    
    private void SetObjectColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            rend.material.color = color;
        }
    }
}
