using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlipTracker : MonoBehaviour
{
    DrivingGameManager drivingGameManager;
    Vehicle vehicle;
    TruckAnimationHandler animHandler;
    RaycastHit2D hit;
    public AudioManager audioManager;
    public StageManager stageManager;
    public int hitPointIndex;
    float initTruckRotation;

    public bool jumpStarted = false;
    
    [Space(10)]
    [Header("Multiple Flip Values")]
    public int flipCap = 10;
    public float percentBoost = 0.1f;
    public float timeBoost = 0.05f;
    public bool firstFlipCounts = true;
    public GameObject boostSprite;
    float boostSpriteY;


    [Space(10)]
    public TutorialManager uiScript;
    public int flipCount;
    private bool flipCounted;
    public int totalLandedFlips;
    public float currAirTime;

    [Space(10)]
    public float perfectLandingRotationBound = 1;
    public float perfectLandingMinAirTime = 0.75f;

    [Space(10)]
    public float currRot;
    public float startJumpRot;
    public float endJumpRot;

    [Space(10)]
    public float groundPointRotation;


    void Start()
    {
        vehicle = GetComponent<Vehicle>();
        animHandler = GetComponent<TruckAnimationHandler>();
        drivingGameManager = GameObject.FindGameObjectWithTag("DrivingGameManager").GetComponent<DrivingGameManager>();
        stageManager = GameObject.FindGameObjectWithTag("DrivingGameManager").GetComponent<DrivingGameManager>().playAreaStageManager;
        initTruckRotation = transform.rotation.eulerAngles.z;
        audioManager = GameObject.FindGameObjectWithTag("AudioManager").GetComponent<AudioManager>();
        boostSpriteY = boostSprite.transform.localScale.y;
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        if (drivingGameManager.state != DRIVINGGAME_STATE.PLAY) { return; }

        currRot = transform.rotation.eulerAngles.z - initTruckRotation;

        // get point underneath truck
        hit = Physics2D.Raycast(transform.position, Vector2.down, Mathf.Infinity, vehicle.groundLayer);
        if (hit.collider == null) { return; }

        // get point underneath truck
        if (stageManager == null) { Debug.LogError("ERROR: Stage Manager is null");  return; }
        hitPointIndex = stageManager.PosToGroundPointIndex(hit.point);

        // << TRIGGER WHEN IN AIR >>
        if (vehicle.state == DRIVE_STATE.IN_AIR && !jumpStarted)
        {
            // reset
            startJumpRot = 0;
            endJumpRot = 0;

            // set values
            jumpStarted = true;
            startJumpRot = currRot;
        }

        // << TRIGGER WHEN GROUNDED >>
        if ((vehicle.state == DRIVE_STATE.UPHILL_GROUNDED || vehicle.state == DRIVE_STATE.GROUNDED) && jumpStarted)
        {
            // set values
            jumpStarted = false;
            endJumpRot = currRot;

            //Reset truck's velocity to prevent extra-bouncy landings
            vehicle.GetComponent<Rigidbody2D>().angularVelocity = 0;

            groundPointRotation = stageManager.allStageGroundRotations[hitPointIndex];

            if (IsPerfectLanding(endJumpRot, groundPointRotation) && flipCount > 0) 
            {
                int flips = Mathf.Min(flipCount, flipCap);


                flips = (firstFlipCounts) ? flips : flips-1;
                float flipBoost = flips*percentBoost;
                Vector2 newBoost = new Vector2(((flipBoost)+1)*vehicle.perfectLandingBoostForce.x, vehicle.perfectLandingBoostForce.y);
                float newTime = ((flips*timeBoost)+1)*vehicle.activePerfectBoostTime;

                var instance = audioManager.Play(audioManager.flipBoostSFX);
                Debug.Log("FLIP BOOST %: " + flipBoost);
                instance.setParameterByName("flipBoost", flipBoost);

                boostSprite.transform.localScale = new Vector3(boostSprite.transform.localScale.x, boostSpriteY*((flips*percentBoost)+1), boostSprite.transform.localScale.z);
                StartCoroutine(vehicle.PerfectLandingBoost(newBoost, newTime));
                
            }else{
                audioManager.Play(audioManager.truckLandingSFX);
            }
        }

        // track in air time
        if (jumpStarted && vehicle.state == DRIVE_STATE.IN_AIR)
        {
            currAirTime += Time.deltaTime;

            ActiveFlipCounter();
        }
        else 
        {
            currAirTime = 0;
            flipCount = 0;
        }
    }

    public bool IsPerfectLanding(float landPointRot, float groundPointRot)
    {
        if (vehicle.state == DRIVE_STATE.CRASH) { return false; }

        // if rotation is within bound and enough time has passed and landing downhill
        if (Mathf.Abs(groundPointRot - landPointRot) < perfectLandingRotationBound && currAirTime > perfectLandingMinAirTime)
        {
            uiScript.showFlipCountUI(flipCount);
            totalLandedFlips += flipCount;

            return true;
        }
        return false;
    }

    private float accumulatedRotation;
    private bool isRotatingClockwise;
    public void ActiveFlipCounter()
    {
        float currentRotation = transform.rotation.eulerAngles.z;

        if (isRotatingClockwise && currentRotation < 90f)
        {
            // Crossed from clockwise to counterclockwise rotation
            if (accumulatedRotation >= 360f || accumulatedRotation <= 270f)
            {
                flipCount++;
                accumulatedRotation %= 360f;
            }
            isRotatingClockwise = false;
        }
        else if (!isRotatingClockwise && currentRotation > 270f)
        {
            // Crossed from counterclockwise to clockwise rotation
            if (accumulatedRotation <= -360f || accumulatedRotation >= -90f)
            {
                flipCount++;
                accumulatedRotation %= 360f;
            }
            isRotatingClockwise = true;
        }

        // Update accumulated rotation
        float rotationDelta = currentRotation - accumulatedRotation;
        if (rotationDelta > 180f)
        {
            rotationDelta -= 360f;
        }
        else if (rotationDelta < -180f)
        {
            rotationDelta += 360f;
        }
        accumulatedRotation += rotationDelta;
    }

    private void OnDrawGizmos()
    {
        if (stageManager != null && stageManager.allStageGroundPoints.Count > 1)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(stageManager.allStageGroundPoints[hitPointIndex], 4);
        }
    }
}
