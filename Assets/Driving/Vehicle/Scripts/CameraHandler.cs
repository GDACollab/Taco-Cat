using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CameraHandler : MonoBehaviour
{
    private Rigidbody2D vehicleRb;

    public GameObject vehicle;
    public StageManager playAreaGroundGeneration;

    public AudioManager audioManager;

    [Space(10)]
    public bool foundGenerationPoints;

    [Space(10)]
    public float camSpeed = 5f;
    public Vector3 offsetAdjustSpeed = new Vector3(3, 3, 3);

    [Header("Cam Horz Adjustment")]
    public Vector2 xPosRange = new Vector2(0, -100); // the range of x positions the camera should adjust between
    public Vector2 truckVelocityValueRange = new Vector2(300, 500);
    public Vector2 truckCurrVelocity;
    public float currVelocityPercent;

    [Header("Cam Height Adjustment")]
    public Vector2 yPosRange = new Vector2(0, -200); // the range of y positions the camera should adjust between
    public float heightOffsetPercentage = -0.25f;
    public Vector2 truckHeightValueRange = new Vector2(300, 500);
    public float truckHeight;

    [Header("Cam Zoom Adjustment")]
    public Vector2 zPosRange = new Vector2(-800, -2000); // the range of z positions the camera should adjust between

    [Header("Override")]
    public bool overrideCam;
    public Vector3 overrideCamOffset;

    [Header("Camera Generation-Based Offset")]
    public Vector3 currCamOffset;
    public Vector3 currCamPos;

    // Each value = 1/6 of the distance from the ceenter to the screen edge, + is to the left and - is to the right
    public float cameraSixthOffset = 2;
    float currZeroPos;
    Vector3 currApoint;
    Vector3 currBpoint;
    float slope;
    float constant;
    // Used for determining if new bezier points should be added to the list
    Vector3 lastCurvePoint = new Vector3(-99999999, 99999999, 0); //Tracks lowest Point from the last curve to judge what points should get added

    List<Vector3> camBezierPoints = new List<Vector3>(); //List of points used to determine where the 'zero' point should be
    public int bezierPointsListTracker = 0; //Current positioning in the list of points

    [Header("Camera Shake")]
    [Range(0, 5)]
    public float perfect_camShakeMagnitude = 0.5f;
    [Range(0, 5)]
    public float nitro_camShakeMagnitude = 0.5f;

    [Header("Fade Transition")]
    public Image blackImage;


    private void Start()
    {
        vehicleRb = vehicle.GetComponent<Rigidbody2D>();
    }
    private void Awake(){
        audioManager = GetComponentInChildren<AudioManager>();
    }

    public void Init()
    {
        StartCoroutine(Initialize());
    }

    public IEnumerator Initialize()
    {


        // << GET ALL GENERATION POINTS >>
        foundGenerationPoints = false;
        while (!foundGenerationPoints)
        {
            foreach (BezierCurveGeneration chunk in playAreaGroundGeneration.allStageChunks)
            {
                // Send info to camera
                if (chunk.p1_pos.y < chunk.p2_pos.y)
                {
                    AddToCamGenPoints(chunk.p1_pos);
                }
                else
                {
                    AddToCamGenPoints(chunk.p2_pos);
                }
            }

            if (camBezierPoints.Count > 2)
            {
                foundGenerationPoints = true;
            }
            else
            {
                Debug.LogWarning("Cannot Find Cam Bezier Points", this.gameObject);
                camBezierPoints.Clear();
            }

            yield return null; // Wait for the next frame
        }
        // set camera to player position
        transform.position = playAreaGroundGeneration.environmentGenerator.playerSpawnPoint.position;

        yield return null; // Optional: Wait for the next frame
    }


    public IEnumerator BoostShake(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-2f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-0.5f, 0.5f) * magnitude;

            transform.position += new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }


    //Called in the bezier generator, adds the P_1/P_2 point to the bezierPoints list
    //(To do so, whenever a P_1/2 point is made, run this function with that point before moving onto the next point)
    public void AddToCamGenPoints(Vector3 point)
    {
        /*
        if (lastCurvePoint.y > point.y && lastCurvePoint.x != point.x) //y check to prevent unwanted height increases, x to prevent duplicates
        {
            camBezierPoints.Add(point);
        }
        */

        if (lastCurvePoint.x != point.x) //y check to prevent unwanted height increases, x to prevent duplicates
        {
            camBezierPoints.Add(point);
        }

        lastCurvePoint = point;
    }

    //Determines the numbers used for making the equation
    public void MakeEquation(Vector3 A_pos, Vector3 B_pos)
    {
        currApoint = A_pos;
        currBpoint = B_pos;

        slope = (currBpoint.y - currApoint.y) / (currBpoint.x - currApoint.x);

        constant = (currBpoint.y - (slope * (-1))); //-1 = cos(pi)
    }

    //Runs the equation to determine that determines the current 'zero' point
    float CalculateZero(Vector3 car_pos) 
    {
        //Check if car is within range of list
        if (bezierPointsListTracker < 0)
        {
            //If not, the zero is the y of the closest point
            //Debug.Log("Calculate A");
            return camBezierPoints[0].y;
        }
        else if (bezierPointsListTracker >= camBezierPoints.Count - 1)
        {
            //Debug.Log("Calculate B");
            return camBezierPoints[camBezierPoints.Count - 1].y;
        }
        else
        {
            //If so, perform equation
            //Debug.Log("Calculate C");
            float currPosOnCurve = slope * Mathf.Cos(car_pos.x * Mathf.PI / currBpoint.x) + constant;
            //Debug.Log(currPosOnCurve);
            //Debug.Log("Calculate C DONE");
      
            return currPosOnCurve;
        }
    }

    float GetCurrentZero()
    {
        //If the vehicle was able to be grabbed earlier in the script
        if (vehicle)
        {
            // We check if the car is NOT between the current 2 points
            if (!(currApoint.x <= vehicle.transform.position.x && vehicle.transform.position.x <= currBpoint.x)) 
            {
                //If so, we either increment or decrement the tracker...
                if (currBpoint.x > vehicle.transform.position.x)
                {
                    bezierPointsListTracker -= 1;
                }
                else
                {
                    bezierPointsListTracker += 1;
                }

                //Check to make sure that the car is within range of the points
                if (bezierPointsListTracker < 0)
                {
                    //If not, the zero is the y of the closest point
                    currApoint.x = float.NegativeInfinity;
                    currBpoint.x = camBezierPoints[0].x;
                    //Debug.Log("A RETURN");
                    return camBezierPoints[0].y;
                }
                else if(bezierPointsListTracker >= camBezierPoints.Count - 1)
                {
                    currApoint.x = camBezierPoints[camBezierPoints.Count - 1].x;
                    currBpoint.x = float.PositiveInfinity;
                    //Debug.Log("B RETURN");
                    return camBezierPoints[camBezierPoints.Count - 1].y;
                }
                else
                {
                    //And then form a new equation for 0!
                    MakeEquation(camBezierPoints[bezierPointsListTracker], camBezierPoints[bezierPointsListTracker + 1]);
                }
            }
            //Then we calculate and return a 'zero' point using the current (whether new or old) equation
            //Debug.Log("C RETURN");
            return CalculateZero(vehicle.transform.position);
        }

        //If no vehicle was grabbed, defaults to 0
        Debug.Log("D RETURN");
        return 0.0f;
    }

    void FixedUpdate()
    {
        // Check if vehicle is found
        if (!overrideCam && vehicle && foundGenerationPoints)
        {
            Vector3 vehiclePos = vehicle.transform.position;

            // Calculate the zero position based on the car's y position
            currZeroPos = GetCurrentZero();
            //Debug.Log("currZeroPos " + currZeroPos);

            truckHeight = Mathf.Abs((vehiclePos.y - currZeroPos));
            //Debug.Log("Truck height from currZero: " + truckHeight);
            
            //AUDIOMANAGER STUFF HERE FOR AMBIENCE
            if(audioManager!=null){
                audioManager.PlayDrivingAmbience(Math.Max(0,truckHeight-currZeroPos));
                Debug.Log("PLAYDRIVINGAMBIENCE UPDATE: "+ Math.Max(0,truckHeight-currZeroPos));
            }



            // << HORZ CAMERA SHIFT >>
            truckCurrVelocity = vehicle.GetComponent<Vehicle>().rb_vehicle.velocity;
            // Calculate the current velocity as a percentage of the velocityRange
            currVelocityPercent = Mathf.InverseLerp(truckVelocityValueRange.x, truckVelocityValueRange.y, vehicleRb.velocity.magnitude);


            // Calculate the x position of the camera based on the car's x position and camera shift
            float newXOffset = (xPosRange.y * currVelocityPercent);
            newXOffset = Mathf.Clamp(newXOffset, xPosRange.x, xPosRange.y);

            // << VERT CAMERA SHIFT >>
            // Calculate the camera shift based on the difference between car's y position and zero position
            float newYOffset = -(truckHeight) * heightOffsetPercentage;
            newYOffset = Mathf.Clamp(newYOffset, yPosRange.x, yPosRange.y);

            // << CAMERA ZOOM >>
            // Calculate the z position of the camera based on the car's y position and camera's FOV
            //float newZOffset = -(Mathf.Abs((vehiclePos.y - currZeroPos)) / Mathf.Tan(30 * Mathf.Deg2Rad));

            // get percentage of height out of max height range
            float zoomPercentage = (float)(truckHeight / truckHeightValueRange.y);
            float newZOffset = zPosRange.y * zoomPercentage; // get zoom percentage from z pos range
            newZOffset = Mathf.Clamp(newZOffset, zPosRange.y, zPosRange.x); // clamp offset


            // if too tall, multiply height offset
            if (truckHeight > truckHeightValueRange.y)
            {
                newYOffset = -(truckHeight) * (0.5f);
                newZOffset = zPosRange.y * 1; // get zoom percentage from z pos range
            }


            currCamOffset = new Vector3(newXOffset, newYOffset, newZOffset);

            // [[ LERP CAMERA ]]
            // Lerp the offset / zoom individually
            currCamPos.x = Mathf.Lerp(currCamPos.x, vehiclePos.x + newXOffset, offsetAdjustSpeed.x * Time.fixedDeltaTime);
            currCamPos.y = Mathf.Lerp(currCamPos.y, vehiclePos.y + newYOffset, offsetAdjustSpeed.y * Time.fixedDeltaTime);
            currCamPos.z = Mathf.Lerp(currCamPos.z, newZOffset, offsetAdjustSpeed.z * Time.fixedDeltaTime);
            
            // Lerp the camera position to the specific offset ^^
            transform.position = Vector3.Lerp(transform.position, currCamPos, camSpeed * Time.fixedDeltaTime);
        }
        else
        {
            currCamOffset = overrideCamOffset;
            Vector3 vehiclePos = vehicle.transform.position;

            // [[ LERP CAMERA ]]
            // Lerp the offset / zoom individually
            currCamPos.x = Mathf.Lerp(currCamPos.x, vehiclePos.x + overrideCamOffset.x, offsetAdjustSpeed.x * Time.fixedDeltaTime);
            currCamPos.y = Mathf.Lerp(currCamPos.y, vehiclePos.y + overrideCamOffset.y, offsetAdjustSpeed.y * Time.fixedDeltaTime);
            currCamPos.z = Mathf.Lerp(currCamPos.z, overrideCamOffset.z, offsetAdjustSpeed.z * Time.fixedDeltaTime);

            // Lerp the camera position to the specific offset ^^
            transform.position = Vector3.Lerp(transform.position, currCamPos, camSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnDrawGizmos()
    {

        Vector3 vehiclePos = vehicle.transform.position;

        Gizmos.color = Color.black;

        // << CURR ZERO LINE FOR CHUNK >>
        Gizmos.DrawLine(new Vector3(currApoint.x, currZeroPos, 0), new Vector3(currBpoint.x, currZeroPos, 0));

        // << CURR ZERO LINE FROM TRUCK Y >>
        Gizmos.DrawLine(new Vector3(vehiclePos.x, currZeroPos, 0), new Vector3(vehiclePos.x, vehiclePos.y, 0));


        // << CURR HORZ OFFSET >>
        Gizmos.DrawCube(new Vector3(vehiclePos.x, currCamPos.y, 0), Vector3.one * 25);
        Gizmos.DrawCube(new Vector3(currCamPos.x, vehiclePos.y, 0), Vector3.one * 25);


        // << SHOW ALL CAM BEZIER POINTS >>
        for (int i = 0; i < camBezierPoints.Count; i++)
        {
            if (i == bezierPointsListTracker) { Gizmos.color = Color.red; }
            else if (i == bezierPointsListTracker + 1) { Gizmos.color = Color.blue; }
            else { Gizmos.color = Color.black; }

            Gizmos.DrawSphere(camBezierPoints[i], 50);


            // << ZERO POINT >>
            // Calculate the position of the zero point at each bezier point
            float zeroPoint = CalculateZero(vehicle.transform.position);

            // Set the color for the Gizmos line based on the position of the zero point
            if (zeroPoint > 0f)
            {
                Gizmos.color = Color.green; // Zero point above y-axis
            }
            else if (zeroPoint < 0f)
            {
                Gizmos.color = Color.red; // Zero point below y-axis
            }
            else
            {
                Gizmos.color = Color.yellow; // Zero point at y-axis
            }

            // Draw a line from the bezier point to the zero point
            Gizmos.DrawLine(camBezierPoints[i], new Vector3(camBezierPoints[i].x, zeroPoint, 0f));
        }
    }


}
