using UnityEngine;

public class GestureManager : MonoBehaviour
{
    public Camera sceneCamera;
    public OVRHand leftHand;
    public OVRHand rightHand;
    public OVRSkeleton skeleton;

    public GameObject agent;
    public Rigidbody rb;
    public float speed;
    private bool isIndexFingerPinching;

    private LineRenderer line;
    private Transform p2;

    private Transform handIndexTipTransform;
    public Animator anim;
    public LayerMask obstaclesLayer;
    public float wallCheckDistance = 10f;
    [SerializeField]private float counter=5f;


    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.material.color = Color.green;

        if (skeleton != null && skeleton.Bones != null && skeleton.Bones.Count > 0)
        {
            foreach (var b in skeleton.Bones)
            {
                if (b.Id == OVRSkeleton.BoneId.Hand_IndexTip)
                {
                    handIndexTipTransform = b.Transform;
                    break;
                }
            }
        }

        rb = agent.GetComponent<Rigidbody>();
        anim = agent.GetComponent<Animator>();
    }

    void Update()
    {
        if (leftHand.IsTracked)
        {
            isIndexFingerPinching = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            if (isIndexFingerPinching)
            {
                line.enabled = true;
                p2 = handIndexTipTransform;

                anim.SetBool("walking", true);
                line.SetPosition(0, p2.transform.position);
                line.SetPosition(1, p2.transform.position + (leftHand.PointerPose.transform.forward * 3f));

                bool isWallInFront = Physics.Raycast(agent.transform.position, leftHand.PointerPose.transform.forward, wallCheckDistance, obstaclesLayer);
                if (isWallInFront)
                {
                    line.material.color = Color.red;
                    Debug.Log("wall ahead!!!");
                    anim.SetBool("wallAhead", true);
                    anim.SetBool("walking", false);
                }
                else
                {
                    line.material.color = Color.green;
                    rb.AddForce(leftHand.PointerPose.transform.forward * speed, ForceMode.Force);
                    anim.SetBool("wallAhead", false);
                    anim.SetBool("walking", true);
                    counter -= Time.deltaTime;
                }            
            }
            else
            {
                line.enabled = false;
                anim.SetBool("walking", false);
            }
        }

    }

}