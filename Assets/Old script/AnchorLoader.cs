using System;
using TMPro;
using UnityEngine;

public class AnchorLoader : MonoBehaviour
{
    private OVRSpatialAnchor anchorPrefab;
    private SpatialAnchorManager spatialAnchorManager;

    // Delegate for handling localization callbacks
    Action<OVRSpatialAnchor.UnboundAnchor, bool> _onLoadAnchor;

    private void Awake()
    {
        spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        anchorPrefab = spatialAnchorManager.anchorPrefab.GetComponent<OVRSpatialAnchor>();
        _onLoadAnchor = OnLocalized;
    }

    public void LoadAnchorsByUuid()
    {
        if (!PlayerPrefs.HasKey(SpatialAnchorManager.NumUuidsPlayerPref))
        {
            PlayerPrefs.SetInt(SpatialAnchorManager.NumUuidsPlayerPref, 0);
        }

        int playerUuidCount = PlayerPrefs.GetInt(SpatialAnchorManager.NumUuidsPlayerPref);
        if (playerUuidCount == 0) return;

        var uuids = new Guid[playerUuidCount];
        for (int i = 0; i < playerUuidCount; ++i)
        {
            string uuidKey = "uuid" + i;
            string currentUuid = PlayerPrefs.GetString(uuidKey);
            uuids[i] = new Guid(currentUuid);
        }

        Load(new OVRSpatialAnchor.LoadOptions
        {
            Timeout = 0,
            StorageLocation = OVRSpace.StorageLocation.Local,
            Uuids = uuids
        });
    }

    private async void Load(OVRSpatialAnchor.LoadOptions options)
    {
        OVRSpatialAnchor.UnboundAnchor[] anchors = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(options);

        if (anchors == null || anchors.Length == 0) return;

        foreach (var anchor in anchors)
        {
            if (anchor.Localized)
            {
                _onLoadAnchor(anchor, true);
            }
            else if (!anchor.Localizing)
            {
                anchor.Localize(_onLoadAnchor);
            }
        }
    }

    private void OnLocalized(OVRSpatialAnchor.UnboundAnchor unboundAnchor, bool success)
    {
        if (!success) return;

        var pose = unboundAnchor.Pose;
        var spatialAnchor = Instantiate(anchorPrefab, pose.position, pose.rotation);
        unboundAnchor.BindTo(spatialAnchor);

        if (spatialAnchor.TryGetComponent<OVRSpatialAnchor>(out var anchor))
        {
            var uuidText = spatialAnchor.GetComponentInChildren<TextMeshProUGUI>();
            var savedStatusText = spatialAnchor.GetComponentsInChildren<TextMeshProUGUI>()[1];

            uuidText.text = "UUID: " + spatialAnchor.Uuid.ToString();
            savedStatusText.text = "Loaded from Device";
        }
    }
}