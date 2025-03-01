using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour {


    protected Vehicle truck;
    protected DrivingGameManager drivingGameManager;

    // Store across levels:
    public static class TutorialManagerInfo
    {
        public static bool showGas = false;
        public static bool showRotation = false;
        public static bool showNitro = false;
    }

    public float fadeSpeed = 1f;

    public Image space;
    public Image arrowLeft;
    public Image arrowRight;
    public Image nitro;

    [Space(20)]
    public Animator truckUIAnim;
    public FlipTracker flipTracker;
    public TextMeshProUGUI flipCount;


    private void Start() {
        TutorialManagerInfo.showGas = false;
        TutorialManagerInfo.showRotation = false;

        space = transform.GetChild(0).GetComponent<Image>();
        arrowLeft = transform.GetChild(1).GetChild(0).GetComponent<Image>();
        arrowRight = transform.GetChild(1).GetChild(1).GetComponent<Image>();
        nitro = transform.GetChild(2).GetComponent<Image>();

        space.color = new Color(1, 1, 1, 0);
        arrowLeft.color = new Color(1, 1, 1, 0);
        arrowRight.color = new Color(1, 1, 1, 0);
        nitro.color = new Color(1, 1, 1, 0);

        truck = GetComponentInParent<Vehicle>();
        drivingGameManager = GameObject.FindGameObjectWithTag("DrivingGameManager").GetComponent<DrivingGameManager>();
    }

    private delegate bool AwaitTutorialCompletion();

    private IEnumerator ShowTutorialMessage(Image message, AwaitTutorialCompletion check) {
        message.color = Color.white;
        var timeStart = Time.time;
        while (check() == false) {
            yield return null;
        }
        var diff = Time.time - timeStart;
        if (diff < 2) {
            yield return new WaitForSeconds(2 - diff);
        }
        while (message.color.a > 0) {
            message.color = new Color(1, 1, 1, message.color.a - fadeSpeed * Time.deltaTime);
            yield return new WaitForEndOfFrame();
        }
    }

    private void Update()
    {

        if (drivingGameManager.state == DRIVINGGAME_STATE.PLAY)
        {
            if (truck.rb_vehicle.velocity.x <= 0.1f)
            {
                if (!TutorialManagerInfo.showGas)
                {
                    TutorialManagerInfo.showGas = true;
                    StartCoroutine(ShowTutorialMessage(space, () =>
                    {
                        return Input.GetKey(KeyCode.Space);
                    }));
                }

            }
            else if (TutorialManagerInfo.showGas && truck.rb_vehicle.velocity.x > 0 && !TutorialManagerInfo.showNitro)
            {
                TutorialManagerInfo.showNitro = true;
                StartCoroutine(ShowTutorialMessage(nitro, () =>
                {
                    return Input.GetKey(KeyCode.LeftShift);
                }));
            }

            if (truck.rb_vehicle.velocity.y > 10 && truck.state == DRIVE_STATE.IN_AIR && !TutorialManagerInfo.showRotation)
            {
                TutorialManagerInfo.showRotation = true;
                StartCoroutine(ShowTutorialMessage(arrowLeft, () =>
                {
                    return (Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.RightArrow));
                }));
                StartCoroutine(ShowTutorialMessage(arrowRight, () =>
                {
                    return (Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.RightArrow));
                }));
            }
        }
    }

    public void showFlipCountUI(int count)
    {
        // << FLIP COUNT >>
        flipCount.text = "" + count;

        if (count <= 0) { return; }

        truckUIAnim.Play("FlipCount");

    }
}
