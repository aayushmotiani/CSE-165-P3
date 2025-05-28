using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class SpatialAnchorManager : MonoBehaviour
{ 
    public GameObject anchorPrefab;   // Prefab with OVRSpatialAnchor
    //public GameObject wallPrefab;     // A cube-like wall prefab (with Collider and Mesh)
    public const string NumUuidsPlayerPref = "numUuid";
    
    private TextMeshProUGUI uuidText;
    private TextMeshProUGUI savedStatusText;
    private List<OVRSpatialAnchor> anchors = new List<OVRSpatialAnchor>();
    private OVRSpatialAnchor lastCreatedAnchor;
    private Canvas canvas; 
    private AnchorLoader anchorLoader;

    private void Awake()
    {
        anchorLoader = GetComponent<AnchorLoader>();       
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            CreateSpatialAnchorWithWall();
        }
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            SaveLastCreatedAnchor();
        }
            
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            UnsaveLastCreatedAnchor();
        }
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            UnsaveAllAnchors();
        }
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
        {
            LoadSavedAnchors();
        }
            
    }

    public void CreateSpatialAnchorWithWall()
    {
        
        Vector3 controllerPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        
        Vector3 controllerForward = controllerRotation * Vector3.forward;

        // Project onto XZ plane to keep the wall upright
        Vector3 flatForward = new Vector3(controllerForward.x, 0, controllerForward.z).normalized;

        // If user is pointing perfectly up/down, fallback to forward
        if (flatForward == Vector3.zero)
            flatForward = Vector3.forward;

        // Rotate wall to face sideways to pointing direction (perpendicular)
        Quaternion wallRotation = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(0, 90f, 0);

        // Instantiate anchor + wall prefab at controller tip, with upright wall rotation
        OVRSpatialAnchor workingAnchor = Instantiate(anchorPrefab, controllerPosition, wallRotation).GetComponent<OVRSpatialAnchor>();
        canvas = workingAnchor.gameObject.transform.GetChild(0).GetComponent<Canvas>();
        Debug.Log(canvas == null);
        uuidText = canvas.gameObject.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        Debug.Log(uuidText == null);
        if (uuidText != null)
        {
            Debug.Log("UUID detected");
        }
        savedStatusText = canvas.gameObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        Debug.Log(savedStatusText == null);
        if (savedStatusText != null)
        {
            Debug.Log("savedStatusText detected");
        }

        StartCoroutine(AnchorCreated(workingAnchor));
    }

    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor)
    {
        // Wait until anchor is created and localized
        while (!workingAnchor.Created && !workingAnchor.Localized)
        {
            yield return new WaitForEndOfFrame();
        }

        Guid anchorGuid = workingAnchor.Uuid;
        anchors.Add(workingAnchor);
        lastCreatedAnchor = workingAnchor;

        uuidText.text =  "UUID: " + anchorGuid.ToString();
        savedStatusText.text = "Not Saved";
        
    }

    public async void SaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null)
        {
            savedStatusText.text = "No anchor to save!";
            return;
        }

        bool success = await lastCreatedAnchor.SaveAnchorAsync();
        if (success)
        {
            savedStatusText.text = "Saved";
            SaveUuidToPlayerPrefs(lastCreatedAnchor.Uuid);
        }
        else
        {
            savedStatusText.text = "Save Failed";
        }
    }

    void SaveUuidToPlayerPrefs(Guid uuid)
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref))
        { 
            PlayerPrefs.SetInt(NumUuidsPlayerPref, 0); 
        }
           

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref);
        PlayerPrefs.SetString("uuid" + count, uuid.ToString());
        PlayerPrefs.SetInt(NumUuidsPlayerPref, count + 1);
    }

    // ========== UNSAVE ==========
    public async void UnsaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null)
        {
            savedStatusText.text = "No anchor to erase!";
            return;
        }

        bool success = await lastCreatedAnchor.EraseAnchorAsync();
        if (success)
        {
            savedStatusText.text = "Not Saved";
        }
        else
        {
            savedStatusText.text = "Unsave Failed";
        }
    }

    private void UnsaveAllAnchors()
    {
        foreach (var anchor in anchors)
        {
            UnsaveAnchorAsync(anchor);
        }
        anchors.Clear();
        ClearAllUuidsFromPlayerPrefs();
    }

    private async void UnsaveAnchorAsync(OVRSpatialAnchor anchor)
    {
        bool success = await anchor.EraseAnchorAsync();
        if (success)
        {
            var textComponenets = anchor.GetComponentsInChildren<TextMeshProUGUI>();
            if (textComponenets.Length > 1)
            {
                var savedStatusText = textComponenets[1];
                savedStatusText.text = "Not Saved";
            }
        }
       
    }

    private void ClearAllUuidsFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            int playerNumUuid = PlayerPrefs.GetInt(NumUuidsPlayerPref);
            for (int i = 0; i < playerNumUuid; i++)
            {
                PlayerPrefs.DeleteKey("uuid" + i);
            }
            PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
            PlayerPrefs.Save();
        }
    }

    public void LoadSavedAnchors()
    {
        anchorLoader.LoadAnchorsByUuid();
    }

}