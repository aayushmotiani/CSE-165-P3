using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class SurfaceAnchorManager : MonoBehaviour
{
    public GameObject anchorPrefab;
    public Transform controller;
    public TMP_Dropdown surfaceTypeDropdown;
    public Button placeAnchorButton;

    private string currentSurfaceType = "Wall";

    private void Start()
    {
        surfaceTypeDropdown.onValueChanged.AddListener(OnDropdownChanged);
        placeAnchorButton.onClick.AddListener(() => PlaceAnchor());
    }
    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
        {
            PlaceAnchor();
        }
    }


    private void OnDropdownChanged(int index)
    {
        currentSurfaceType = surfaceTypeDropdown.options[index].text;
    }

    public async void PlaceAnchor()
    {
        Vector3 position = controller.position + controller.forward * 1.0f;
        Quaternion rotation = Quaternion.LookRotation(-controller.forward); // Face the user

        GameObject anchorGO = Instantiate(anchorPrefab, position, rotation);
        anchorGO.name = "Anchor_" + currentSurfaceType;

        // Attach a quad mesh as a visual representation
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(anchorGO.transform);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(1.5f, 1.5f, 1);
        quad.GetComponent<Renderer>().material.color = GetColorByType(currentSurfaceType);

        // Save the anchor using SaveAnchorAsync
        OVRSpatialAnchor anchor = anchorGO.GetComponent<OVRSpatialAnchor>();
        if (anchor != null)
        {
            var result = await anchor.SaveAnchorAsync();

            if (result.Success)
            {
                Guid uuid = anchor.Uuid;
                PlayerPrefs.SetString("uuid_" + currentSurfaceType + "_" + uuid.ToString(), uuid.ToString());
                Debug.Log($" Anchor {uuid} saved successfully as {currentSurfaceType}.");
            }
            else
            {
                Debug.LogError($" Failed to save anchor {anchor.Uuid} with error {result.Status}");
            }
        }
    }

    private Color GetColorByType(string type)
    {
        return type switch
        {
            "Wall" => Color.red,
            "Floor" => Color.green,
            "Table" => Color.blue,
            "Chair" => Color.yellow,
            _ => Color.white,
        };
    }
}
