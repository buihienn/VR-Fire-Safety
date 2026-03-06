using UnityEngine;
using Oculus.Interaction;

public class DoorRightOpen : MonoBehaviour
{
    [Header("Refs")]
    public Transform leftDoorPivot;
    public OneGrabRotateTransformer rightRotateTransformer;
    public float openThreshold = 10f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rightRotateTransformer.Constraints.MinAngle.Value = 0;
        rightRotateTransformer.Constraints.MaxAngle.Value = 0;
    }

    // Update is called once per frame
    void Update()
    {
        float angle = leftDoorPivot.localEulerAngles.y;

        if (angle > 180f)
            angle -= 360f;

        bool leftOpen = Mathf.Abs(angle) > openThreshold;

        if (!leftOpen)
        {
            rightRotateTransformer.Constraints.MinAngle.Value = 0;
            rightRotateTransformer.Constraints.MaxAngle.Value = 0;
        }
        else
        {
            rightRotateTransformer.Constraints.MinAngle.Value = -90;
            rightRotateTransformer.Constraints.MaxAngle.Value = 0;
        }

    }
}
